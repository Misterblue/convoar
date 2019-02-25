/*
 * Copyright (c) 2017 Robert Adams
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.CoreModules;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using org.herbal3d.cs.CommonEntitiesUtil;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.cs.os.CommonEntities {
    // Convert things from OpenSimulator to Instances and Displayables things
    public class BConverterOS {
        private static readonly string _logHeader = "[BConverterOS]";

        private readonly BLogger _log;
        private readonly IParameters _params;

        public BConverterOS(BLogger pLog, IParameters pParams) {
            _log = pLog;
            _params = pParams;
        }

        public async Task<BScene> ConvertRegionToBScene(Scene scene, AssetManager assetManager) {
            BScene bScene = null;

            // Convert SOGs from OAR into EntityGroups
            // _log.Log("Num assets = {0}", assetService.NumAssets);
            LogBProgress("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

            PrimToMesh mesher = new PrimToMesh(_log, _params);

            BInstance[] instances = new BInstance[0];
            try {
                // Convert SOGs => BInstances
                // Create a collection of parallel tasks for the SOG conversions.
                List<Task<BInstance>> convertAllSOGs = new List<Task<BInstance>>();
                foreach (var sog in scene.GetSceneObjectGroups()) {
                    convertAllSOGs.Add(ConvertSogToInstance(sog, assetManager, mesher));
                }
                instances = await Task.WhenAll(convertAllSOGs.ToArray());
            }
            catch (AggregateException ae) {
                foreach (var e in ae.InnerExceptions) {
                    _log.ErrorFormat("Convert SOGs exception: {0}", e);
                }
            }
            catch (Exception e) {
                _log.ErrorFormat("Convert SOGs exception: {0}", e);
            }

            try {
                _log.DebugFormat("{0} Num instances = {1}", _logHeader, instances.ToList().Count);
                List<BInstance> instanceList = new List<BInstance>();
                instanceList.AddRange(instances);

                // Add the terrain mesh to the scene
                BInstance terrainInstance = null;
                if (_params.P<bool>("AddTerrainMesh")) {
                    _log.DebugFormat("{0} Creating terrain for scene", _logHeader);
                    // instanceList.Add(ConvoarTerrain.CreateTerrainMesh(scene, mesher, assetManager));
                    terrainInstance = await Terrain.CreateTerrainMesh(scene, mesher, assetManager, _log, _params);
                    CoordAxis.FixCoordinates(terrainInstance, new CoordAxis(CoordAxis.RightHand_Yup | CoordAxis.UVOriginLowerLeft));
                }

                // Twist the OpenSimulator Z-up coordinate system to the OpenGL Y-up
                foreach (var inst in instanceList) {
                    CoordAxis.FixCoordinates(inst, new CoordAxis(CoordAxis.RightHand_Yup | CoordAxis.UVOriginLowerLeft));
                }

                // package instances into a BScene
                RegionInfo ri = scene.RegionInfo;
                bScene = new BScene {
                    instances = instanceList,
                    name = ri.RegionName,
                    terrainInstance = terrainInstance
                };
                bScene.attributes.Add("RegionName", ri.RegionName);
                bScene.attributes.Add("RegionSizeX", ri.RegionSizeX);
                bScene.attributes.Add("RegionSizeY", ri.RegionSizeY);
                bScene.attributes.Add("RegionSizeZ", ri.RegionSizeZ);
                bScene.attributes.Add("RegionLocX", ri.RegionLocX);
                bScene.attributes.Add("RegionLocY", ri.RegionLocY);
                bScene.attributes.Add("WorldLocX", ri.WorldLocX);
                bScene.attributes.Add("WorldLocY", ri.WorldLocY);
                bScene.attributes.Add("WaterHeight", ri.RegionSettings.WaterHeight);
                bScene.attributes.Add("DefaultLandingPorint", ri.DefaultLandingPoint);
            }
            catch (Exception e) {
                _log.ErrorFormat("{0} failed SOG conversion: {1}", _logHeader, e);
                throw new Exception(String.Format("Failed conversion: {0}", e));
            }

            return bScene;
        }

        // Convert a SceneObjectGroup into an instance with displayables
        public async Task<BInstance> ConvertSogToInstance(SceneObjectGroup sog, AssetManager assetManager, PrimToMesh mesher) {
            BInstance ret = null;

            Displayable[] renderables = new Displayable[0];
            try {
                LogBProgress("{0} ConvertSogToInstance: name={1}, id={2}, SOPs={3}",
                            _logHeader, sog.Name, sog.UUID, sog.Parts.Length);
                // Create meshes for all the parts of the SOG
                List<Task<Displayable>> convertSOPs = new List<Task<Displayable>>();
                foreach (var sop in sog.Parts) {
                    LogBProgress("{0} ConvertSOGToInstance: Calling CreateMeshResource for sog={1}, sop={2}",
                                    _logHeader, sog.UUID, sop.UUID);
                    OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                    convertSOPs.Add(mesher.CreateMeshResource(sog, sop, aPrim, assetManager, OMVR.DetailLevel.Highest));
                }
                renderables = await Task.WhenAll(convertSOPs.ToArray());
            }
            catch (AggregateException ae) {
                foreach (var e in ae.InnerExceptions) {
                    _log.ErrorFormat("ConvertSogToInstance: exception: {0}", e);
                }
            }
            catch (Exception e) {
                _log.ErrorFormat("ConvertSogToInstance: exception: {0}", e);
            }

            try {
                // Remove any failed SOG/SOP conversions.
                List<Displayable> filteredRenderables = renderables.Where(rend => rend != null).ToList();

                // 'filteredRenderables' are the DisplayRenderables for all the SOPs in the SOG
                // Get the root prim of the SOG
                List<Displayable> rootDisplayableList = filteredRenderables.Where(disp => {
                    return disp.baseSOP.IsRoot;
                }).ToList();
                if (rootDisplayableList.Count != 1) {
                    // There should be only one root prim
                    string errorMsg = String.Format("{0} ConvertSOGToInstance: Found not one root prim in SOG. ID={1}, numRoots={2}",
                                _logHeader, sog.UUID, rootDisplayableList.Count);
                    _log.ErrorFormat(errorMsg);
                    throw new Exception(errorMsg);
                }

                // The root of the SOG
                Displayable rootDisplayable = rootDisplayableList.First();

                // Collect all the children prims and add them to the root Displayable
                rootDisplayable.children = filteredRenderables.Where(disp => {
                    return !disp.baseSOP.IsRoot;
                    // }).Select(disp => {
                    //     return disp;
                }).ToList();

                // Add the Displayable into the collection of known Displayables for instancing
                assetManager.Assets.AddUniqueDisplayable(rootDisplayable);

                // Package the Displayable into an instance that is position in the world
                ret = new BInstance {
                    Position = sog.AbsolutePosition,
                    Rotation = sog.GroupRotation,
                    Representation = rootDisplayable
                };

                if (_params.P<bool>("LogBuilding")) {
                    DumpInstance(ret);
                }
            }
            catch (Exception e) {
                 _log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", _logHeader, sog.UUID, e);
                 throw new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e));
            }
            return ret;
        }

        private void DumpInstance(BInstance inst) {
            Displayable instDisplayable = inst.Representation;
            LogBProgress("{0} created instance. handle={1}, pos={2}, rot={3}",
                _logHeader, inst.handle, inst.Position, inst.Rotation);
            DumpDisplayable(inst.Representation, "Representation", 0);
        }

        private void DumpDisplayable(Displayable disp, string header, int level) {
            string spaces = "                                                           ";
            string spacer = spaces.Substring(0, level * 2);
            LogBProgress("{0}{1}  displayable: name={2}, pos={3}, rot={4}",
                _logHeader, spacer, disp.name, disp.offsetPosition, disp.offsetRotation);
            if (disp.renderable is RenderableMeshGroup rmg) {
                rmg.meshes.ForEach(mesh => {
                    LogBProgress("{0}{1}    mesh: mesh={2}. material={3}",
                        _logHeader, spacer, mesh.mesh, mesh.material);
                });
            }
            disp.children.ForEach(child => {
                DumpDisplayable(child, "Child", level + 1);
            });
        }

        public void LogBProgress(string msg, params Object[] args) {
            if (_params.P<bool>("LogBuilding")) {
                _log.Log(msg, args);
            }
        }
    }
}
