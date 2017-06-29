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

        private static string _logHeader = "[BConverterOS]";

        public BConverterOS() {
        }

        public Promise<BScene> ConvertOarToScene(IAssetService assetService, IAssetFetcher assetFetcher) {

            Promise<BScene> prom = new Promise<BScene>();

            // Read in OAR
            Dictionary<string, object> options = new Dictionary<string, object>();
            // options.Add("merge", false);
            string optDisplacement = ConvOAR.Globals.parms.Displacement;
            if (optDisplacement != null) options.Add("displacement", optDisplacement);
            string optRotation = ConvOAR.Globals.parms.Rotation;
            if (optRotation != null) options.Add("rotation", optRotation);
            // options.Add("default-user", OMV.UUID.Random());
            // if (_optSkipAssets != null) options.Add('skipAssets', true);
            // if (_optForceTerrain != null) options.Add("force-terrain", true);
            // if (_optNoObjects != null) options.Add("no-objects", true);

            string regionName = "convoar";
            if (String.IsNullOrEmpty(ConvOAR.Globals.parms.RegionName)) {
                // Try to build the region name from the OAR filesname
                regionName = Path.GetFileNameWithoutExtension(ConvOAR.Globals.parms.InputOAR);
            }
            else {
                regionName = ConvOAR.Globals.parms.RegionName;
            }
            Scene scene = CreateScene(assetService, regionName);

            // Load the archive into our scene
            ArchiveReadRequest archive = new ArchiveReadRequest(scene, ConvOAR.Globals.parms.InputOAR, Guid.Empty, options);
            archive.DearchiveRegion(false);

            // Convert SOGs from OAR into EntityGroups
            // ConvOAR.Globals.log.Log("Num assets = {0}", assetService.NumAssets);
            LogBProgress("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

            PrimToMesh mesher = new PrimToMesh();

            Promise<BInstance>.All(
                scene.GetSceneObjectGroups().Select(sog => {
                    return ConvertSogToInstance(sog, assetFetcher, mesher);
                })
            )
            .Catch(e => {
                prom.Reject(new Exception(String.Format("Failed conversion: {0}", e)));
            })
            .Done(instances => {
                ConvOAR.Globals.log.DebugFormat("Num instances = {0}", instances.ToList().Count);
                BInstanceList instanceList = new BInstanceList();
                instanceList.AddRange(instances);

                BScene bScene = new BScene();
                bScene.instances = instanceList;
                RegionInfo ri = scene.RegionInfo;
                bScene.name = ri.RegionName;
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
            });

            return prom;

        }

        // Create an OpenSimulator Scene and add enough auxillery services and objects
        //   to it so it will do a asset load;
        public Scene CreateScene(IAssetService memAssetService, string regionName) {
            RegionInfo regionInfo = new RegionInfo(0, 0, null, regionName);
            regionInfo.RegionName = regionName;
            regionInfo.RegionSizeX = regionInfo.RegionSizeY = Constants.RegionSize;
            regionInfo.RegionID = OMV.UUID.Random();
            var estateSettings = new EstateSettings();
            estateSettings.EstateOwner = OMV.UUID.Random();
            regionInfo.EstateSettings = estateSettings;

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
            var terrainModule = new TerrainModule();
            terrainModule.AddRegion(scene);

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
            var prom = new Promise<BInstance>();

            LogBProgress("{0} ConvertSogToInstance: name={1}, id={2}, SOPs={3}",
                        _logHeader, sog.Name, sog.UUID, sog.Parts.Length);
            // Create meshes for all the parts of the SOG
            Promise<Displayable>.All(
                sog.Parts.Select(sop => {
                    // ConvOAR.Globals.log.DebugFormat("{0} calling CreateMeshResource for sog={1}, sop={2}",
                    //             _logHeader, sog.UUID, sop.UUID);
                    OMV.Primitive aPrim = sop.Shape.ToOmvPrimitive();
                    return mesher.CreateMeshResource(sog, sop, aPrim, assetFetcher, OMVR.DetailLevel.Highest);
                } )
            )
            .Then(renderables => {
                // 'renderables' are the DisplayRenderables for all the SOPs in the SOG
                // Get the root prim of the SOG
                List<Displayable> rootDisplayableList = renderables.Where(disp => {
                    return disp.baseSOP.IsRoot;
                }).ToList();
                if (rootDisplayableList.Count != 1) {
                    // There should be only one root prim
                    ConvOAR.Globals.log.ErrorFormat("{0} Found not one root prim in SOG. ID={1}, numRoots={2}",
                                _logHeader, sog.UUID, rootDisplayableList.Count);
                    prom.Reject(new Exception(String.Format("Found more than one root prim in SOG. ID={0}", sog.UUID)));
                    return null;
                }

                // The root of the SOG
                Displayable rootDisplayable = rootDisplayableList.First();

                // Collect all the children prims and add them to the root Displayable
                rootDisplayable.children = renderables.Where(disp => {
                    return !disp.baseSOP.IsRoot;
                }).Select(disp => {
                    return disp;
                }).ToList();

                return rootDisplayable;

            })
            .Catch(e => {
                ConvOAR.Globals.log.ErrorFormat("{0} Failed meshing of SOG. ID={1}: {2}", _logHeader, sog.UUID, e);
                prom.Reject(new Exception(String.Format("failed meshing of SOG. ID={0}: {1}", sog.UUID, e)));
            })
            .Done (rootDisplayable => {
                BInstance inst = new BInstance();
                inst.Position = sog.AbsolutePosition;
                inst.Rotation = sog.GroupRotation;
                inst.Representation = rootDisplayable;

                prom.Resolve(inst);
            }) ;

            return prom;
        }

        public static void LogBProgress(string msg, params Object[] args) {
            if (ConvOAR.Globals.parms.LogBuilding) {
                ConvOAR.Globals.log.Log(msg, args);
            }
        }

        /*
        /// <summary>
        /// Scan through all the ExtendedPrims and finish any texture updating.
        /// This includes UV coordinate mappings and fetching any image that goes with the texture.
        /// </summary>
        /// <param name="epGroup">Collections of meshes to update</param>
        /// <param name="assetFetcher">Fetcher for getting images, etc</param>
        /// <param name="pMesher"></param>
        private void UpdateTextureInfo(ExtendedPrimGroup epGroup, IAssetFetcher assetFetcher, PrimToMesh mesher) {
            ExtendedPrim ep = epGroup.primaryExtendePrim;
            foreach (FaceInfo faceInfo in ep.faces) {

                // While we're in the neighborhood, map the texture coords based on the prim information
                mesher.UpdateCoords(faceInfo, ep.fromOS.primitive);

                UpdateFaceInfoWithTexture(faceInfo, assetFetcher);
            }
        }

        // Check to see if the FaceInfo has a textureID and, if so, read it in and populate the FaceInfo
        //    with that texture data.
        public void UpdateFaceInfoWithTexture(FaceInfo faceInfo, IAssetFetcher assetFetcher) {
            // If the texture includes an image, read it in.
            OMV.UUID texID = faceInfo.textureEntry.TextureID;
            try {
                faceInfo.hasAlpha = (faceInfo.textureEntry.RGBA.A != 1.0f);
                if (texID != OMV.UUID.Zero && texID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                    faceInfo.textureID = texID;
                    faceInfo.persist = new BasilPersist(Gltf.MakeAssetURITypeImage, texID.ToString());
                    faceInfo.persist.GetUniqueTextureData(faceInfo, assetFetcher)
                        .Catch(e => {
                            // Could not get the texture. Print error and otherwise blank out the texture
                            faceInfo.textureID = null;
                            faceInfo.faceImage = null;
                            ConvOAR.Globals.log.ErrorFormat("{0} UpdateTextureInfo. {1}", _logHeader, e);
                        })
                        .Then(imgInfo => {
                            faceInfo.faceImage = imgInfo.image;
                            faceInfo.hasAlpha |= imgInfo.hasTransprency;
                        });
                }
            }
            catch (Exception e) {
                ConvOAR.Globals.log.ErrorFormat("{0}: UpdateFaceInfoWithTexture: exception updating faceInfo. id={1}: {2}",
                                    _logHeader, texID, e);
            }
        }
        */
    }
}
