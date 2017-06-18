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
        public EntityHandle handle;
        public List<OMVR.Vertex> vertexs;
        public List<int> indices;
        public OMV.Vector3 faceCenter;

        private BHash _hash = null;

        public MeshInfo() {
            handle = new EntityHandle();
            vertexs = new List<OMVR.Vertex>();
            indices = new List<int>();
            faceCenter = OMV.Vector3.Zero;
        }

        // The hash is just a function of the vertices and indices
        // TODO: figure out how to canonicalize the vertices order.
        //    At the moment this relies on the determinism of the vertex generators.
        public BHash GetHash() {
            return GetHash(false);
        }
        public BHash GetHash(bool force) {
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
    }
}
