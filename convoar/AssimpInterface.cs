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

using Assimp;

namespace org.herbal3d.convoar {
    public class AssimpInterface : IDisposable {

        private static string _logHeader = "[AssimpInterface]";

        AssimpContext assimpContext;

        public AssimpInterface() {
            try {
                assimpContext = new AssimpContext();
                Assimp.ExportFormatDescription[] exportFormats = assimpContext.GetSupportedExportFormats();
                exportFormats.ToList().ForEach(ef => {
                    ConvOAR.Globals.log.DebugFormat("Assimp: export format {0}, desc={1}, id={2}",
                        ef.FileExtension, ef.Description, ef.FormatId);
                });
            }
            catch (Exception e) {
                ConvOAR.Globals.log.ErrorFormat("{0} Failed load of AssimpContext: {1}", _logHeader, e);
                assimpContext = null;
            }
        }

        // IDisposable.Dispose()
        public void Dispose() {
            if (assimpContext != null) {
                if (!assimpContext.IsDisposed) {
                    assimpContext.Dispose();
                    assimpContext = null;
                }
            }
        }

        // Pass over everything in the scene and convert everything to AssimpNet structures.
        public Assimp.Scene ConvertBSceneToAssimpScene(BScene bScene, IAssetFetcher contextAssets, int imageSizeConstraint) {
            Assimp.Scene aScene = new Assimp.Scene();
            aScene.RootNode = new Node(bScene.name);    // top level node has region name
            aScene.RootNode.Transform = Assimp.Matrix4x4.Identity;

            IAssetFetcher assets = new NullAssetFetcher();

            // The entities in the scene could be a subset of everything in the OAR file so
            //     pass through the instances and fill 'assets' with the meshes, materials, and
            //     images used.
            // CollectPiecesForThisScene(bScene, contextAssets, assets, imageSizeConstraint);

            bScene.instances.ForEach(inst => {
                AddChild(aScene, aScene.RootNode, inst.Representation, inst.Position, inst.Rotation, contextAssets);
            });

            return aScene;
        }
        
        private void AddChild(Assimp.Scene aScene, Node parentNode, Displayable disp, OMV.Vector3 pos, OMV.Quaternion rot, IAssetFetcher assets) {
            ConvOAR.Globals.log.DebugFormat("{0} AddChild: parent={1}, rep={2}, offset={3}, rot={4}",
                        _logHeader, parentNode.Name, disp.name, pos, rot);
            Node newNode = new Node(disp.name, parentNode);
            float[] aT = Utilities.ComposeMatrix4(pos, rot, disp.scale);
            newNode.Transform = new Matrix4x4(
                            aT[00], aT[01], aT[02], aT[03],
                            aT[04], aT[05], aT[06], aT[07],
                            aT[08], aT[09], aT[10], aT[11],
                            aT[12], aT[13], aT[14], aT[15] );
            ConvOAR.Globals.log.DebugFormat("{0} AddChild: trans={1}/{2}/{3}/{4}/{5}/{6}/{7}/{8}/{9}/{10}/{11}/{12}/{13}/{14}/{15}/{16}",
                            _logHeader,
                            aT[00], aT[01], aT[02], aT[03],
                            aT[04], aT[05], aT[06], aT[07],
                            aT[08], aT[09], aT[10], aT[11],
                            aT[12], aT[13], aT[14], aT[15] );

            DisplayableRenderable dr = assets.GetRenderable(disp.renderable.GetBHash(), null);
                if (dr != null) {
                    RenderableMeshGroup rmg = dr as RenderableMeshGroup;
                    if (rmg != null) {
                        rmg.meshes.ForEach(renderableMesh => {
                            ConvOAR.Globals.log.DebugFormat("{0} AddChild: renderableMesh={1}", _logHeader, renderableMesh.num);
                            string meshName = renderableMesh.GetBHash().ToString();
                            // Find this mesh in the scene (or create the mesh/material if needed)
                            int meshIndex = FindOrCreateMesh(aScene, meshName, renderableMesh);
                            newNode.MeshIndices.Add(meshIndex);
                        });
                        
                    }
                }

            // Recursivily add the children to this node
            disp.children.ForEach(child => {
                AddChild(aScene, newNode, child, child.offsetPosition, child.offsetRotation, assets);
            });

            parentNode.Children.Add(newNode);
        }

        // Find this mesh in the scene (or create the mesh/material if needed)
        private int FindOrCreateMesh(Assimp.Scene aScene, string meshName, RenderableMesh renderableMesh) {
            ConvOAR.Globals.log.DebugFormat("{0} FindOrCreateMesh: meshName={1}", _logHeader, meshName);
            int meshIndex = aScene.Meshes.FindIndex(mesh => { return mesh.Name == meshName; });
            if (meshIndex < 0) {
                // The mesh isn't in the scene yet. Create same.
                meshIndex = CreateAssimpMesh(aScene, meshName, renderableMesh);
            }
            return meshIndex;
        }
        
        // Create the mesh and needed materials.
        // Return the index of the mesh after it has been added to the scene.
        // Note the side effect of adding the mesh and materials to the Assimp.Scene
        private int CreateAssimpMesh(Assimp.Scene aScene, string meshName, RenderableMesh rMesh) {
            ConvOAR.Globals.log.DebugFormat("{0} CreateAssimpMesh: meshName={1}", _logHeader, meshName);
            int ret = -1;
            Assimp.Mesh newMesh = new Mesh(meshName, Assimp.PrimitiveType.Triangle);
            newMesh.SetIndices(rMesh.mesh.indices.ToArray(), 3);
            newMesh.Vertices.AddRange(rMesh.mesh.vertexs.Select(vert => { 
                return new Assimp.Vector3D(vert.Position.X, vert.Position.Y, vert.Position.Z); }).ToList()
            );
            newMesh.Normals.AddRange(rMesh.mesh.vertexs.Select(vert => { 
                return new Assimp.Vector3D(vert.Normal.X, vert.Normal.Y, vert.Normal.Z); }).ToList()
            );
            newMesh.TextureCoordinateChannels[0] = new List<Vector3D>();
            newMesh.TextureCoordinateChannels[0].AddRange(rMesh.mesh.vertexs.Select(vert => { 
                return new Assimp.Vector3D(vert.Normal.X, vert.Normal.Y, vert.Normal.Z); }).ToList()
            );

            newMesh.MaterialIndex = FindOrCreateMaterial(aScene, rMesh.material);

            ret = aScene.MeshCount;
            aScene.Meshes.Add(newMesh);

            return ret;
        }

        private int FindOrCreateMaterial(Assimp.Scene aScene, MaterialInfo matInfo) {
            string matName = matInfo.GetBHash().ToString();
            ConvOAR.Globals.log.DebugFormat("{0} FindOrCreateMaterial: matName={1}", _logHeader, matName);
            int matIndex = aScene.Materials.FindIndex(mat => { return mat.Name == matName; });
            if (matIndex < 0) {
                matIndex = CreateAssimpMaterial(aScene, matName, matInfo);
            }

            return matIndex;
        }

        private int CreateAssimpMaterial(Assimp.Scene aScene, string matName, MaterialInfo matInfo) {
            ConvOAR.Globals.log.DebugFormat("{0} CreateAssimpMaterial: matName={1}", _logHeader, matName);
            int ret = -1;

            Assimp.Material newMaterial = new Material();
            newMaterial.Name = matName;
            if (matInfo.faceTexture.RGBA.A != 1.0f) {
                newMaterial.Opacity = matInfo.faceTexture.RGBA.A;
            }
            if (matInfo.shiny != OMV.Shininess.None) {
                newMaterial.Shininess = (float)matInfo.shiny / 256f;
            }
            newMaterial.ColorDiffuse = new Assimp.Color4D(matInfo.RGBA.R, matInfo.RGBA.G, matInfo.RGBA.R, matInfo.RGBA.A);

            /*
            if (matInfo.image != null) {
                newMaterial.TextureDiffuse = FindOrCreateTexture(aScene, matInfo);
                TextureSlot textureSlot = FindOrCreateTexture(aScene, matInfo);
                newMaterial.AddMaterialTexture(ref textureSlot);
            }
            */

            ret = aScene.MaterialCount;
            aScene.Materials.Add(newMaterial);

            return ret;
        }

            /*
        private TextureSlot FindOrCreateTexture(Assimp.Scene aScene, MaterialInfo matInfo) {
            TextureSlot newTexture = new TextureSlot(
                            filePath,
                            TextureType.Diffuse,
                            texIndex
                            TextureMapping.Plane,
                            uvIndex,
                            blendFactor,
                            TextureOperation.Add,
                            TextureWrapMode.Wrap,
                            TextureWrapMode.Wrap,
                            flags);
            return newTexture;
        }
                            */

        // Scan all the instances in the passed scene and fill 'dstAssets' with the referenced
        //    meshes, images, and materials in 'srcAssets'.
        private void CollectPiecesForThisScene(BScene bScene, IAssetFetcher srcAssets, IAssetFetcher dstAssets, int imageSizeConstraint) {
            // Function to recursively walk the node tree
            void CollectAllChildren (Displayable child, int sizeConstraint) {
                DisplayableRenderable disp = srcAssets.GetRenderable(child.renderable.GetBHash(), null);
                if (disp != null) {
                    RenderableMeshGroup rmg = disp as RenderableMeshGroup;
                    if (rmg != null) {
                        rmg.meshes.ForEach(renderableMesh => {
                            dstAssets.AddUniqueMeshInfo(renderableMesh.mesh);
                            dstAssets.AddUniqueMatInfo(renderableMesh.material);
                            if (renderableMesh.material.textureID != null) {
                                ImageInfo imgInfo = srcAssets.GetImageInfo((OMV.UUID)renderableMesh.material.textureID, sizeConstraint);
                                if (imgInfo != null) {
                                    dstAssets.AddUniqueImageInfo(imgInfo);
                                }
                            }
                        });
                        
                    }
                }
                // Follow down the tree of children
                child.children.ForEach(subchild => {
                    CollectAllChildren(subchild, imageSizeConstraint);
                });
            }

            // Pass through all instances and add the used meshes, materials, and images
            bScene.instances.ForEach(inst => {
                CollectAllChildren(inst.Representation, imageSizeConstraint);
            });
        }

        // Export the passed scene in the specified format in the specified place
        public void Export(Assimp.Scene aScene, string path, string format) {
            assimpContext.ExportFile(aScene, path, format);
        }

        // Export the passed scene in the specified format in the specified place
        public void Export(Assimp.Scene aScene, string path, string format, Assimp.PostProcessSteps post) {
            assimpContext.ExportFile(aScene, path, format, post);
        }

    }
}
