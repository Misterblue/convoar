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
using System.Drawing;

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
    public abstract class IAssetFetcher : IDisposable {
        public abstract IPromise<OMVA.AssetTexture> FetchTexture(EntityHandle handle);
        public abstract IPromise<Image> FetchTextureAsImage(EntityHandle handle);
        public abstract IPromise<byte[]> FetchRawAsset(EntityHandle handle);
        public abstract void Dispose();
    }

    // Fetch an asset from  the OpenSimulator asset system
    public class OSAssetFetcher : IAssetFetcher {
        private string _logHeader = "[OSAssetFetcher]";
        private GlobalContext _context;

        private Scene _scene;
        private IAssetService _assetService;

        public OSAssetFetcher(Scene pScene, IAssetService pAssetService, GlobalContext pGC) {
            _scene = pScene;
            _assetService = pAssetService;
            _context = pGC;
        }

        public override IPromise<byte[]> FetchRawAsset(EntityHandle handle) {
            var prom = new Promise<byte[]>();

            // Don't bother with async -- this call will hang until the asset is fetched
            byte[] returnBytes = _assetService.GetData(handle.GetOSAssetString());
            if (returnBytes.Length > 0) {
                prom.Resolve(returnBytes);
            }
            else {
                prom.Reject(new Exception("FetchRawAsset: could not fetch asset " + handle.ToString()));
            }
            return prom;
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
            AssetBase asset = _assetService.Get(handle.GetOSAssetString());
            if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                OMVA.AssetTexture tex = new OMVA.AssetTexture(handle.GetUUID(), asset.Data);
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
            AssetBase asset = _assetService.Get(handle.GetOSAssetString());
            if (asset != null) {
                if (asset.IsBinaryAsset && asset.Type == (sbyte)OMV.AssetType.Texture) {
                    try {
                        Image imageDecoded = null;
                        if (_context.parms.UseOpenSimImageDecoder) {
                            IJ2KDecoder imgDecoder = _scene.RequestModuleInterface<IJ2KDecoder>();
                            imageDecoded = imgDecoder.DecodeToImage(asset.Data);
                        }
                        else {
                            ManagedImage mimage;
                            if (OpenJPEG.DecodeToImage(asset.Data, out mimage, out imageDecoded)) {
                                mimage = null;
                            }
                            else {
                                imageDecoded = null;
                            }
                        }
                        prom.Resolve(imageDecoded);
                    }
                    catch (Exception e) {
                        prom.Reject(new Exception("FetchTextureAsImage: exception decoding JPEG2000 texture. ID=" + handle.ToString()
                                    + ", e=" + e.ToString()));
                    }
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
            _scene = null;
            _assetService = null;
        }
    }

    class BasilAssetss {
    }
}
