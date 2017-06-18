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

        OMV.UUID m_uuid;

        public EntityHandle() {
            m_uuid = OMV.UUID.Random();
        }

        public EntityHandle(OMV.UUID id) {
            m_uuid = id;
        }

        // OpenSim likes to specify assets with a simple string of the asset's UUID
        public string GetOSAssetString() {
            return m_uuid.ToString();
        }

        public OMV.UUID GetUUID() {
            return m_uuid;
        }

        public override string ToString() {
            return m_uuid.ToString();
        }

        public byte[] ToBytes() {
            byte[] ret = new byte[24];
            return m_uuid.GetBytes();
        }

        // IComparable
        public int CompareTo(object obj) {
            int ret = 0;
            EntityHandle other = obj as EntityHandle;
            if (other == null) {
                throw new ArgumentException("CompareTo in EntityHandle: other type not EntityHandle");
            }
            if (this.m_uuid != other.m_uuid) {
                string thisOne = this.m_uuid.ToString();
                string otherOne = this.m_uuid.ToString();
                ret = thisOne.CompareTo(otherOne);
            }
            return ret;
        }

        // System.Object.GetHashCode()
        public override int GetHashCode() {
            return m_uuid.GetHashCode();
        }

        // IEqualityComparer.Equals
        public bool Equals(EntityHandle x, EntityHandle y) {
            return x.m_uuid.CompareTo(y.m_uuid) == 0;
        }

        // IEqualityComparer.GetHashCode
        public int GetHashCode(EntityHandle obj) {
            return obj.m_uuid.GetHashCode();
        }
    }
}
