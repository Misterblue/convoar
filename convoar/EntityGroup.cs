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
using System.Drawing;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

/*
    EntityGroupList is a collection of entities in a scene
    EntityGroup is a group of prims that make up a scene entity (single prim or linkset)
    ExtendedPrimGroup are the lod versions of an individual prim (usually only lod1)
    ExtendedPrim is an individual prim (derived from Primitive, Sculpty, or Mesh)
    FaceInfo are the meshes that make up an individual prim
 */

namespace org.herbal3d.convoar {
    public class CoordSystem {
        public const int Handedness = 0x200;    // bit that specifies the handedness
        public const int UpDimension = 0x00F;   // field that specifies the up dimension
        public const int UVOrigin = 0x030;      // field that specifies UV origin location
        public const int RightHand = 0x000;
        public const int LeftHand = 0x200;
        public const int Yup = 0x001;
        public const int Zup = 0x002;
        public const int UVOriginUpperLeft = 0x000; // most of the world has origin in upper left
        public const int UVOriginLowerLeft = 0x010; // OpenGL specifies UV origin in lower left
        public const int RightHand_Yup = RightHand + Yup;
        public const int LeftHand_Yup = LeftHand + Yup;
        public const int RightHand_Zup = RightHand + Zup;
        public const int LeftHand_Zup = LeftHand + Zup;
        // RightHand_Zup: SL
        // RightHand_Yup: OpenGL
        // LeftHand_Yup: DirectX, Babylon, Unity

        public int system;
        public CoordSystem() {
            system = RightHand_Zup; // default to SL
        }
        public CoordSystem(int initCoord) {
            system = initCoord;
        }
        public int getUpDimension { get  { return system & UpDimension; } }
        public int getHandedness { get  { return system & Handedness; } }
        public int getUVOrigin { get  { return system & UVOrigin; } }
        public bool isHandednessChanging(CoordSystem nextSystem) {
            return (system & Handedness) != (nextSystem.system & Handedness);
        }
        public string SystemName { get { return SystemNames[system]; } }
        public static Dictionary<int, string> SystemNames = new Dictionary<int, string>() {
            { RightHand_Yup, "RightHand,Y-up" },
            { RightHand_Zup, "RightHand,Z-up" },
            { LeftHand_Yup, "LeftHand,Y-up" },
            { LeftHand_Zup, "LeftHand,Z-up" }
        };
        public CoordSystem clone() {
            return new CoordSystem(this.system);
        }
    }

    public class FaceInfo {
        public int num;                 // number of this face on the prim
        public List<OMVR.Vertex> vertexs;
        public List<ushort> indices;
        public OMV.Vector3 faceCenter;

        public ExtendedPrim containingPrim;

        // Information about the material decorating the vertices
        public OMV.Primitive.TextureEntryFace textureEntry;
        public OMV.UUID? textureID;     // UUID of the texture if there is one
        public Image faceImage;
        public bool hasAlpha;           // true if there is some transparancy in the surface
        public bool fullAlpha;          // true if the alpha is everywhere
        // public string imageFilename;    // filename built for this face material
        // public string imageURI;         // URI built for this face material
        public BasilPersist persist;    // Information on persisting the image as a file and an URI

        private static OMV.Primitive.TextureEntryFace DefaultWhite =
                        new OMV.Primitive.TextureEntryFace(null) { TextureID = OMV.Primitive.TextureEntry.WHITE_TEXTURE };

        public FaceInfo(int pNum, ExtendedPrim pContainingPrim) {
            Init(pNum, pContainingPrim);
        }

        public FaceInfo(int pNum, ExtendedPrim pContainingPrim, OMVR.Face aFace, OMV.Primitive.TextureEntryFace tef) {
            Init(pNum, pContainingPrim);
            vertexs = aFace.Vertices.ToList();
            indices = aFace.Indices.ToList();
            faceCenter = aFace.Center;
            textureEntry = tef;
            if (tef.RGBA.A != 1f) {
                fullAlpha = true;
            }
        }

        private void Init(int pNum, ExtendedPrim pContainingPrim) {
            num = pNum;
            containingPrim = pContainingPrim;
            vertexs = new List<OMVR.Vertex>();
            indices = new List<ushort>();
            faceCenter = OMV.Vector3.Zero;
            // textureEntry = DefaultWhite;
            hasAlpha = false;
            fullAlpha = false;
            faceImage = null;       // flag saying if an image is present
            textureID = null;       // flag saying if an image is present
        }

        // TextureEntryFace.GetHashCode() includes all the texture displacement which is
        //    really already applied to the UV of the mesh.
        // This is a truncated version of the hash computation in TextureEntryFace.
        public int GetTextureHash() {
            int hash = 0;
            if (textureEntry != null) {
                hash =
                 textureEntry.RGBA.GetHashCode() ^
                 // textureEntry.RepeatU.GetHashCode() ^
                 // textureEntry.RepeatV.GetHashCode() ^
                 // textureEntry.OffsetU.GetHashCode() ^
                 // textureEntry.OffsetV.GetHashCode() ^
                 // textureEntry.Rotation.GetHashCode() ^
                 textureEntry.Glow.GetHashCode() ^
                 textureEntry.Bump.GetHashCode() ^
                 textureEntry.Shiny.GetHashCode() ^
                 textureEntry.Fullbright.GetHashCode() ^
                 textureEntry.MediaFlags.GetHashCode() ^
                 textureEntry.TexMapType.GetHashCode() ^
                 textureEntry.TextureID.GetHashCode() ^
                 textureEntry.MaterialID.GetHashCode();
            }
            else {
                var rnd = new Random();
                hash = rnd.Next();
            }
            return hash;
        }
    }

    // An extended description of an entity that includes the original
    //     prim description as well as the mesh.
    // All the information about the meshed piece is collected here so other mappings
    //     can happen with the returned information (creating Basil Entitities, etc)
    public class ExtendedPrim {
        public struct FromOSEntities {
            // This separates pointers into OpenSimulator structures so it is
            //     easy to find the references. All info should be copied into
            //     the ExtendedPrim when created so these are here for debugging mostly.
            public SceneObjectGroup SOG { get; set; }
            public SceneObjectPart SOP { get; set; }
            public OMV.Primitive primitive { get; set; }
            public OMVR.FacetedMesh facetedMesh { get; set; }
        };
        public FromOSEntities fromOS;
        public OMV.UUID ID;
        public string Name;

        public CoordSystem coordSystem; // coordinate system of this prim
        public OMV.Vector3 translation;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Matrix4? transform;
        public bool positionIsParentRelative;

        // The data is taken out of the structures above and copied here for mangling
        public List<FaceInfo> faces;

        // This logic is here mostly because there are some entities that are not scene objects.
        // Terrain, in particular.
        // Someone can force root state by setting this value otherwise the rootedness of the underlying
        //     SOP is used.
        private bool? m_isRoot = null;
        public bool isRoot {
            get {
                bool ret = true;
                if (m_isRoot == null) {
                    if (fromOS.SOP != null && !fromOS.SOP.IsRoot)
                        ret = false;
                }
                else {
                    ret = (bool)m_isRoot;
                }
                return ret;
            }
            set {
                m_isRoot = value;
            }
        }

        // A very empty ExtendedPrim. You must initialize everything by hand after creating this.
        public ExtendedPrim() {
            transform = null;
            coordSystem = new CoordSystem(CoordSystem.RightHand_Zup);    // default to SL coordinates
            faces = new List<FaceInfo>();
        }

        // Make a new extended prim based on an existing one
        public ExtendedPrim(ExtendedPrim ep) : this(ep.fromOS.SOG, ep.fromOS.SOP, ep.fromOS.primitive, ep.fromOS.facetedMesh) {
        }

        // Initialize an ExtendedPrim from the OpenSimulator structures.
        // Note that the translation and rotation are copied into the ExtendedPrim for later coordinate modification.
        public ExtendedPrim(SceneObjectGroup pSOG, SceneObjectPart pSOP, OMV.Primitive pPrim, OMVR.FacetedMesh pFMesh) {
            fromOS.SOG = pSOG;
            fromOS.SOP = pSOP;
            fromOS.primitive = pPrim;
            fromOS.facetedMesh = pFMesh;
            translation = new OMV.Vector3(0, 0, 0);
            rotation = OMV.Quaternion.Identity;
            scale = OMV.Vector3.One;
            transform = null;       // matrix overrides the translation/rotation. Start with no matrix.
            coordSystem = new CoordSystem(CoordSystem.RightHand_Zup);    // default to SL coordinates

            if (fromOS.SOP != null) {
                ID = fromOS.SOP.UUID;
                Name = fromOS.SOP.Name;
            }
            else {
                ID = OMV.UUID.Random();
                Name = "Custom";
            }

            if (fromOS.SOP != null) {
                if (fromOS.SOP.IsRoot) {
                    translation = fromOS.SOP.GetWorldPosition();
                    rotation = fromOS.SOP.GetWorldRotation();
                    positionIsParentRelative = false;
                }
                else {
                    translation = fromOS.SOP.OffsetPosition;
                    rotation = fromOS.SOP.RotationOffset;
                    positionIsParentRelative = true;
                }
                scale = fromOS.SOP.Scale;
            }

            // Copy the vertex information into our face information array.
            // Only the vertex and indices information is put into the face info.
            //       The texture info must be added later.
            faces = new List<FaceInfo>();

            if (pPrim != null) {
                for (int ii = 0; ii < pFMesh.Faces.Count; ii++) {
                    OMV.Primitive.TextureEntryFace tef = pPrim.Textures.FaceTextures[ii];
                    if (tef == null) {
                        tef = pPrim.Textures.DefaultTexture;
                    }
                    OMVR.Face aFace = pFMesh.Faces[ii];
                    FaceInfo faceInfo = new FaceInfo(ii, this, aFace, tef);

                    faces.Add(faceInfo);
                }
            }
        }

        public override int GetHashCode() {
            int ret = 0;
            if (fromOS.primitive != null) {
                ret = fromOS.primitive.GetHashCode();
            }
            else {
                ret = base.GetHashCode();
            }
            return ret;
        }

        public string Stats() {
            StringBuilder buff = new StringBuilder();
            buff.Append("ep:faces=" + this.faces.Count.ToString());
            return buff.ToString();
        }
    };

    // A prim mesh can be made up of many versions
    public enum PrimGroupType {
        physics,
        lod1,   // this is default and what is built for a standard prim
        lod2,
        lod3,
        lod4
    };

    // Some prims (like the mesh type) have multiple versions to make one entity
    public class ExtendedPrimGroup: Dictionary<PrimGroupType, ExtendedPrim> {
        public ExtendedPrimGroup() : base() {
        }

        // Create with a single prim
        public ExtendedPrimGroup(ExtendedPrim singlePrim) : base() {
            this.Add(PrimGroupType.lod1, singlePrim);
        }

        // Return the primary version of this prim which is the highest LOD verstion.
        public ExtendedPrim primaryExtendePrim {
            get {
                ExtendedPrim ret = null;
                this.TryGetValue(PrimGroupType.lod1, out ret);
                return ret;
            }
        }
        public string Stats() {
            StringBuilder buff = new StringBuilder();
            buff.Append("epg:lods=" + this.Keys.Count.ToString());
            buff.Append(",");
            buff.Append(this.primaryExtendePrim.Stats());
            return buff.ToString();
        }
    }

    // some entities are made of multiple prims (linksets)
    public class EntityGroup : List<ExtendedPrimGroup> {
        public EntityGroup() : base() {
        }
        public EntityGroup(List<ExtendedPrimGroup> list) : base(list) {
        }
        public string Stats() {
            StringBuilder buff = new StringBuilder();
            buff.Append("EntityGroup: cnt=" + this.Count.ToString() + ":");
            this.ForEach(epg => {
                buff.Append(epg.Stats());
                buff.Append(",");
            });
            return buff.ToString();
        }
    }

    // list of entities
    public class EntityGroupList : List<EntityGroup> {
        public EntityGroupList() : base() {
        }
        public EntityGroupList(EntityGroupList list) : base(list) {
        }
        public EntityGroupList(List<EntityGroup> list) : base(list) {
        }

        // Add the entity group to the list if it is not alreayd in the list
        public bool AddUniqueEntity(EntityGroup added) {
            bool ret = false;
            if (!base.Contains(added)) {
                base.Add(added);
                ret = true;
            }
            return ret;
        }

        // Perform an action on every extended prim in this EntityGroupList
        public void ForEachExtendedPrim(Action<ExtendedPrim> aeg) {
            this.ForEach(eGroup => {
                eGroup.ForEach(ePGroup => {
                    ExtendedPrim ep = ePGroup.primaryExtendePrim;  // the interesting one is the high rez one
                    aeg(ep);
                });
            });
        }
    }
}
