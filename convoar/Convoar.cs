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

            // There used to be GLTF version 1 and version 2. Version 1 is no
            //     more so force version 2.
            if (Globals.parms.P<string>("ExportFormat") == "gltf") {
                Globals.parms.SetParameterValue("ExportFormat", "gltf2");
            }

            // If the format is not gltf, must use Assimp
            if (Globals.parms.P<string>("ExportFormat") != "gltf2") {
                Globals.parms.SetParameterValue("UseAssimp", "true");
            }
            else {
                // Note that 'UseAssimp' could have been forced to 'true' to use it for GLTF
                Globals.parms.SetParameterValue("ExportGltf", "true");
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

                            // Create reduced resolution versions of the images
                            List<int> textureSizes = Globals.parms.P<string>("ReducedTextureSizes").Split(',').Select<string,int>(x => {
                                return int.Parse(x);
                            }).ToList();
                            List<ImageInfo> resizedImages = new List<ImageInfo>();
                            textureSizes.ForEach(maxTextureSize => {
                                assetFetcher.Images.ForEach(img => {
                                    if (img.image != null && (img.image.Width > maxTextureSize || img.image.Height > maxTextureSize)) {
                                        // See if the image has already been resized and is in the filesystem
                                        PersistRules pr = img.persist.Clone();
                                        pr.baseDirectory = PersistRules.JoinFilePieces(img.persist.baseDirectory, maxTextureSize.ToString());
                                        ImageInfo newImage = new ImageInfo();
                                        newImage.imageIdentifier = img.imageIdentifier;   // the new one is the same image
                                        newImage.persist.baseDirectory = pr.baseDirectory;
                                        if (File.Exists(pr.filename)) {
                                            // If the file exists, just read it in
                                            newImage.SetImage(System.Drawing.Image.FromFile(pr.filename));
                                        }
                                        else {
                                            // If it hasn't been cached, create the resized image
                                            newImage.SetImage(img.image);
                                            newImage.ConstrainTextureSize(maxTextureSize);
                                        }
                                        resizedImages.Add(newImage);
                                    }
                                });
                            });
                            // The resized versions of the images go back into the list of available images.
                            // Note: all images have the same UUID handle (since they are the same image. You need to
                            //     use the asset fetch with image size to get the version needed.
                            if (resizedImages.Count > 0) {
                                resizedImages.ForEach(img => {
                                    assetFetcher.Images.Add(img.GetBHash(), img.handle, img);
                                    // Globals.log.DebugFormat("{0} resized image: {1} to {2}", _logHeader, img, img.persist.filename);
                                });
                            }

                            if (Globals.parms.P<bool>("UseAssimp")) {
                                Globals.log.DebugFormat("{0} initializing Assimp", _logHeader);

                                using (AssimpInterface assimp = new AssimpInterface()) {
                                    Assimp.Scene aScene = assimp.ConvertBSceneToAssimpScene(bScene, assetFetcher,
                                                    Globals.parms.P<int>("TextureMaxSize"));

                                    if (Globals.parms.P<bool>("ExportTextures")) {
                                        assimp.WriteImages(aScene);
                                    }

                                    // format dae, desc = COLLADA - Digital Asset Exchange Schema, id = collada
                                    // format x, desc = X Files, id = x
                                    // format stp, desc = Step Files, id = stp
                                    // format obj, desc = Wavefront OBJ format, id = obj
                                    // format obj, desc = Wavefront OBJ format without material file, id = objnomtl
                                    // format stl, desc = Stereolithography, id = stl
                                    // format stl, desc = Stereolithography(binary), id = stlb
                                    // format ply, desc = Stanford Polygon Library, id = ply
                                    // format ply, desc = Stanford Polygon Library(binary), id = plyb
                                    // format 3ds, desc = Autodesk 3DS(legacy), id = 3ds
                                    // format gltf, desc = GL Transmission Format, id = gltf
                                    // format glb, desc = GL Transmission Format(binary), id = glb
                                    // format gltf2, desc = GL Transmission Format v. 2, id = gltf2
                                    // format assbin, desc = Assimp Binary, id = assbin
                                    // format assxml, desc = Assxml Document, id = assxml
                                    // format x3d, desc = Extensible 3D, id = x3d
                                    // format 3mf, desc = The 3MF - File - Format, id = 3mf

                                    /*
                                    Assimp.PostProcessSteps postProcessingFlags = Assimp.PostProcessSteps.None;
                                    // Flips all UV coordinates along the y-axis
                                    // and adjusts material settings/bitangents accordingly.
                                    if (Globals.parms.P<bool>("FlipUVs"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.FlipUVs;
                                    // Searches for redundant/unreferenced materials and removes them.
                                    if (Globals.parms.P<bool>("RemoveRedundantMaterials"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.RemoveRedundantMaterials;
                                    // Re-orders triangles for better vertex cache locality.
                                    if (Globals.parms.P<bool>("ImproveCacheLocality"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.ImproveCacheLocality;
                                    // This step converts non-UV mappings (such as spherical or
                                    // cylindrical mapping) to proper texture coordinate channels.
                                    if (Globals.parms.P<bool>("TransformUVCoords"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.TransformUVCoords;
                                    // Identifies and joins identical vertex data sets within all imported meshes.
                                    if (Globals.parms.P<bool>("JoinIdenticalVertices"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.JoinIdenticalVertices;
                                    // Optimizes scene hierarchy. Nodes with no animations, bones,
                                    // lights, or cameras assigned are collapsed and joined.
                                    if (Globals.parms.P<bool>("OptimizeGraph"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.OptimizeGraph;
                                    // Attempts to reduce the number of meshes (and draw calls). 
                                    if (Globals.parms.P<bool>("OptimizeMeshes"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.OptimizeMeshes;
                                    // Removes the node graph and "bakes" (pre-transforms) all
                                    // vertices with the local transformation matrices of their nodes.
                                    if (Globals.parms.P<bool>("PreTransformVertices"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.PreTransformVertices;
                                    // Splits large meshes into smaller submeshes.
                                    if (Globals.parms.P<bool>("SplitLargeMeshes"))
                                        postProcessingFlags |= Assimp.PostProcessSteps.SplitLargeMeshes;
                                        */

                                    string exportFormat = Globals.parms.P<string>("ExportFormat");
                                    Globals.log.DebugFormat("{0}: Doing Assimp export to format '{1}'", _logHeader, exportFormat);
                                    string ext = assimp.GetFileExtensionForFormat(exportFormat);
                                    // Kludge since 'gltf2' standard changed so extension is just 'gltf'
                                    ext = (ext == "gltf2") ? "gltf" : ext;
                                    // assimp.Export(aScene, aScene.RootNode.Name + "." + ext, exportFormat, postProcessingFlags);
                                    assimp.Export(aScene, aScene.RootNode.Name + "." + ext, exportFormat);
                                    Globals.log.DebugFormat("{0}: Export completed", _logHeader, exportFormat);
                                }
                            }
                            else {
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
