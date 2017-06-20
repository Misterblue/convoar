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
        public EntityHandle handle;
        public OMV.UUID? textureID;     // UUID of the texture if there is one
        public EntityHandle image;
        public OMV.Primitive.TextureEntryFace faceTexture;
        public OMV.MappingType textureMapping;
        public OMV.Color4 RGBA;
        public bool fullAlpha;
        public OMV.Bumpiness bump;
        public float glow;
        public OMV.Shininess shiny; // None, Low, Medium, High


        private BHash _hash = null;

        public MaterialInfo(OMV.Primitive.TextureEntryFace defaultTexture) {
            faceTexture = new OMV.Primitive.TextureEntryFace(defaultTexture);
        }

        public MaterialInfo(OMVR.Face face, OMV.Primitive.TextureEntryFace defaultTexture) {
            handle = new EntityHandle();
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
        }

        public BHash GetHash() {
            return GetHash(false);
        }

        public BHash GetHash(bool force) {
            if (force) _hash = null;

            if (_hash == null) {
                int intHash;
                if (faceTexture != null) {
                    intHash =
                        faceTexture.RGBA.GetHashCode() ^
                        // faceTexture.RepeatU.GetHashCode() ^
                        // faceTexture.RepeatV.GetHashCode() ^
                        // faceTexture.OffsetU.GetHashCode() ^
                        // faceTexture.OffsetV.GetHashCode() ^
                        // faceTexture.Rotation.GetHashCode() ^
                        faceTexture.Glow.GetHashCode() ^
                        faceTexture.Bump.GetHashCode() ^
                        faceTexture.Shiny.GetHashCode() ^
                        faceTexture.Fullbright.GetHashCode() ^
                        faceTexture.MediaFlags.GetHashCode() ^
                        faceTexture.TexMapType.GetHashCode() ^
                        faceTexture.TextureID.GetHashCode() ^
                        faceTexture.MaterialID.GetHashCode();
                }
                else {
                    var rnd = new Random();
                    intHash = rnd.Next();
                }
                _hash = new BHashULong(intHash);
            }
            return _hash;
        }
    }
}
