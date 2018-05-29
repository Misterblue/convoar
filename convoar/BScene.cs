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

namespace org.herbal3d.convoar {

    // Representation of instances and whole scene information
    public class BScene {

        public string name;
        public List<BInstance> instances = new List<BInstance>();
        public BAttributes attributes = new BAttributes();
        public BInstance terrainInstance;

        public BScene() {
            name = "no name";
        }

        public BScene(string pName) {
            name = pName;
        }

        // Create a new scene based on an existing scene.
        // NOTE: this is NOT a clone. Instances are not copied and other things just
        //    have their pointers moved so the items are shared.
        public BScene(BScene bScene) {
            name = bScene.name;
            attributes = bScene.attributes;
            terrainInstance = bScene.terrainInstance;
        }
    }
}
