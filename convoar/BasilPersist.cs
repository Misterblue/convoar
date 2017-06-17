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
    public class BasilPersist {
        private string _assetType;
        private string _assetInfo;
        private GlobalContext _context;

        private static string _logHeader = "BasilPersist";

        // Texture cache used when processing one region
        private static Dictionary<int, ImageInfo> textureCache = new Dictionary<int, ImageInfo>();

        public BasilPersist(string pAssetType, string pInfo, GlobalContext pContext) {
            _assetType = pAssetType;
            _assetInfo = pInfo;
            _context = pContext;
        }

        public void WriteImage(Image image) {
            string texFilename = CreateFilename();
            if (!File.Exists(texFilename)) {
                Image texImage = ConstrainTextureSize(image);
                try {
                    /*
                    using (Bitmap textureBitmap = new Bitmap(texImage.Width, texImage.Height,
                                System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                        // convert the raw image into a channeled image
                        using (Graphics graphics = Graphics.FromImage(textureBitmap)) {
                            graphics.DrawImage(texImage, 0, 0);
                            graphics.Flush();
                        }
                        // Write out the converted image as PNG
                        textureBitmap.Save(texFilename, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    */
                    // _context.log.DebugFormat("{0} WriteOutImageForEP: id={1}, hasAlpha={2}, format={3}",
                    //                 _logHeader, faceInfo.textureID, faceInfo.hasAlpha, texImage.PixelFormat);
                    texImage.Save(texFilename, ImageFormat.Png);
                }
                catch (Exception e) {
                    _context.log.LogError("{0} FAILED PNG FILE CREATION: {0}", e);
                }
            }
        }

        // Keep a cache if image data and either fetch and Image or return a cached instance.
        public Promise<ImageInfo> GetUniqueTextureData(FaceInfo faceInfo, IAssetFetcher assetFetcher) {

            string imageFilename = CreateFilename();

            Promise<ImageInfo> prom = new Promise<ImageInfo>();
            EntityHandle textureHandle = new EntityHandle((OMV.UUID)faceInfo.textureID);
            int hash = textureHandle.GetHashCode();
            if (textureCache.ContainsKey(hash)) {
                // _context.log.DebugFormat("{0} GetUniqueTextureData. found image in cache. {1}", _logHeader, faceInfo.textureID);
                prom.Resolve(textureCache[hash]);
            }
            else {
                // If the converted file already exists, read that one in
                if (File.Exists(imageFilename)) {
                    var anImage = Image.FromFile(imageFilename);
                    // _context.log.DebugFormat("{0} GetUniqueTextureData: reading in existing image from {1}", _logHeader, imageFilename);
                    ImageInfo imgInfo = new ImageInfo(anImage);
                    imgInfo.CheckForTransparency();
                    textureCache.Add(hash, imgInfo);
                    prom.Resolve(imgInfo);
                }
                else {
                    // If not in the cache or converted file, get it from the asset server
                    // _context.log.DebugFormat("{0} GetUniqueTextureData. not in file or cache. Fetching image. {1}", _logHeader, faceInfo.textureID);
                    assetFetcher.FetchTextureAsImage(textureHandle)
                    .Catch(e => {
                        prom.Reject(new Exception(String.Format("Could not fetch texture. handle={0}. e={1}", textureHandle, e)));
                    })
                    .Then(theImage => {
                        try {
                            // _context.log.DebugFormat("{0} GetUniqueTextureData. adding to cache. {1}", _logHeader, faceInfo.textureID);
                            ImageInfo imgInfo = new ImageInfo(theImage);
                            imgInfo.CheckForTransparency();
                            textureCache.Add(textureHandle.GetHashCode(), imgInfo);
                            // _context.log.DebugFormat("{0} GetUniqueTextureData. handle={1}, hash={2}, caching", _logHeader, textureHandle, hash);
                            prom.Resolve(imgInfo);
                        }
                        catch (Exception e) {
                            prom.Reject(new Exception(String.Format("Texture conversion failed. handle={0}. e={1}", textureHandle, e)));
                        }
                    });
                }
            }
            return prom;
        }

        public string CreateFilename() {
            return CreateFilename(_assetType, _assetInfo);
        }

        public string CreateFilename(string assetType, string assetInfo) {
            string fname = "";

            string targetDir = ResolveAndCreateDir(_context.parms.GltfTargetDir);
            if (targetDir != null) {
                if (assetType == Gltf.MakeAssetURITypeImage) {
                    fname = JoinFilePieces(targetDir, assetInfo + ".png");
                }
                if (assetType == Gltf.MakeAssetURITypeBuff) {
                    fname = JoinFilePieces(targetDir, _context.contextName + "_" + assetInfo + ".bin");
                }
                if (assetType == Gltf.MakeAssetURITypeMesh) {
                    fname = JoinFilePieces(targetDir, assetInfo + ".mesh");
                }
            }
            return fname;
        }

        public string CreateURI() {
            return CreateURI(_assetType, _assetInfo);
        }

        public string CreateURI(string assetType, string assetInfo) {
            string uuri = "";

            string targetDir = ResolveAndCreateDir(_context.parms.GltfTargetDir);
            if (targetDir != null) {
                if (assetType == Gltf.MakeAssetURITypeImage) {
                    uuri = _context.parms.URIBase + assetInfo + ".png";
                }
                if (assetType == Gltf.MakeAssetURITypeBuff) {
                    uuri = _context.parms.URIBase + _context.contextName + "_" + assetInfo + ".bin";
                }
                if (assetType == Gltf.MakeAssetURITypeMesh) {
                    uuri = _context.parms.URIBase + assetInfo + ".mesh";
                }
            }
            return uuri;
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
                // _context.log.ErrorFormat("{0} Failed creation of GLTF file directory. dir={1}, e: {2}",
                //             _logHeader, absDir, e);
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
            while (f.EndsWith(separator)) f = f.Substring(f.Length - 1);
            while (l.StartsWith(separator)) l = l.Substring(1, l.Length - 1);
            return f + separator + l;
        }

    }

}
