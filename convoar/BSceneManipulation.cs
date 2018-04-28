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

    // Representation of instances and whole scene information
    public class BSceneManipulation {
        private static string _logHeader = "[BSceneManipulation]";

        public BSceneManipulation() {
        }

        private class InvertedMesh {
            public BScene containingScene;
            public BInstance containingInstance;
            public Displayable containingDisplayable;
            public DisplayableRenderable containingDisplayableRenderable;
            public RenderableMesh renderableMesh;

            public InvertedMesh(BScene pBs, BInstance pInst, Displayable pDisp, DisplayableRenderable pDisprend, RenderableMesh pRm) {
                containingScene = pBs;
                containingInstance = pInst;
                containingDisplayable = pDisp;
                containingDisplayableRenderable = pDisprend;
                renderableMesh = pRm;
            }
        }

        public static BScene MergeSharedMaterialMeshes(BScene bScene) {
            BScene ret = new BScene(bScene.name);

            try {
                // Create collections of meshes with similar materials
                Dictionary<BHash, List<InvertedMesh>> meshByMaterial = new Dictionary<BHash, List<InvertedMesh>>();
                foreach (BInstance inst in bScene.instances) {
                    MapMaterials(meshByMaterial, bScene, inst, inst.Representation);
                }

                ConvOAR.Globals.log.DebugFormat("{0} MergeShareMaterialHashes: number of materials = {1}",
                                    _logHeader, meshByMaterial.Count);

                // Merge the meshes and create an Instance containing the new mesh set
                ret.instances = (BInstanceList)meshByMaterial.Keys.Select(meshHash => {
                    ConvOAR.Globals.log.DebugFormat("{0} MergeShareMaterialHashes: creating instance for material {1} . meshes={2}",
                                _logHeader, meshHash, meshByMaterial[meshHash].Count);
                    return CreateInstanceFromSharedMaterialMeshes(meshByMaterial[meshHash]);
                }).ToList();

                // The output scene has the same attributes as the input scene
                foreach (string key in bScene.attributes.Keys) {
                    ret.attributes.Add(key, bScene.attributes[key]);
                }
            }
            catch (Exception e) {
                ConvOAR.Globals.log.DebugFormat("{0} MergeShareMaterialHashes: exception: {1}", _logHeader, e);
            }

            return ret;
        }

        // Find all the meshes in passed Displayable and add them to the lists indexed by their material hashes
        private static void MapMaterials(Dictionary<BHash, List<InvertedMesh>> hashes, BScene pBs, BInstance pInst, Displayable disp) {
            RenderableMeshGroup rmg = disp.renderable as RenderableMeshGroup;
            if (rmg != null) {
                foreach (RenderableMesh rMesh in rmg.meshes) {
                    BHash materialHash = rMesh.material.GetBHash();
                    if (!hashes.ContainsKey(materialHash)) {
                        hashes.Add(materialHash, new List<InvertedMesh>());
                    }
                    hashes[materialHash].Add(new InvertedMesh(pBs, pInst, disp, rmg, rMesh));
                }
            }
            foreach (Displayable child in disp.children) {
                MapMaterials(hashes, pBs, pInst, child);
            }
        }

        private static BInstance CreateInstanceFromSharedMaterialMeshes(List<InvertedMesh> meshes) {
            // Pick one of the meshes to be the 'root' mesh.
            // Someday may need to find the most center mesh to work from.
            InvertedMesh rootIMesh = meshes.First();

            // The new instance will be at the location of the root mesh with no rotation
            BInstance inst = new BInstance();
            inst.Position = rootIMesh.containingInstance.Position;
            inst.Rotation = OMV.Quaternion.Identity;

            try {
                // The mesh we're going to build
                MeshInfo meshInfo = new MeshInfo();
                foreach (InvertedMesh imesh in meshes) {
                    // Go through the mesh, map all vertices to global coordinates then convert relative to root
                    meshInfo.vertexs.AddRange(imesh.renderableMesh.mesh.vertexs.Select(vert => {
                        OMVR.Vertex newVert = new OMVR.Vertex();
                        var worldLocationOfVertex = vert.Position * imesh.containingInstance.Rotation + imesh.containingInstance.Position;
                        newVert.Position = worldLocationOfVertex - inst.Position;
                        newVert.Normal = vert.Normal * imesh.containingInstance.Rotation;   // is this correct?
                        newVert.TexCoord = vert.TexCoord;
                        return newVert;
                    }));
                    int indicesBase = meshInfo.indices.Count;
                    meshInfo.indices.AddRange(imesh.renderableMesh.mesh.indices.Select(ind => ind + indicesBase));
                }

                RenderableMesh newMesh = new RenderableMesh();
                newMesh.num = 0;
                newMesh.material = rootIMesh.renderableMesh.material;   // The material we share
                newMesh.mesh = meshInfo;

                RenderableMeshGroup meshGroup = new RenderableMeshGroup();
                meshGroup.meshes.Add(newMesh);

                Displayable displayable = new Displayable(meshGroup);

                inst.Representation = displayable;
            }
            catch (Exception e) {
                ConvOAR.Globals.log.ErrorFormat("{0} CreateInstanceFromSharedMaterialMeshes: exception: {1}", _logHeader, e);
            }

            return inst;
        }
    }
}
