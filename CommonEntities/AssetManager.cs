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
using System.Threading.Tasks;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

using OMV = OpenMetaverse;
using OMVA = OpenMetaverse.Assets;
using OpenMetaverse.Imaging;

using org.herbal3d.cs.CommonEntitiesUtil;

using CSJ2K;

namespace org.herbal3d.cs.os.CommonEntities {

    /// <summary>
    /// An async interface to the asset fetcher.
    /// Also includes storage for global meshes, materials, and textures.
    /// </summary>
    public abstract class AssetManager : IDisposable {

        // Fetching operations that happen through to the underlying simulator asset system
        public abstract Task<Image> FetchTextureAsImage(EntityHandle handle);
        public abstract Task<byte[]> FetchRawAsset(EntityHandle handle);
        public abstract void StoreRawAsset(EntityHandle handle, string name, OMV.AssetType assetType, OMV.UUID creatorID, byte[] data);
        public abstract void StoreTextureImage(EntityHandle handle, string name, OMV.UUID creatorID, Image pImage);

#pragma warning disable 414     // disable 'assigned but not used' warning
        private static readonly string _logHeader = "[AssetFetcher]";
#pragma warning restore 414

        protected readonly BLogger _log;
        protected readonly IParameters _params;

        public AssetManager(BLogger pLog, IParameters pParam) {
            _log = pLog;
            _params = pParam;
            Displayables = new Dictionary<BHash, Displayable>();
            Renderables = new Dictionary<BHash, DisplayableRenderable>();
            Meshes = new OMV.DoubleDictionary<BHash, EntityHandle, MeshInfo>();
            Materials = new OMV.DoubleDictionary<BHash, EntityHandle, MaterialInfo>();
            Images = new OMV.DoubleDictionary<BHash, EntityHandle, ImageInfo>();
        }

        // Displayables are the linksetable prim equivilient
        // The top Displayable is the root prim and the children Displayables are the linkset members
        // Displayable == linkset
        public Dictionary<BHash, Displayable> Displayables;
        // DisplayableRenderables are the rendering mesh for a Displayable (usually a list of meshes).
        // The list of meshes are the faces of a prim.
        // DisplayableRenderable == prim
        public Dictionary<BHash, DisplayableRenderable> Renderables;
        // Meshes are each of the individual meshes with material
        // Mesh == prim faces (that optionally reference a MaterialInfo and/or ImageInfo)
        public OMV.DoubleDictionary<BHash, EntityHandle, MeshInfo> Meshes;
        public OMV.DoubleDictionary<BHash, EntityHandle, MaterialInfo> Materials;
        public OMV.DoubleDictionary<BHash, EntityHandle, ImageInfo> Images;

        // When done with this instance, clear all the lists
        public virtual void Dispose() {
            Displayables.Clear();
            Renderables.Clear();
            Meshes.Clear();
            Materials.Clear();
            Images.Clear();
        }

        // Adds this Displayable if it's not already in the list.
        // Return 'true' if the Displayable was added to the list.
        public bool AddUniqueDisplayable(Displayable disp) {
            bool ret = false;
            BHash dispHash = disp.GetBHash();
            lock (Displayables) {
                if (!Displayables.TryGetValue(dispHash, out Displayable maybeDisp)) {
                    Displayables.Add(dispHash, disp);
                }
            }
            return ret;
        }

        public bool GetDisplayable(BHash hash, out Displayable disp) {
            return Displayables.TryGetValue(hash, out disp);
        }

        // Fetch a DisplayableRenderable corresponding to the passed hash but, if the
        //   DisplayableRenderable is not in the table, invoke the passed builder to create
        //   an instance of the needed DisplayableRenderable.
        public delegate DisplayableRenderable RenderableBuilder();
        public DisplayableRenderable GetRenderable(BHash hash, RenderableBuilder builder) {
            DisplayableRenderable renderable = null;

            lock (Renderables) {
                if (!Renderables.TryGetValue(hash, out renderable)) {
                    try {
                        if (builder != null) {
                            renderable = builder();
                        }
                        else {
                            renderable = null;
                        }
                    }
                    catch (Exception e) {
                        _log.ErrorFormat("{0} GetRenderable: builder exception: {1}", _logHeader, e);
                    }
                    Renderables.Add(hash, renderable);
                }
            }
            return renderable;
        }
        // Short form that just returns 'null' if not found.
        public DisplayableRenderable GetRenderable(BHash hash) {
            return GetRenderable(hash, null);
        }

        // Add the passed MeshInfo the to list if it is not already in the list
        public void AddUniqueMeshInfo(MeshInfo meshInfo) {
            lock (Meshes) {
                if (!Meshes.TryGetValue(meshInfo.GetBHash(), out MeshInfo existingMeshInfo)) {
                    // If not already in the list, add this MeshInfo
                    Meshes.Add(meshInfo.GetBHash(), meshInfo.handle, meshInfo);
                }
            }
        }

        // Fetch a MeshInfo corresponding to the passed hash but, if the
        //   MeshInfo is not in the table, invoke the passed builder to create
        //   an instance of the needed MeshInfo.
        public delegate MeshInfo MeshInfoBuilder();
        public MeshInfo GetMeshInfo(BHash hash, MeshInfoBuilder builder) {
            MeshInfo meshInfo = null;
            lock (Meshes) {
                if (!Meshes.TryGetValue(hash, out meshInfo)) {
                    if (builder != null) {
                        meshInfo = builder();
                        Meshes.Add(hash, meshInfo.handle, meshInfo);
                        // Assert the hash we're indexing it under is the one in meshInfo
                        if (!hash.Equals(meshInfo.GetBHash())) {
                            _log.ErrorFormat( "AssetFetcher.GetMeshInfo: adding mesh with different hash!");
                            _log.ErrorFormat( "AssetFetcher.GetMeshInfo: meshInfo.handle={0}, passed hash={1}, meshInfo.hash={2}",
                                        meshInfo.handle, hash.ToString(), meshInfo.GetBHash().ToString());
                        }
                    }
                    else {
                        meshInfo = null;
                    }
                }
            }
            return meshInfo;
        }
        // Short form that just returns 'null' if not found.
        public MeshInfo GetMeshInfo(BHash hash) {
            return GetMeshInfo(hash, null);
        }

        // Add the passed MaterialInfo the to list if it is not already in the list
        public void AddUniqueMatInfo(MaterialInfo matInfo) {
            lock (Materials) {
                if (!Materials.TryGetValue(matInfo.GetBHash(), out MaterialInfo existingMatInfo)) {
                    // If not already in the list, add this MeshInfo
                    Materials.Add(matInfo.GetBHash(), matInfo.handle, matInfo);
                }
            }
        }

        // Fetch a MaterialInfo corresponding to the passed hash but, if the
        //   MaterialInfo is not in the table, invoke the passed builder to create
        //   an instance of the needed MaterialInfo.
        public delegate MaterialInfo MaterialInfoBuilder();
        public MaterialInfo GetMaterialInfo(BHash hash, MaterialInfoBuilder builder) {
            MaterialInfo matInfo = null;
            lock (Materials) {
                if (!Materials.TryGetValue(hash, out matInfo)) {
                    if (builder != null) {
                        matInfo = builder();
                        Materials.Add(hash, matInfo.handle, matInfo);
                    }
                    else {
                        matInfo = null;
                    }
                }
            }
            return matInfo;
        }
        // Short form that just returns 'null' if not found.
        public MaterialInfo GetMaterialInfo(BHash hash) {
            return GetMaterialInfo(hash, null);
        }

        // Add the passed MaterialInfo the to list if it is not already in the list
        public void AddUniqueImageInfo(ImageInfo imgInfo) {
            lock (Images) {
                if (!Images.TryGetValue(imgInfo.GetBHash(), out ImageInfo existingImageInfo)) {
                    // If not already in the list, add this MeshInfo
                    Images.Add(imgInfo.GetBHash(), imgInfo.handle, imgInfo);
                }
            }
        }

        // Fetch a ImageInfo corresponding to the passed hash but, if the
        //   ImageInfo is not in the table, invoke the passed builder to create
        //   an instance of the needed ImageInfo.
        public delegate Task<ImageInfo> ImageInfoBuilder();
        public async Task<ImageInfo> GetImageInfo(BHash hash, ImageInfoBuilder builder) {
            if (!Images.TryGetValue(hash, out ImageInfo imageInfo)) {
                if (builder != null) {
                    imageInfo = await builder();
                }
                else {
                    imageInfo = null;
                }
            }
            if (imageInfo != null) {
                // A pitiful excuse for handling the race condition of two identical images
                //    being accessed at the same time. If one was already created, just
                //    ignore what we built above.
                lock (Images) {
                    if (Images.ContainsKey(hash)) {
                        Images.TryGetValue(hash, out imageInfo);
                    }
                    else {
                        Images.Add(hash, imageInfo.handle, imageInfo);
                    }
                }
            }
            return imageInfo;
        }
        // Short form that just returns 'null' if not found.
        public Task<ImageInfo> GetImageInfo(BHash hash) {
            return GetImageInfo(hash, null);
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

    // An AssetFetcher that does not have an asset system behind it but is used for the
    //    lists and their access functions in the base class;
    public class NullAssetFetcher : AssetManager {

        public NullAssetFetcher(BLogger pLog, IParameters pParams) : base(pLog, pParams) {
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<byte[]> FetchRawAsset(EntityHandle handle) {
            throw new NotImplementedException();
        }

        public override async Task<Image> FetchTextureAsImage(EntityHandle handle) {
            throw new NotImplementedException();
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        public override void StoreRawAsset(EntityHandle handle, string name, OMV.AssetType assetType, OMV.UUID creatorID, byte[] data) {
            throw new NotImplementedException();
        }

        public override void StoreTextureImage(EntityHandle handle, string name, OMV.UUID creatorID, Image pImage) {
            throw new NotImplementedException();
        }

        public override string ToString() {
            return base.ToString();
        }
    }

    // Fetch an asset from  the OpenSimulator asset system
    public class OSAssetFetcher : AssetManager {
    #pragma warning disable 414
        private readonly string _logHeader = "[OSAssetFetcher]";
    #pragma warning restore 414
        private IAssetService _assetService;

        public OSAssetFetcher(IAssetService pAssetService, BLogger pLog, IParameters pParam) : base(pLog, pParam) {
            _assetService = pAssetService;
        }

        public override async Task<byte[]> FetchRawAsset(EntityHandle handle) {
            byte[] returnBytes = new byte[0];
            var asset = await AssetServiceGetAsync(handle);
            if (asset != null) {
                returnBytes = asset.Data;
            }
            if (returnBytes.Length == 0) {
                throw new Exception("FetchRawAsset: could not fetch asset " + handle.ToString());
            }
            return returnBytes;
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
        /// in the AssetTexture is the UUID and the binary data.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public override async Task<Image> FetchTextureAsImage(EntityHandle handle) {
            Image imageDecoded = null;

            AssetBase asset = await AssetServiceGetAsync(handle);
            if (asset != null) {
                if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                    try {
                        if (_params.P<bool>("UseOpenJPEG")) {
                            if (OpenJPEG.DecodeToImage(asset.Data, out ManagedImage mimage, out imageDecoded)) {
                                mimage = null;  // 'mimage' is unused so release the reference
                            }
                            else {
                                // Could not decode the image. Odd.
                                imageDecoded = null;
                            }
                        }
                        else {
                            // Code for using NuGet CSJ2K. Thought it might be better but noticed no difference.
                            CSJ2K.Util.BitmapImageCreator.Register();
                            imageDecoded = CSJ2K.J2kImage.FromBytes(asset.Data).As<Bitmap>();
                        }
                    }
                    catch (Exception e) {
                        throw new Exception("FetchTextureAsImage: exception decoding JPEG2000 texture. ID=" + handle.ToString()
                                    + ", e=" + e.ToString());
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
                }
                else {
                    throw new Exception("FetchTextureAsImage: asset was not of type texture. ID=" + handle.ToString());
                }
            }
            else {
                throw new Exception("FetchTextureAsImage: could not fetch texture asset. ID=" + handle.ToString());
            }

            return imageDecoded;
        }

        // An async/await version of async call to OpenSimulator AssetService.
        public async Task<AssetBase> AssetServiceGetAsync(EntityHandle pHandle) {
            var tcs = new TaskCompletionSource<AssetBase>();
            _assetService.Get(pHandle.GetUUID().ToString(), this, (rid, rsender, rasset) => {
                tcs.SetResult(rasset);
            });

            return await tcs.Task;
        }


        public override void Dispose() {
            base.Dispose();
            _assetService = null;
        }
    }
}
