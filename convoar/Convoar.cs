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

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {

    class ConvOAR {

        Dictionary<string, string> _parameters;
        string _outputDir;
        string _inputOAR;

        // options passed to the OAR parser which are applied to the read content
        string _optDisplacement;
        string _optRotation;

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
                        Logger.Verbose = true;
                        break;
                    case ParameterParse.LAST_PARAM:
                        _inputOAR = kvp.Value;
                        break;
                    case ParameterParse.ERROR_PARAM:
                        // this means the parser found something it didn't like
                        Logger.LogError("Parameter error: " + kvp.Value);
                        Logger.LogError(Invocation());
                        return;
                    default:
                        Logger.LogError("ERROR: Unknown Parameter: " + kvp.Key);
                        Logger.LogError(Invocation());
                        return;

                }
            }

            // Validate parameters
            if (String.IsNullOrEmpty(_inputOAR)) {
                Logger.LogError("An input OAR file must be specified");
                Logger.LogError(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(_outputDir)) {
                _outputDir = "./out";
                Logger.LogDebug("Output directory defaulting to {0}", _outputDir);
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

            scene.LandChannel = new TestLandChannel(scene); // simple land with no parcels
            var terrainModule = new TerrainModule();
            terrainModule.AddRegion(scene);

            SceneManager.Instance.Add(scene);

            // Load the archive into our scene
            ArchiveReadRequest archive = new ArchiveReadRequest(scene, _inputOAR, Guid.Empty, options);
            archive.DearchiveRegion();

            // Convert SOGs from OAR into EntityGroups
            Logger.Log("Num assets = {0}", memAssetService.NumAssets);
            Logger.Log("Num SOGs = {0}", scene.GetSceneObjectGroups().Count);

        }
    }
}
