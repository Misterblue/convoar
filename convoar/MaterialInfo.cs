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

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.convoar {

    public class MaterialInfo {
        public EntityHandleUUID handle;
        public OMV.UUID? textureID;     // UUID of the texture if there is one
        public ImageInfo image;
        public OMV.Primitive.TextureEntryFace faceTexture;
        public OMV.MappingType textureMapping;
        public OMV.Color4 RGBA;
        public bool fullAlpha;
        public OMV.Bumpiness bump;
        public float glow;
        public OMV.Shininess shiny; // None, Low, Medium, High
        public bool twoSided;

        private BHash _hash = null;

        public MaterialInfo(OMV.Primitive.TextureEntryFace defaultTexture) {
            faceTexture = new OMV.Primitive.TextureEntryFace(defaultTexture);
        }

        public MaterialInfo(OMVR.Face face, OMV.Primitive.TextureEntryFace defaultTexture) {
            handle = new EntityHandleUUID();
            faceTexture = face.TextureFace;
            if (faceTexture == null) {
                faceTexture = defaultTexture;
            }
            textureID = faceTexture.TextureID;
            if (faceTexture.RGBA.A != 1f) {
                fullAlpha = true;
            }
            RGBA = faceTexture.RGBA;
            bump = faceTexture.Bump;
            glow = faceTexture.Glow;
            shiny = faceTexture.Shiny;
            twoSided = ConvOAR.Globals.parms.P<bool>("DoubleSided");
        }

        public BHash GetBHash() {
            return GetBHash(false);
        }

        public BHash GetBHash(bool force) {
            if (force) _hash = null;

            if (_hash == null) {
                BHasher hasher = new BHasherMdjb2();
                hasher.Add(RGBA.R); // Not using RGBA.GetHashCode() as it always returns the same value
                hasher.Add(RGBA.G);
                hasher.Add(RGBA.B);
                hasher.Add(RGBA.A);
                hasher.Add((int)bump);
                hasher.Add(glow);
                hasher.Add((int)shiny);
                if (textureID.HasValue) {
                    hasher.Add(textureID.Value.GetHashCode());
                }
                _hash = hasher.Finish();
                // ConvOAR.Globals.log.DebugFormat("MaterialInfo.GetBHash: rgba={0},bump={1},glow={2},shiny={3},tex={4},hash={5}",
                //     RGBA, bump, glow, shiny, textureID.HasValue ? textureID.Value.ToString() : "none", _hash.ToString());
            }
            return _hash;
        }

        public override string ToString() {
            string ret = handle.ToString();
            if (image != null) {
                ret += "/hasImg";
                if (image.hasTransprency) ret += "/hasTrans";
            }
            if (fullAlpha) ret += "/fAlpha";
            return ret;
        }
    }
}
