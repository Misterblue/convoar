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
    public class AssetManager : IDisposable {
    #pragma warning disable 414
        private readonly string _logHeader = "[AssetManager]";
    #pragma warning restore 414
        protected readonly BLogger _log;
        protected readonly IParameters _params;

        public OSAssetFetcher OSAssets;
        public BAssets Assets;
        public BAssetStorage AssetStorage;

        public AssetManager(IAssetService pAssetService, BLogger pLog, IParameters pParam) {
            _log = pLog;
            _params = pParam;
            OSAssets = new OSAssetFetcher(pAssetService, pLog, pParam);
            Assets = new BAssets(pLog, pParam);
            AssetStorage = new BAssetStorage(pLog, pParam);
        }

        public void Dispose() {
            if (OSAssets != null) {
                OSAssets.Dispose();
                OSAssets = null;
            }
            if (Assets != null) {
                Assets.Dispose();
                Assets = null;
            }
            if (AssetStorage != null) {
                AssetStorage.Dispose();
                AssetStorage = null;
            }
        }
    }

}
