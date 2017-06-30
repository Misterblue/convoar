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

using OpenSim.Region.Framework.Scenes;

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {
    /// <summary>
    /// A set of classes that hold viewer displayable items. These can be
    /// meshes, procedures, or whatever.
    /// </summary>
    public class Displayable {
        public string name = "no name";
        public OMV.Vector3 offsetPosition = OMV.Vector3.Zero;
        public OMV.Quaternion offsetRotation = OMV.Quaternion.Identity;
        public CoordAxis coordAxis = new CoordAxis();
        public OMV.Vector3 scale = new OMV.Vector3(1,1,1);

        // Information on how to display
        public DisplayableRenderable renderable = null;
        public List<Displayable> children = new List<Displayable>();

        // Information from OpenSimulator
        public OMV.UUID baseUUID = OMV.UUID.Zero;   // the UUID of the original object that careated is displayable
        public SceneObjectPart baseSOP = null;
        public BAttributes attributes = new BAttributes();

        public Displayable() {
        }

        public Displayable(DisplayableRenderable pRenderable) {
            renderable = pRenderable;
        }

        public Displayable(DisplayableRenderable pRenderable, SceneObjectPart sop) {
            name = sop.Name;
            baseSOP = sop;
            baseUUID = sop.UUID;
            // If not a root prim, add the offset to the root. 
            // The root Displayable will be zeros (not world position which is in the BInstance).
            if (!sop.IsRoot) {
                offsetPosition = baseSOP.OffsetPosition;
                offsetRotation = baseSOP.RotationOffset;
            }
            if (ConvOAR.Globals.parms.DisplayTimeScaling) {
                scale = sop.Scale;
            }

            attributes.Add("HasSciptsInInventory", sop.Inventory.ContainsScripts());
            attributes.Add("IsPhysical", (sop.PhysActor != null && sop.PhysActor.IsPhysical));
            renderable = pRenderable;
        }
    }

    /// <summary>
    /// The parent class of the renderable parts of the displayable.
    /// Could be a mesh or procedure or whatever.
    /// </summary>
    public abstract class DisplayableRenderable {
        public EntityHandle handle;
        public DisplayableRenderable() {
            handle = new EntityHandleUUID();
        }
    }

    /// <summary>
    /// A group of meshes that make up a renderable item.
    /// For OpenSimulator conversions, this is usually prim faces.
    /// </summary>
    public class RenderableMeshGroup : DisplayableRenderable {
        // The meshes that make up this Renderable
        public List<RenderableMesh> meshes;

        public RenderableMeshGroup() : base() {
            meshes = new List<RenderableMesh>();
        }
    }
        
    public class RenderableMesh {
        public int num;                 // number of this face on the prim
        public EntityHandle mesh;
        public EntityHandle material;
    }
} 