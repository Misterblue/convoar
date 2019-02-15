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
using System.Threading.Tasks;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.os.CommonEntities
{
    // A very simple, non-persistant, in memory asset service.
    public class MemAssetService : IAssetService, IDisposable {

        private Dictionary<string, AssetBase> assets;

        public MemAssetService() {
            assets = new Dictionary<string, AssetBase>();
        }

        // Return the number of assets in storage
        public int NumAssets {
            get { return assets.Count(); }
        }

        // IAssetService.AssetsExist
        public bool[] AssetsExist(string[] ids) {
            bool[] ret = new bool[ids.Length];
            for (int ii = 0; ii < ids.Length; ii++) {
                ret[ii] = assets.ContainsKey(ids[ii]);
            }
            return ret;
        }

        // IAssetService.Delete
        public bool Delete(string id) {
            return assets.Remove(id);
        }

        // IAssetService.Get
        public AssetBase Get(string id) {
            AssetBase ret = null;
            assets.TryGetValue(id, out ret);
            return ret;
        }

        // IAssetService.Get
        public bool Get(string id, object sender, AssetRetrieved handler) {
            AssetBase asset = this.Get(id);
            handler(id, sender, asset);
            return (asset != null);
        }

        // IAssetService.GetCached
        public AssetBase GetCached(string id) {
            // doesn't matter if cached or not
            return this.Get(id);
        }

        // IAssetService.GetData
        public byte[] GetData(string id) {
            byte[] ret = null;
            AssetBase asset = null;
            assets.TryGetValue(id, out asset);
            if (asset != null) {
                ret = asset.Data;
            }
            return ret;
        }

        // IAssetService.GetMetadata
        public AssetMetadata GetMetadata(string id) {
            AssetMetadata ret = null;
            AssetBase asset = null;
            assets.TryGetValue(id, out asset);
            if (asset != null) {
                ret = asset.Metadata;
            }
            return ret;
        }

        // IAssetService.Store
        public string Store(AssetBase asset) {
            string id = asset.ID;
            // logic from OpenSim.Services.FSAssetService.FSAssetConnector
            if (String.IsNullOrEmpty(id)) {
                if (asset.FullID == OMV.UUID.Zero) {
                    asset.FullID = OMV.UUID.Random();
                }
                id = asset.FullID.ToString();
            }
            else {
                if (asset.FullID == OMV.UUID.Zero) {
                    OMV.UUID uuid = OMV.UUID.Zero;
                    if (OMV.UUID.TryParse(asset.ID, out uuid)) {
                        asset.FullID = uuid;
                    }
                    else {
                        asset.FullID = OMV.UUID.Random();
                        id = asset.FullID.ToString();
                    }
                }
            }
            if (!assets.ContainsKey(id)) {
                assets.Add(id, asset);
            }
            return id;
        }

        // IAssetService.UpdateContent
        public bool UpdateContent(string id, byte[] data) {
            bool ret = false;
            AssetBase asset = null;
            assets.TryGetValue(id, out asset);
            if (asset != null) {
                asset.Data = data;
                ret = true;
            }
            return ret;
        }

        // IDisposable.Dispose()
        public void Dispose()
        {
            if (assets != null) {
                assets.Clear();
                assets = null;
            }
        }
    }
}
