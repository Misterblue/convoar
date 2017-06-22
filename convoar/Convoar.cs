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
using System.Text;
using System.Threading.Tasks;

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

using Nini;

using RSG;

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {

    // Class passed around for global context for this region module instance.
    // NOT FOR PASSING DATA! Only used for global resources like logging, configuration
    //    parameters, statistics, and the such.
    public class GlobalContext {
        public ConvoarParams parms;
        public BasilStats stats;
        public Logger log;
        public string contextName;  // a unique identifier for this context -- used in filenames, ...

        public GlobalContext(ConvoarParams pParms, Logger pLog)
        {
            parms = pParms;
            log = pLog;
            stats = null;
            contextName = String.Empty;
        }
    }

    class ConvOAR {

        GlobalContext _context;

        string _outputDir;

        private string Invocation() {
            return @"Invocation:
convoar
     -d directoryForOutputFiles
     --displacement <x,y,z>
     --rotation degrees
     --verbose
     inputOARfile
";

        }

        static void Main(string[] args) {
            ConvOAR prog = new ConvOAR();
            prog.Start(args);
            return;
        }

        public void Start(string[] args) {
            _context = new GlobalContext(new ConvoarParams(), new LoggerLog4Net());

            try {
                _context.parms.MergeCommandLine(args, null, "InputOAR");
            }
            catch (Exception e) {
                _context.log.ErrorFormat("ERROR: bad parameters: " + e.Message);
                _context.log.ErrorFormat(Invocation());
                return;
            }

            // Validate parameters
            if (String.IsNullOrEmpty(_context.parms.InputOAR)) {
                _context.log.ErrorFormat("An input OAR file must be specified");
                _context.log.ErrorFormat(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(_context.parms.OutputDirectory)) {
                _outputDir = "./out";
                _context.log.DebugFormat("Output directory defaulting to {0}", _outputDir);
            }

            // Read in OAR
            Dictionary<string, object> options = new Dictionary<string, object>();
            // options.Add("merge", false);
            string optDisplacement = _context.parms.Displacement;
            if (optDisplacement != null) options.Add("displacement", optDisplacement);
            string optRotation = _context.parms.Rotation;
            if (optRotation != null) options.Add("rotation", optRotation);
            // options.Add("default-user", OMV.UUID.Random());
            // if (_optSkipAssets != null) options.Add('skipAssets', true);
            // if (_optForceTerrain != null) options.Add("force-terrain", true);
            // if (_optNoObjects != null) options.Add("no-objects", true);

            using (MemAssetService memAssetService = new MemAssetService()) {

                Scene scene = CreateScene(memAssetService);

                // Load the archive into our scene
                ArchiveReadRequest archive = new ArchiveReadRequest(scene, _context.parms.InputOAR, Guid.Empty, options);
                archive.DearchiveRegion(false);

                // Convert SOGs from OAR into EntityGroups
                _context.log.Log("Num assets = {0}", memAssetService.NumAssets);
                _context.log.Log("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

                // Convert all the loaded SOGs and images into meshes and our format
                BConverterOS converter = new BConverterOS(_context);

                IAssetFetcher assetFetcher = new OSAssetFetcher(scene, memAssetService, _context);

                PrimToMesh mesher = new PrimToMesh(_context);

                Promise<BInstance>.All(
                    scene.GetSceneObjectGroups().Select(sog => {
                        return converter.Convert(sog, assetFetcher, mesher);
                    })
                )
                .Catch(e => {
                })
                .Done(instances => {
                    _context.log.Log("Num instances = {0}", instances.ToList().Count);
                    
                });

            }
        }

        // Create an OpenSimulator Scene and add enough auxillery services and objects
        //   to it so it will do a asset load;
        public static Scene CreateScene(MemAssetService memAssetService) {
            RegionInfo regionInfo = new RegionInfo(0, 0, null, "convoar");
            regionInfo.RegionName = "convoar";
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

        public static PhysicsScene CreateSimplePhysicsEngine() {
            Nini.Config.IConfigSource config = new Nini.Config.IniConfigSource();
            config.AddConfig("Startup");
            config.Configs["Startup"].Set("physics", "basicphysics");

            PhysicsScene pScene = new BasicScene();
            INonSharedRegionModule mod = pScene as INonSharedRegionModule;
            mod.Initialise(config);

            return pScene;
        }
    }
}
