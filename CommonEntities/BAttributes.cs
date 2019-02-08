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

namespace org.herbal3d.cs.os.CommonEntities {
    // Holds a list of attributes.
    // These are name/value pairs and are used to pass information from 
    //    an entitys sources representation to the output representation.
    //    Like an OpenSimulator region would have attributes like water height
    //    that should show up in the output scene information but don't need
    //    to be processed in the middle. In this case, the converter would put
    //    such attributes into a BAttribute structure that that would be
    //    serialized at the output end.
    public class BAttributes : Dictionary<string, Object> {
        
    }
}
