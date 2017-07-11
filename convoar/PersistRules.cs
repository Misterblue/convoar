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

using log4net;

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {

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
            Gltf,
            Gltf2
        };

        public enum TargetType {
            Default,
            PNG,
            JPEG,
            GIF,
            BMP,
            Obj,
            Fbx,
            Gltf
        };

        public Dictionary<AssetType, string> AssetTypeToSubDir = new Dictionary<AssetType, string>()
            { { AssetType.Image, "image" },
              { AssetType.ImageTrans, "image" },
              { AssetType.Mesh, "" },
              { AssetType.Buff, "" },
              { AssetType.Gltf, "gltf" },
              { AssetType.Gltf2, "gltf2" }
        };

        public Dictionary<TargetType, string> TargetTypeToExtension = new Dictionary<TargetType, string>()
            { { TargetType.Default, "" },
              { TargetType.PNG, "png" },
              { TargetType.JPEG, "jpg" },
              { TargetType.Obj, "obj" },
              { TargetType.Gltf, "gltf" }
        };

        private string _baseDirectory;
        private AssetType _assetType;
        private TargetType _targetType;
        private string _assetInfo;
        private string _imageExtension; // the format type of the image as the filename extension

    #pragma warning disable 414
        private static string _logHeader = "[PersistRules]";
    #pragma warning restore 414

        public PersistRules(AssetType pAssetType, string pInfo) {
            _assetType = pAssetType;
            _assetInfo = pInfo;
            string subDirByType = AssetTypeToSubDir[_assetType];
            _baseDirectory = JoinFilePieces(ConvOAR.Globals.parms.TargetDir, subDirByType);
        }

        public PersistRules GetTypePersister(string pAssetType, string pInfo) {
            return new PersistRules(pAssetType, pInfo);
        }

        public string baseDirectory {
            get {
                return _baseDirectory;
            }
            set {
                _baseDirectory = value;
            }
        }

        public string filename {
            get {
                return CreateFilename();
            }
        }

        public string uri {
            get {
                return CreateURI();
            }
        }

        public void WriteImage(ImageInfo imageInfo) {
        }

        public void WriteImage(ImageInfo imageInfo, TargetType targetType) {
            string texFilename = CreateFilename(targetType);
            if (!File.Exists(texFilename)) {
                Image texImage = imageInfo.image;
                try {
                    // _context.log.DebugFormat("{0} WriteOutImageForEP: id={1}, hasAlpha={2}, format={3}",
                    //                 _logHeader, faceInfo.textureID, faceInfo.hasAlpha, texImage.PixelFormat);
                    texImage.Save(texFilename, ConvertNameToFormatCode(_imageExtension));
                }
                catch (Exception e) {
                    ConvOAR.Globals.log.ErrorFormat("{0} FAILED PNG FILE CREATION: {0}", e);
                }
            }
        }

        private ImageFormat ConvertTargetToImageFormatCode(TargetType targetType) {
            ImageFormat ret = ImageFormat.Png;
            switch (targetType) {
                case TargetType.PNG:
                    ret = ImageFormat.Png;
                    break;
                case TargetType.GIF:
                    ret = ImageFormat.Gif;
                    break;
                case TargetType.JPEG:
                    ret = ImageFormat.Jpeg;
                    break;
                case TargetType.BMP:
                    ret = ImageFormat.Bmp;
                    break;
                case TargetType.Default:
                default:
                    if (_assetType == AssetType.ImageTrans) {
                    }
                    else
                    
                    break;
            }
            return ret;
        }

        private string CreateFilename() {
            return CreateFilename(_assetType, _assetInfo, SetImageExtension(_assetType));
        }

        private string CreateFilename(AssetType assetType, string assetInfo, string imageExtension) {
            string fname = "";

            string targetDir = _baseDirectory;
            if (targetDir != null) {
                if (assetType == AssetType.Image || assetType ==  AssetType.ImageTrans) {
                    fname = JoinFilePieces(targetDir, assetInfo + "." + imageExtension);
                }
                if (assetType == AssetType.Buff) {
                    fname = JoinFilePieces(targetDir, ConvOAR.Globals.contextName + "_" + assetInfo + ".bin");
                }
                if (assetType == AssetType.Mesh) {
                    fname = JoinFilePieces(targetDir, assetInfo + ".mesh");
                }
                if (assetType == AssetType.Gltf) {
                    fname = JoinFilePieces(targetDir, assetInfo + ".gltf");
                }
            }
            return fname;
        }

        private string CreateURI() {
            return CreateURI(_assetType, _assetInfo, SetImageExtension(_assetType));
        }

        private string CreateURI(AssetType assetType, string assetInfo, string imageExtension) {
            string uuri = "";

            string targetDir = _baseDirectory;
            if (targetDir != null) {
                if (assetType == AssetType.Image || assetType ==  AssetType.ImageTrans) {
                    uuri = ConvOAR.Globals.parms.URIBase + assetInfo + "." + imageExtension;
                }
                if (assetType == AssetType.Buff) {
                    uuri = ConvOAR.Globals.parms.URIBase + ConvOAR.Globals.contextName + "_" + assetInfo + ".bin";
                }
                if (assetType == AssetType.Mesh) {
                    uuri = ConvOAR.Globals.parms.URIBase + assetInfo + ".mesh";
                }
                if (assetType == AssetType.Gltf) {
                    uuri = ConvOAR.Globals.parms.URIBase + assetInfo + ".gltf";
                }
            }
            return uuri;
        }

        private string SetImageExtension(AssetType assetType) {
            if (assetType == AssetType.ImageTrans) {
                _imageExtension = ConvOAR.Globals.parms.PreferredTextureFormat.ToLower();
            }
            else {
                _imageExtension = ConvOAR.Globals.parms.PreferredTextureFormatIfNoTransparency.ToLower();
            }
            return _imageExtension;
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
                if (!Directory.Exists(absDir)) {
                    Directory.CreateDirectory(absDir);
                }
            }
            catch (Exception e) {
                ConvOAR.Globals.log.ErrorFormat("{0} Failed creation of GLTF file directory. dir={1}, e: {2}",
                            _logHeader, absDir, e);
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
            string separator = "" + Path.DirectorySeparatorChar;
            // string separator = "/";     // both .NET and mono are happy with forward slash
            string f = first;
            string l = last;
            while (f.EndsWith("/")) f = f.Substring(f.Length - 1);
            while (f.EndsWith(separator)) f = f.Substring(f.Length - 1);
            while (l.StartsWith("/")) l = l.Substring(1, l.Length - 1);
            while (l.StartsWith(separator)) l = l.Substring(1, l.Length - 1);
            return f + separator + l;
        }

    }

}
