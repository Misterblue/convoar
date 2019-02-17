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
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

using org.herbal3d.cs.CommonEntitiesUtil;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.os.CommonEntities {

    // Classes to handle persistance of output.
    // The converted images go somewhere for later fetching.
    // These classes wrap the logic for storing the binary, images, and json files
    //     for later processing.
    public class PersistRules {

        public enum AssetType {
            Unknown,
            Image,
            ImageTrans,
            Mesh,
            Buff,
            Scene
        };

        public enum TargetType {
            Default,
            PNG,
            JPEG,
            GIF,
            BMP,
            Mesh,
            Buff,
            Gltf
        };

        // Some asset types have their own sub-directory to live in
        public readonly static Dictionary<AssetType, string> AssetTypeToSubDir = new Dictionary<AssetType, string>()
            { { AssetType.Image, "images" },
              { AssetType.ImageTrans, "images" },
              { AssetType.Mesh, "" },
              { AssetType.Buff, "" },
              { AssetType.Scene, "" },
        };

        // Asset types have a target type when stored
        public readonly static Dictionary<AssetType, TargetType> AssetTypeToTargetType = new Dictionary<AssetType, TargetType>()
            { { AssetType.Image, TargetType.Default},   // 'Default' means look things up in parameters for the AssetType
              { AssetType.ImageTrans, TargetType.Default},
              { AssetType.Mesh, TargetType.Mesh},
              { AssetType.Buff, TargetType.Buff},
              { AssetType.Scene, TargetType.Gltf},
        };

        // The extension to add to target type filenames when stored
        public readonly static Dictionary<TargetType, string> TargetTypeToExtension = new Dictionary<TargetType, string>()
            { { TargetType.Default, "" },
              { TargetType.PNG, "png" },
              { TargetType.JPEG, "jpg" },
              { TargetType.GIF, "gif" },
              { TargetType.BMP, "bmp" },
              { TargetType.Mesh, "mesh" },
              { TargetType.Buff, "buf" },
              { TargetType.Gltf, "gltf" },
        };

        // Parameter system can specify types to output. THis converts the parameter to a target type code
        public readonly static Dictionary<string, TargetType> TextureFormatToTargetType = new Dictionary<string, TargetType>()
            { { "png", TargetType.PNG},
              { "gif", TargetType.GIF},
              { "jpg", TargetType.JPEG},
              { "jpeg", TargetType.JPEG},
              { "bmp", TargetType.BMP}
        };

        // Output target formats use different conversion code parameters for .NET
        public readonly static Dictionary<TargetType, ImageFormat> TargetTypeToImageFormat = new Dictionary<TargetType, ImageFormat>()
            { { TargetType.PNG, ImageFormat.Png },
              { TargetType.GIF, ImageFormat.Gif },
              { TargetType.JPEG, ImageFormat.Jpeg },
              { TargetType.BMP, ImageFormat.Bmp },
              { TargetType.Default, ImageFormat.Png },
        };

        public string BaseDirectory { get; set; }
        public AssetType AssetAssetType;
        public TargetType AssetTargetType;
        public string AssetName;

        // If target type is not specified, select the image type depending on parameters and transparency
        public static TargetType FigureOutTargetTypeFromAssetType(AssetType pAssetType, IParameters pParams) {
            TargetType ret = AssetTypeToTargetType[pAssetType];

            // If target type is not specified, select the image type depending on parameters and transparency
            if (ret == TargetType.Default) {
                if (pAssetType == AssetType.Image) {
                    ret = TextureFormatToTargetType[pParams.P<string>("PreferredTextureFormatIfNoTransparency").ToLower()];
                }
                if (pAssetType == AssetType.ImageTrans) {
                    ret = TextureFormatToTargetType[pParams.P<string>("PreferredTextureFormat").ToLower()];
                }
            }
            return ret;
        }

        // Pass in a relative directory name and return a full directory path
        //     and create the directory if it doesn't exist.
        public static string CreateDirectory(string pDir, IParameters pParams) {
            string baseDir = pParams.P<string>("OutputDir");
            string fullDir = PersistRules.JoinFilePieces(baseDir, pDir);
            string absDir = Path.GetFullPath(fullDir);
            if (!Directory.Exists(absDir)) {
                Directory.CreateDirectory(absDir);
            }
            return absDir;
        }

        // Compute the filename of this object when written out.
        // Mostly about computing the file extension based on the AssetType.
        // public static string GetFilename(GltfClass pObject, string pLongName, IParameters pParams) {
        public static string GetFilename(AssetType pAssetType, string pReadableName, string pLongName, IParameters pParams) {
            string ret = null;
            if (pParams.P<bool>("UseReadableFilenames")) {
                var targetType = FigureOutTargetTypeFromAssetType(pAssetType, pParams);
                ret = pReadableName + "." + PersistRules.TargetTypeToExtension[targetType];
            }
            else {
                var targetType = FigureOutTargetTypeFromAssetType(pAssetType, pParams);
                ret = pLongName + "." + PersistRules.TargetTypeToExtension[targetType];
            }
            return ret;
        }

        // Given a directory base and a filename, return the directory that that filename
        //    should be stored in.
        // Uses sub-directories made out of the filename.
        //     "01234567890123456789" => "baseDirectory/01/23/45/6789"
        public static string StorageDirectory(string baseDirectory, string pHash, IParameters pParams) {
            string ret = null;
            if (pParams.P<bool>("UseDeepFilenames") && pHash.Length >= 10) {
                if (String.IsNullOrEmpty(baseDirectory)) {
                    ret = Path.Combine(pHash.Substring(0, 2),
                            Path.Combine(pHash.Substring(2, 2),
                                Path.Combine(pHash.Substring(4, 2),
                                    pHash.Substring(6, 4)
                            )));
                }
                else {
                    ret = Path.Combine(baseDirectory,
                            Path.Combine(pHash.Substring(0, 2),
                                Path.Combine(pHash.Substring(2, 2),
                                    Path.Combine(pHash.Substring(4, 2),
                                        pHash.Substring(6, 4)
                            ))));
                }
            }
            else {
                ret = String.IsNullOrEmpty(baseDirectory) ? "" : baseDirectory;
            }
            return ret;
        }

        // Create the URI for referring to this object.
        // THis is as opposed to the storage directory as the HTTP server resolving
        //     this URL will do any extra filesystem hashing to access the file.
        public static string ReferenceURL(string pBaseDirectory, string pStorageName) {
            return JoinURIPieces(pBaseDirectory, pStorageName);
        }

        /// <summary>
        /// Combine two filename pieces so there is one directory separator between.
        /// This replaces System.IO.Path.Combine which has the nasty feature that it
        /// ignores the first string if the second begins with a separator.
        /// It assumes that it's root and you don't want to join. Wish they had asked me.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="last"></param>
        /// <returns></returns>
        public static string JoinFilePieces(string first, string last) {
            // string separator = "" + Path.DirectorySeparatorChar;
            string separator = "/";     // both .NET and mono are happy with forward slash
            string f = first;
            string l = last;
            if (String.IsNullOrEmpty(f) && String.IsNullOrEmpty(l))
                return String.Empty;
            if (String.IsNullOrEmpty(f))
                return l;
            if (String.IsNullOrEmpty(l))
                return f;
            while (f.EndsWith("/")) f = f.Substring(0, f.Length - 1);
            // while (f.EndsWith(separator)) f = f.Substring(0, f.Length - 1);
            while (l.StartsWith("/")) l = l.Substring(1);
            // while (l.StartsWith(separator)) l = l.Substring(1);
            return f + separator + l;
        }

        public static string JoinURIPieces(string first, string last) {
            string separator = "/";
            string f = first;
            string l = last;
            if (String.IsNullOrEmpty(f) && String.IsNullOrEmpty(l))
                return String.Empty;
            if (String.IsNullOrEmpty(f))
                return l;
            if (String.IsNullOrEmpty(l))
                return f;
            while (f.EndsWith(separator)) f = f.Substring(0, f.Length - 1);
            while (l.StartsWith(separator)) l = l.Substring(1);
            return f + separator + l;
        }

    }

}
