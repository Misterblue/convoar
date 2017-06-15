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

using Nini.Config;
using log4net;

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

        Dictionary<string, string> _parameters;
        string _outputDir;
        string _inputOAR;

        // options passed to the OAR parser which are applied to the read content
        string _optDisplacement;
        string _optRotation;

        GlobalContext _context;

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
            _context = new GlobalContext(new ConvoarParams(), new Logger());

            _context.parms.MergeCommandLine(args);

            _parameters = ParameterParse.ParseArguments(args, /*firstOpFlag*/ false, /*multipleFiles*/ false);
            foreach (KeyValuePair<string, string> kvp in _parameters) {
                switch (kvp.Key) {
                    case "-d":
                        _outputDir = kvp.Value;
                        break;
                    case "--displacement":
                        _optDisplacement = kvp.Value;
                        break;
                    case "--rotation":
                        _optRotation = kvp.Value;
                        break;
                    case "--verbose":
                        _context.log.Verbose = true;
                        break;
                    case ParameterParse.LAST_PARAM:
                        _inputOAR = kvp.Value;
                        break;
                    case ParameterParse.ERROR_PARAM:
                        // this means the parser found something it didn't like
                        _context.log.LogError("Parameter error: " + kvp.Value);
                        _context.log.LogError(Invocation());
                        return;
                    default:
                        if (! _context.parms.SetParameterValue(kvp.Key, kvp.Value)) {
                            _context.log.LogError("ERROR: Unknown Parameter: " + kvp.Key);
                            _context.log.LogError(Invocation());
                            return;
                        }
                        break;
                }
            }

            // Validate parameters
            if (String.IsNullOrEmpty(_inputOAR)) {
                _context.log.LogError("An input OAR file must be specified");
                _context.log.LogError(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(_outputDir)) {
                _outputDir = "./out";
                _context.log.LogDebug("Output directory defaulting to {0}", _outputDir);
            }


            // Read in OAR
            Dictionary<string, object> options = new Dictionary<string, object>();
            // options.Add("merge", false);
            if (_optDisplacement != null) options.Add("displacement", _optDisplacement);
            if (_optRotation != null) options.Add("rotation", _optRotation);
            // options.Add("default-user", OMV.UUID.Random());
            // if (_optSkipAssets != null) options.Add('skipAssets', true);
            // if (_optForceTerrain != null) options.Add("force-terrain", true);
            // if (_optNoObjects != null) options.Add("no-objects", true);

            RegionInfo regionInfo = new RegionInfo(0, 0, null, "convoar");
            regionInfo.RegionName = "convoar";
            regionInfo.RegionSizeX = regionInfo.RegionSizeY = Constants.RegionSize;
            regionInfo.RegionID = OMV.UUID.Random();
            var estateSettings = new EstateSettings();
            estateSettings.EstateOwner = OMV.UUID.Random();
            regionInfo.EstateSettings = estateSettings;

            Scene scene = new Scene(regionInfo);

            // Add an in-memory asset service for all the loaded assets to go into
            MemAssetService memAssetService = new MemAssetService();
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

            // Load the archive into our scene
            ArchiveReadRequest archive = new ArchiveReadRequest(scene, _inputOAR, Guid.Empty, options);
            archive.DearchiveRegion(false);

            // Convert SOGs from OAR into EntityGroups
            _context.log.Log("Num assets = {0}", memAssetService.NumAssets);
            _context.log.Log("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

            // Convert all the loaded SOGs and images into meshes and our format


        }

        private PhysicsScene CreateSimplePhysicsEngine() {
            IConfigSource config = new IniConfigSource();
            config.AddConfig("Startup");
            config.Configs["Startup"].Set("physics", "basicphysics");

            PhysicsScene pScene = new BasicScene();
            INonSharedRegionModule mod = pScene as INonSharedRegionModule;
            mod.Initialise(config);

            return pScene;
        }
    }
}
