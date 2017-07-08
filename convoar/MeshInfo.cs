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

    public class MeshInfo {
        public EntityHandleUUID handle;
        public List<OMVR.Vertex> vertexs;
        public List<int> indices;
        public OMV.Vector3 faceCenter;
        public OMV.Vector3 scale;  // scaling that has been applied to this mesh
        public CoordAxis coordAxis = new CoordAxis(CoordAxis.RightHand_Zup);    // SL coordinates

        private BHash _hash = null;

        public MeshInfo() {
            handle = new EntityHandleUUID();
            vertexs = new List<OMVR.Vertex>();
            indices = new List<int>();
            scale = OMV.Vector3.One;
            faceCenter = OMV.Vector3.Zero;
        }

        // Create a new MeshInfo with a copy of what is in another MeshInfo.
        // Note that we need to do a deep'ish copy since the values of the 
        //     vertices may be modified in the copy.
        // OMVR.Vertex and OMV.Vextor3 are structs so they are copied.
        public MeshInfo(MeshInfo other) {
            handle = new EntityHandleUUID();
            vertexs = other.vertexs.ConvertAll(v => {
                OMVR.Vertex newV = new OMVR.Vertex();
                newV.Position = v.Position;
                newV.Normal = v.Normal;
                newV.TexCoord = v.TexCoord;
                return newV;
            });
            // vertexs = new List<OMVR.Vertex>(other.vertexs);
            indices = new List<int>(other.indices);
            scale = new OMV.Vector3(other.scale);
            faceCenter = new OMV.Vector3(other.faceCenter);
        }

        // The hash is just a function of the vertices and indices
        // TODO: figure out how to canonicalize the vertices order.
        //    At the moment this relies on the determinism of the vertex generators.
        public BHash GetBHash() {
            return GetBHash(false);
        }
        public BHash GetBHash(bool force) {
            if (force) _hash = null;

            if (_hash == null) {
                BHasher hasher = new BHasherMdjb2();

                vertexs.ForEach(vert => {
                    hasher.Add(vert.GetHashCode());
                });
                indices.ForEach(ind => {
                    hasher.Add(ind);
                });
                hasher.Add(faceCenter.GetHashCode());

                _hash = hasher.Finish();
            }
            return _hash;
        }

        public override string ToString() {
            return String.Format("{0}/v={1}/i={2}", handle, vertexs.Count, indices.Count);
        }
    }
}
