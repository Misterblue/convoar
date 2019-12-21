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
using System.Threading.Tasks;
using System.Xml;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.CoreModules;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.CoreModules.World.Archiver;
using OpenSim.Region.PhysicsModule.BasicPhysics;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Tests.Common;
using OpenSim.Data.Null;

using org.herbal3d.cs.CommonEntities;
using org.herbal3d.cs.CommonEntitiesUtil;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.convoar {
    // Convert things from OpenSimulator to Instances and Displayables things
    public class OarConverter {
        private static readonly string _logHeader = "[OarConverter]";

        private readonly BLogger _log;
        private readonly IParameters _params;
        private readonly BConverterOS _converter;

        public OarConverter(BLogger pLog, IParameters pParams) {
            _log = pLog;
            _params = pParams;
            _converter = new BConverterOS(pLog, pParams);
        }

        public async Task<BScene> ConvertOarToScene(IAssetService assetService, AssetManager assetManager) {

            // Assemble all the parameters that loadoar takes and uses
            Dictionary<string, object> options = new Dictionary<string, object> {
                // options.Add("merge", false);
                { "displacement", _params.P<OMV.Vector3>("Displacement") }
            };
            string optRotation = _params.P<string>("Rotation");
            if (optRotation != null) options.Add("rotation", float.Parse(optRotation, System.Threading.Thread.CurrentThread.CurrentCulture));
            // options.Add("default-user", OMV.UUID.Random());
            // if (optSkipAssets != null) options.Add('skipAssets', true);
            // if (optForceTerrain != null) options.Add("force-terrain", true);
            // if (optNoObjects != null) options.Add("no-objects", true);
            string optSubRegion = _params.P<string>("SubRegion");
            if (optSubRegion != null) {
                List<float> bounds = optSubRegion.Split(',').Select<string, float>(x => { return float.Parse(x); }).ToList();
                options.Add("bounding-origin", new OMV.Vector3(bounds[0], bounds[1], bounds[2]));
                options.Add("bounding-size", new OMV.Vector3(bounds[3] - bounds[0], bounds[4] - bounds[1], bounds[5] - bounds[2]));
            }

            // Create an OpenSimulator region and scene to load the OAR into
            string regionName = "convoar";
            if (String.IsNullOrEmpty(_params.P<String>("RegionName"))) {
                // Try to build the region name from the OAR filesname
                regionName = Path.GetFileNameWithoutExtension(_params.P<string>("InputOAR"));
            }
            else {
                regionName = _params.P<string>("RegionName");
            }
            Scene scene = CreateScene(assetService, regionName);

            // Load the archive into our scene
            ArchiveReadRequest archive = new ArchiveReadRequest(scene, _params.P<string>("InputOAR"), Guid.Empty, options);
            archive.DearchiveRegion(false);

            return await _converter.ConvertRegionToBScene(scene, assetManager);
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
                _log.Log("BConverterOS.CreateScene: exception adding region:");
                _log.Log(errorMessage);
            }
            catch (Exception e) {
                _log.Log("BConverterOS.CreateScene: exception adding region: {0}", e);
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

        public void LogBProgress(string msg, params Object[] args) {
            if (_params.P<bool>("LogBuilding")) {
                _log.Log(msg, args);
            }
        }
    }
}
