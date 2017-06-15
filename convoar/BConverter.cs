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
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using log4net;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;


namespace org.herbal3d.convoar {
    // Convert entitygroup items -- simpification and mergings
    public class BConverter {

        private static string _logHeader = "BConverter";

        private GlobalContext _context;

        // Lists of similar faces indexed by the texture hash
        public class SimilarFaces : Dictionary<BHash, List<FaceInfo>> {
            public SimilarFaces() : base() {
            }
            public void AddSimilarFace(BHash pHash, FaceInfo pFace) {
                if (!this.ContainsKey(pHash)) {
                    this.Add(pHash, new List<FaceInfo>());
                }
                this[pHash].Add(pFace);
            }
        }

        public BConverter(GlobalContext context) {
            _context = context;
        }

        // Loop through all the shared faces (faces that share the same material) and create
        //    one mesh for all the faces. This entails selecting one of the faces to be the
        //    root face and then displacing all the vertices, rotations, ... to be based
        //    from that root face.
        // We find the root face by looking for one "in the middle"ish so as to keep the offset
        //    math as small as possible.
        // This creates reorgScene.rebuildFaceEntities from reorgScene.similarFaces.
        public EntityGroupList ConvertEntitiesIntoSharedMaterialMeshes(EntityGroupList staticEntities) {

            // Go through all the static items and make a list of all the meshes with similar textures
            SimilarFaces similarFaces = new SimilarFaces();
            staticEntities.ForEachExtendedPrim(ep => {
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    BHash hashCode = new BHashULong(tef.GetHashCode());
                    similarFaces.AddSimilarFace(hashCode, faceInfo);
                });
            });

            EntityGroupList rebuilt = new EntityGroupList(
                similarFaces.Values.Select(similarFaceList => {
                    ExtendedPrim ep = CreateExtendedPrimFromSimilarFaces(similarFaceList);
                    // The created ExtendedPrim needs to be packaged into an EntityGroup
                    EntityGroup eg = new EntityGroup();
                    eg.Add(new ExtendedPrimGroup(ep));
                    return eg;
                }).ToList()
            );

            return rebuilt;
        }

        // Check all the faces in an EntityGroup (usually a single SL entity) and
        //    merge faces using the same material into single meshes.
        // This reduces large linksets into smaller sets of meshes and also merges
        //    similar prim faces into single meshes.
        public EntityGroupList ConvertEntityGroupIntoSharedMaterialMeshes(EntityGroup eg) {
            // Go through all the materialed meshes and see if there are meshes to share
            SimilarFaces similarFaces = new SimilarFaces();
            // int totalFaces = 0; // DEBUG DEBUG
            eg.ForEach(epg => {
                ExtendedPrim ep = epg.primaryExtendePrim;
                ep.faces.ForEach(faceInfo => {
                    OMV.Primitive.TextureEntryFace tef = faceInfo.textureEntry;
                    BHash hashCode = new BHashULong(tef.GetHashCode());
                    similarFaces.AddSimilarFace(hashCode, faceInfo);
                    // totalFaces++;   // DEBUG DEBUG
                });
            });

            /*
            EntityGroup rebuilt = new EntityGroup(
                similarFaces.Values.Select(similarFaceList => {
                    ExtendedPrim ep = CreateExtendedPrimFromSimilarFaces(similarFaceList);
                    _context.log.DebugFormat("{0} ConvertEntityGroupIntoSharedMaterialMeshes: create ExtendedPrim: {1}",
                                            _logHeader, ep.Stats());
                    return new ExtendedPrimGroup(ep);
                }).ToList()
            );
            */

            EntityGroupList rebuilt = new EntityGroupList(
                similarFaces.Values.Select(similarFaceList => {
                    ExtendedPrim ep = CreateExtendedPrimFromSimilarFaces(similarFaceList);
                    // The created ExtendedPrim needs to be packaged into an EntityGroup
                    EntityGroup newEg = new EntityGroup();
                    newEg.Add(new ExtendedPrimGroup(ep));
                    return newEg;
                }).ToList()
            );

            return rebuilt;
        }

        // Given a list of faces, merge the meshes into a single mesh.
        // THese faces could be from different prims so position computation has to go
        //    to the face's containing prim and then relocated to its new root.
        // The returned ExtendedPrim has a location in the world and all the mesh vertices
        //    have been moved and oriented to that new location.
        private ExtendedPrim CreateExtendedPrimFromSimilarFaces(List<FaceInfo> similarFaceList) {
            // Loop through the faces and find the root. If this is faces from a single linkset, this
            //    will find the root prim as  the reference. Otherwise it will just find some root
            //    prim.
            // There might be a need to find the 'middle' prim of a cluster if position jitter
            //    becomes a problem.
            FaceInfo rootFace = null;
            foreach (FaceInfo faceInfo in similarFaceList) {
                if (faceInfo.containingPrim != null && faceInfo.containingPrim.isRoot) {
                    rootFace = faceInfo;
                    // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: one similar is root.", _logHeader);
                    break;
                }
            }
            if (rootFace == null) {
                // If there wasn't a root entity in the list, just pick a random one
                rootFace = similarFaceList.First();
                // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: no root. Assuming first face", _logHeader);
            }
            ExtendedPrim rootEp = rootFace.containingPrim;

            // Create the new combined object
            ExtendedPrim newEp = new ExtendedPrim(rootEp);
            newEp.ID = OMV.UUID.Random();
            newEp.coordSystem = rootEp.coordSystem.clone();
            newEp.isRoot = true;
            newEp.positionIsParentRelative = false;

            // The merged mesh is located at the root's location with no rotation
            if (rootEp.fromOS.SOP != null) {
                newEp.translation = rootEp.fromOS.SOP.GetWorldPosition();
            }
            else {
                _context.log.LogDebug("{0} CreateExtendedPrimFromSimilarFaces: no OS link. Assuming translation of zero", _logHeader);
                newEp.translation = OMV.Vector3.Zero;
            }
            newEp.rotation = OMV.Quaternion.Identity;

            newEp.scale = rootEp.scale;

            // The 'new ExtendedPrim' above copied the faceted mesh faces. We're doing it over so undo that.
            newEp.faces.Clear();
            FaceInfo newFace = new FaceInfo(999, rootEp);
            newFace.textureEntry = rootFace.textureEntry;
            newFace.textureID = rootFace.textureID;
            newFace.faceImage = rootFace.faceImage;
            newFace.hasAlpha = rootFace.hasAlpha;
            newEp.faces.Add(newFace);

            // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: newEp.trans={1}, newEp.rot={2}",
            //             _logHeader, newEp.translation, newEp.rotation);

            // Based of the root face, create a new mesh that holds all the faces
            similarFaceList.ForEach(faceInfo => {
                // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: adding face {1} from {2}, verts={3}, ind={4}",
                //                 _logHeader, faceInfo.num, faceInfo.containingPrim.ID,
                //                 faceInfo.vertexs.Count, faceInfo.indices.Count);
                // 'faceInfo' and 'ep' is the vertex/indices we're adding to 'newFace'
                ExtendedPrim ep = faceInfo.containingPrim;
                // The indices of the mesh being added needs to be advanced 'indicesBase' since the vertices are
                //     added to the end of the existing list.
                int indicesBase = newFace.vertexs.Count;

                // Translate all the new vertices to world coordinates then subtract the 'newEp' location.
                // All rotation is removed to make computation simplier

                OMV.Vector3 worldPos = OMV.Vector3.Zero;
                OMV.Quaternion worldRot = OMV.Quaternion.Identity;
                if (ep.fromOS.SOP != null) {
                    worldPos = ep.fromOS.SOP.GetWorldPosition();
                    worldRot = ep.fromOS.SOP.GetWorldRotation();
                }
                // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: map {1}, wPos={2}, wRot={3}",
                //                 _logHeader, faceInfo.containingPrim.ID, worldPos, worldRot);
                newFace.vertexs.AddRange(faceInfo.vertexs.Select(vert => {
                    OMVR.Vertex newVert = new OMVR.Vertex();
                    var worldLocationOfVertex = vert.Position * worldRot + worldPos;
                    newVert.Position = worldLocationOfVertex - newEp.translation;
                    newVert.Normal = vert.Normal * worldRot;
                    newVert.TexCoord = vert.TexCoord;
                    return newVert;
                }));
                newFace.indices.AddRange(faceInfo.indices.Select(ind => (ushort)(ind + indicesBase)));
            });
            // _context.log.DebugFormat("{0} CreateExtendedPrimFromSimilarFaces: COMPLETE: verts={1}. ind={2}",
            //             _logHeader, newFace.vertexs.Count, newFace.indices.Count);
            return newEp;
        }

    }
}
