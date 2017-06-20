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

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {

    // Class for collecting all me mess around asset names.
    // All filename, type, and version conversions are done here.
    //
    // At the moment, an entity just has a UUID
    public class EntityHandle : IEqualityComparer<EntityHandle> {

        OMV.UUID _uuid;

        public EntityHandle() {
            _uuid = OMV.UUID.Random();
        }

        public EntityHandle(OMV.UUID id) {
            _uuid = id;
        }

        // OpenSim likes to specify assets with a simple string of the asset's UUID
        public string GetOSAssetString() {
            return _uuid.ToString();
        }

        public OMV.UUID GetUUID() {
            return _uuid;
        }

        public override string ToString() {
            return _uuid.ToString();
        }

        public byte[] ToBytes() {
            byte[] ret = new byte[24];
            return _uuid.GetBytes();
        }

        // IComparable
        public int CompareTo(object obj) {
            int ret = 0;
            EntityHandle other = obj as EntityHandle;
            if (other == null) {
                throw new ArgumentException("CompareTo in EntityHandle: other type not EntityHandle");
            }
            if (this._uuid != other._uuid) {
                string thisOne = this._uuid.ToString();
                string otherOne = this._uuid.ToString();
                ret = thisOne.CompareTo(otherOne);
            }
            return ret;
        }

        // System.Object.GetHashCode()
        public override int GetHashCode() {
            return _uuid.GetHashCode();
        }

        // IEqualityComparer.Equals
        public bool Equals(EntityHandle x, EntityHandle y) {
            return x._uuid.CompareTo(y._uuid) == 0;
        }

        // IEqualityComparer.GetHashCode
        public int GetHashCode(EntityHandle obj) {
            return obj._uuid.GetHashCode();
        }
    }
}
