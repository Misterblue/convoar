/*
 * Copyright (c) 2016 Robert Adams
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
/*
 * Some code covered by: Copyright (c) Contributors, http://opensimulator.org/
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using org.herbal3d.cs.CommonUtil;

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {
    public class ConfigParam : Attribute {
        public ConfigParam(string name, Type valueType, string desc = "", string alt = null) {
            this.name = name;
            this.valueType = valueType;
            this.desc = desc;
            this.alt = alt;
        }
        public string name;
        public Type valueType;
        public string desc;
        public string alt;
    }

    public class ConvoarParams {

        public ConvoarParams() {
        }

        // ========== General Input and Output Parameters
        [ConfigParam(name: "InputOAR", valueType: typeof(string), desc: "Input OAR file", alt: "i")]
        public string InputOAR = null;
        [ConfigParam(name: "OutputDir", valueType: typeof(string), desc: "Directory (relative to current dir) to store output files", alt: "d")]
        public string OutputDir = "./convoar";
        [ConfigParam(name: "URIBase", valueType: typeof(string), desc: "String added to the beginning of asset name to create URI")]
        public string URIBase = ""; 
        [ConfigParam(name: "UseReadableFilenames", valueType: typeof(bool), desc: "Whether filenames should be human readable or UUIDs")]
        public bool UseReadableFilenames = true; 
        [ConfigParam(name: "UseDeepFilenames", valueType: typeof(bool), desc: "Whether filenames organized into deep directory structure")]
        public bool UseDeepFilenames = false; 
        [ConfigParam(name: "WriteBinaryGltf", valueType: typeof(bool), desc: "Whether to write .gltf or .glb file")]
        public bool WriteBinaryGltf = false;

        // ========== OAR Reading Specific Parameters"
        [ConfigParam(name: "ConvoarID", valueType: typeof(string), desc: "GUID for 'convoar' identity (used for CreatorID, ...)")]
        public string ConvoarID = "e67a2ff8-597d-4f03-b559-930aeaf4836b";
        [ConfigParam(name: "RegionName", valueType: typeof(string), desc: "Name to use for the region (default generated from OAR filename)")]
        public string RegionName = String.Empty;
        [ConfigParam(name: "Displacement", valueType: typeof(OMV.Vector3), desc: "Optional displacement to add to OAR entities")]
        public OMV.Vector3 Displacement = OMV.Vector3.Zero;
        [ConfigParam(name: "Rotation", valueType: typeof(float), desc: "Optional rotation to add to OAR entities")]
        public float Rotation = 0;
        [ConfigParam(name: "SubRegion", valueType: typeof(string), desc: "Bounds of subregion to export. upper-right to lower-left as (X,Y,Z,X,Y,Z). Whole region if empty or null")]
        public string SubRegion = null;

        // ========== Optimizations
        [ConfigParam(name: "MergeSharedMaterialMeshes", valueType: typeof(bool), desc: "whether to merge meshes with similar materials", alt: "m")]
        public bool MergeSharedMaterialMeshes = false;
        [ConfigParam(name: "UseOpenJPEG", valueType: typeof(bool), desc: "Use OpenJPEG to decode JPEG2000 images. Alternative is CSJ2K")]
        public bool UseOpenJPEG = true;

        /*
        [ConfigParam(name: "DoMeshSimplification", valueType: typeof(bool), desc: "pass over all the meshes and simplify if needed")]
        public bool DoMeshSimplification = true;
        [ConfigParam(name: "DoSceneOptimizations", valueType: typeof(bool), desc: "optimize the instances in the scene")]
        public bool DoSceneOptimizations = true;
        [ConfigParam(name: "SeparateInstancedMeshes", valueType: typeof(bool), desc: "whether to find instanced meshes and not do shared meshes with them")]
        public bool SeparateInstancedMeshes = true;
        [ConfigParam(name:"MeshShareThreshold", valueType: typeof(int), desc: "meshes used more than this many times are not material combined")]
        public int MeshShareThreshold = 5;
        [ConfigParam(name: "CreateStaticLayer", valueType: typeof(bool), desc: "whether to merge meshes with similar materials in static objects")]
        public bool CreateStaticLayer = false;
        [ConfigParam(name: "CreateDynamicLayer", valueType: typeof(bool), desc: "whether to merge meshes within non-static entities ")]
        public bool CreateDynamicLayer = false;
        */

        // ========== Export Parameters
        [ConfigParam(name: "TerrainOnly", valueType: typeof(bool), desc: "Only create and output the terrain and terrain texture")]
        public bool TerrainOnly = false;
        [ConfigParam(name: "GltfCopyright", valueType: typeof(string), desc: "Copyright notice embedded into generated GLTF files")]
        public string GltfCopyright = "Copyright 2022. All rights reserved";
        [ConfigParam(name: "ExportTextures", valueType: typeof(bool), desc: "Convert textures to PNGs and export to target dir")]
        public bool ExportTextures = true;
        [ConfigParam(name: "TexturesDir", valueType: typeof(string), desc: "sub-directory for all the image files")]
        public string TexturesDir = "images";
        [ConfigParam(name: "TextureMaxSize", valueType: typeof(int), desc: "The maximum size of textures for a simple export")]
        public int TextureMaxSize = 256;
        [ConfigParam(name: "PreferredTextureFormat", valueType: typeof(string), desc: "One of: PNG, JPG, GIF, BMP")]
        public string PreferredTextureFormat = "PNG";
        [ConfigParam(name: "PreferredTextureFormatIfNoTransparency", valueType: typeof(string), desc: "One of: PNG, JPG, GIF, BMP")]
        public string PreferredTextureFormatIfNoTransparency = "JPG";
        [ConfigParam(name: "VerticesMaxForBuffer", valueType: typeof(int), desc: "Number of vertices to cause splitting of buffer files")]
        public int VerticesMaxForBuffer = 50000;
        [ConfigParam(name: "DisplayTimeScaling", valueType: typeof(bool), desc: "If to delay mesh scaling to display/GPU time")]
        public bool DisplayTimeScaling = false;
        [ConfigParam(name: "DoubleSided", valueType: typeof(bool), desc: "specify whether double sided mesh rendering")]
        public bool DoubleSided = false;
        [ConfigParam(name: "AddUniqueCodes", valueType: typeof(bool), desc: "Add an extras.unique value to some GLTF objects as a unique hash")]
        public bool AddUniqueCodes = true;

        // ========== Terrain Generation Parameters
        [ConfigParam(name: "AddTerrainMesh", valueType: typeof(bool), desc: "whether to create and add a terrain mesh")]
        public bool AddTerrainMesh = true;
        [ConfigParam(name: "HalfRezTerrain", valueType: typeof(bool), desc: "Whether to reduce the terrain resolution by 2")]
        public bool HalfRezTerrain = true;
        [ConfigParam(name: "CreateTerrainSplat", valueType: typeof(bool), desc: "whether to generate a terrain mesh splat texture")]
        public bool CreateTerrainSplat = true;

        // ========== Debugging
        [ConfigParam(name: "Quiet", valueType: typeof(bool), desc: "supress as much informational output as possible", alt: "q")]
        public bool Quiet = false;
        [ConfigParam(name: "Verbose", valueType: typeof(bool), desc: "enable DEBUG information logging", alt: "v")]
        public bool Verbose = false;
        [ConfigParam(name: "LogToConsole", valueType: typeof(bool), desc: "Whether to also output logs to console")]
        public bool LogToConsole = true;
        [ConfigParam(name: "LogToFile", valueType: typeof(bool), desc: "Whether to output logs to files")]
        public bool LogToFiles = false;
        [ConfigParam(name: "LogFilename", valueType: typeof(string), desc: "Base filename of log files")]
        public string LogFilename = "Logs/convoar.log";
        [ConfigParam(name: "LogBuilding", valueType: typeof(bool), desc: "log detailed BScene/BInstance object building")]
        public bool LogBuilding = false;
        [ConfigParam(name: "LogGltfBuilding", valueType: typeof(bool), desc: "log detailed Gltf object building")]
        public bool LogGltfBuilding = false;

        // Find the parameter definition and return the config info and the field info
        // Returns 'true' of the parameter is found. False otherwise.
        public bool TryGetParameterInfo(string pName, out ConfigParam pConfigParam, out FieldInfo pFieldInfo) {
            var lName = pName.ToLower();
            foreach (FieldInfo fi in this.GetType().GetFields()) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(fi)) {
                    ConfigParam cp = attr as ConfigParam;
                    if (cp != null) {
                        if (cp.name.ToLower() == lName || (cp.alt != null && cp.alt == lName)) {
                            pConfigParam = cp;
                            pFieldInfo = fi;
                            return true;
                        }
                    }
                }
            }
            pConfigParam = null;
            pFieldInfo = null;
            return false;
        }

        // Return a string version of a particular parameter value
        public string GetParameterValue(string pName) {
            var ret = String.Empty;
            foreach (FieldInfo fi in this.GetType().GetFields()) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(fi)) {
                    ConfigParam cp = attr as ConfigParam;
                    if (cp != null) {
                        if (cp.name == pName) {
                            var val = fi.GetValue(this);
                            if (val != null) {
                                ret = val.ToString();
                            }
                            break;
                        }
                    }
                }
                if (ret != String.Empty) {
                    break;
                }
            }
            return ret;
        }
        // Set a parameter value
        public bool SetParameterValue(string pName, string pVal) {
            var ret = false;
            foreach (FieldInfo fi in this.GetType().GetFields()) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(fi)) {
                    ConfigParam cp = attr as ConfigParam;
                    if (cp != null) {
                        if (cp.name == pName) {
                            fi.SetValue(this, ParamBlock.ConvertToObj(cp.valueType, pVal));
                            ret = true;
                            break;
                        }
                    }
                }
                if (ret) {
                    break;
                }
            }
            return ret;
        }
        // Return a list of all the parameters and their descriptions
        public Dictionary<string, string> ListParameters() {
            var ret = new Dictionary<string,string>();
            foreach (FieldInfo fi in this.GetType().GetFields()) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(fi)) {
                    ConfigParam cp = attr as ConfigParam;
                    if (cp != null) {
                        ret.Add(cp.name, cp.desc);
                    }
                }
            }
            return ret;
        }

        public void MergeCommandLine(string[] args) {
            MergeCommandLine(args, null, null);
        }

        // Given parameters from the command line, read the parameters and set values specified
        // <param name="args">array of command line tokens</param>
        // <param name="firstOpFlag">if 'true' presume the first token in the parameter line
        // is a special value that should be assigned to the keyword "--firstparam".</param>
        // <param name="multipleLastParameters">if 'true' presume multiple specs at the end of the line
        // are filenames and pack them together into a CSV string in LAST_PARAM.</param>
        public bool MergeCommandLine(string[] args, string firstOpParameter, string multipleLastParameters) {
            bool ret = true;    // start out assuming parsing worked

            bool firstOpFlag = false;   // no first op
            if (!String.IsNullOrEmpty(firstOpParameter)) {
                firstOpFlag = true;
            }
            bool multipleLast = false;
            if (!String.IsNullOrEmpty(multipleLastParameters)) {
                multipleLast = true;
            }

            for (int ii = 0; ii < args.Length; ii++) {
                string para = args[ii];
                // is this a parameter?
                if (para[0] == '-') {
                    ii += AddCommandLineParameter(para, (ii==(args.Length-1)) ? null : args[ii + 1]);
                }
                else {
                    if (ii == 0 && firstOpFlag) {
                        // if the first thing is not a parameter, make like it's an op or something
                        ii += AddCommandLineParameter(firstOpParameter, args[ii + 1]);
                    }
                    else {
                        if (multipleLast) {
                            // Pack all remaining arguments into a comma-separated list as LAST_PARAM
                            StringBuilder multFiles = new StringBuilder();
                            for (int jj = ii; jj < args.Length; jj++) {
                                if (multFiles.Length != 0) {
                                    multFiles.Append(",");
                                }
                                multFiles.Append(args[jj]);
                            }
                            AddCommandLineParameter(multipleLastParameters, multFiles.ToString());

                            // Skip them all
                            ii = args.Length;
                        }
                        else {
                            throw new ArgumentException("Unknown parameter " + para);
                        }
                    }
                }
            }

            return ret;
        }

        // Store the value for the parameter.
        // If we accept the value as a good value for the parameter, return 1 else 0.
        // A 'good value' is one that does not start with '-' or is not after a boolean parameter.
        // Return the number of parameters to advance the parameter line. That means, return
        //    a zero of we didn't used the next parameter and a 1 if the next parameter
        //    was used as a value so don't consider it the next parameter.
        private int AddCommandLineParameter(string pParm, string val) {
            // System.Console.WriteLine(String.Format("AddCommandLineParameter: parm={0}, val={1}", pParm, val));
            int ret = 1;    // start off assuming the next token is the value we're setting
            string parm = pParm.ToLower();
            // Strip leading hyphens
            while (parm[0] == '-') {
                parm = parm.Substring(1);
            }

            // If the boolean parameter starts with "no", turn it off rather than on.
            string positiveAssertion = "true";
            if (parm.Length > 2 && parm[0] == 'n' && parm[1] == 'o') {
                string maybeParm = parm.Substring(2);
                if (TryGetParameterInfo(parm, out ConfigParam bcp, out FieldInfo bfi)) {
                    if (bcp.valueType == typeof(Boolean)) {
                        // The parameter without the 'no' exists and is a boolean
                        positiveAssertion = "false";
                        parm = maybeParm;
                    }
                }
            }

            // If the next token starts with a parameter mark, it's not really a value
            if (val == null) {
                ret = 0;    // the next token is not used here to set the value
            }
            else {
                if (val[0] == '-') {
                    val = null; // don't use the next token as a value
                    ret = 0;    // the next token is not used here to set the value
                }
            }

            if (TryGetParameterInfo(parm, out ConfigParam cp, out FieldInfo fi)) {
                // If the parameter is a boolean type and the next value is not a parameter,
                //      don't try to take up the next value.
                // This handles boolean flags.
                // If there is a value next (val != null) and that value is not the
                //    values 'true' or 'false' or 't' or 'f', then ignore the next value
                //    as not belonging to this flag. THis allows (and the logic above)
                //    allows:
                //        "--flag --otherFlag ...",
                //        "--flag something ...",
                //        "--flag true --otherFlag ...",
                //        "--noflag --otherflag ...",
                //        etc
                if (cp.valueType == typeof(Boolean)) {
                    if (val != null) {
                        string valL = val.ToLower();
                        if (valL != "true" && valL != "t" && valL != "false" && valL != "f") {
                            // The value is not associated with this boolean so ignore it
                            val = null; // don't use the val token
                            ret = 0;    // the next token is not used here to set the value
                        }
                    }
                    if (val == null) {
                        // If the value is assumed, use the value based on the optional 'no'
                        val = positiveAssertion;
                    }
                }
                // Set the named parameter to the passed value
                fi.SetValue(this, ParamBlock.ConvertToObj(cp.valueType, val));
            }
            else {
                throw new ArgumentException("Unknown parameter " + parm);
            }
            return ret;
        }
    }
}
