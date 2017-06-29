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
using System.Threading.Tasks;


namespace org.herbal3d.convoar {

    // Class passed around for global context for this region module instance.
    // NOT FOR PASSING DATA! Only used for global resources like logging, configuration
    //    parameters, statistics, and the such.
    public class GlobalContext {
        public ConvoarParams parms;
        public ConvoarStats stats;
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
        private static string _logHeader = "[ConvOAR]";

        public static GlobalContext Globals;

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
            Globals = new GlobalContext(new ConvoarParams(), new LoggerLog4Net());
            Globals.stats = new ConvoarStats();

            try {
                Globals.parms.MergeCommandLine(args, null, "InputOAR");
            }
            catch (Exception e) {
                Globals.log.ErrorFormat("ERROR: bad parameters: " + e.Message);
                Globals.log.ErrorFormat(Invocation());
                return;
            }

            if (Globals.parms.Verbose) {
                Globals.log.SetVerbose(Globals.parms.Verbose);
            }

            // Validate parameters
            if (String.IsNullOrEmpty(Globals.parms.InputOAR)) {
                Globals.log.ErrorFormat("An input OAR file must be specified");
                Globals.log.ErrorFormat(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(Globals.parms.OutputDirectory)) {
                _outputDir = "./out";
                Globals.log.DebugFormat("Output directory defaulting to {0}", _outputDir);
            }
           
            using (MemAssetService memAssetService = new MemAssetService()) {

                using (IAssetFetcher assetFetcher = new OSAssetFetcher(memAssetService)) {

                    BConverterOS converter = new BConverterOS();

                    try {
                        converter.ConvertOarToScene(memAssetService, assetFetcher)
                            .Catch(e => {
                                Globals.log.ErrorFormat("{0} Exception converting scene: {1}", _logHeader, e);
                            })
                            .Then(bScene => {
                                Globals.contextName = bScene.name;

                                Globals.log.DebugFormat("{0} Scene created. name={1}, instances={2}",
                                    _logHeader, bScene.name, bScene.instances.Count);
                                Globals.log.DebugFormat("{0}    num assetFetcher.images={1}", _logHeader, assetFetcher.Images.Count);
                                Globals.log.DebugFormat("{0}    num assetFetcher.materials={1}", _logHeader, assetFetcher.Materials.Count);
                                Globals.log.DebugFormat("{0}    num assetFetcher.meshes={1}", _logHeader, assetFetcher.Meshes.Count);
                                Globals.log.DebugFormat("{0}    num assetFetcher.renderables={1}", _logHeader, assetFetcher.Renderables.Count);

                                // Perform any optimizations on the scene and its instances

                                // Output the transformed scene
                                Gltf gltf = new Gltf();
                                gltf.LoadScene(bScene, assetFetcher);

                                Globals.log.DebugFormat("{0}   num Gltf.nodes={1}", _logHeader, gltf.nodes.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.meshes={1}", _logHeader, gltf.meshes.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.materials={1}", _logHeader, gltf.materials.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.images={1}", _logHeader, gltf.images.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.accessor={1}", _logHeader, gltf.accessors.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.buffers={1}", _logHeader, gltf.buffers.Count);
                                Globals.log.DebugFormat("{0}   num Gltf.bufferViews={1}", _logHeader, gltf.bufferViews.Count);

                                PersistRules.ResolveAndCreateDir(ConvOAR.Globals.parms.GltfTargetDir);
                                string gltfFilename = PersistRules.CreateFilename(PersistRules.AssetTypeGltf, bScene.name, "");

                                using (StreamWriter outt = File.CreateText(gltfFilename)) {
                                    gltf.ToJSON(outt);
                                }
                                gltf.WriteBinaryFiles();
                            }
                        );
                    }
                    catch (Exception e) {
                        Globals.log.ErrorFormat("{0} Exception converting scene: {1}", _logHeader, e);
                    }
                }
            }
        }
    }
}
