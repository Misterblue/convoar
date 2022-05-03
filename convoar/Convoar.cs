/*
 * Copyright (c) 2022 Robert Adams
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
using System.Threading;
using System.Threading.Tasks;

using OpenSim.Services.Interfaces;

using org.herbal3d.cs.CommonEntities;
using org.herbal3d.cs.CommonUtil;

namespace org.herbal3d.convoar {

    // Class passed around for global context for this region module instance.
    // NOT FOR PASSING DATA! Only used for global resources like logging, configuration
    //    parameters, statistics, and the such.
    public class GlobalContext {
        public ConvoarParams parms;
        public ConvoarStats stats;
        public BLogger log;
        public string contextName;  // a unique identifier for this context -- used in filenames, ...
        public string version;
        public string versionLong;
        public string buildDateShort;
        public string gitCommit;

        public GlobalContext()
        {
            stats = null;
            contextName = String.Empty;
            version = VersionInfo.appVersion;
            buildDateShort = VersionInfo.buildDate;
            versionLong = VersionInfo.longVersion;
        }
    }

    class ConvOAR {
        private static readonly string _logHeader = "[ConvOAR]";

        public static GlobalContext Globals;

        string _outputDir;

        private string Invocation() {
            StringBuilder buff = new StringBuilder();
            buff.AppendLine("Invocation: convoar <parameters> inputOARfile");
            buff.AppendLine("   Possible parameters are (negate bool parameters by prepending 'no'):");
            string[] paramDescs = Globals.parms.ListParameters().Select(kvp => { return kvp.Key + ": " + kvp.Value; }).ToArray();
            buff.AppendLine(String.Join(Environment.NewLine, paramDescs));
            return buff.ToString();
        }

        static void Main(string[] args) {
            ConvOAR prog = new ConvOAR();
            CancellationToken cancelToken = new CancellationToken();
            prog.Start(cancelToken, args).Wait();
            return;
        }

        // If run from the command line, create instance and call 'Start' with args.
        // If run programmatically, create instance and call 'Start' with parameters.
        public async Task Start(CancellationToken cancelToken, string[] args) {
            var parms = new ConvoarParams();
            var logger = new BLoggerNLog(parms.LogFilename, parms.LogToConsole, parms.LogToFiles);
            // var logger = new LoggerLog4Net(),
            // var logger = new LoggerConsole(),

            Globals = new GlobalContext() {
                log = logger,
                stats = new ConvoarStats(),
                parms = parms,
            };

            // A single parameter of '--help' outputs the invocation parameters
            if (args.Length > 0 && args[0] == "--help") {
                System.Console.Write(Invocation());
                return;
            }

            // 'ConvoarParams' initializes to default values.
            // Over ride default values with command line parameters.
            try {
                // Note that trailing parameters will be put into "InputOAR" parameter
                Globals.parms.MergeCommandLine(args, null, "InputOAR");
            }
            catch (Exception e) {
                Globals.log.Error("ERROR: bad parameters: " + e.Message);
                Globals.log.Error(Invocation());
                return;
            }

            if (Globals.parms.Verbose) {
                Globals.log.SetLogLevel(LogLevels.Debug);
            }

            if (!Globals.parms.Quiet) {
                System.Console.WriteLine("Convoar " + Globals.versionLong);
                System.Console.WriteLine("   using Herbal3d.CommonEntities " + org.herbal3d.cs.CommonEntities.VersionInfo.longVersion);
                System.Console.WriteLine("   using Herbal3d.CommonEntitiesConv " + org.herbal3d.cs.CommonEntitiesConv.VersionInfo.longVersion);
                System.Console.WriteLine("   using Herbal3d.CommonUtil " + org.herbal3d.cs.CommonUtil.VersionInfo.longVersion);
            }

            // Validate parameters
            if (String.IsNullOrEmpty(Globals.parms.InputOAR)) {
                Globals.log.Error("An input OAR file must be specified");
                Globals.log.Error(Invocation());
                return;
            }
            if (String.IsNullOrEmpty(Globals.parms.OutputDir)) {
                _outputDir = "./out";
                Globals.log.Debug("Output directory defaulting to {0}", _outputDir);
            }

            // Base asset storage system -- 'MemAssetService' is in-memory storage
            using (MemAssetService memAssetService = new MemAssetService()) {

                // 'assetManager' is the asset cache and fetching code -- where all the mesh,
                //    material, and instance information is stored for later processing.
                using (AssetManager assetManager = new AssetManager(memAssetService, Globals.log, Globals.parms.OutputDir)) {

                    try {
                        BScene bScene = await LoadOAR(memAssetService, assetManager);

                        Globals.contextName = bScene.name;

                        Globals.log.Debug("{0} Scene created. name={1}, instances={2}",
                            _logHeader, bScene.name, bScene.instances.Count);
                        Globals.log.Debug("{0}    num assetFetcher.images={1}", _logHeader, assetManager.Assets.Images.Count);
                        Globals.log.Debug("{0}    num assetFetcher.materials={1}", _logHeader, assetManager.Assets.Materials.Count);
                        Globals.log.Debug("{0}    num assetFetcher.meshes={1}", _logHeader, assetManager.Assets.Meshes.Count);
                        Globals.log.Debug("{0}    num assetFetcher.renderables={1}", _logHeader, assetManager.Assets.Renderables.Count);

                        if (ConvOAR.Globals.parms.TerrainOnly) {
                            ConvOAR.Globals.log.Debug("{0} Clearing out scene so there's only terrain (TerrainOnly)", _logHeader);
                            bScene.instances.Clear();
                            bScene.instances.Add(bScene.terrainInstance);
                        }

                        /*
                        // Perform any optimizations on the scene and its instances
                        if (Globals.parms.P<bool>("DoMeshSimplification")) {
                            // TODO:
                        }
                        if (Globals.parms.P<bool>("DoSceneOptimizations")) {
                            using (BSceneManipulation optimizer = new BSceneManipulation()) {
                                bScene = optimizer.OptimizeScene(bScene);
                                Globals.log.DebugFormat("{0} merged BScene. numInstances={1}", _logHeader, bScene.instances.Count);
                            }
                        }
                        */
                        if (Globals.parms.MergeSharedMaterialMeshes) {
                            // using (BSceneManipulation optimizer = new BSceneManipulation(Globals.log, Globals.parms)) {
                            var manipParams = new ParamBlock(new Dictionary<string, object> {
                                { "SeparateInstancedMeshes", false },
                                { "MergeSharedMaterialMeshes", false },
                                { "MeshShareThreshold", 5 }

                            });
                            using (BSceneManipulation optimizer = new BSceneManipulation(Globals.log, manipParams)) {
                                bScene = optimizer.RebuildSceneBasedOnSharedMeshes(bScene);
                                Globals.log.Debug("{0} merged meshes in scene. numInstances={1}", _logHeader, bScene.instances.Count);
                            }
                        }

                        // Output the transformed scene as Gltf version 2
                        GltfB gltf = new GltfB(bScene.name, Globals.log, new gltfParamsB() {
                            inputOAR = Globals.parms.InputOAR,
                            uriBase = Globals.parms.URIBase,
                            verticesMaxForBuffer = Globals.parms.VerticesMaxForBuffer,
                            gltfCopyright = Globals.parms.GltfCopyright,
                            addUniqueCodes = Globals.parms.AddUniqueCodes,
                            doubleSided = Globals.parms.DoubleSided,
                            textureMaxSize = Globals.parms.TextureMaxSize,
                            logBuilding = Globals.parms.LogBuilding,
                            logGltfBuilding = Globals.parms.LogGltfBuilding,
                            versionLong = Globals.versionLong
                        });

                        try {
                            gltf.LoadScene(bScene);

                            Globals.log.Debug("{0}   num Gltf.nodes={1}", _logHeader, gltf.nodes.Count);
                            Globals.log.Debug("{0}   num Gltf.meshes={1}", _logHeader, gltf.meshes.Count);
                            Globals.log.Debug("{0}   num Gltf.materials={1}", _logHeader, gltf.materials.Count);
                            Globals.log.Debug("{0}   num Gltf.images={1}", _logHeader, gltf.images.Count);
                            Globals.log.Debug("{0}   num Gltf.accessor={1}", _logHeader, gltf.accessors.Count);
                            Globals.log.Debug("{0}   num Gltf.buffers={1}", _logHeader, gltf.buffers.Count);
                            Globals.log.Debug("{0}   num Gltf.bufferViews={1}", _logHeader, gltf.bufferViews.Count);
                        }
                        catch (Exception e) {
                            Globals.log.Error("{0} Exception loading GltfScene: {1}", _logHeader, e);
                        }

                        try {
                            if (gltf.scenes.Count > 0) {
                                string gltfFilename = gltf.GetFilename(gltf.IdentifyingString);
                                using (var outm = new MemoryStream()) {
                                    using (var outt = new StreamWriter(outm)) {
                                        gltf.ToJSON(outt);
                                    }
                                    await assetManager.AssetStorage.Store(gltfFilename, outm.ToArray());
                                }
                                gltf.WriteBinaryFiles(assetManager.AssetStorage);

                                if (Globals.parms.ExportTextures) {
                                    gltf.WriteImages(assetManager.AssetStorage);
                                }
                            }
                            else {
                                Globals.log.Error("{0} Not writing out GLTF because no scenes", _logHeader);
                            }
                        }
                        catch (Exception e) {
                            Globals.log.Error("{0} Exception writing GltfScene: {1}", _logHeader, e);
                        }

                        /*
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
                        */
                    }
                    catch (Exception e) {
                        Globals.log.Error("{0} Global exception converting scene: {1}", _logHeader, e);
                        // A common error is not having all the DLLs for OpenSimulator. Print out what's missing.
                        if (e is ReflectionTypeLoadException refE) {
                            foreach (var ee in refE.LoaderExceptions) {
                                Globals.log.Error("{0} reference exception: {1}", _logHeader, ee);
                            }
                        }
                    }
                }
            }
        }

        // Initialization if using ConvOAR programmatically.
        public void Start(ConvoarParams pParameters, BLogger pLogger) {
            Globals = new GlobalContext() {
                log = pLogger,
                parms = pParameters,
                stats = new ConvoarStats()
            };
        }

        // Load the OARfile specified in Globals.params.InputOAR.
        // Parameters are in 'ConvOAR.Globals.params'.
        // For the moment, the OAR file must be specified with a string because of how OpenSimulator
        //     processes the input files. Note that the filename can be an 'http:' type URL.
        public async Task<BScene> LoadOAR(IAssetService assetService, AssetManager assetManager) {
            BScene ret = null;

            try {
                OarConverter converter = new OarConverter(Globals.log, Globals.parms);
                ret = await converter.ConvertOarToScene(assetService, assetManager);
            }
            catch (Exception e) {
                ConvOAR.Globals.log.Error("{0} LoadOAR exception: {1}", _logHeader, e);
                throw (e);
            }
            return ret;
        }

    }
}
