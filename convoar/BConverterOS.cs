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
using System.IO;
using System.Reflection;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.CoreModules;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.CoreModules.World.Archiver;
using OpenSim.Tests.Common;
using OpenSim.Data.Null;
using OpenSim.Region.PhysicsModule.BasicPhysics;
using OpenSim.Region.PhysicsModules.SharedBase;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.convoar {
    // Convert things from OpenSimulator to Instances and Displayables things
    public class BConverterOS {

        private static readonly string _logHeader = "[BConverterOS]";

        public BConverterOS() {
        }

        public Promise<BScene> ConvertOarToScene(IAssetService assetService, IAssetFetcher assetFetcher) {

            Promise<BScene> prom = new Promise<BScene>();

            // Assemble all the parameters that loadoar takes and uses
            Dictionary<string, object> options = new Dictionary<string, object> {
                // options.Add("merge", false);
                { "displacement", ConvOAR.Globals.parms.P<OMV.Vector3>("Displacement") }
            };
            string optRotation = ConvOAR.Globals.parms.P<string>("Rotation");
            if (optRotation != null) options.Add("rotation", float.Parse(optRotation, System.Threading.Thread.CurrentThread.CurrentCulture));
            // options.Add("default-user", OMV.UUID.Random());
            // if (optSkipAssets != null) options.Add('skipAssets', true);
            // if (optForceTerrain != null) options.Add("force-terrain", true);
            // if (optNoObjects != null) options.Add("no-objects", true);
            string optSubRegion = ConvOAR.Globals.parms.P<string>("SubRegion");
            if (optSubRegion != null) {
                List<float> bounds = optSubRegion.Split(',').Select<string,float>(x => { return float.Parse(x); }).ToList();
                options.Add("bounding-origin", new OMV.Vector3(bounds[0], bounds[1], bounds[2]));
                options.Add("bounding-size", new OMV.Vector3(bounds[3]-bounds[0], bounds[4]-bounds[1], bounds[5]-bounds[2]));
            }

            // Create an OpenSimulator region and scene to load the OAR into
            string regionName = "convoar";
            if (String.IsNullOrEmpty(ConvOAR.Globals.parms.P<String>("RegionName"))) {
                // Try to build the region name from the OAR filesname
                regionName = Path.GetFileNameWithoutExtension(ConvOAR.Globals.parms.P<string>("InputOAR"));
            }
            else {
                regionName = ConvOAR.Globals.parms.P<string>("RegionName");
            }
            Scene scene = CreateScene(assetService, regionName);

            // Load the archive into our scene
            ArchiveReadRequest archive = new ArchiveReadRequest(scene, ConvOAR.Globals.parms.P<string>("InputOAR"), Guid.Empty, options);
            archive.DearchiveRegion(false);

            // Convert SOGs from OAR into EntityGroups
            // ConvOAR.Globals.log.Log("Num assets = {0}", assetService.NumAssets);
            LogBProgress("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

            PrimToMesh mesher = new PrimToMesh();

            // Convert SOGs => BInstances
            Promise<BInstance>.All(
                scene.GetSceneObjectGroups().Select(sog => {
                    return ConvertSogToInstance(sog, assetFetcher, mesher);
                })
            )
            .Done(instances => {
                ConvOAR.Globals.log.DebugFormat("{0} Num instances = {1}", _logHeader, instances.ToList().Count);
                List<BInstance> instanceList = new List<BInstance>();
                instanceList.AddRange(instances);

                // Add the terrain mesh to the scene
                BInstance terrainInstance = null;
                if (ConvOAR.Globals.parms.P<bool>("AddTerrainMesh")) {
                    ConvOAR.Globals.log.DebugFormat("{0} Creating terrain for scene", _logHeader);
                    // instanceList.Add(ConvoarTerrain.CreateTerrainMesh(scene, mesher, assetFetcher));
                    terrainInstance = ConvoarTerrain.CreateTerrainMesh(scene, mesher, assetFetcher);
                    CoordAxis.FixCoordinates(terrainInstance, new CoordAxis(CoordAxis.RightHand_Yup | CoordAxis.UVOriginLowerLeft));
                }

                // Twist the OpenSimulator Z-up coordinate system to the OpenGL Y-up
                foreach (var inst in instanceList) {
                    CoordAxis.FixCoordinates(inst, new CoordAxis(CoordAxis.RightHand_Yup | CoordAxis.UVOriginLowerLeft));
                }

                // package instances into a BScene
                RegionInfo ri = scene.RegionInfo;
                BScene bScene = new BScene {
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

                prom.Resolve(bScene);
            }, e => {
                ConvOAR.Globals.log.ErrorFormat("{0} failed SOG conversion: {1}", _logHeader, e);
                // prom.Reject(new Exception(String.Format("Failed conversion: {0}", e)));
            });

            return prom;

        }

        // Create an OpenSimulator Scene and add enough auxillery services and objects
        //   to it so it will allow the loading of assets.
        public Scene CreateScene(IAssetService memAssetService, string regionName) {
            var estateSettings = new EstateSettings {
                EstateOwner = OMV.UUID.Random()
            };
            RegionInfo regionInfo = new RegionInfo(0, 0, null, regionName) {
                RegionName = regionName,
                RegionSizeX = Constants.RegionSize,
                RegionSizeY = Constants.RegionSize,
                RegionID = OMV.UUID.Random(),
                EstateSettings = estateSettings
            };

            Scene scene = new Scene(regionInfo);

            // Add an in-memory asset service for all the loaded assets to go into
            scene.RegisterModuleInterface<IAssetService>(memAssetService);

            ISimulationDataService simulationDataService = new NullDataService();
            scene.RegisterModuleInterface<ISimulationDataService>(simulationDataService);

            IRegionSerialiserModule serializerModule = new SerialiserModule();
            scene.RegisterModuleInterface<IRegionSerialiserModule>(serializerModule);

            IUserAccountService userAccountService = new NullUserAccountService();
            scene.RegisterModuleInterface<IUserAccountService>(userAccountService);

            PhysicsScene physScene = CreateSimplePhysicsEngine();
            ((INonSharedRegionModule)physScene).AddRegion(scene);
            ((INonSharedRegionModule)physScene).RegionLoaded(scene);
            scene.PhysicsScene = physScene;

            scene.LandChannel = new TestLandChannel(scene); // simple land with no parcels
            Nini.Config.IConfigSource config = new Nini.Config.IniConfigSource();
            config.AddConfig("Terrain");
            config.Configs["Terrain"].Set("InitialTerrain", "flat");
            var terrainModule = new TerrainModule();
            try {
                terrainModule.Initialise(config);
                terrainModule.AddRegion(scene);
            }
            catch (ReflectionTypeLoadException e) {
                // The terrain module loads terrain brushes and they might not all have been included
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in e.LoaderExceptions) {
                    sb.AppendLine(exSub.Message);
                    if (exSub is FileNotFoundException exFileNotFound) {
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog)) {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                ConvOAR.Globals.log.Log("BConverterOS.CreateScene: exception adding region:");
                ConvOAR.Globals.log.Log(errorMessage);
            }
            catch (Exception e) {
                ConvOAR.Globals.log.Log("BConverterOS.CreateScene: exception adding region: {0}", e);
            }

            SceneManager.Instance.Add(scene);

            return scene;
        }

        public PhysicsScene CreateSimplePhysicsEngine() {
            Nini.Config.IConfigSource config = new Nini.Config.IniConfigSource();
            config.AddConfig("Startup");
            config.Configs["Startup"].Set("physics", "basicphysics");

            PhysicsScene pScene = new BasicScene();
            INonSharedRegionModule mod = pScene as INonSharedRegionModule;
            mod.Initialise(config);

            return pScene;
        }

        // Convert a SceneObjectGroup into an instance with displayables
        public IPromise<BInstance> ConvertSogToInstance(SceneObjectGroup sog, IAssetFetcher assetFetcher, PrimToMesh mesher) {
            return new Promise<BInstance>((promResolve, promReject) => {
                LogBProgress("{0} ConvertSogToInstance: name={1}, id={2}, SOPs={3}",
                            _logHeader, sog.Name, sog.UUID, sog.Parts.Length);
                // Create meshes for all the parts of the SOG
                Promise<Displayable>.All(
                    sog.Parts.Select(sop => {
                        LogBProgress("{0} ConvertSOGToInstance: Calling CreateMeshResource for sog={1}, sop={2}",
                                    _logHeader, sog.UUID, sop.UUID);
                        OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                        return mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest);
                    } )
                )
                .Then(renderables => {
                    // Remove any failed SOG/SOP conversions.
                    List<Displayable> filteredRenderables = renderables.Where(rend => rend != null).ToList();

                    // 'filteredRenderables' are the DisplayRenderables for all the SOPs in the SOG
                    // Get the root prim of the SOG
                    List<Displayable> rootDisplayableList = filteredRenderables.Where(disp => {
                        return disp.baseSOP.IsRoot;
                    }).ToList();
                    if (rootDisplayableList.Count != 1) {
                        // There should be only one root prim
                        ConvOAR.Globals.log.ErrorFormat("{0} ConvertSOGToInstance: Found not one root prim in SOG. ID={1}, numRoots={2}",
                                    _logHeader, sog.UUID, rootDisplayableList.Count);
                        promReject(new Exception(String.Format("Found more than one root prim in SOG. ID={0}", sog.UUID)));
                        return null;
                    }

                    // The root of the SOG
                    Displayable rootDisplayable = rootDisplayableList.First();

                    // Collect all the children prims and add them to the root Displayable
                    rootDisplayable.children = filteredRenderables.Where(disp => {
                        return !disp.baseSOP.IsRoot;
                    // }).Select(disp => {
                    //     return disp;
                    }).ToList();

                    return rootDisplayable;

                })
                .Done(rootDisplayable => {
                    // Add the Displayable into the collection of known Displayables for instancing
                    assetFetcher.AddUniqueDisplayable(rootDisplayable);

                    // Package the Displayable into an instance that is position in the world
                    BInstance inst = new BInstance {
                        Position = sog.AbsolutePosition,
                        Rotation = sog.GroupRotation,
                        Representation = rootDisplayable
                    };

                    if (ConvOAR.Globals.parms.P<bool>("LogBuilding")) {
                        DumpInstance(inst);
                    }

                    promResolve(inst);
                }, e => {
                     ConvOAR.Globals.log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", _logHeader, sog.UUID, e);
                     promReject(new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e)));
                });
            });

            /*
            var prom = new Promise<BInstance>();
            LogBProgress("{0} ConvertSogToInstance: name={1}, id={2}, SOPs={3}",
                        _logHeader, sog.Name, sog.UUID, sog.Parts.Length);
            // Create meshes for all the parts of the SOG
            Promise<Displayable>.All(
                sog.Parts.Select(sop => {
                    LogBProgress("{0} ConvertSOGToInstance: Calling CreateMeshResource for sog={1}, sop={2}",
                                _logHeader, sog.UUID, sop.UUID);
                    OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                    return mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest);
                } )
            )
            .Then(renderables => {
                // Remove any failed SOG/SOP conversions.
                List<Displayable> filteredRenderables = renderables.Where(rend => rend != null).ToList();

                // 'filteredRenderables' are the DisplayRenderables for all the SOPs in the SOG
                // Get the root prim of the SOG
                List<Displayable> rootDisplayableList = filteredRenderables.Where(disp => {
                    return disp.baseSOP.IsRoot;
                }).ToList();
                if (rootDisplayableList.Count != 1) {
                    // There should be only one root prim
                    ConvOAR.Globals.log.ErrorFormat("{0} ConvertSOGToInstance: Found not one root prim in SOG. ID={1}, numRoots={2}",
                                _logHeader, sog.UUID, rootDisplayableList.Count);
                    prom.Reject(new Exception(String.Format("Found more than one root prim in SOG. ID={0}", sog.UUID)));
                    return null;
                }

                // The root of the SOG
                Displayable rootDisplayable = rootDisplayableList.First();

                // Collect all the children prims and add them to the root Displayable
                rootDisplayable.children = filteredRenderables.Where(disp => {
                    return !disp.baseSOP.IsRoot;
                // }).Select(disp => {
                //     return disp;
                }).ToList();

                return rootDisplayable;

            })
            .Done(rootDisplayable => {
                // Add the Displayable into the collection of known Displayables for instancing
                assetFetcher.AddUniqueDisplayable(rootDisplayable);

                // Package the Displayable into an instance that is position in the world
                BInstance inst = new BInstance();
                inst.Position = sog.AbsolutePosition;
                inst.Rotation = sog.GroupRotation;
                inst.Representation = rootDisplayable;

                if (ConvOAR.Globals.parms.P<bool>("LogBuilding")) {
                    DumpInstance(inst);
                }

                prom.Resolve(inst);
            }, e => {
                 ConvOAR.Globals.log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", _logHeader, sog.UUID, e);
                 prom.Reject(new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e)));
            });

            return prom;
            */
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

        public static void LogBProgress(string msg, params Object[] args) {
            if (ConvOAR.Globals.parms.P<bool>("LogBuilding")) {
                ConvOAR.Globals.log.Log(msg, args);
            }
        }
    }
}
