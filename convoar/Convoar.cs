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

        // If run from the command line, create instance and call 'Start' with args.
        // If run programmatically, create instance and call 'Start' with parameters.
        public void Start(string[] args) {
            Globals = new GlobalContext(new ConvoarParams(), new LoggerLog4Net());
            Globals.stats = new ConvoarStats();

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

            if (Globals.parms.Verbose) {
                Globals.log.SetVerbose(Globals.parms.Verbose);
            }

            // Validate parameters
            if (String.IsNullOrEmpty(Globals.parms.InputOAR)) {
                Globals.log.ErrorFormat("An input OAR file must be specified");
                Globals.log.ErrorFormat(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(Globals.parms.OutputDir)) {
                _outputDir = "./out";
                Globals.log.DebugFormat("Output directory defaulting to {0}", _outputDir);
            }

            // Base asset storage system -- 'MemAssetService' is in-memory storage
            using (MemAssetService memAssetService = new MemAssetService()) {

                // Asset cache and fetching wrapper code -- where all the mesh, material, and instance
                //    information is stored for later processing.
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

                            // Create reduced resolution versions of the images
                            int maxTextureSize = Globals.parms.MaxTextureSize;
                            List<ImageInfo> resizedImages = new List<ImageInfo>();
                            assetFetcher.Images.ForEach(delegate (ImageInfo img) {
                                if (img.image != null && (img.image.Width > maxTextureSize || img.image.Height > maxTextureSize)) {
                                    ImageInfo newImage = img.Clone();
                                    newImage.imageIdentifier = img.imageIdentifier;   // the new one is the same image
                                    newImage.ConstrainTextureSize(maxTextureSize);
                                    // The resized images go into a subdir named after the new size
                                    newImage.persist.baseDirectory =
                                        PersistRules.JoinFilePieces(newImage.persist.baseDirectory, maxTextureSize.ToString());
                                    resizedImages.Add(newImage);
                                }
                            });
                            if (resizedImages.Count > 0) {
                                resizedImages.ForEach(img => {
                                    assetFetcher.Images.Add(img.GetBHash(), img.handle, img);
                                    // Globals.log.DebugFormat("{0} resized image: {1} to {2}", _logHeader, img, img.persist.filename);
                                });
                            }

                            // Output the transformed scene as Gltf version 1 or 2
                            if (Globals.parms.ExportGltf) {
                                Gltf gltf = new Gltf(bScene.name, 1);

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

                                    if (Globals.parms.ExportTextures) {
                                        gltf.WriteImages();
                                    }
                                }
                                catch (Exception e) {
                                    Globals.log.ErrorFormat("{0} Exception loading GltfScene: {1}", _logHeader, e);
                                }
                            }
                            if (Globals.parms.ExportGltf2) {
                                Gltf gltf = new Gltf(bScene.name, 2);

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

                                    if (Globals.parms.ExportTextures) {
                                        gltf.WriteImages();
                                    }
                                }
                                catch (Exception e) {
                                    Globals.log.ErrorFormat("{0} Exception loading GltfScene: {1}", _logHeader, e);
                                }
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
