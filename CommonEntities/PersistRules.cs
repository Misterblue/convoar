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

using RSG;

using org.herbal3d.cs.Util;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.os.CommonEntities {

    // Classes to handle persistance of output.
    // The converted images go somewhere for later fetching.
    // These classes wrap the logic for storing the binary, images, and json files
    //     for later processing.
    public class PersistRules {

        public enum AssetType {
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
            { { AssetType.Image, TargetType.Default},
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
        private AssetType _assetType;
        private TargetType _targetType;
        private string _assetInfo;

        private BLogger _log;
        private IParameters _params;

    #pragma warning disable 414
        private static readonly string _logHeader = "[PersistRules]";
    #pragma warning restore 414

        // Rules for storing files into TargetDir and into type specific sub-directory therein
        public PersistRules(AssetType pAssetType, string pInfo, BLogger pLog, IParameters pParams) {
            PersistInit(pAssetType, pInfo, TargetType.Default, pLog, pParams);
        }

        public PersistRules(AssetType pAssetType, string pInfo, TargetType pTargetType, BLogger pLog, IParameters pParams) {
            PersistInit(pAssetType, pInfo, pTargetType, pLog, pParams);
        }

        public PersistRules Clone() {
            PersistRules pr = new PersistRules(_assetType, _assetInfo, _targetType, _log, _params) {
                BaseDirectory = this.BaseDirectory
            };
            return pr;
        }

        private void PersistInit(AssetType pAssetType, string pInfo, TargetType pTargetType, BLogger pLog, IParameters pParams) {
            _assetType = pAssetType;
            _assetInfo = pInfo;
            _targetType = FigureOutTargetType();
            _log = pLog;
            _params = pParams;

            BaseDirectory = AssetTypeToSubDir[_assetType];
        }

        // If target type is not specified, select the image type depending on parameters and transparency
        private TargetType FigureOutTargetType() {
            TargetType ret = AssetTypeToTargetType[_assetType];

            // If target type is not specified, select the image type depending on parameters and transparency
            if (_targetType == TargetType.Default) {
                if (_assetType == AssetType.Image) {
                    ret = TextureFormatToTargetType[_params.P<string>("PreferredTextureFormatIfNoTransparency").ToLower()];
                }
                if (_assetType == AssetType.ImageTrans) {
                    ret = TextureFormatToTargetType[_params.P<string>("PreferredTextureFormat").ToLower()];
                }
            }
            return ret;
        }

        public string Filename {
            get {
                return CreateFilename();
            }
        }

        public string Uri {
            get {
                return CreateURI();
            }
        }

        public void WriteImage(ImageInfo imageInfo) {
            string texFilename = CreateFilename();
            if (imageInfo.image != null && !File.Exists(texFilename)) {
                Image texImage = imageInfo.image;
                try {
                    // _log.DebugFormat("{0} WriteOutImageForEP: id={1}, hasAlpha={2}, format={3}",
                    //                 _logHeader, faceInfo.textureID, faceInfo.hasAlpha, texImage.PixelFormat);
                    PersistRules.ResolveAndCreateDir(texFilename);
                    texImage.Save(texFilename, TargetTypeToImageFormat[_targetType]);
                }
                catch (Exception e) {
                    _log.ErrorFormat("{0} FAILED PNG FILE CREATION: {0}", e);
                }
            }
        }


        private string CreateFilename() {
            // string fnbase = JoinFilePieces(_params.P<string>("OutputDir"), baseDirectory);
            string fnbase = BaseDirectory;
            return JoinFilePieces(fnbase, _assetInfo + "." + TargetTypeToExtension[_targetType]);
        }

        private string CreateURI() {
            string uribase = JoinURIPieces(_params.P<string>("URIBase"), BaseDirectory);
            return JoinURIPieces(uribase, _assetInfo + "." + TargetTypeToExtension[_targetType]);
        }

        /// <summary>
        /// Turn the passed relative path name into an absolute directory path and
        /// create the directory if it does not exist.
        /// </summary>
        /// <param name="pDir">Absolute or relative path to a directory</param>
        /// <returns>Absolute path to directory or 'null' if cannot resolve or create the directory</returns>
        public static string ResolveAndCreateDir(string pDir) {
            string absDir = null;
            try {
                absDir = Path.GetFullPath(pDir);
                absDir = Path.GetDirectoryName(absDir);
                if (!Directory.Exists(absDir)) {
                    Directory.CreateDirectory(absDir);
                }
            }
            catch (Exception e) {
                // _log.ErrorFormat("{0} Failed creation of directory. dir={1}, e: {2}",
                //             _logHeader, absDir, e);
                var temp = e;   // supress warning
                return null;
            }
            return absDir;
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
