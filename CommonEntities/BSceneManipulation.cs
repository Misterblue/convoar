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

using org.herbal3d.cs.CommonEntitiesUtil;

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.cs.os.CommonEntities {

    // Representation of instances and whole scene information
    public class BSceneManipulation : IDisposable {
        private static readonly string _logHeader = "[BSceneManipulation]";
        private readonly BLogger _log;
        private readonly IParameters _params;

        public BSceneManipulation(BLogger pLog, IParameters pParams) {
            _log = pLog;
            _params = pParams;
        }

        public void Dispose() {
        }

        public BScene OptimizeScene(BScene bScene) {
            List<BInstance> newInstances = new List<BInstance>();

            // Create collections of meshes with similar materials
            using (SceneAnalysis analysis = new SceneAnalysis(bScene)) {

                int lastInstanceCount = newInstances.Count;
                if (_params.P<bool>("SeparateInstancedMeshes")) {
                    newInstances.AddRange(SeparateMeshInstances(bScene, analysis));
                }
                // Any shared meshes have been gathered into instances in 'newInstances'
                //     and the meshes have been removed from the shared materials in the analysis.
                int instancesAdded = newInstances.Count - lastInstanceCount;
                _log.DebugFormat("{0} OptimizeScene: BInstances added by mesh instances = {1}", _logHeader, instancesAdded);

                lastInstanceCount = newInstances.Count;
                if (_params.P<bool>("MergeSharedMaterialMeshes")) {
                    newInstances.AddRange(MergeSharedMaterialMeshes(bScene, analysis));
                }
                instancesAdded = newInstances.Count - lastInstanceCount;
                _log.DebugFormat("{0} OptimizeScene: BInstances added by material sharing = {1}", _logHeader, instancesAdded);
            }

            return bScene;
        }

        // Return a new scene whos instances have been created by combining meshes that share
        //    materials.
        public BScene RebuildSceneBasedOnSharedMeshes(BScene bScene) {
            List<BInstance> newInstances = new List<BInstance>();

            // Create collections of meshes with similar materials
            using (SceneAnalysis analysis = new SceneAnalysis(bScene)) {
                newInstances.AddRange(MergeSharedMaterialMeshes(bScene, analysis));
            }

            BScene newScene = new BScene(bScene) {
                instances = newInstances
            };

            return newScene;
        }

        private class InvertedMesh {
            public BScene containingScene;
            public BInstance containingInstance;
            public Displayable containingDisplayable;
            public DisplayableRenderable containingDisplayableRenderable;
            public RenderableMesh renderableMesh;
            // Position and rotation in global, axis aligned coordinates
            public OMV.Vector3 globalPosition;
            public OMV.Quaternion globalRotation;

            public InvertedMesh(BScene pBs, BInstance pInst, Displayable pDisp, DisplayableRenderable pDisprend, RenderableMesh pRm) {
                containingScene = pBs;
                containingInstance = pInst;
                containingDisplayable = pDisp;
                containingDisplayableRenderable = pDisprend;
                renderableMesh = pRm;
                // Compute the global position of the Displayable
                globalPosition = containingDisplayable.offsetPosition * containingInstance.Rotation + containingInstance.Position;
                globalRotation = containingDisplayable.offsetRotation * containingInstance.Rotation;
            }
            // Add another level of parenting
            public InvertedMesh AddDisplayableLevel(InvertedMesh imesh, Displayable pDisp, DisplayableRenderable pDispRend) {
                InvertedMesh newIMesh = new InvertedMesh(imesh.containingScene, imesh.containingInstance,
                        imesh.containingDisplayable, imesh.containingDisplayableRenderable, imesh.renderableMesh);
                globalPosition += pDisp.offsetPosition;
                globalRotation *= pDisp.offsetRotation;
                newIMesh.containingDisplayable = pDisp;
                newIMesh.containingDisplayableRenderable = pDispRend;
                return newIMesh;
            }
        }

        private class SceneAnalysis : IDisposable {
            // meshes organized by the material they use
            public Dictionary<BHash, List<InvertedMesh>> meshByMaterial = new Dictionary<BHash, List<InvertedMesh>>();
            // meshes organized by the mesh they share (for finding instances of identical mesh
            public Dictionary<BHash, List<InvertedMesh>> sharedMeshes = new Dictionary<BHash, List<InvertedMesh>>();
            public BScene scene;
            private readonly BLogger _log;

            public SceneAnalysis(BLogger pLog) {
                _log = pLog;
            }
            public SceneAnalysis(BScene bScene) {
                this.scene = bScene;
                BuildAnalysis(bScene);
            }
            public void Dispose() {
                foreach (BHash bhash in meshByMaterial.Keys) {
                    meshByMaterial[bhash].Clear();
                }
                meshByMaterial.Clear();
                foreach (BHash bhash in sharedMeshes.Keys) {
                    sharedMeshes[bhash].Clear();
                }
                sharedMeshes.Clear();
            }

            public void BuildAnalysis(BScene bScene) {
                foreach (BInstance inst in bScene.instances) {
                    MapMaterialsAndMeshes(bScene, inst, inst.Representation);
                }
            }

            // Mesh is used in some optimization (it will go out to the renderer) so remove it
            // from the enclosing scene and the optimization structures.
            public void MeshUsed(List<InvertedMesh> iMeshList) {
                RemoveMeshesFromMaterials(iMeshList);
                RemoveMeshesFromShared(iMeshList);
                RemoveMeshesFromScene(iMeshList);
            }

            // Given a list of meshes, remove them from the collection of meshes arranged by used materials.
            // This is used by other optimizations to remove meshes that have been optimized elsewhere.
            public void RemoveMeshesFromMaterials(List<InvertedMesh> meshList) {
                // Remove these meshes from the ones that are shared by material
                foreach (InvertedMesh imesh in meshList) {
                    BHash materialHash = imesh.renderableMesh.material.GetBHash();
                    if (!meshByMaterial[materialHash].Remove(imesh)) {
                        _log.DebugFormat("{0} RemoveMeshesFromMaterials: couldn't remove imesh. matHash={1}",
                                _logHeader, materialHash);
                    }
                }
            }

            public void RemoveMeshesFromShared(List<InvertedMesh> meshList) {
                // Remove these meshes from the ones that are shared by material
                foreach (InvertedMesh imesh in meshList) {
                    BHash shapeHash = imesh.renderableMesh.mesh.GetBHash();
                    if (!sharedMeshes[shapeHash].Remove(imesh)) {
                        _log.DebugFormat("{0} RemoveMeshesFromShared: couldn't remove imesh. shapeHash={1}",
                                _logHeader, shapeHash);
                    }
                }
            }

            public void RemoveMeshesFromScene(List<InvertedMesh> meshList) {
                // Remove these meshes from the ones that are shared by material
                foreach (InvertedMesh imesh in meshList) {
                    if (imesh.containingDisplayableRenderable is RenderableMeshGroup renderableMeshGroup) {
                        if (!renderableMeshGroup.meshes.Remove(imesh.renderableMesh)) {
                            _log.DebugFormat("{0} RemoveMeshesFromScene: couldn't remove imesh.",
                                    _logHeader);
                            return;
                        }
                        // If Displayable has no more meshes, remove it too
                        if (renderableMeshGroup.meshes.Count == 0) {
                            if (imesh.containingDisplayable.renderable == renderableMeshGroup) {
                                imesh.containingDisplayable.renderable = null;
                            }
                            else {
                                imesh.containingDisplayable.children.Remove(imesh.containingDisplayable);
                            }
                        }
                    }
                }
            }

            // Find all the meshes in passed Displayable and add them to the lists indexed by their material
            //     mesh hashes.
            private void MapMaterialsAndMeshes(BScene pBs, BInstance pInst, Displayable pDisp) {
                if (pDisp.renderable is RenderableMeshGroup rmg) {
                    foreach (RenderableMesh rMesh in rmg.meshes) {
                        InvertedMesh imesh = new InvertedMesh(pBs, pInst, pDisp, rmg, rMesh);

                        BHash meshHash = rMesh.mesh.GetBHash();
                        if (!sharedMeshes.ContainsKey(meshHash)) {
                            sharedMeshes.Add(meshHash, new List<InvertedMesh>());
                        }
                        sharedMeshes[meshHash].Add(imesh);

                        BHash materialHash = rMesh.material.GetBHash();
                        if (!meshByMaterial.ContainsKey(materialHash)) {
                            meshByMaterial.Add(materialHash, new List<InvertedMesh>());
                        }
                        meshByMaterial[materialHash].Add(imesh);
                    }
                }
                foreach (Displayable child in pDisp.children) {
                    MapMaterialsAndMeshes(pBs, pInst, child);
                }
            }
        }

        private List<BInstance> SeparateMeshInstances(BScene bScene, SceneAnalysis analysis) {
            List<BInstance> ret = new List<BInstance>();
            try {
                // If there are lots of instances of the same mesh, it is better to have multiple instances
                //    that point to the same mesh. If a mesh is not shared, consolidating the meshes
                //    into a single instance is best. It's a balance of transferring vertices vs fewer draws.

                // Any meshes that are used more than 'MeshShareThreshold' will be sent out with their
                //    instances rather than being combined.
                // The GLTF output code will not send out duplicate meshes and combining the meshes to
                //    share materials destroys the duplicatable mesh shapes.
                // The duplicated meshes usually share a material so pull them together into meshes
                //    in one instance.
                // Note: the 'SelectMany' is used to flatten the list of lists
                int meshShareThreshold = _params.P<int>("MeshShareThreshold");
                _log.DebugFormat("{0} SeparateMeshes: Separating instanced meshes. threshold={1}",
                            _logHeader, meshShareThreshold);

                /*
                foreach (BHash key in analysis.sharedMeshes.Keys) {     // DEBUG DEBUG
                    _log.DebugFormat("{0} SeparateMeshes: mesh hash {1} . meshes={2}",       // DEBUG DEBUG
                            _logHeader, key, analysis.sharedMeshes[key].Count);     // DEBUG DEBUG
                };      // DEBUG DEBUG
                */

                ret.AddRange(analysis.sharedMeshes.Values.Where(val => val.Count > meshShareThreshold).SelectMany(meshList => {
                    // Creates Instances for the shared messes in this list and also takes the meshes out of 'meshByMaterial'
                    _log.DebugFormat("{0} MergeSharedMaterialMeshes: shared mesh hash: {1}/{2}, cnt={3}",
                            _logHeader, meshList.First().renderableMesh.mesh.GetBHash(),
                            meshList.First().renderableMesh.material.GetBHash(),
                            meshList.Count);
                    // Since mesh will be in this group, remove it from the meshes with shared materials
                    analysis.MeshUsed(meshList);
                    return CreateInstancesForSharedMeshes(meshList);
                }).ToList() );
            }
            catch (Exception e) {
                _log.DebugFormat("{0} SeparateMeshInstances: exception: {1}", _logHeader, e);
            }

            return ret;
        }

        private List<BInstance> MergeSharedMaterialMeshes(BScene bScene, SceneAnalysis analysis) {

            List<BInstance> ret = new List<BInstance>();

            try {
                // 'analysis.meshByMaterial' has all meshes/instances grouped by material used
                // 'analysis.sharedMeshes' has all meshes grouped by the mesh
                _log.DebugFormat("{0} MergeShareMaterialHashes: number of materials = {1}",
                                    _logHeader, analysis.meshByMaterial.Count);

                // Merge the meshes and create an Instance containing the new mesh set
                ret.AddRange(analysis.meshByMaterial.Keys.SelectMany(materialHash => {
                    _log.DebugFormat("{0} MergeShareMaterialHashes: material hash {1} . meshes={2}",
                                _logHeader, materialHash, analysis.meshByMaterial[materialHash].Count);
                    return CreateInstancesFromSharedMaterialMeshes(materialHash, analysis.meshByMaterial[materialHash]);
                }).ToList() );
            }
            catch (Exception e) {
                _log.DebugFormat("{0} MergeShareMaterialHashes: exception: {1}", _logHeader, e);
            }

            return ret;
        }

        // Create one or more Instances from this list of meshes.
        // There might be more than 2^15 vertices so, to keep the indices a ushort, might need
        //    to break of the meshes.
        private List<BInstance> CreateInstancesFromSharedMaterialMeshes(BHash materialHash, List<InvertedMesh> meshes) {
            List<BInstance> ret = new List<BInstance>();

            List<InvertedMesh> partial = new List<InvertedMesh>();
            int totalVertices = 0;
            foreach (InvertedMesh imesh in meshes) {
                if (totalVertices + imesh.renderableMesh.mesh.vertexs.Count > 50000) {
                    // if adding this mesh will push us over the max, create instances and start again
                    ret.Add(CreateOneInstanceFromMeshes(materialHash, partial));
                    partial.Clear();
                    totalVertices = 0;
                }
                totalVertices += imesh.renderableMesh.mesh.vertexs.Count;
                partial.Add(imesh);
            }
            if (partial.Count > 0) {
                ret.Add(CreateOneInstanceFromMeshes(materialHash, partial));
            }

            return ret;
        }

        // Given a list of meshes, combine them into one mesh and return a containing BInstance.
        private BInstance CreateOneInstanceFromMeshes(BHash materialHash, List<InvertedMesh> meshes) {
            // Pick one of the meshes to be the 'root' mesh.
            // Someday may need to find the most center mesh to work from.
            InvertedMesh rootIMesh = meshes.First();

            // The new instance will be at the location of the root mesh with no rotation
            BInstance inst = new BInstance {
                Position = rootIMesh.containingInstance.Position,
                Rotation = OMV.Quaternion.Identity,
                coordAxis = rootIMesh.containingInstance.coordAxis
            };

            try {
                // The mesh we're going to build
                MeshInfo meshInfo = new MeshInfo();
                foreach (InvertedMesh imesh in meshes) {
                    int indicesBase = meshInfo.vertexs.Count;
                    // Go through the mesh, map all vertices to global coordinates then convert relative to root
                    meshInfo.vertexs.AddRange(imesh.renderableMesh.mesh.vertexs.Select(vert => {
                        OMVR.Vertex newVert = new OMVR.Vertex();
                        OMV.Vector3 worldPos = vert.Position;
                        worldPos = worldPos * imesh.containingDisplayable.offsetRotation
                                + imesh.containingDisplayable.offsetPosition;
                        worldPos = worldPos * imesh.containingInstance.Rotation
                                + imesh.containingInstance.Position;
                        // Make new vert relative to the BInstance it's being added to
                        newVert.Position = worldPos - inst.Position;
                        newVert.Normal = vert.Normal
                            * imesh.containingDisplayable.offsetRotation
                            * imesh.containingInstance.Rotation;
                        newVert.TexCoord = vert.TexCoord;
                        return newVert;
                    }));
                    meshInfo.indices.AddRange(imesh.renderableMesh.mesh.indices.Select(ind => ind + indicesBase));
                }

                RenderableMesh newMesh = new RenderableMesh {
                    num = 0,
                    material = rootIMesh.renderableMesh.material,   // The material we share
                    mesh = meshInfo
                };

                RenderableMeshGroup meshGroup = new RenderableMeshGroup();
                meshGroup.meshes.Add(newMesh);

                Displayable displayable = new Displayable(meshGroup, _params) {
                    name = "combinedMaterialMeshes-" + materialHash.ToString()
                };

                inst.Representation = displayable;
            }
            catch (Exception e) {
                _log.ErrorFormat("{0} CreateInstanceFromSharedMaterialMeshes: exception: {1}", _logHeader, e);
            }

            return inst;
        }

        // Creates Instances for the shared messes in this list and also takes the meshes out of 'meshByMaterial'.
        // Current algorithm: create one instance and add all shared meshes as children.
        // When the instances are created (or copied over), the meshes must be removed from the
        //     'meshByMaterial' structure so they are not combined with other material sharing meshes.
        private List<BInstance> CreateInstancesForSharedMeshes(List<InvertedMesh> meshList) {
            List<BInstance> ret = new List<BInstance>();

            BInstance inst = new BInstance();
            InvertedMesh rootMesh = meshList.First();
            inst.Position = rootMesh.globalPosition;
            inst.Rotation = OMV.Quaternion.Identity;

            // Create Displayable for each identical mesh (so we can keep the pos and rot.
            // Take the root mesh out of the list and use it as the representation
            List<Displayable> repackagedMeshes = PackMeshesIntoDisplayables(meshList, inst.Position,
                imesh => "sharedMesh-" + imesh.renderableMesh.mesh.GetBHash().ToString() + "-" + imesh.renderableMesh.GetBHash().ToString() );
            Displayable rootDisplayable = repackagedMeshes.First();
            repackagedMeshes.RemoveAt(0);
            inst.Representation = rootDisplayable;
            rootDisplayable.children = repackagedMeshes;

            ret.Add(inst);

            return ret;
        }

        delegate string CreateNameFunc(InvertedMesh imesh);
        private List<Displayable> PackMeshesIntoDisplayables(List<InvertedMesh> meshList, OMV.Vector3 gPos, CreateNameFunc createName) {
            return meshList.Select(imesh => {
                /*
                _log.DebugFormat("{0} CreateInstanceForSharedMeshes: hash={1}, instPos={2}, dispPos={3}, numVerts={4}",
                                _logHeader, imesh.renderableMesh.mesh.GetBHash(),
                                imesh.containingInstance.Position,
                                imesh.containingDisplayable.offsetPosition,
                                imesh.renderableMesh.mesh.vertexs.Count);
                */

                RenderableMeshGroup mesh = new RenderableMeshGroup();
                mesh.meshes.Add(imesh.renderableMesh);

                Displayable disp = new Displayable(mesh, _params) {
                    name = createName(imesh),
                    offsetPosition = imesh.globalPosition - gPos,
                    offsetRotation = imesh.globalRotation,
                    renderable = mesh
                };

                return disp;
            }).ToList();

        }

    }
}
