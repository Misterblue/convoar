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
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;

using log4net;

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.convoar {

    // The base class for all of the different types.
    public abstract class GltfClass {
        public Gltf gltfRoot;
        public string ID;
        public abstract void ToJSON(StreamWriter outt, int level);

        public GltfClass() { }
        public GltfClass(Gltf pRoot, string pID) {
            gltfRoot = pRoot;
            ID = pID;
        }
    }

    // Base class of a list of a type.
    public abstract class GltfListClass<T> : List<T> {
        public Gltf gltfRoot;
        public string ID;
        public abstract void ToJSON(StreamWriter outt, int level);
        public abstract void ToJSONIDArray(StreamWriter outt, int level);
        public GltfListClass(Gltf pRoot) {
            gltfRoot = pRoot;
        }

        public void ToJSONArrayOfIDs(StreamWriter outt, int level) {
            outt.Write("[ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(JSONHelpers.Indent(level) + "\"" + gl.ID +"\"");
                first = false;
            });
            outt.Write("]");
        }

        public void ToJSONMapOfJSON(StreamWriter outt, int level) {
            outt.Write("{ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                GltfClass gl = xx as GltfClass;
                outt.Write(JSONHelpers.Indent(level) + "\"" + gl.ID + "\": ");
                gl.ToJSON(outt, level+1);
                first = false;
            });
            outt.Write(" }");
        }
    }

    public class GltfVector16 : GltfClass {
        public float[] vector = new float[16];

        public GltfVector16() : base() {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" [ ");
            for (int ii = 0; ii < vector.Length; ii++) {
                if (ii > 0) outt.Write(",");
                outt.Write(vector[ii].ToString());
            }
            outt.Write(" ] ");
        }
    }

    // =============================================================
    public class Gltf : GltfClass {
        private static string _logHeader = "Gltf";

        public GltfAttributes extensionsUsed;   // list of extensions used herein

        public string defaultSceneID;   // ID of default scene

        public GltfAsset asset;
        public GltfScenes scenes;       // scenes that make up this package
        public GltfNodes nodes;         // nodes in the scenes
        public GltfMeshes meshes;       // the meshes for the nodes
        public GltfMaterials materials; // materials that make up the meshes
        public GltfAccessors accessors; // access to the mesh bin data
        public GltfBufferViews bufferViews; //
        public GltfBuffers buffers; //
        public GltfTechniques techniques;
        public GltfPrograms programs;
        public GltfShaders shaders;
        public GltfTextures textures;
        public GltfImages images;
        public GltfSamplers samplers;

        public GltfSampler defaultSampler;

        public Gltf() : base() {
            gltfRoot = this;

            extensionsUsed = new GltfAttributes();
            asset = new GltfAsset(this);
            scenes = new GltfScenes(this);
            nodes = new GltfNodes(this);
            meshes = new GltfMeshes(this);
            materials = new GltfMaterials(this);
            accessors = new GltfAccessors(this);
            bufferViews = new GltfBufferViews(this);
            buffers = new GltfBuffers(this);
            techniques = new GltfTechniques(this);
            programs = new GltfPrograms(this);
            shaders = new GltfShaders(this);
            textures = new GltfTextures(this);
            images = new GltfImages(this);
            samplers = new GltfSamplers(this);

            // 20170201: ThreeJS defaults to GL_CLAMP but GLTF should default to GL_REPEAT/WRAP
            // Create a sampler for all the textures that forces WRAPing
            defaultSampler = new GltfSampler(gltfRoot, "simpleTextureRepeat");
            defaultSampler.values.Add("magFilter", WebGLConstants.LINEAR);
            defaultSampler.values.Add("minFilter", WebGLConstants.LINEAR_MIPMAP_LINEAR);
            defaultSampler.values.Add("wrapS", WebGLConstants.REPEAT);
            defaultSampler.values.Add("wrapT", WebGLConstants.REPEAT);
        }

        // Say this scene is using the extension.
        public void UsingExtension(string extName) {
            if (!extensionsUsed.ContainsKey(extName)) {
                extensionsUsed.Add(extName, null);
            }
        }

        // Add all the objects from a scene into this empty Gltf instance.
        public void LoadScene(BScene scene, IAssetFetcher assetFetcher) {
            // Load the pointed to items first and then the complex items

            GltfScene gltfScene = new GltfScene(this, scene.name);

            // Load Images
            assetFetcher.Images.ForEach(delegate(ImageInfo pImageInfo) {
                GltfImage newImage = new GltfImage(this, pImageInfo);
                GltfTexture newTexture = new GltfTexture(this, pImageInfo, newImage);
            });
            // Load Materials
            assetFetcher.Materials.ForEach(delegate (MaterialInfo pMatInfo) {
                GltfMaterial newMaterial = new GltfMaterial(this, pMatInfo);
            });
            // Load Meshes
            assetFetcher.Meshes.ForEach(delegate (MeshInfo pMeshInfo) {
                GltfMesh newMesh = new GltfMesh(this, pMeshInfo);
            });
            // Load Nodes
            scene.instances.ForEach(delegate (BInstance pInstance) {
                GltfNode newNode = new GltfNode(this, gltfScene, pInstance);
            });
        }

        // Function called below to create a URI from an asset ID.
        // 'type' may be one of 'image', 'mesh', ?
        // public delegate string MakeAssetURI(string type, OMV.UUID uuid);
        public delegate void MakeAssetURI(string type, string info, out string filename, out string uri);
        public const string MakeAssetURITypeImage = "image";    // image of type PNG
        public const string MakeAssetURITypeMesh = "mesh";
        public const string MakeAssetURITypeBuff = "buff";      // binary buffer

        // After all the nodes have been added to a Gltf class, build all the
        //    dependent structures
        public void BuildAccessorsAndBuffers(BasilPersist persist, GlobalContext context) {
            
            // Scan all the created meshes and create the Buffers, BufferViews, and Accessors
            BuildBuffers(persist, context.parms.VerticesMaxForBuffer);
        }

        // Meshes with MeshInfo's have been added to the scene. Pass over all
        //   the meshes and create the Buffers, BufferViews, and Accessors.
        // Called before calling ToJSON().
        public void BuildBuffers(BasilPersist persist, int maxVerticesPerBuffer) {
            // Partition the meshes into smaller groups based on number of vertices going out
            List<GltfMesh> partial = new List<GltfMesh>();
            int totalVertices = 0;
            meshes.ForEach(mesh => {
                // If adding this mesh will push the total vertices in this buffer over the max, flush this buffer.
                if ((totalVertices + mesh.meshInfo.vertexs.Count) > maxVerticesPerBuffer) {
                    BuildBufferForSomeMeshes(partial, persist);
                    partial.Clear();
                    totalVertices = 0;
                }
                totalVertices += mesh.meshInfo.vertexs.Count;
                partial.Add(mesh);
            });
            if (partial.Count > 0) {
                BuildBufferForSomeMeshes(partial, persist);
            }
        }

        // For a collection of meshes, create the buffers and accessors.
        public void BuildBufferForSomeMeshes(List<GltfMesh> someMeshes, BasilPersist persist) {
            // Pass over all the vertices in all the meshes and collect common vertices into 'vertexCollection'
            int numMeshes = 0;
            int numVerts = 0;
            Dictionary<OMVR.Vertex, ushort> vertexIndex = new Dictionary<OMVR.Vertex, ushort>();
            List<OMVR.Vertex> vertexCollection = new List<OMVR.Vertex>();
            ushort vertInd = 0;
            someMeshes.ForEach(mesh => {
                numMeshes++;
                MeshInfo meshInfo = mesh.meshInfo;
                meshInfo.vertexs.ForEach(vert => {
                    numVerts++;
                    if (!vertexIndex.ContainsKey(vert)) {
                        vertexIndex.Add(vert, vertInd);
                        vertexCollection.Add(vert);
                        vertInd++;
                    }
                });
            });
            ConvOAR.Globals.log.DebugFormat("{0} BuildBuffers: total meshes = {1}", _logHeader, numMeshes);
            ConvOAR.Globals.log.DebugFormat("{0} BuildBuffers: total vertices = {1}", _logHeader, numVerts);
            ConvOAR.Globals.log.DebugFormat("{0} BuildBuffers: total unique vertices = {1}", _logHeader, vertInd);


            // Remap all the indices to the new, compacted vertex collection.
            //     mesh.underlyingMesh.face to mesh.newIndices
            // TODO: if num verts > ushort.maxValue, create array if uint's
            int numIndices = 0;
            someMeshes.ForEach(mesh => {
                MeshInfo meshInfo = mesh.meshInfo;
                ushort[] newIndices = new ushort[meshInfo.indices.Count];
                for (int ii = 0; ii < meshInfo.indices.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[(int)meshInfo.indices[ii]];
                    newIndices[ii] = vertexIndex[aVert];
                }
                mesh.newIndices = newIndices;
                numIndices += newIndices.Length;
            });

            // The vertices have been unique'ified into 'vertexCollection' and each mesh has
            //    updated indices in GltfMesh.newIndices.

            int sizeofVertices = vertexCollection.Count * sizeof(float) * 8;
            int sizeofOneIndices = sizeof(ushort);
            int sizeofIndices = numIndices * sizeofOneIndices;
            // The offsets must be multiples of a good access unit so pad to a good alignment
            int padUnit = sizeof(float) * 8;
            int paddedSizeofIndices = sizeofIndices;
            // There might be padding for each mesh. An over estimate but hopefully not too bad.
            paddedSizeofIndices += someMeshes.Count * sizeof(float);
            paddedSizeofIndices += (padUnit - (paddedSizeofIndices % padUnit)) % padUnit;

            // A key added to the buffer, vertices, and indices names to uniquify them
            byte[] binBuffRaw = new byte[paddedSizeofIndices + sizeofVertices];
            string buffNum =  String.Format("{0:000}", buffers.Count + 1);
            string buffName = "buffer" + buffNum;
            string buffFilename = persist.CreateFilename(Gltf.MakeAssetURITypeBuff, buffName);
            string buffURI = persist.CreateURI(Gltf.MakeAssetURITypeBuff, buffName);
            // ConvOAR.Globals.log.DebugFormat("{0} BuildBuffers: make buffer: name={1}, filename={2}, uri={3}", LogHeader, buffName, buffFilename, buffURI);
            GltfBuffer binBuff = new GltfBuffer(gltfRoot, buffName, "arraybuffer", buffFilename, buffURI);
            binBuff.bufferBytes = binBuffRaw;

            GltfBufferView binIndicesView = new GltfBufferView(gltfRoot, "bufferViewIndices" + buffNum);
            binIndicesView.buffer = binBuff;
            binIndicesView.byteOffset = 0;
            binIndicesView.byteLength = paddedSizeofIndices;
            binIndicesView.target = WebGLConstants.ELEMENT_ARRAY_BUFFER;

            GltfBufferView binVerticesView = new GltfBufferView(gltfRoot, "bufferViewVertices" + buffNum);
            binVerticesView.buffer = binBuff;
            binVerticesView.byteOffset = paddedSizeofIndices;
            binVerticesView.byteLength = sizeofVertices;
            binVerticesView.target = WebGLConstants.ARRAY_BUFFER;

            // Copy the vertices into the output binary buffer 
            // Buffer.BlockCopy only moves primitives. Copy the vertices into a float array.
            // This also separates the verts from normals from texCoord since the Babylon
            //     Gltf reader doesn't handle stride.
            float[] floatVertexRemapped = new float[vertexCollection.Count * sizeof(float) * 8];
            int vertexBase = 0;
            int normalBase = vertexCollection.Count * 3;
            int texCoordBase = normalBase + vertexCollection.Count * 3;
            int jj = 0; int kk = 0;
            vertexCollection.ForEach(vert => {
                floatVertexRemapped[vertexBase + 0 + jj] = vert.Position.X;
                floatVertexRemapped[vertexBase + 1 + jj] = vert.Position.Y;
                floatVertexRemapped[vertexBase + 2 + jj] = vert.Position.Z;
                floatVertexRemapped[normalBase + 0 + jj] = vert.Normal.X;
                floatVertexRemapped[normalBase + 1 + jj] = vert.Normal.Y;
                floatVertexRemapped[normalBase + 2 + jj] = vert.Normal.Z;
                floatVertexRemapped[texCoordBase + 0 + kk] = vert.TexCoord.X;
                floatVertexRemapped[texCoordBase + 1 + kk] = vert.TexCoord.Y;
                jj += 3;
                kk += 2;
            });
            Buffer.BlockCopy(floatVertexRemapped, 0, binBuffRaw, binVerticesView.byteOffset, binVerticesView.byteLength);
            floatVertexRemapped = null;

            // Gltf requires min and max values for all the mesh vertex collections
            OMV.Vector3 vmin = new OMV.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            OMV.Vector3 vmax = new OMV.Vector3(float.MinValue, float.MinValue, float.MinValue);
            OMV.Vector3 nmin = new OMV.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            OMV.Vector3 nmax = new OMV.Vector3(float.MinValue, float.MinValue, float.MinValue);
            OMV.Vector2 umin = new OMV.Vector2(float.MaxValue, float.MaxValue);
            OMV.Vector2 umax = new OMV.Vector2(float.MinValue, float.MinValue);
            vertexCollection.ForEach(vert => {
                // OMV.Vector3 has a Min and Max function but it does a 'new' which causes lots of GC thrash
                vmin.X = Math.Min(vmin.X, vert.Position.X);
                vmin.Y = Math.Min(vmin.Y, vert.Position.Y);
                vmin.Z = Math.Min(vmin.Z, vert.Position.Z);
                vmax.X = Math.Max(vmax.X, vert.Position.X);
                vmax.Y = Math.Max(vmax.Y, vert.Position.Y);
                vmax.Z = Math.Max(vmax.Z, vert.Position.Z);

                nmin.X = Math.Min(nmin.X, vert.Normal.X);
                nmin.Y = Math.Min(nmin.Y, vert.Normal.Y);
                nmin.Z = Math.Min(nmin.Z, vert.Normal.Z);
                nmax.X = Math.Max(nmax.X, vert.Normal.X);
                nmax.Y = Math.Max(nmax.Y, vert.Normal.Y);
                nmax.Z = Math.Max(nmax.Z, vert.Normal.Z);

                umin.X = Math.Min(umin.X, vert.TexCoord.X);
                umin.Y = Math.Min(umin.Y, vert.TexCoord.Y);
                umax.X = Math.Max(umax.X, vert.TexCoord.X);
                umax.Y = Math.Max(umax.Y, vert.TexCoord.Y);
            });

            // Build one large group of vertices/normals/UVs that the individual mesh
            //     indices will reference. The vertices have been uniquified above.
            GltfAccessor vertexAccessor = new GltfAccessor(gltfRoot, buffName + "_accCVer");
            vertexAccessor.bufferView = binVerticesView;
            vertexAccessor.count = vertexCollection.Count;
            vertexAccessor.byteOffset = 0;
            vertexAccessor.byteStride = sizeof(float) * 3;
            vertexAccessor.componentType = WebGLConstants.FLOAT;
            vertexAccessor.type = "VEC3";
            vertexAccessor.min = new object[3] { vmin.X, vmin.Y, vmin.Z };
            vertexAccessor.max = new object[3] { vmax.X, vmax.Y, vmax.Z };

            GltfAccessor normalsAccessor = new GltfAccessor(gltfRoot, buffName + "_accNor");
            normalsAccessor.bufferView = binVerticesView;
            normalsAccessor.count = vertexCollection.Count;
            normalsAccessor.byteOffset = normalBase * sizeof(float);
            normalsAccessor.byteStride = sizeof(float) * 3;
            normalsAccessor.componentType = WebGLConstants.FLOAT;
            normalsAccessor.type = "VEC3";
            normalsAccessor.min = new object[3] { nmin.X, nmin.Y, nmin.Z };
            normalsAccessor.max = new object[3] { nmax.X, nmax.Y, nmax.Z };

            GltfAccessor UVAccessor = new GltfAccessor(gltfRoot, buffName + "_accUV");
            UVAccessor.bufferView = binVerticesView;
            UVAccessor.count = vertexCollection.Count;
            UVAccessor.byteOffset = texCoordBase * sizeof(float);
            UVAccessor.byteStride = sizeof(float) * 2;
            UVAccessor.componentType = WebGLConstants.FLOAT;
            UVAccessor.type = "VEC2";
            UVAccessor.min = new object[2] { umin.X, umin.Y };
            UVAccessor.max = new object[2] { umax.X, umax.Y };

            // For each mesh, copy the indices into the binary output buffer and create the accessors
            //    that point from the mesh into the binary info.
            int indicesOffset = binIndicesView.byteOffset;
            someMeshes.ForEach(mesh => {
                int meshIndicesSize = mesh.newIndices.Length * sizeofOneIndices;
                Buffer.BlockCopy(mesh.newIndices, 0, binBuffRaw, indicesOffset, meshIndicesSize);

                GltfAccessor indicesAccessor = new GltfAccessor(gltfRoot, mesh.ID + "_accInd");
                indicesAccessor.bufferView = binIndicesView;
                indicesAccessor.count = mesh.newIndices.Length;
                indicesAccessor.byteOffset = indicesOffset;
                indicesAccessor.byteStride = sizeofOneIndices;
                indicesAccessor.componentType = WebGLConstants.UNSIGNED_SHORT;
                indicesAccessor.type = "SCALAR";
                ushort imin = UInt16.MaxValue; ushort imax = 0;
                for (int ii = 0; ii < mesh.newIndices.Length; ii++) {
                    imin = Math.Min(imin, mesh.newIndices[ii]);
                    imax = Math.Max(imax, mesh.newIndices[ii]);
                }
                indicesAccessor.min = new object[1] { imin };
                indicesAccessor.max = new object[1] { imax };

                // ConvOAR.Globals.log.DebugFormat("{0} indices: meshIndSize={1}, cnt={2}, offset={3}", LogHeader,
                //                 meshIndicesSize, indicesAccessor.count, indicesOffset);

                indicesOffset += meshIndicesSize;
                // Align the indices to float boundries
                indicesOffset += (sizeof(float) - (indicesOffset % sizeof(float)) % sizeof(float));

                mesh.onePrimitive.indices = indicesAccessor;
                mesh.onePrimitive.position = vertexAccessor;
                mesh.onePrimitive.normals = normalsAccessor;
                mesh.onePrimitive.texcoord = UVAccessor;
            });
        }

        public void ToJSON(StreamWriter outt) {
            this.ToJSON(outt, 0);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" {\n");

            if (extensionsUsed.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"extensionsUsed\": ");
                // the extensions are listed here as an array of names
                extensionsUsed.ToJSONIDArray(outt, level+1);
                outt.Write(",\n");
            }

            if (!String.IsNullOrEmpty(defaultSceneID)) {
                outt.Write("\"scene\": \"" + defaultSceneID + "\"");
                outt.Write(",\n");
            }

            outt.Write(JSONHelpers.Indent(level) + "\"asset\": ");
            asset.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(JSONHelpers.Indent(level) + "\"scenes\": ");
            scenes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(JSONHelpers.Indent(level) + "\"nodes\": ");
            nodes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(JSONHelpers.Indent(level) + "\"meshes\": ");
            meshes.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(JSONHelpers.Indent(level) + "\"accessors\": ");
            accessors.ToJSON(outt, level+1);
            outt.Write(",\n");

            outt.Write(JSONHelpers.Indent(level) + "\"bufferViews\": ");
            bufferViews.ToJSON(outt, level+1);
            outt.Write(",\n");

            if (materials.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"materials\": ");
                materials.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (techniques.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"techniques\": ");
                techniques.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (textures.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"textures\": ");
                textures.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (images.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"images\": ");
                images.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (samplers.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"samplers\": ");
                samplers.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (programs.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"programs\": ");
                programs.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            if (shaders.Count > 0) {
                outt.Write(JSONHelpers.Indent(level) + "\"shaders\": ");
                shaders.ToJSON(outt, level+1);
                outt.Write(",\n");
            }

            // there will always be a buffer and there doesn't need to be a comma after
            outt.Write(JSONHelpers.Indent(level) + "\"buffers\": ");
            buffers.ToJSON(outt, level+1);
            outt.Write("\n");

            outt.Write(" }\n");
        }

        // Write the binary files into the specified target directory
        public void WriteBinaryFiles(string targetDir) {
            buffers.ForEach(buff => {
                string outFilename = buff.filename;
                // ConvOAR.Globals.log.DebugFormat("{0} WriteBinaryFiles: filename={1}", LogHeader, outFilename);
                File.WriteAllBytes(outFilename, buff.bufferBytes);
            });
        }
    }


    // =============================================================
    // A simple collection to keep name/value strings
    // The value is an Object so it can hold strings, numbers, or arrays and have the
    //     values serialized properly in the output JSON.
    public class GltfAttributes : Dictionary<string, Object> {

        // Output a JSON map of the key/value pairs.
        // The value Objects are inspected and output properly as JSON strings, arrays, or numbers.
        // Note: to add an array, do: GltfAttribute.Add(key, new Object[] { 1, 2, 3, 4 } );
        public void ToJSON(StreamWriter outt, int level) {
            outt.Write(" {\n");
            bool first = true;
            foreach (KeyValuePair<string, Object> kvp in this) {
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, kvp.Key, kvp.Value);
            }
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }

        // Output an array of the keys. 
        public void ToJSONIDArray(StreamWriter outt, int level) {
            outt.Write("[ ");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            foreach (string key in this.Keys) {
                if (!first) {
                    outt.Write(",\n");
                }
                outt.Write(JSONHelpers.Indent(level) + "\"" + key +"\"");
                first = false;
            }
            outt.Write(" ]");
        }
    }

    // =============================================================
    public class GltfAsset : GltfClass {
        public string generator = "BasilConversion";
        public bool premulitpliedAlpha = false;
        public string version = "1.1";
        public GltfAttributes profile;

        public GltfAsset(Gltf pRoot) : base(pRoot, "") {
            profile = new GltfAttributes();
            profile.Add("api", "WebGL");
            profile.Add("version", "1.0");
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "generator", generator);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "premultipliedAlpha", premulitpliedAlpha);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "version", version);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"profile\": ");
            profile.ToJSON(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfScenes : GltfListClass<GltfScene> {
        public GltfScenes(Gltf pRoot) : base(pRoot) {
        }
        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfScene : GltfClass {
        public GltfNodes nodes;      // IDs of top level nodes in the scene
        public string name;
        public string extensions;
        public string extras;

        public GltfScene(Gltf pRoot, string pID) : base(pRoot, pID) {
            nodes = new GltfNodes(gltfRoot);
            name = pID;
            gltfRoot.scenes.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "name", name);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfNodes : GltfListClass<GltfNode> {
        public GltfNodes(Gltf pRoot) : base(pRoot) {
        }
            
        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            outt.Write(JSONHelpers.Indent(level) + "\"nodes\": ");
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfNode : GltfClass {
        public string camera;       // non-empty if a camera definition
        public GltfNodes children;
        public string[] skeleton;   // IDs of skeletons
        public string skin;
        public string jointName;
        public GltfMeshes meshes;
        // has either 'matrix' or 'rotation/scale/translation'
        public OMV.Matrix4 matrix;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Vector3 translation;
        public string name;
        public string extensions;   // more JSON describing the extensions used
        public string extras;       // more JSON with additional, beyond-the-standard values

        // Add a node that is not top level in a scene
        public GltfNode(Gltf pRoot, string pID) : base(pRoot, pID) {
            NodeInit(pRoot, null);
        }

        // Add a node that is top level in a scene
        public GltfNode(Gltf pRoot, GltfScene containingScene, BInstance pInstance) : base(pRoot, pInstance.handle.ToString() + "_inst") {
            NodeInit(pRoot, containingScene);
            InitFromDisplayable(pInstance.Representation);
            translation = pInstance.Position;
            rotation = pInstance.Rotation;
        }

        public GltfNode(Gltf pRoot, GltfScene containingScene, Displayable pDisplayable) : base(pRoot, pDisplayable.baseUUID.ToString() + "_disp") {
            NodeInit(pRoot, containingScene);
            InitFromDisplayable(pDisplayable);
        }

        private void NodeInit(Gltf pRoot, GltfScene containingScene) {
            meshes = new GltfMeshes(gltfRoot);
            children = new GltfNodes(gltfRoot);
            matrix = OMV.Matrix4.Zero;
            rotation = new OMV.Quaternion();
            scale = new OMV.Vector3(1, 1, 1);
            translation = new OMV.Vector3(0, 0, 0);

            gltfRoot.nodes.Add(this);
            if (containingScene != null)
                containingScene.nodes.Add(this);
        }

        private void InitFromDisplayable(Displayable pDisplayable) {
            name = pDisplayable.name;
            translation = pDisplayable.offsetPosition;
            rotation = pDisplayable.offsetRotation;
            // only know how to handle a displayable of meshes
            RenderableMeshGroup meshGroup = pDisplayable.renderable as RenderableMeshGroup;
            if (meshGroup != null) {
                this.meshes.AddRange(meshGroup.meshes.Select(renderableMesh => {
                    OMV.UUID meshUUID = ((EntityHandleUUID)renderableMesh.mesh).GetUUID();
                    GltfMesh thisMesh = null;
                    if (!gltfRoot.meshes.GetByUUID(meshUUID, out thisMesh)) {
                        ConvOAR.Globals.log.ErrorFormat("GltfClasses.GltfNode: could not find mesh. id={0}", meshUUID);
                    }
                    return thisMesh;
                }));
            }
            this.children.AddRange(pDisplayable.children.Select(child => {
                return new GltfNode(gltfRoot, null, child);
            }));
        }

        public override void ToJSON(StreamWriter outt, int level) {

            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "name", name);
            if (matrix != OMV.Matrix4.Zero) {
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "matrix", matrix);
            }
            else {
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "translation", translation);
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "scale", scale);
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "rotation", OMV.Quaternion.Normalize(rotation));
            }
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"children\": ");
            children.ToJSONArrayOfIDs(outt, level+1);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"meshes\": ");
            meshes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfMeshes : GltfListClass<GltfMesh> {
        public GltfMeshes(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfMesh theMesh) {
            foreach (GltfMesh mesh in this) {
                if (mesh.underlyingUUID != null && mesh.underlyingUUID == aUUID) {
                    theMesh = mesh;
                    return true;
                }
            }
            theMesh = null;
            return false;
        }
    }

    public class GltfMesh : GltfClass {
        public string name;
        public OMV.UUID underlyingUUID;
        public GltfPrimitives primitives;
        public GltfPrimitive onePrimitive;  // a mesh has one primitive
        public GltfAttributes attributes;
        public MeshInfo meshInfo;
        public Displayable underlyingDisplayable;
        public ushort[] newIndices; // remapped indices posinting to global vertex list
        public GltfMesh(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.meshes.Add(this);
            primitives = new GltfPrimitives(gltfRoot);
            onePrimitive = new GltfPrimitive(gltfRoot);
            primitives.Add(onePrimitive);
        }

        public GltfMesh(Gltf pRoot, MeshInfo pMeshInfo) : base(pRoot, pMeshInfo.handle.ToString() + "_mesh") {
            meshInfo = pMeshInfo;
            underlyingUUID = ((EntityHandleUUID)pMeshInfo.handle).GetUUID();

            gltfRoot.meshes.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "name", name);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"primitives\": ");
            primitives.ToJSONArray(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfPrimitives : GltfListClass<GltfPrimitive> {
        public GltfPrimitives(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        // primitives don't have names and are output as an array
        public void ToJSONArray(StreamWriter outt, int level) {
            outt.Write("[");
            if (this.Count != 0)
                outt.Write("\n");
            bool first = true;
            this.ForEach(xx => {
                if (!first) {
                    outt.Write(",\n");
                }
                xx.ToJSON(outt, level+1);
                first = false;
            });
            outt.Write("]");
        }
    }

    public class GltfPrimitive : GltfClass {
        public int mode;
        public GltfAccessor indices;
        public GltfAccessor normals;
        public GltfAccessor position;
        public GltfAccessor texcoord;
        public GltfMaterial material;
        public GltfPrimitive(Gltf pRoot) : base(pRoot, "primitive") {
            mode = 4;
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{ ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "mode", mode);

            if (indices != null) {
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "indices", indices.ID);
            }
            if (material != null) {
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "material", material.ID);
            }
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            bool first2 = true;
            outt.Write(JSONHelpers.Indent(level) + "\"attributes\": {\n");
            if (normals != null) {
                JSONHelpers.WriteJSONValueLine(outt, level+1, ref first2, "NORMAL", normals.ID);
            }
            if (position != null) {
                JSONHelpers.WriteJSONValueLine(outt, level+1, ref first2, "POSITION", position.ID);
            }
            if (texcoord != null) {
                JSONHelpers.WriteJSONValueLine(outt, level+1, ref first2, "TEXCOORD_0", texcoord.ID);
            }
            outt.Write("\n" + JSONHelpers.Indent(level) + "}");
            outt.Write("\n" + JSONHelpers.Indent(level) + " }");
        }
    }

    // =============================================================
    public class GltfMaterials : GltfListClass<GltfMaterial> {
        public GltfMaterials(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        /*
        // Find the material in this collection that has the hash from the texture entry
        public bool GetHash(int hash, out GltfMaterial foundMaterial) {
            foreach (GltfMaterial mat in this) {
                if (mat.hash == hash) {
                    foundMaterial = mat;
                    return true;
                }
            }
            foundMaterial = null;
            return false;
        }
        */
    }

    public class GltfMaterial : GltfClass {
        public string name;
        public GltfAttributes values;
        public GltfExtensions extensions;
        public GltfMaterial(Gltf pRoot, string pID) : base(pRoot, pID) {
            values = new GltfAttributes();
            extensions = new GltfExtensions(pRoot);
            gltfRoot.materials.Add(this);
        }

        public GltfMaterial(Gltf pRoot, MaterialInfo matInfo) : base(pRoot, matInfo.handle.ToString() + "_mat") {
            values = new GltfAttributes();
            extensions = new GltfExtensions(pRoot);
            gltfRoot.materials.Add(this);

            GltfExtension ext = new GltfExtension(gltfRoot, "KHR_materials_common");
            ext.technique = "BLINN";  // 'LAMBERT' or 'BLINN' or 'PHONG'

            OMV.Color4 surfaceColor = matInfo.RGBA;
            OMV.Color4 aColor = OMV.Color4.Black;

            ext.values.Add(GltfExtension.valDiffuse, surfaceColor);
            // ext.values.Add(GltfExtension.valDoubleSided, true);
            // ext.values.Add(GltfExtension.valEmission, aColor);
            // ext.values.Add(GltfExtension.valSpecular, aColor); // not a value in LAMBERT
            if (matInfo.shiny != OMV.Shininess.None) {
                float shine = (float)matInfo.shiny / 256f;
                ext.values.Add(GltfExtension.valShininess, shine);
            }
            if (surfaceColor.A != 1.0f) {
                ext.values.Add(GltfExtension.valTransparency, surfaceColor.A);
            }

            if (matInfo.textureID != null) {
                GltfTexture theTexture = null;
                pRoot.textures.GetByUUID((OMV.UUID)matInfo.textureID, out theTexture);

                // Remove the defaults created above and add new values for the texture
                ext.values.Remove(GltfExtension.valDiffuse);
                ext.values.Add(GltfExtension.valDiffuse, theTexture.ID);

                ext.values.Remove(GltfExtension.valTransparent);
                if (theTexture.source != null && theTexture.source.imageInfo.hasTransprency) {
                    // the spec says default value is 'false' so only specify if 'true'
                    ext.values.Add(GltfExtension.valTransparent, true);
                }
            }

            extensions.Add(ext);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "name", name);
            if (values.Count > 0) {
                JSONHelpers.WriteJSONLineEnding(outt, ref first);
                outt.Write(JSONHelpers.Indent(level) + "\"values\": ");
                values.ToJSON(outt, level+1);
            }
            if (extensions != null && extensions.Count > 0) {
                JSONHelpers.WriteJSONLineEnding(outt, ref first);
                outt.Write(JSONHelpers.Indent(level) + "\"extensions\": ");
                extensions.ToJSON(outt, level + 1);
            }
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfAccessors : GltfListClass<GltfAccessor> {
        public GltfAccessors(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfAccessor : GltfClass {
        public GltfBufferView bufferView;
        public int count;
        public uint componentType;
        public string type;
        public int byteOffset;
        public int byteStride;
        public object[] min;
        public object[] max;
        public GltfAccessor(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.accessors.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "bufferView", bufferView.ID);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "count", count);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "componentType", componentType);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "type", type);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "byteOffset", byteOffset);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "byteStride", byteStride);
            if (min != null && min.Length > 0)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "min", min);
            if (max != null && max.Length > 0)
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "max", max);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfBuffers : GltfListClass<GltfBuffer> {
        public GltfBuffers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfBuffer : GltfClass {
        public byte[] bufferBytes;
        public string type;
        public string filename;
        public string uri;
        public GltfBuffer(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.buffers.Add(this);
        }

        public GltfBuffer(Gltf pRoot, string pID, string pType, string pFilename, string pUri) : base(pRoot, pID) {
            type = pType;
            filename = pFilename;
            uri = pUri;
            gltfRoot.buffers.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "byteLength", bufferBytes.Length);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "type", "arraybuffer");
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "uri", uri);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfBufferViews : GltfListClass<GltfBufferView> {
        public GltfBufferViews(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfBufferView : GltfClass {
        public GltfBuffer buffer;
        public int byteOffset;
        public int byteLength;
        public uint target;

        public GltfBufferView(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.bufferViews.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "buffer", buffer.ID);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "byteOffset", byteOffset);
            if (byteLength > 0)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "byteLength", byteLength);
            if (target > 0)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "target", target);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfTechniques : GltfListClass<GltfTechnique> {
        public GltfTechniques(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfTechnique : GltfClass {
        public GltfTechnique(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.techniques.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
        /*
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "name", name);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"nodes\": ");
            nodes.ToJSONArrayOfIDs(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
            */
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfPrograms : GltfListClass<GltfProgram> {
        public GltfPrograms(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfProgram : GltfClass {
        public GltfProgram(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.programs.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfShaders : GltfListClass<GltfShader> {
        public GltfShaders(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfShader : GltfClass {
        public GltfShader(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.shaders.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write("{\n");
            outt.Write(" }");
        }
    }

    // =============================================================
    public class GltfTextures : GltfListClass<GltfTexture> {
        public GltfTextures(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfTexture theTexture) {
            foreach (GltfTexture tex in this) {
                if (tex.underlyingUUID != null && tex.underlyingUUID == aUUID) {
                    theTexture = tex;
                    return true;
                }
            }
            theTexture = null;
            return false;
        }
    }

    public class GltfTexture : GltfClass {
        public OMV.UUID underlyingUUID;
        public uint target;
        public uint type;
        public uint format;
        public uint internalFormat;
        public GltfImage source;
        public GltfSampler sampler;

        public GltfTexture(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.textures.Add(this);
        }

        public GltfTexture(Gltf pRoot, ImageInfo pImageInfo, GltfImage pImage) : base(pRoot, pImageInfo.handle.ToString() + "_tex") {
            EntityHandleUUID handleU = pImageInfo.handle as EntityHandleUUID;
            if (handleU != null) {
                underlyingUUID = handleU.GetUUID();
            }
            this.target = WebGLConstants.TEXTURE_2D;
            this.type = WebGLConstants.UNSIGNED_BYTE;
            this.format = WebGLConstants.RGBA;
            this.internalFormat = WebGLConstants.RGBA;
            this.sampler = pRoot.defaultSampler;
            this.source = pImage;

            gltfRoot.textures.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "target", target);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "type", type);
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "format", format);
            if (internalFormat != 0)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "internalFormat", internalFormat);
            if (source != null)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "source", source.ID);
            if (sampler != null)
                JSONHelpers.WriteJSONValueLine(outt, level, ref first, "sampler", sampler.ID);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfImages : GltfListClass<GltfImage> {
        public GltfImages(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfImage theImage) {
            foreach (GltfImage img in this) {
                if (img.underlyingUUID != null && img.underlyingUUID == aUUID) {
                    theImage = img;
                    return true;
                }
            }
            theImage = null;
            return false;
        }
    }

    public class GltfImage : GltfClass {
        public OMV.UUID underlyingUUID;
        public ImageInfo imageInfo;
        public string uri;
        public string filename;
        public GltfImage(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.images.Add(this);
        }

        public GltfImage(Gltf pRoot, ImageInfo pImageInfo) : base(pRoot, pImageInfo.handle.ToString() + "_mat") {
            imageInfo = pImageInfo;
            EntityHandleUUID handleU = pImageInfo.handle as EntityHandleUUID;
            if (handleU != null) {
                underlyingUUID = handleU.GetUUID();
            }
            gltfRoot.images.Add(this);
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "uri", uri);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }

    // =============================================================
    public class GltfSamplers : GltfListClass<GltfSampler> {
        public GltfSamplers(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfSampler : GltfClass {
        public GltfAttributes values;
        public GltfSampler(Gltf pRoot, string pID) : base(pRoot, pID) {
            gltfRoot.samplers.Add(this);
            values = new GltfAttributes();
        }

        public override void ToJSON(StreamWriter outt, int level) {
            values.ToJSON(outt, level + 1);
        }
    }

    // =============================================================
    public class GltfExtensions : GltfListClass<GltfExtension> {
        public GltfExtensions(Gltf pRoot) : base(pRoot) {
        }

        public override void ToJSON(StreamWriter outt, int level) {
            this.ToJSONMapOfJSON(outt, level+1);
        }
        public override void ToJSONIDArray(StreamWriter outt, int level) {
            this.ToJSONArrayOfIDs(outt, level+1);
        }
    }

    public class GltfExtension : GltfClass {
        public string technique;
        public GltfAttributes values;
        // possible entries in 'values'
        public static string valAmbient = "ambient";    // ambient color of surface (OMV.Vector4)
        public static string valDiffuse = "diffuse";    // diffuse color of surface (OMV.Vector4 or textureID)
        public static string valDoubleSided = "doubleSided";    // whether surface has backside ('true' or 'false')
        public static string valEmission = "emission";    // light emitted by surface (OMV.Vector4 or textureID)
        public static string valSpecular = "specular";    // color reflected by surface (OMV.Vector4 or textureID)
        public static string valShininess = "shininess";  // specular reflection from surface (float)
        public static string valTransparency = "transparency";  // transparency of surface (float)
        public static string valTransparent = "transparent";  // whether the surface has transparency ('true' or 'false;)

        public GltfExtension(Gltf pRoot, string pID) : base(pRoot, pID) {
            pRoot.UsingExtension(pID);
            values = new GltfAttributes();
        }

        public override void ToJSON(StreamWriter outt, int level) {
            outt.Write(" { ");
            bool first = true;
            JSONHelpers.WriteJSONValueLine(outt, level, ref first, "technique", technique);
            JSONHelpers.WriteJSONLineEnding(outt, ref first);
            outt.Write(JSONHelpers.Indent(level) + "\"values\": ");
            values.ToJSON(outt, level+1);
            outt.Write("\n" + JSONHelpers.Indent(level) + "}\n");
        }
    }
}
