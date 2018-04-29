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

using OpenSim.Services.Interfaces;

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
            StringBuilder buff = new StringBuilder();
            buff.AppendLine(" Invocation: convoar <parameters> inputOARfile");
            buff.AppendLine("where possible parameters are (negate bool parameters by prepending 'no'):");
            string[] paramDescs = Globals.parms.ParameterDefinitions.Select(pp => { return pp.ToString(); }).ToArray();
            buff.AppendLine(String.Join(Environment.NewLine, paramDescs));
            return buff.ToString();
        }

        static void Main(string[] args) {
            ConvOAR prog = new ConvOAR();
            prog.Start(args);
            return;
        }

        // If run from the command line, create instance and call 'Start' with args.
        // If run programmatically, create instance and call 'Start' with parameters.
        public void Start(string[] args) {
            Globals = new GlobalContext(new ConvoarParams(), new LoggerLog4Net());
            Globals.stats = new ConvoarStats();

            // A single parameter of '--help' outputs the invocation parameters
            if (args.Length == 2 && args[1] == "--help") {
                System.Console.Write(Invocation());
                return;
            }

            // 'ConvoarParams' initializes to default values.
            // Over ride default values with command line parameters.
            try {
                Globals.parms.MergeCommandLine(args, null, "InputOAR");
            }
            catch (Exception e) {
                Globals.log.ErrorFormat("ERROR: bad parameters: " + e.Message);
                Globals.log.ErrorFormat(Invocation());
                return;
            }

            if (Globals.parms.P<bool>("Verbose")) {
                Globals.log.SetVerbose(Globals.parms.P<bool>("Verbose"));
            }

            // Validate parameters
            if (String.IsNullOrEmpty(Globals.parms.P<string>("InputOAR"))) {
                Globals.log.ErrorFormat("An input OAR file must be specified");
                Globals.log.ErrorFormat(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(Globals.parms.P<string>("OutputDir"))) {
                _outputDir = "./out";
                Globals.log.DebugFormat("Output directory defaulting to {0}", _outputDir);
            }

            // Base asset storage system -- 'MemAssetService' is in-memory storage
            using (MemAssetService memAssetService = new MemAssetService()) {

                // 'assetFetcher' is the asset cache and fetching code -- where all the mesh,
                //    material, and instance information is stored for later processing.
                using (IAssetFetcher assetFetcher = new OSAssetFetcher(memAssetService)) {

                    try {
                        LoadOAR(memAssetService, assetFetcher, bScene => {
                            Globals.contextName = bScene.name;

                            Globals.log.DebugFormat("{0} Scene created. name={1}, instances={2}",
                                _logHeader, bScene.name, bScene.instances.Count);
                            Globals.log.DebugFormat("{0}    num assetFetcher.images={1}", _logHeader, assetFetcher.Images.Count);
                            Globals.log.DebugFormat("{0}    num assetFetcher.materials={1}", _logHeader, assetFetcher.Materials.Count);
                            Globals.log.DebugFormat("{0}    num assetFetcher.meshes={1}", _logHeader, assetFetcher.Meshes.Count);
                            Globals.log.DebugFormat("{0}    num assetFetcher.renderables={1}", _logHeader, assetFetcher.Renderables.Count);

                            // Perform any optimizations on the scene and its instances
                            if (Globals.parms.P<bool>("MergeSharedMaterialMeshes")) {
                                bScene = BSceneManipulation.MergeSharedMaterialMeshes(bScene);
                                Globals.log.DebugFormat("{0} merged BScene. numInstances={1}", _logHeader, bScene.instances.Count);
                            }

                            // Output the transformed scene as Gltf version 2
                            if (Globals.parms.P<bool>("ExportGltf")) {
                                Gltf gltf = new Gltf(bScene.name);

                                try {
                                    gltf.LoadScene(bScene, assetFetcher);

                                    Globals.log.DebugFormat("{0}   num Gltf.nodes={1}", _logHeader, gltf.nodes.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.meshes={1}", _logHeader, gltf.meshes.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.materials={1}", _logHeader, gltf.materials.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.images={1}", _logHeader, gltf.images.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.accessor={1}", _logHeader, gltf.accessors.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.buffers={1}", _logHeader, gltf.buffers.Count);
                                    Globals.log.DebugFormat("{0}   num Gltf.bufferViews={1}", _logHeader, gltf.bufferViews.Count);

                                    PersistRules.ResolveAndCreateDir(gltf.persist.filename);

                                    using (StreamWriter outt = File.CreateText(gltf.persist.filename)) {
                                        gltf.ToJSON(outt);
                                    }
                                    gltf.WriteBinaryFiles();

                                    if (Globals.parms.P<bool>("ExportTextures")) {
                                        gltf.WriteImages();
                                    }
                                }
                                catch (Exception e) {
                                    Globals.log.ErrorFormat("{0} Exception loading GltfScene: {1}", _logHeader, e);
                                }
                            }

                            // Output all the instances in the scene as individual GLTF files
                            if (Globals.parms.P<bool>("ExportIndividualGltf")) {
                                bScene.instances.ForEach(instance => {
                                    string instanceName = instance.handle.ToString();
                                    Gltf gltf = new Gltf(instanceName);
                                    gltf.persist.baseDirectory = bScene.name;
                                    // gltf.persist.baseDirectory = PersistRules.JoinFilePieces(bScene.name, instanceName);
                                    GltfScene gltfScene = new GltfScene(gltf, instanceName);
                                    gltf.defaultScene = gltfScene;

                                    Displayable rootDisp = instance.Representation;
                                    GltfNode rootNode = GltfNode.GltfNodeFactory(gltf, gltfScene, rootDisp, assetFetcher);
                                    rootNode.translation = instance.Position;
                                    rootNode.rotation = instance.Rotation;

                                    gltf.BuildAccessorsAndBuffers();
                                    gltf.UpdateGltfv2ReferenceIndexes();

                                    // After the building, get rid of the default scene name as we're not outputting a scene
                                    gltf.defaultScene = null;

                                    PersistRules.ResolveAndCreateDir(gltf.persist.filename);

                                    using (StreamWriter outt = File.CreateText(gltf.persist.filename)) {
                                        gltf.ToJSON(outt);
                                    }
                                    gltf.WriteBinaryFiles();

                                    if (Globals.parms.P<bool>("ExportTextures")) {
                                        gltf.WriteImages();
                                    }
                                });
                            }

                        });
                    }
                    catch (Exception e) {
                        Globals.log.ErrorFormat("{0} Global exception converting scene: {1}", _logHeader, e);
                        // A common error is not having all the DLLs for OpenSimulator. Print out what's missing.
                        ReflectionTypeLoadException refE = e as ReflectionTypeLoadException;
                        if (refE != null) {
                            foreach (var ee in refE.LoaderExceptions) {
                                Globals.log.ErrorFormat("{0} reference exception: {1}", _logHeader, ee);
                            }
                        }
                    }
                }
            }
        }

        // Initialization if using ConvOAR programmatically.
        public void Start(ConvoarParams pParameters, Logger pLogger) {
            Globals = new GlobalContext(pParameters, pLogger);
            Globals.stats = new ConvoarStats();
        }

        // Load the OARfile specified in Globals.params.InputOAR.
        // Parameters are in 'ConvOAR.Globals.params'.
        // For the moment, the OAR file must be specified with a string because of how OpenSimulator
        //     processes the input files. Note that the filename can be an 'http:' type URL.
        // (Tried building this with Promises but had execution problems so resorted to using a callback)
        public delegate void BSceneLoadedCallback(BScene loadedScene);
        public bool LoadOAR(IAssetService assetService, IAssetFetcher assetFetcher, BSceneLoadedCallback loadedCallback) {
            bool ret = false;

            BConverterOS converter = new BConverterOS();

            converter.ConvertOarToScene(assetService, assetFetcher)
                .Catch(e => {
                    throw (e);
                })
                .Then(bScene => {
                    loadedCallback(bScene);
                });

            return ret;
        }

    }
}
