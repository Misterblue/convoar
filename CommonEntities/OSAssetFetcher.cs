/*
 * Copyright (c) 2019 Robert Adams
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
using System.Linq;
using System.Text;
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

    // Fetch an asset from  the OpenSimulator asset system
    public class OSAssetFetcher : IDisposable{
    #pragma warning disable 414
        private readonly string _logHeader = "[OSAssetFetcher]";
    #pragma warning restore 414
        protected readonly BLogger _log;
        protected readonly IParameters _params;

        private IAssetService _assetService;

        public OSAssetFetcher(IAssetService pAssetService, BLogger pLog, IParameters pParam) {
            _assetService = pAssetService;
            _log = pLog;
            _params = pParam;
        }

        // In OpenSimulator storage, objects are stored as typed AssetBase objects.
        // The 'raw' part of this function means just returning the binary blob.
        public async Task<byte[]> FetchRawAsset(EntityHandle handle) {
            byte[] returnBytes = new byte[0];
            AssetBase asset = await AssetServiceGetAsync(handle);
            if (asset != null) {
                returnBytes = asset.Data;
            }
            if (returnBytes.Length == 0) {
                throw new Exception("FetchRawAsset: could not fetch asset " + handle.ToString());
            }
            return returnBytes;
        }

        /// <summary>
        /// Fetch a texture and return an OMVA.AssetTexture. The only information initialized
        /// in the AssetTexture is the UUID and the binary data.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public async Task<Image> FetchTextureAsImage(EntityHandle handle) {
            Image imageDecoded = null;

            AssetBase asset = await AssetServiceGetAsync(handle);
            if (asset != null) {
                if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                    try {
                        // if (_params.P<bool>("UseOpenJPEG")) {
                            if (OpenJPEG.DecodeToImage(asset.Data, out ManagedImage mimage, out imageDecoded)) {
                                mimage = null;  // 'mimage' is unused so release the reference
                            }
                            else {
                                // Could not decode the image. Odd.
                                imageDecoded = null;
                            }
                        // }
                        // else {
                        //     // Code for using NuGet CSJ2K. Thought it might be better but noticed no difference.
                        //     CSJ2K.Util.BitmapImageCreator.Register();
                        //     imageDecoded = CSJ2K.J2kImage.FromBytes(asset.Data).As<Bitmap>();
                        // }
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

        /*  Operations that we don't use on the OpenSim asset system.

        // Get the binary part and make it text
        public async Task<string> FetchText(EntityHandle handle) {
            byte[] asset = await FetchRawAsset(handle);
            return Encoding.UTF8.GetString(asset);
        }

        public Task StoreAssetBase(EntityHandle handle, string name, OMV.AssetType assetType, OMV.UUID creatorID, byte[] data) {
            return Task.Run(() => {
                AssetBase newAsset = new AssetBase(((EntityHandleUUID)handle).GetUUID(), name, (sbyte)assetType, creatorID.ToString());
                _assetService.Store(newAsset);
            });
        }

        public Task StoreTextureImage(EntityHandle handle, string name, OMV.UUID creatorID, Image pImage) {
            return Task.Run(() => {
                // This application overloads AssetType.TExtureTGA to be our serialized image
                AssetBase newAsset = new AssetBase(((EntityHandleUUID)handle).GetUUID(), name, (sbyte)OMV.AssetType.TextureTGA, creatorID.ToString());
                using (MemoryStream byteStream = new MemoryStream()) {
                    pImage.Save(byteStream, System.Drawing.Imaging.ImageFormat.Png);
                    newAsset.Data = byteStream.ToArray();
                }
                _assetService.Store(newAsset);
            });
        }
        */

        // An async/await version of async call to OpenSimulator AssetService.
        public async Task<AssetBase> AssetServiceGetAsync(EntityHandle pHandle) {
            var tcs = new TaskCompletionSource<AssetBase>();
            _assetService.Get(pHandle.GetUUID().ToString(), this, (rid, rsender, rasset) => {
                tcs.SetResult(rasset);
            });

            return await tcs.Task;
        }


        public void Dispose() {
            _assetService = null;
        }
    }
}
