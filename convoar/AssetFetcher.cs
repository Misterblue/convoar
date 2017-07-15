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
using System.Drawing;
using System.IO;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

using RSG;

using OMV = OpenMetaverse;
using OMVA = OpenMetaverse.Assets;
using OpenMetaverse.Imaging;

namespace org.herbal3d.convoar {

    // A Promise based interface to the asset fetcher
    /// <summary>
    /// A Promise based interface to the asset fetcher.
    /// Also includes storage for global meshes, materials, and textures.
    /// </summary>
    public abstract class IAssetFetcher : IDisposable {
        public abstract IPromise<OMVA.AssetTexture> FetchTexture(EntityHandle handle);
        public abstract IPromise<Image> FetchTextureAsImage(EntityHandle handle);
        public abstract IPromise<byte[]> FetchRawAsset(EntityHandle handle);
        public abstract void StoreRawAsset(EntityHandle handle, string name, OMV.AssetType assetType, OMV.UUID creatorID, byte[] data);
        public abstract void StoreTextureImage(EntityHandle handle, string name, OMV.UUID creatorID, Image pImage);
        public abstract void Dispose();

#pragma warning disable 414
        private static string _logHeader = "[IAssetFetcher]";
#pragma warning restore 414

        // Displayables are the linksetable prim equivilient
        // The top Displayable is the root prim and the children Displayables are the linkset members
        public Dictionary<BHash, Displayable> Displayables;
        // DisplayableRenderables are the rendering mesh for a Displayable (usually a list of meshes).
        // The list of meshes are the faces of a prim.
        public Dictionary<BHash, DisplayableRenderable> Renderables;
        // Meshes are each of the individual meshes with material
        public OMV.DoubleDictionary<BHash, EntityHandle, MeshInfo> Meshes;
        public OMV.DoubleDictionary<BHash, EntityHandle, MaterialInfo> Materials;
        public OMV.DoubleDictionary<BHash, EntityHandle, ImageInfo> Images;

        public IAssetFetcher() {
            Displayables = new Dictionary<BHash, Displayable>();
            Renderables = new Dictionary<BHash, DisplayableRenderable>();
            Meshes = new OMV.DoubleDictionary<BHash, EntityHandle, MeshInfo>();
            Materials = new OMV.DoubleDictionary<BHash, EntityHandle, MaterialInfo>();
            Images = new OMV.DoubleDictionary<BHash, EntityHandle, ImageInfo>();
        }

        // Adds this Displayable if it's not already in the list.
        // Return 'true' if the Displayable was added to the list.
        public bool AddUniqueDisplayable(Displayable disp) {
            bool ret = false;
            Displayable maybeDisp;
            BHash dispHash = disp.GetBHash();
            if (!Displayables.TryGetValue(dispHash, out maybeDisp)) {
                Displayables.Add(dispHash, disp);
            }
            return ret;
        }

        public bool GetDisplayable(BHash hash, out Displayable disp) {
            return Displayables.TryGetValue(hash, out disp);
        }

        public delegate DisplayableRenderable RenderableBuilder();
        public DisplayableRenderable GetRenderable(BHash hash, RenderableBuilder builder) {
            DisplayableRenderable renderable = null;

            lock (Renderables) {
                if (!Renderables.TryGetValue(hash, out renderable)) {
                    try {
                        renderable = builder();
                    }
                    catch (Exception e) {
                        ConvOAR.Globals.log.ErrorFormat("{0} GetRenderable: builder exception: {1}", _logHeader, e);
                    }
                    Renderables.Add(hash, renderable);
                }
            }
            return renderable;
        }

        public delegate MeshInfo MeshInfoBuilder();
        public MeshInfo GetMeshInfo(BHash hash, MeshInfoBuilder builder) {
            MeshInfo meshInfo = null;
            lock (Meshes) {
                if (!Meshes.TryGetValue(hash, out meshInfo)) {
                    meshInfo = builder();
                    Meshes.Add(hash, meshInfo.handle, meshInfo);
                }
            }
            return meshInfo;
        }
        public delegate MaterialInfo MaterialInfoBuilder();
        public MaterialInfo GetMaterialInfo(BHash hash, MaterialInfoBuilder builder) {
            MaterialInfo matInfo = null;
            lock (Materials) {
                if (!Materials.TryGetValue(hash, out matInfo)) {
                    matInfo = builder();
                    Materials.Add(hash, matInfo.handle, matInfo);
                }
            }
            return matInfo;
        }
        public delegate ImageInfo ImageInfoBuilder();
        public ImageInfo GetImageInfo(BHash hash, ImageInfoBuilder builder) {
            ImageInfo imageInfo = null;
            lock (Images) {
                if (!Images.TryGetValue(hash, out imageInfo)) {
                    imageInfo = builder();
                    Images.Add(hash, imageInfo.handle, imageInfo);
                }
            }
            return imageInfo;
        }
        // Search through the images and get one that matches the hash but has a
        //    size smaller than the constraint. Used for reduced resolution versions
        //    of images.
        public ImageInfo GetImageInfo(OMV.UUID uuid, int sizeContstraint) {
            ImageInfo imageInfo = null;
            lock (Images) {
                Images.ForEach(delegate (ImageInfo img) {
                    if (img.imageIdentifier == uuid) {
                        if (img.xSize <= sizeContstraint && img.ySize <= sizeContstraint) {
                            if (imageInfo != null) {
                                if (imageInfo.xSize > img.xSize || imageInfo.ySize > img.ySize) {
                                    imageInfo = img;
                                }
                            }
                            else {
                                imageInfo = img;
                            }
                        }
                    }
                });
            }
            return imageInfo;
        }

    }

    // Fetch an asset from  the OpenSimulator asset system
    public class OSAssetFetcher : IAssetFetcher {
    #pragma warning disable 414
        private string _logHeader = "[OSAssetFetcher]";
    #pragma warning restore 414
        private IAssetService _assetService;

        public OSAssetFetcher(IAssetService pAssetService) : base() {
            _assetService = pAssetService;
        }

        public override IPromise<byte[]> FetchRawAsset(EntityHandle handle) {
            var prom = new Promise<byte[]>();

            // Don't bother with async -- this call will hang until the asset is fetched
            byte[] returnBytes = _assetService.GetData(handle.ToString());
            if (returnBytes.Length > 0) {
                prom.Resolve(returnBytes);
            }
            else {
                prom.Reject(new Exception("FetchRawAsset: could not fetch asset " + handle.ToString()));
            }
            return prom;
        }

        public override void StoreRawAsset(EntityHandle handle, string name, OMV.AssetType assetType, OMV.UUID creatorID, byte[] data) {
            AssetBase newAsset = new AssetBase(((EntityHandleUUID)handle).GetUUID(), name, (sbyte)assetType, creatorID.ToString());
            _assetService.Store(newAsset);

        }

        public override void StoreTextureImage(EntityHandle handle, string name, OMV.UUID creatorID, Image pImage) {
            // This application overloads AssetType.TExtureTGA to be our serialized image
            AssetBase newAsset = new AssetBase(((EntityHandleUUID)handle).GetUUID(), name, (sbyte)OMV.AssetType.TextureTGA, creatorID.ToString());
            using (MemoryStream byteStream = new MemoryStream()) {
                pImage.Save(byteStream, System.Drawing.Imaging.ImageFormat.Png);
                newAsset.Data = byteStream.ToArray();
            }
            _assetService.Store(newAsset);
        }

        /// <summary>
        /// Fetch a texture and return an OMVA.AssetTexture. The only information initialized
        /// in the AssetTexture is the UUID and the binary data.s
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public override IPromise<OMVA.AssetTexture> FetchTexture(EntityHandle handle) {
            var prom = new Promise<OMVA.AssetTexture>();

            // Don't bother with async -- this call will hang until the asset is fetched
            AssetBase asset = _assetService.Get(handle.ToString());
            if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                OMVA.AssetTexture tex = new OMVA.AssetTexture(((EntityHandleUUID)handle).GetUUID(), asset.Data);
                try {
                    if (tex.Decode()) {
                        prom.Resolve(tex);
                    }
                    else {
                        prom.Reject(new Exception("FetchTexture: could not decode JPEG2000 texture. ID=" + handle.ToString()));
                    }
                }
                catch (Exception e) {
                    prom.Reject(new Exception("FetchTexture: exception decoding JPEG2000 texture. ID=" + handle.ToString()
                                + ", e=" + e.ToString()));
                }
            }
            else {
                prom.Reject(new Exception("FetchTexture: asset was not of type texture. ID=" + handle.ToString()));
            }

            return prom;
        }

        /// <summary>
        /// Fetch a texture and return an OMVA.AssetTexture. The only information initialized
        /// in the AssetTexture is the UUID and the binary data.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public override IPromise<Image> FetchTextureAsImage(EntityHandle handle) {
            var prom = new Promise<Image>();

            // Don't bother with async -- this call will hang until the asset is fetched
            AssetBase asset = _assetService.Get(handle.ToString());
            if (asset != null) {
                Image imageDecoded = null;
                if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                    try {
                        ManagedImage mimage;
                        if (OpenJPEG.DecodeToImage(asset.Data, out mimage, out imageDecoded)) {
                            mimage = null;
                        }
                        else {
                            imageDecoded = null;
                        }
                        prom.Resolve(imageDecoded);
                    }
                    catch (Exception e) {
                        prom.Reject(new Exception("FetchTextureAsImage: exception decoding JPEG2000 texture. ID=" + handle.ToString()
                                    + ", e=" + e.ToString()));
                    }
                }
                // THis application overloads the definition of TextureTGA to be a PNG format bytes
                else if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.TextureTGA) {
                    using (Stream byteStream = new MemoryStream(asset.Data)) {
                        Bitmap readBitmap = new Bitmap(byteStream);
                        // Doing this clone because of the comment about keeping the stream open for
                        //     the life if the Bitmap in the MS documentation. Odd but making a copy.
                        imageDecoded = (Image)readBitmap.Clone();
                        readBitmap.Dispose();
                    }
                    prom.Resolve(imageDecoded);
                }
                else {
                    prom.Reject(new Exception("FetchTextureAsImage: asset was not of type texture. ID=" + handle.ToString()));
                }
            }
            else {
                prom.Reject(new Exception("FetchTextureAsImage: could not fetch texture asset. ID=" + handle.ToString()));
            }

            return prom;
        }

        public override void Dispose() {
            _assetService = null;
        }
    }
}
