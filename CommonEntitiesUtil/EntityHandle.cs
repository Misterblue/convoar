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

namespace org.herbal3d.cs.CommonEntitiesUtil {

    // Class for collecting all me mess around asset names.
    // All filename, type, and version conversions are done here.
    //
    // At the moment, an entity just has a UUID
    public abstract class EntityHandle : IEqualityComparer<EntityHandle> {

        // System.Object.GetHashCode()
        public override abstract int GetHashCode();

        // IComparable
        public abstract int CompareTo(object obj);

        // IEqualityComparer.Equals
        public abstract bool Equals(EntityHandle x, EntityHandle y);

        // IEqualityComparer.GetHashCode
        public abstract int GetHashCode(EntityHandle obj);

        // System.Object.ToString()
        // ToString() returns what is needed for the constructor that takes a string
        public override abstract string ToString();

        public abstract BHash GetBHash();

        // THis is usually overridden by the sub-class
        public abstract OMV.UUID GetUUID();
    }

    public class EntityHandleUUID : EntityHandle {

        protected OMV.UUID _uuid;

        public EntityHandleUUID() {
            _uuid = OMV.UUID.Random();
        }

        public EntityHandleUUID(string handleString) {
            _uuid = new OMV.UUID(handleString);
        }

        public EntityHandleUUID(OMV.UUID id) {
            _uuid = id;
        }

        public override OMV.UUID GetUUID() {
            return _uuid;
        }

        // ToString() returns a 'name' for this entity that can be used to look it up
        public override string ToString() {
            return _uuid.ToString();
        }

        public byte[] ToBytes() {
            byte[] ret = new byte[24];
            return _uuid.GetBytes();
        }

        // IComparable
        public override int CompareTo(object obj) {
            int ret = 0;
            if (!(obj is EntityHandleUUID other)) {
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

        public override BHash GetBHash() {
            return new BHashULong(_uuid.GetHashCode());
        }

        // IEqualityComparer.Equals
        public override bool Equals(EntityHandle x, EntityHandle y) {
            bool ret = false;
            EntityHandleUUID yU = y as EntityHandleUUID;
            if (x is EntityHandleUUID xU && yU != null) {
                ret = xU._uuid.CompareTo(yU._uuid) == 0;
            }
            return ret;
        }

        // IEqualityComparer.GetHashCode
        public override int GetHashCode(EntityHandle obj) {
            int ret = 0;
            if (obj is EntityHandleUUID objU) {
                ret = objU._uuid.GetHashCode();
            }
            return ret;
        }
    }
}
