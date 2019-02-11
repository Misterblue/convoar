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
using System.Linq;
using System.Collections.Generic;
using System.IO;

using org.herbal3d.cs.CommonEntitiesUtil;
using org.herbal3d.cs.os.CommonEntities;

// I hoped to keep the Gltf classes separate from the OMV requirement but
//    it doesn't make sense to copy all the mesh info into new structures.
using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.convoar {

    // The base class for all of the different types.
    public abstract class GltfClass {
        public Gltf gltfRoot;
        public string ID;
        public int referenceID;
        public abstract Object AsJSON();    // return object that's serializable as JSON

        protected BLogger _log;
        protected IParameters _params;

        public GltfClass() { }
        public GltfClass(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) {
            BaseInit(pRoot, pID, pLog, pParams);
        }

        protected void BaseInit(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) {
            gltfRoot = pRoot;
            ID = pID;
            _log = pLog;
            _params = pParams;
            referenceID = -1;   // illegal value that could show up when debugging
        }

        // Output messge of --LogGltfBuilding was specified
        protected void LogGltf(string msg, params Object[] args) {
            if (ConvOAR.Globals.parms.P<bool>("LogGltfBuilding")) {
                ConvOAR.Globals.log.Log(msg, args);
            }
        }
    }

    // Base class of a list of a type.
    public abstract class GltfListClass<T> : Dictionary<BHash, T> {
        public Gltf gltfRoot;
        public GltfListClass(Gltf pRoot) {
            gltfRoot = pRoot;
        }

        // Gltfv2 references items by array index. Make sure all reference index
        //    numbers are up to date.
        public void UpdateGltfv2ReferenceIndexes() {
            int refIndex = 0;
            foreach (T entry in this.Values) {
#pragma warning disable IDE0019 // Use pattern matching
                GltfClass entryGltf = entry as GltfClass;
#pragma warning restore IDE0019 // Use pattern matching
                if (entryGltf != null) {
                    entryGltf.referenceID = refIndex;
                }
                refIndex++;
            }
        }

        // Return an array of the referenceIDs of the values in this collection
        public Array AsArrayOfIDs() {
            return this.Values.OfType<GltfClass>()
            .Select(val => {
                return val.referenceID;
            }).ToArray();
        }

        public Array AsArrayOfValues() {
            // return this.Values.OfType<GltfClass>().ToArray();
            return this.Values.OfType<GltfClass>()
            .Select( val => {
                return val.AsJSON();
            }).ToArray();
        }

        // Return a dictionary map of value.Id => value.AsJSON
        public Dictionary<string, Object> ToJSONMapOfNames() {
            return this.Values.OfType<GltfClass>()
            .ToDictionary(t => t.ID, t => t.AsJSON());
        }
    }

    public class GltfVector16 : GltfClass {
        public float[] vector = new float[16];
        public GltfVector16() : base() {
        }

        public override Object AsJSON() {
            return vector;
        }
    }

    // =============================================================
    public class Gltf : GltfClass {
#pragma warning disable 414     // disable 'assigned but not used' warning
        private static readonly string _logHeader = "[Gltf]";
#pragma warning restore 414

        public GltfAttributes extensionsUsed;   // list of extensions used herein

        public GltfScene defaultScene;   // ID of default scene

        public GltfAsset asset;
        public GltfScenes scenes;       // scenes that make up this package
        public GltfNodes nodes;         // nodes in the scenes
        public GltfMeshes meshes;       // the meshes for the nodes
        public GltfMaterials materials; // materials that make up the meshes
        public GltfAccessors accessors; // access to the mesh bin data
        public GltfBufferViews bufferViews; //
        public GltfBuffers buffers;
        public GltfTechniques techniques;
        public GltfPrograms programs;
        public GltfShaders shaders;
        public GltfTextures textures;
        public GltfImages images;
        public GltfSamplers samplers;

        public GltfPrimitives primitives;

        public GltfSampler defaultSampler;

        public PersistRules persist;

        public Gltf(string pSceneName, BLogger pLog, IParameters pParams) : base() {
            gltfRoot = this;
            _log = pLog;
            _params = pParams;
            persist = new PersistRules(PersistRules.AssetType.Scene, pSceneName, PersistRules.TargetType.Gltf, _log, _params);

            extensionsUsed = new GltfAttributes();
            asset = new GltfAsset(this, _log, _params);
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

            primitives = new GltfPrimitives(this);

            // 20170201: ThreeJS defaults to GL_CLAMP but GLTF should default to GL_REPEAT/WRAP
            // Create a sampler for all the textures that forces WRAPing
            defaultSampler = new GltfSampler(gltfRoot, "simpleTextureRepeat", _log, _params) {
                name = "simpleTextureRepeat",
                magFilter = WebGLConstants.LINEAR,
                minFilter = WebGLConstants.LINEAR_MIPMAP_LINEAR,
                wrapS = WebGLConstants.REPEAT,
                wrapT = WebGLConstants.REPEAT
            };
        }

        public void UpdateGltfv2ReferenceIndexes() {
            // extensionsUsed.UpdateGltfv2ReferenceIndexes();
            // asset.UpdateGltfv2ReferenceIndexes();
            scenes.UpdateGltfv2ReferenceIndexes();
            nodes.UpdateGltfv2ReferenceIndexes();
            meshes.UpdateGltfv2ReferenceIndexes();
            materials.UpdateGltfv2ReferenceIndexes();
            accessors.UpdateGltfv2ReferenceIndexes();
            bufferViews.UpdateGltfv2ReferenceIndexes();
            buffers.UpdateGltfv2ReferenceIndexes();
            techniques.UpdateGltfv2ReferenceIndexes();
            programs.UpdateGltfv2ReferenceIndexes();
            shaders.UpdateGltfv2ReferenceIndexes();
            textures.UpdateGltfv2ReferenceIndexes();
            images.UpdateGltfv2ReferenceIndexes();
            samplers.UpdateGltfv2ReferenceIndexes();

            primitives.UpdateGltfv2ReferenceIndexes();
        }

        // Say this scene is using the extension.
        public void UsingExtension(string extName) {
            if (!extensionsUsed.ContainsKey(extName)) {
                extensionsUsed.Add(extName, null);
            }
        }

        // Add all the objects from a scene into this empty Gltf instance.
        public void LoadScene(BScene scene, AssetManager assetManager) {

            _log.DebugFormat("Gltf.LoadScene: loading scene {0}", scene.name);
            GltfScene gltfScene = new GltfScene(this, scene.name, _log, _params);
            defaultScene = gltfScene;

            // Adding the nodes creates all the GltfMesh's, etc.
            scene.instances.ForEach(pInstance => {
                Displayable rootDisp = pInstance.Representation;
                // _log.DebugFormat("Gltf.LoadScene: Loading node {0}", rootDisp.name);    // DEBUG DEBUG
                GltfNode rootNode = GltfNode.GltfNodeFactory(gltfRoot, gltfScene, rootDisp, assetManager, _log, _params);
                rootNode.translation = pInstance.Position;
                rootNode.rotation = pInstance.Rotation;
            });

            // Load the pointed to items first and then the complex items

            // Meshes, etc  have been added to the scene. Pass over all
            //   the meshes and create the Buffers, BufferViews, and Accessors.
            _log.DebugFormat("Gltf.LoadScene: starting building buffers and accessors ");    // DEBUG DEBUG
            BuildAccessorsAndBuffers();
            _log.DebugFormat("Gltf.LoadScene: updating reference indexes");    // DEBUG DEBUG
            UpdateGltfv2ReferenceIndexes();
            _log.DebugFormat("Gltf.LoadScene: done loading");
        }

        // After all the nodes have been added to a Gltf class, build all the
        //    dependent structures
        public void BuildAccessorsAndBuffers() {
            int maxVerticesPerBuffer = ConvOAR.Globals.parms.P<int>("VerticesMaxForBuffer");

            // Partition the meshes into smaller groups based on number of vertices going out
            List<GltfPrimitive> partial = new List<GltfPrimitive>();
            int totalVertices = 0;
            foreach (var prim in primitives.Values) {
                // If adding this mesh will push the total vertices in this buffer over the max, flush this buffer.
                if ((totalVertices + prim.meshInfo.vertexs.Count) > maxVerticesPerBuffer) {
                    BuildBufferForSomeMeshes(partial);
                    partial.Clear();
                    totalVertices = 0;
                }
                totalVertices += prim.meshInfo.vertexs.Count;
                partial.Add(prim);
            };
            if (partial.Count > 0) {
                BuildBufferForSomeMeshes(partial);
            }
        }

        // For a collection of meshes, create the buffers and accessors.
        public void BuildBufferForSomeMeshes(List<GltfPrimitive> somePrimitives) {
            // Pass over all the vertices in all the meshes and collect common vertices into 'vertexCollection'
            int numMeshes = 0;
            int numVerts = 0;
            Dictionary<BHash, ushort> vertexIndex = new Dictionary<BHash, ushort>();
            List<OMVR.Vertex> vertexCollection = new List<OMVR.Vertex>();
            ushort vertInd = 0;
            // This generates a collection of unique vertices (vertexCollection) and a dictionary
            //    that maps a vertex to its index (vertexIndex). The latter is used later to remap
            //    the existing indices values to new ones for the new unique vertex list.
            somePrimitives.ForEach(prim => {
                numMeshes++;
                prim.meshInfo.vertexs.ForEach(vert => {
                    numVerts++;
                    BHash vertHash = MeshInfo.VertexBHash(vert);
                    if (!vertexIndex.ContainsKey(vertHash)) {
                        vertexIndex.Add(vertHash, vertInd);
                        vertexCollection.Add(vert);
                        vertInd++;
                    }
                });
            });
            // _log.DebugFormat("{0} BuildBuffers: total meshes = {1}", _logHeader, numMeshes);
            // _log.DebugFormat("{0} BuildBuffers: total vertices = {1}", _logHeader, numVerts);
            // _log.DebugFormat("{0} BuildBuffers: total unique vertices = {1}", _logHeader, vertInd);


            // Remap all the indices to the new, compacted vertex collection.
            //     mesh.underlyingMesh.face to mesh.newIndices
            // TODO: if num verts > ushort.maxValue, create array if uint's
            int numIndices = 0;
            somePrimitives.ForEach(prim => {
                MeshInfo meshInfo = prim.meshInfo;
                ushort[] newIndices = new ushort[meshInfo.indices.Count];
                for (int ii = 0; ii < meshInfo.indices.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[meshInfo.indices[ii]];
                    BHash vertHash = MeshInfo.VertexBHash(aVert);
                    newIndices[ii] = vertexIndex[vertHash];
                }
                prim.newIndices = newIndices;
                numIndices += newIndices.Length;
                if (newIndices.Length == 0) {
                    _log.ErrorFormat("{0} zero indices count", _logHeader);
                }
            });

            // The vertices have been unique'ified into 'vertexCollection' and each mesh has
            //    updated indices in GltfMesh.newIndices.

            int sizeofOneVertex = sizeof(float) * 8;
            int sizeofVertices = vertexCollection.Count * sizeofOneVertex;
            int sizeofOneIndices = sizeof(ushort);
            int sizeofIndices = numIndices * sizeofOneIndices;
            // The offsets must be multiples of a good access unit so pad to a good alignment
            int padUnit = sizeof(float) * 8;
            int paddedSizeofIndices = sizeofIndices;
            // There might be padding for each mesh. An over estimate but hopefully not too bad.
            // paddedSizeofIndices += somePrimitives.Count * sizeof(float);
            paddedSizeofIndices += (padUnit - (paddedSizeofIndices % padUnit)) % padUnit;

            // A key added to the buffer, vertices, and indices names to uniquify them
            string buffNum =  String.Format("{0:000}", buffers.Count + 1);
            string buffName = this.defaultScene.name + "-buffer" + buffNum;
            byte[] binBuffRaw = new byte[paddedSizeofIndices + sizeofVertices];
            GltfBuffer binBuff = new GltfBuffer(gltfRoot, buffName, _log, _params) {
                bufferBytes = binBuffRaw
            };

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
            Buffer.BlockCopy(floatVertexRemapped, 0, binBuffRaw, paddedSizeofIndices, sizeofVertices);
            floatVertexRemapped = null;

            // Create BufferView's for each of the four sections of the buffer
            GltfBufferView binIndicesView = new GltfBufferView(gltfRoot, "indices" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = 0,
                byteLength = paddedSizeofIndices,
                byteStride = sizeofOneIndices
            };
            // binIndicesView.target = WebGLConstants.ELEMENT_ARRAY_BUFFER;

            GltfBufferView binVerticesView = new GltfBufferView(gltfRoot, "viewVertices" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = paddedSizeofIndices,
                byteLength = vertexCollection.Count * 3 * sizeof(float),
                byteStride = 3 * sizeof(float)
            };
            // binVerticesView.target = WebGLConstants.ARRAY_BUFFER;

            GltfBufferView binNormalsView = new GltfBufferView(gltfRoot, "normals" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = binVerticesView.byteOffset + binVerticesView.byteLength,
                byteLength = vertexCollection.Count * 3 * sizeof(float),
                byteStride = 3 * sizeof(float)
            };
            // binNormalsView.target = WebGLConstants.ARRAY_BUFFER;

            GltfBufferView binTexCoordView = new GltfBufferView(gltfRoot, "texCoord" + buffNum, _log, _params) {
                buffer = binBuff,
                byteOffset = binNormalsView.byteOffset + binNormalsView.byteLength,
                byteLength = vertexCollection.Count * 2 * sizeof(float),
                byteStride = 2 * sizeof(float)
            };
            // binTexCoordView.target = WebGLConstants.ARRAY_BUFFER;


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
            GltfAccessor vertexAccessor = new GltfAccessor(gltfRoot, buffName + "_accCVer", _log, _params) {
                bufferView = binVerticesView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC3",
                min = new Object[3] { vmin.X, vmin.Y, vmin.Z },
                max = new Object[3] { vmax.X, vmax.Y, vmax.Z }
            };

            GltfAccessor normalsAccessor = new GltfAccessor(gltfRoot, buffName + "_accNor", _log, _params) {
                bufferView = binNormalsView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC3",
                min = new Object[3] { nmin.X, nmin.Y, nmin.Z },
                max = new Object[3] { nmax.X, nmax.Y, nmax.Z }
            };

            GltfAccessor UVAccessor = new GltfAccessor(gltfRoot, buffName + "_accUV", _log, _params) {
                bufferView = binTexCoordView,
                count = vertexCollection.Count,
                byteOffset = 0,
                componentType = WebGLConstants.FLOAT,
                type = "VEC2"
            };
            // The values for TexCoords sometimes get odd
            if (!Single.IsNaN(umin.X) && umin.X > -1000000 && umin.X < 1000000
                    && !Single.IsNaN(umin.Y) && umin.Y > -1000000 && umin.Y < 1000000) {
                UVAccessor.min = new Object[2] { umin.X, umin.Y };
            }
            if (!Single.IsNaN(umax.X) && umax.X > -1000000 && umax.X < 1000000
                    && !Single.IsNaN(umax.Y) && umax.Y > -1000000 && umax.Y < 1000000) {
                UVAccessor.max = new Object[2] { umax.X, umax.Y };
            }

            // For each mesh, copy the indices into the binary output buffer and create the accessors
            //    that point from the mesh into the binary info.
            int indicesOffset = 0;
            somePrimitives.ForEach((Action<GltfPrimitive>)(prim => {
                int meshIndicesSize = prim.newIndices.Length * sizeofOneIndices;
                Buffer.BlockCopy(prim.newIndices, 0, binBuffRaw, indicesOffset, meshIndicesSize);

                GltfAccessor indicesAccessor = new GltfAccessor(gltfRoot, prim.ID + "_accInd", _log, _params) {
                    bufferView = binIndicesView,
                    count = prim.newIndices.Length,
                    byteOffset = indicesOffset,
                    componentType = WebGLConstants.UNSIGNED_SHORT,
                    type = "SCALAR"
                };
                ushort imin = ushort.MaxValue; ushort imax = 0;
                for (int ii = 0; ii < prim.newIndices.Length; ii++) {
                    imin = Math.Min(imin, prim.newIndices[ii]);
                    imax = Math.Max(imax, prim.newIndices[ii]);
                }
                indicesAccessor.min = new Object[1] { imin };
                indicesAccessor.max = new Object[1] { imax };

                // _log.DebugFormat("{0} indices: meshIndSize={1}, cnt={2}, offset={3}", LogHeader,
                //                 meshIndicesSize, indicesAccessor.count, indicesOffset);

                indicesOffset += meshIndicesSize;

                prim.indices = indicesAccessor;
                prim.position = vertexAccessor;
                prim.normals = normalsAccessor;
                prim.texcoord = UVAccessor;
            }));
        }

        public void ToJSON(StreamWriter outt) {
            UpdateGltfv2ReferenceIndexes();
            JSONHelpers.SimpleJSONOutput(outt, this.AsJSON());
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (defaultScene != null) {
                ret.Add("scene", defaultScene.referenceID);
            }

            if (asset.values.Count > 0) {
                ret.Add("asset", asset.AsJSON());
            }

            if (scenes.Count > 0) {
                ret.Add("scenes", scenes.AsArrayOfValues());
            }

            if (nodes.Count > 0) {
                ret.Add("nodes", nodes.AsArrayOfValues());
            }

            if (meshes.Count > 0) {
                ret.Add("meshes", meshes.AsArrayOfValues());
            }

            if (accessors.Count > 0) {
                ret.Add("accessors", accessors.AsArrayOfValues());
            }

            if (bufferViews.Count > 0) {
                ret.Add("bufferViews", bufferViews.AsArrayOfValues());
            }

            if (materials.Count > 0) {
                ret.Add("materials", materials.AsArrayOfValues());
            }

            if (techniques.Count > 0) {
                ret.Add("techniques", techniques.AsArrayOfValues());
            }

            if (textures.Count > 0) {
                ret.Add("textures", textures.AsArrayOfValues());
            }

            if (images.Count > 0) {
                ret.Add("images", images.AsArrayOfValues());
            }

            if (samplers.Count > 0) {
                ret.Add("samplers", samplers.AsArrayOfValues());
            }

            if (programs.Count > 0) {
                ret.Add("programs", programs.AsArrayOfValues());
            }

            if (shaders.Count > 0) {
                ret.Add("shaders", shaders.AsArrayOfValues());
            }

            if (buffers.Count > 0) {
                ret.Add("buffers", buffers.AsArrayOfValues());
            }

            if (extensionsUsed.Count > 0) {
                ret.Add("extensionsUsed", extensionsUsed.AsJSON());
            }

            if (extensionsUsed.Count > 0) {
                ret.Add("extensionsRequired", extensionsUsed.AsJSON());
            }

            return ret;
        } 

        // Write the binary files into the specified target directory
        public void WriteBinaryFiles() {
            foreach (var buff in buffers.Values) {
                string outFilename = buff.persist.Filename;
                // _log.DebugFormat("{0} WriteBinaryFiles: filename={1}", LogHeader, outFilename);
                File.WriteAllBytes(outFilename, buff.bufferBytes);
            }
        }

        public void WriteImages() {
            foreach (var img in images.Values) {
                img.imageInfo.persist.WriteImage(img.imageInfo);
            }
        }
    }


    // =============================================================
    // A simple collection to keep name/value strings
    // The value is an object so it can hold strings, numbers, or arrays and have the
    //     values serialized properly in the output JSON.
    public class GltfAttributes : Dictionary<string, Object> {
        public GltfAttributes() : base() {
        }

        public Object AsJSON() {
            return this;
        }
    }

    // =============================================================
    public class GltfAsset : GltfClass {
        public GltfAttributes values;
        public GltfAttributes extras;

        public GltfAsset(Gltf pRoot, BLogger pLog, IParameters pParams) : base(pRoot, "", pLog, pParams) {
            values = new GltfAttributes {
                { "generator", "convoar" },
                { "version", "2.0" },
                { "copyright", ConvOAR.Globals.parms.P<string>("GltfCopyright") }
            };
            extras = new GltfAttributes {
                { "convoarCommit", ConvOAR.Globals.gitCommit },
                { "convoarVersion", ConvOAR.Globals.version },
                { "convoarBuildDate", ConvOAR.Globals.buildDate },
            };
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>(values);
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfScenes : GltfListClass<GltfScene> {
        public GltfScenes(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfScene : GltfClass {
        public GltfNodes nodes;      // IDs of top level nodes in the scene
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public GltfScene(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            nodes = new GltfNodes(gltfRoot);
            name = pID;
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
            gltfRoot.scenes.Add(new BHashULong(gltfRoot.scenes.Count), this);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (nodes != null && nodes.Count > 0) ret.Add("nodes", nodes.AsArrayOfIDs());
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfNodes : GltfListClass<GltfNode> {
        public GltfNodes(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfNode : GltfClass {
        public string camera;       // non-empty if a camera definition
        public GltfNodes children;
        public string skin;
        // has either 'matrix' or 'rotation/scale/translation'
        public OMV.Matrix4 matrix;
        public GltfMesh mesh;
        public OMV.Quaternion rotation;
        public OMV.Vector3 scale;
        public OMV.Vector3 translation;
        public string[] weights;   // weights of morph tragets
        public string name;
        public GltfExtensions extensions;   // more JSON describing the extensions used
        public GltfAttributes extras;       // more JSON with additional, beyond-the-standard values

        // Add a node that is not top level in a scene
        // Does not add to the built node collection
        public GltfNode(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            NodeInit(pRoot, null);
            LogGltf("{0} GltfNode: created empty. ID={1}", "Gltf", ID);
        }

        public GltfNode(Gltf pRoot, GltfScene containingScene, Displayable pDisplayable, AssetManager assetManager,
                                        BLogger pLog, IParameters pParams)
                            : base(pRoot, pDisplayable.baseUUID.ToString() + "_disp", pLog, pParams) {
            NodeInit(pRoot, containingScene);
            InitFromDisplayable(pDisplayable, containingScene, assetManager);
            LogGltf("{0} GltfNode: created from Displayable. ID={1}, pos={2}, rot={3}, mesh={4}, numCh={5}",
                        "Gltf", ID, translation, rotation, mesh.handle, children.Count);
        }

        // Base initialization of the node instance
        private void NodeInit(Gltf pRoot, GltfScene containingScene) {
            children = new GltfNodes(pRoot);
            matrix = OMV.Matrix4.Zero;
            rotation = new OMV.Quaternion();
            scale = OMV.Vector3.One;
            translation = new OMV.Vector3(0, 0, 0);
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
        }

        private void InitFromDisplayable(Displayable pDisplayable, GltfScene containingScene, AssetManager assetManager) {
            name = pDisplayable.name;
            translation = pDisplayable.offsetPosition;
            rotation = pDisplayable.offsetRotation;
            scale = pDisplayable.scale;
            // only know how to handle a displayable of meshes
            mesh = GltfMesh.GltfMeshFactory(gltfRoot, pDisplayable.renderable, assetManager, _log, _params);

            foreach (var child in pDisplayable.children) {
                var node = GltfNode.GltfNodeFactory(gltfRoot, null, child, assetManager, _log, _params);
                this.children.Add(new BHashULong(this.children.Count), node);
            }
        }

        // Get an existing instance of a node or create a new one
        public static GltfNode GltfNodeFactory(Gltf pRoot, GltfScene containingScene, Displayable pDisplayable,
                        AssetManager assetManager, BLogger pLog, IParameters pParams) {
            if (!pRoot.nodes.TryGetValue(pDisplayable.GetBHash(), out GltfNode node)) {
                node = new GltfNode(pRoot, containingScene, pDisplayable, assetManager, pLog, pParams);
                // This is the only place we should be creating nodes
                pRoot.nodes.Add(pDisplayable.GetBHash(), node);
                if (containingScene != null) {
                    containingScene.nodes.Add(new BHashULong(containingScene.nodes.Count), node);
                }
            }
            return node;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (matrix != OMV.Matrix4.Zero) {
                ret.Add("matrix", matrix);
            }
            else {
                ret.Add("translation", translation);
                ret.Add("scale", scale);
                ret.Add("rotation", rotation);
            }
            if (children.Count > 0) {
                ret.Add("children", children.AsArrayOfIDs());
            }
            ret.Add("mesh", mesh.referenceID);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfMeshes : GltfListClass<GltfMesh> {
        public GltfMeshes(Gltf pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID pUUID, out GltfMesh theMesh) {
            string sUUID = pUUID.ToString();
            foreach (GltfMesh mesh in this.Values) {
                if (mesh.handle.ToString() == sUUID) {
                    theMesh = mesh;
                    return true;
                }
            }
            theMesh = null;
            return false;
        }
    }

    public class GltfMesh : GltfClass {
        public GltfPrimitives primitives;
        public string[] weights;    // weights to apply with morph targets
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public EntityHandle handle;
        public BHash bHash;
        public Displayable underlyingDisplayable;

        public GltfMesh(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            primitives = new GltfPrimitives(gltfRoot);
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
            handle = new EntityHandleUUID();
            LogGltf("{0} GltfMesh: created empty. ID={1}, handle={2}, numPrim={3}",
                        "Gltf", ID, handle, primitives.Count);
        }

        public GltfMesh(Gltf pRoot, DisplayableRenderable pDR, AssetManager assetManager,
                            BLogger pLog, IParameters pParams) : base(pRoot, pDR.handle.ToString() + "_dr", pLog, pParams) {
            primitives = new GltfPrimitives(gltfRoot);
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
            handle = new EntityHandleUUID();
            if (pDR is RenderableMeshGroup rmg) {
                // Add the meshes in the RenderableMeshGroup as primitives in this mesh
                rmg.meshes.ForEach(oneMesh => {
                    // _log.DebugFormat("GltfMesh. create primitive: numVerts={0}, numInd={1}", // DEBUG DEBUG
                    //         oneMesh.mesh.vertexs.Count, oneMesh.mesh.indices.Count);  // DEBUG DEBUG
                    GltfPrimitive prim = GltfPrimitive.GltfPrimitiveFactory(pRoot, oneMesh, assetManager, _log, _params);
                    primitives.Add(new BHashULong(primitives.Count), prim);
                });
            }
            BHasher hasher = new BHasherSHA256();
            primitives.Values.ToList().ForEach(prim => {
                hasher.Add(prim.bHash);
            });
            bHash = hasher.Finish();
            if (ConvOAR.Globals.parms.P<bool>("AddUniqueCodes")) {
                // Add a unique code to the extras section
                extras.Add("uniqueHash", bHash.ToString());
            }
            gltfRoot.meshes.Add(pDR.GetBHash(), this);
            LogGltf("{0} GltfMesh: created from DR. ID={1}, handle={2}, numPrimitives={3}",
                        "Gltf", ID, handle, primitives.Count);
        }

        public static GltfMesh GltfMeshFactory(Gltf pRoot, DisplayableRenderable pDR, AssetManager assetManager,
                                    BLogger pLog, IParameters pParams) {
            if (!pRoot.meshes.TryGetValue(pDR.GetBHash(), out GltfMesh mesh)) {
                mesh = new GltfMesh(pRoot, pDR, assetManager, pLog, pParams);
            }
            return mesh;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (primitives != null && primitives.Count > 0) ret.Add("primitives", primitives.AsArrayOfValues());
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfPrimitives : GltfListClass<GltfPrimitive> {
        public GltfPrimitives(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfPrimitive : GltfClass {
        public GltfAccessor indices;
        public MaterialInfo matInfo;
        public int mode;
        public string[] targets;    // TODO: morph targets
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public MeshInfo meshInfo;
        public BHash bHash;          // generated from meshes and materials for this primitive
        public ushort[] newIndices; // remapped indices posinting to global vertex list
        public GltfAccessor normals;
        public GltfAccessor position;
        public GltfAccessor texcoord;
        public GltfMaterial material;

        public GltfPrimitive(Gltf pRoot, BLogger pLog, IParameters pParams) : base(pRoot, "primitive", pLog, pParams) {
            mode = 4;
            LogGltf("{0} GltfPrimitive: created empty. ID={1}", "Gltf", ID);
        }

        public GltfPrimitive(Gltf pRoot, RenderableMesh pRenderableMesh, AssetManager assetManager,
                        BLogger pLog, IParameters pParams) : base(pRoot, "primitive", pLog, pParams) {
            mode = 4;
            meshInfo = pRenderableMesh.mesh;
            matInfo = pRenderableMesh.material;
            material = GltfMaterial.GltfMaterialFactory(pRoot, matInfo, assetManager, pLog, pParams);
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();

            // My hash is the same as the underlying renderable mesh/material
            bHash = pRenderableMesh.GetBHash();
            ID = bHash.ToString();

            LogGltf("{0} GltfPrimitive: created. ID={1}, mesh={2}, hash={3}", "Gltf", ID, meshInfo, bHash);
            pRoot.primitives.Add(bHash, this);
        }

        public static GltfPrimitive GltfPrimitiveFactory(Gltf pRoot, RenderableMesh pRenderableMesh,
                            AssetManager assetManager, BLogger pLog, IParameters pParams) {
            if (!pRoot.primitives.TryGetValue(pRenderableMesh.GetBHash(), out GltfPrimitive prim)) {
                prim = new GltfPrimitive(pRoot, pRenderableMesh, assetManager, pLog, pParams);
            }
            return prim;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "mode", mode }
            };
            if (indices != null) ret.Add("indices", indices.referenceID);
            if (material != null) ret.Add("material", material.referenceID);

            var attribs = new Dictionary<string, Object>();
            if (normals != null) attribs.Add("NORMAL", normals.referenceID);
            if (position != null) attribs.Add("POSITION", position.referenceID);
            if (texcoord != null) attribs.Add("TEXCOORD_0", texcoord.referenceID);
            ret.Add("attributes", attribs);

            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());

            return ret;
        }
    }

    // =============================================================
    public class GltfMaterials : GltfListClass<GltfMaterial> {
        public GltfMaterials(Gltf pRoot) : base(pRoot) {
        }
    }

    public abstract class GltfMaterial : GltfClass {
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;
        public string[] pbrMetallicRoughness;   // not used: 
        public GltfImage normalTexture;
        public GltfImage occlusionTexture;
        public GltfImage emissiveTexture;
        public OMV.Vector3? emmisiveFactor;
        public string alphaMode;    // one of "OPAQUE", "MASK", "BLEND"
        public float? alphaCutoff;
        public bool? doubleSided;         // whether surface has backside ('true' or 'false')
        
        // parameters coming from OpenSim
        public OMV.Vector4? ambient;      // ambient color of surface (OMV.Vector4)
        public OMV.Color4? diffuse;       // diffuse color of surface (OMV.Vector4 or textureID)
        public GltfTexture diffuseTexture;  // diffuse color of surface (OMV.Vector4 or textureID)
        public float? emission;           // light emitted by surface (OMV.Vector4 or textureID)
        public float? specular;           // color reflected by surface (OMV.Vector4 or textureID)
        public float? shininess;          // specular reflection from surface (float)
        public float? transparency;       // transparency of surface (float)
        public bool? transparent;         // whether the surface has transparency ('true' or 'false;)

        public GltfAttributes topLevelValues;   // top level values that are output as part of the material

        protected void MaterialInit(Gltf pRoot, MaterialInfo matInfo, AssetManager assetManager,
                                BLogger pLog, IParameters pParams) {
            extras = new GltfAttributes();
            topLevelValues = new GltfAttributes();
            extensions = new GltfExtensions(pRoot);
            BaseInit(pRoot, matInfo.handle.ToString() + "_mat", pLog, pParams);
            gltfRoot.materials.Add(matInfo.GetBHash(), this);

            OMV.Color4 surfaceColor = matInfo.RGBA;
            OMV.Color4 aColor = OMV.Color4.Black;

            diffuse = surfaceColor;
            if (surfaceColor.A != 1.0f) {
                transparency = surfaceColor.A;
                transparent = true;
            }
            if (ConvOAR.Globals.parms.P<bool>("DoubleSided")) {
                doubleSided = ConvOAR.Globals.parms.P<bool>("DoubleSided");
            }
            if (matInfo.shiny != OMV.Shininess.None) {
                shininess = (float)matInfo.shiny / 256f;
            }

            if (matInfo.image != null) {
                ImageInfo imageToUse = CheckForResizedImage(matInfo.image, assetManager);
                GltfImage newImage = GltfImage.GltfImageFactory(pRoot, imageToUse, _log, _params);
                diffuseTexture = GltfTexture.GltfTextureFactory(pRoot, imageToUse, newImage, _log, _params);

                if (diffuseTexture.source != null && diffuseTexture.source.imageInfo.hasTransprency) {
                    // 'Transparent' says the image has some alpha that needs blending
                    // the spec says default value is 'false' so only specify if 'true'
                    transparent = true;
                }
            }

            LogGltf("{0} GltfMaterial: created. ID={1}, name='{2}', numExt={3}",
                        "Gltf", ID, name, extensions.Count);
        }

        // NOTE: needed version that didn't have enclosing {} for some reason
        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (topLevelValues != null && topLevelValues.Count > 0) {
                foreach (var key in topLevelValues.Keys) {
                    ret.Add(key, topLevelValues[key]);
                }
            }
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;

        }

        // For Gltf (and the web browser) we can use reduced size images.
        // Check if that is being done and find the reference to the resized image
        private ImageInfo CheckForResizedImage(ImageInfo origImage, AssetManager assetManager) {
            ImageInfo ret = origImage;
            int maxSize = ConvOAR.Globals.parms.P<int>("TextureMaxSize");
            if (origImage.resizable && maxSize > 0 && maxSize < 10000) {
                if (origImage.xSize > maxSize || origImage.ySize > maxSize) {
                    origImage.ConstrainTextureSize(maxSize);
                }
            }
            return ret;
        }

        public static GltfMaterial GltfMaterialFactory(Gltf pRoot, MaterialInfo matInfo,
                            AssetManager assetManager, BLogger pLog, IParameters pParams) {
            if (!pRoot.materials.TryGetValue(matInfo.GetBHash(), out GltfMaterial mat)) {
                // mat = new GltfMaterialCommon2(pRoot, matInfo, assetManager);
                mat = new GltfMaterialPbrMetallicRoughness(pRoot, matInfo, assetManager, pLog, pParams);
                // mat = new GltfMaterialPbrSpecularGlossiness(pRoot, matInfo, assetManager);
            }
            return mat;
        }
    }

    // Material as a HDR_Common_Material for GLTF version 2
    public class GltfMaterialCommon2 : GltfMaterial {
        GltfExtension materialCommonExt;

        public GltfMaterialCommon2(Gltf pRoot, MaterialInfo matInfo, AssetManager assetManager,
                                BLogger pLog, IParameters pParams) {
            MaterialInit(pRoot, matInfo, assetManager, pLog, pParams);
        }

        public override Object AsJSON() {
            materialCommonExt = new GltfExtension(gltfRoot, "KHR_materials_common", _log, _params);
            // Pack the material set values into the extension
            materialCommonExt.values.Add("type", "commonBlinn");
            materialCommonExt.values.Add("diffuseFactor", diffuse.Value);
            if (diffuseTexture != null) {
                materialCommonExt.values.Add("diffuseTexture", diffuseTexture.referenceID);
            }
            if (specular.HasValue) {
                materialCommonExt.values.Add("specularFactor", specular.Value);
            }
            if (shininess.HasValue) {
                materialCommonExt.values.Add("shininessFactor", shininess.Value);
            }
            if (transparent.HasValue) {
                // OPAQUE, MASK, or BLEND
                this.topLevelValues.Add("alphaMode", "BLEND");
                // this.values.Add("alphaCutoff", 0.5f);
            }
            if (doubleSided.HasValue) {
                this.topLevelValues.Add("doubleSided", doubleSided.Value);
            }

            if (materialCommonExt.Count() > 0) {
                extensions.Add(materialCommonExt.ID, materialCommonExt);
            }

            return base.AsJSON();
        }
    }

    // Material as a pbrMetallicRoughness
    public class GltfMaterialPbrMetallicRoughness : GltfMaterial {

        public GltfMaterialPbrMetallicRoughness(Gltf pRoot, MaterialInfo matInfo, AssetManager assetManager,
                                BLogger pLog, IParameters pParams) {
            MaterialInit(pRoot, matInfo, assetManager, pLog, pParams);
        }

        public override Object AsJSON() {
            var pbr = new Dictionary<string, Object>();
            if (diffuse.HasValue) {
                pbr.Add("baseColorFactor", diffuse.Value);
            }
            if (diffuseTexture != null) {
                pbr.Add("baseColorTexture", diffuseTexture.TextureInfo());
            }
            if (specular.HasValue) {
                // 0..1: 1 means 'rough', 0 means 'smooth', linear scale
                pbr.Add("roughnessFactor", specular.Value);
            }
            if (shininess.HasValue) {
                // 0..1: 1 means 'metal', 0 means dieletic, linear scale
                pbr.Add("metallicFactor", shininess.Value);
            }
            else {
                // if no shineess is specified, this is not a metal
                pbr.Add("metallicFactor", 0f);
            }
            if (pbr.Count > 0)
                topLevelValues.Add("pbrMetallicRoughness", pbr);

            if (transparent.HasValue) {
                // OPAQUE, MASK, or BLEND
                this.topLevelValues.Add("alphaMode", "BLEND");
                // this.values.Add("alphaCutoff", 0.5f);
            }
            if (doubleSided.HasValue) {
                this.topLevelValues.Add("doubleSided", doubleSided.Value);
            }
            return base.AsJSON();
        }
    }

    // Material as a HDR_pbr_specularGlossiness

    // =============================================================
    public class GltfAccessors : GltfListClass<GltfAccessor> {
        public GltfAccessors(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfAccessor : GltfClass {
        public GltfBufferView bufferView;
        public int byteOffset;
        public uint componentType;
        public int count;
        public string type;
        public Object[] min;
        public Object[] max;

        public GltfAccessor(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.accessors.Add(new BHashULong(gltfRoot.accessors.Count), this);
            LogGltf("{0} GltfAccessor: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "bufferView", bufferView.referenceID },
                { "byteOffset", byteOffset },
                { "componentType", componentType },
                { "count", count }
            };
            if (!String.IsNullOrEmpty(type)) ret.Add("type", type);
            if (min != null && min.Length > 0) ret.Add("min", min);
            if (max != null && max.Length > 0) ret.Add("max", max);
            return ret;
        }
    }

    // =============================================================
    public class GltfBuffers : GltfListClass<GltfBuffer> {
        public GltfBuffers(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfBuffer : GltfClass {
        public PersistRules persist;
        public byte[] bufferBytes;
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public GltfBuffer(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            persist = new PersistRules(PersistRules.AssetType.Buff, pID, pLog, pParams);
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
            // Buffs go into the directory of the root
            persist.BaseDirectory = pRoot.persist.BaseDirectory;
            gltfRoot.buffers.Add(new BHashULong(gltfRoot.buffers.Count), this);
            LogGltf("{0} GltfBuffer: created. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            ret.Add("byteLength", bufferBytes.Length);
            ret.Add("uri", persist.Uri);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfBufferViews : GltfListClass<GltfBufferView> {
        public GltfBufferViews(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfBufferView : GltfClass {
        public GltfBuffer buffer;
        public int? byteOffset;
        public int? byteLength;
        public int? byteStride;
        public uint? target;
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public GltfBufferView(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.bufferViews.Add(new BHashULong(gltfRoot.bufferViews.Count), this);
            name = pID;
            extensions = new GltfExtensions(pRoot);
            extras = new GltfAttributes();
            LogGltf("{0} GltfBufferView: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            ret.Add("buffer", buffer.referenceID);
            ret.Add("byteOffset", byteOffset);
            ret.Add("byteLength", byteLength);
            // ret.Add("byteStride", byteStride);
            if (target.HasValue) ret.Add("target", target.Value);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfTechniques : GltfListClass<GltfTechnique> {
        public GltfTechniques(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfTechnique : GltfClass {
        public GltfTechnique(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.techniques.Add(new BHashULong(gltfRoot.techniques.Count), this);
            LogGltf("{0} GltfTechnique: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfPrograms : GltfListClass<GltfProgram> {
        public GltfPrograms(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfProgram : GltfClass {
        public GltfProgram(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.programs.Add(new BHashULong(gltfRoot.programs.Count), this);
            LogGltf("{0} GltfTechnique: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfShaders : GltfListClass<GltfShader> {
        public GltfShaders(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfShader : GltfClass {
        public GltfShader(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.shaders.Add(new BHashULong(gltfRoot.shaders.Count), this);
            LogGltf("{0} GltfShader: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            // TODO:
            return ret;
        }
    }

    // =============================================================
    public class GltfTextures : GltfListClass<GltfTexture> {
        public GltfTextures(Gltf pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfTexture theTexture) {
            foreach (var tex in this.Values) {
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
        public GltfSampler sampler;
        public GltfImage source;
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public OMV.UUID underlyingUUID;
        // public uint target;
        // public uint type;
        // public uint format;
        // public uint internalFormat;

        public GltfTexture(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.textures.Add(this);
            LogGltf("{0} GltfTexture: created empty. ID={1}", "Gltf", ID);
        }

        public GltfTexture(Gltf pRoot, ImageInfo pImageInfo, GltfImage pImage,
                    BLogger pLog, IParameters pParams) : base(pRoot, pImageInfo.handle.ToString() + "_tex", pLog, pParams) {
            if (pImageInfo.handle is EntityHandleUUID handleU) {
                underlyingUUID = handleU.GetUUID();
            }
            // this.target = WebGLConstants.TEXTURE_2D;
            // this.type = WebGLConstants.UNSIGNED_BYTE;
            // this.format = WebGLConstants.RGBA;
            // this.internalFormat = WebGLConstants.RGBA;
            this.sampler = pRoot.defaultSampler;
            this.source = pImage;

            gltfRoot.textures.Add(pImageInfo.GetBHash(), this);
            LogGltf("{0} GltfTexture: created. ID={1}, uuid={2}, srcID={3}",
                    "Gltf", ID, underlyingUUID, source.ID);
        }

        public static GltfTexture GltfTextureFactory(Gltf pRoot, ImageInfo pImageInfo, GltfImage pImage,
                            BLogger pLog, IParameters pParams) {
            if (!pRoot.textures.TryGetValue(pImageInfo.GetBHash(), out GltfTexture tex)) {
                tex = new GltfTexture(pRoot, pImageInfo, pImage, pLog, pParams);
            }
            return tex;
        }

        public override Object AsJSON() {
            GltfAttributes ret = new GltfAttributes();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (source != null) ret.Add("source", source.referenceID);
            if (sampler != null) ret.Add("sampler", sampler.referenceID);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }

        public GltfAttributes TextureInfo() {
            GltfAttributes ret = new GltfAttributes {
                { "index", referenceID }
            };
            return ret;
        }
    }

    // =============================================================
    public class GltfImages : GltfListClass<GltfImage> {
        public GltfImages(Gltf pRoot) : base(pRoot) {
        }

        public bool GetByUUID(OMV.UUID aUUID, out GltfImage theImage) {
            foreach (GltfImage img in this.Values) {
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
    
        public GltfImage(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.images.Add(this);
            LogGltf("{0} GltfImage: created empty. ID={1}", "Gltf", ID);
        }

        public GltfImage(Gltf pRoot, ImageInfo pImageInfo, BLogger pLog, IParameters pParams)
                                : base(pRoot, pImageInfo.handle.ToString() + "_img", pLog, pParams) {
            imageInfo = pImageInfo;
            if (pImageInfo.handle is EntityHandleUUID handleU) {
                underlyingUUID = handleU.GetUUID();
            }
            gltfRoot.images.Add(pImageInfo.GetBHash(), this);
            LogGltf("{0} GltfImage: created. ID={1}, uuid={2}, imgInfoHandle={3}",
                    "Gltf", ID, underlyingUUID, imageInfo.handle);
        }

        public static GltfImage GltfImageFactory(Gltf pRoot, ImageInfo pImageInfo, BLogger pLog, IParameters pParams) {
            if (!pRoot.images.TryGetValue(pImageInfo.GetBHash(), out GltfImage img)) {
                img = new GltfImage(pRoot, pImageInfo, pLog, pParams);
            }
            return img;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object> {
                { "uri", imageInfo.persist.Uri }
            };
            return ret;
        }
    }

    // =============================================================
    public class GltfSamplers : GltfListClass<GltfSampler> {
        public GltfSamplers(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfSampler : GltfClass {
        public uint? magFilter;
        public uint? minFilter;
        public uint? wrapS;
        public uint? wrapT;
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public GltfSampler(Gltf pRoot, string pID, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            gltfRoot.samplers.Add(new BHashULong(gltfRoot.samplers.Count), this);
            LogGltf("{0} GltfSampler: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (magFilter != null) ret.Add("magFilter", magFilter);
            if (magFilter != null) ret.Add("minFilter", minFilter);
            if (magFilter != null) ret.Add("wrapS", wrapS);
            if (magFilter != null) ret.Add("wrapT", wrapT);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfLights : GltfListClass<GltfLight> {
        public GltfLights(Gltf pRoot) : base(pRoot) {
        }
    }

    public class GltfLight : GltfClass {
        public string name;
        public GltfExtensions extensions;
        public GltfAttributes extras;

        public GltfLight(Gltf pRoot, string pID, MaterialInfo pMatInfo, BLogger pLog, IParameters pParams) : base(pRoot, pID, pLog, pParams) {
            // gltfRoot.lights.Add(new BHashULong(gltfRoot.samplers.Count), this);
            LogGltf("{0} GltfLight: created empty. ID={1}", "Gltf", ID);
        }

        public static GltfLight GltfLightFactory(Gltf pRoot, MaterialInfo pMatInfo) {
            GltfLight lit = null;
            // if (!pRoot.lights.TryGetValue(pMatInfo.GetBHash(), out lit)) {
            //     lit = new GltfLight(pRoot, "light-" + pMatInfo.GetBHash().ToString(), pMatInfo);
            // }
            return lit;
        }

        public override Object AsJSON() {
            var ret = new Dictionary<string, Object>();
            if (!String.IsNullOrEmpty(name)) ret.Add("name", name);
            if (extensions != null && extensions.Count > 0) ret.Add("extensions", extensions.AsJSON());
            if (extras != null && extras.Count > 0) ret.Add("extras", extras.AsJSON());
            return ret;
        }
    }

    // =============================================================
    public class GltfExtensions : Dictionary<string, GltfExtension> {
        public GltfExtensions(Gltf pRoot) : base() {
        }

        public Object AsJSON() {
            return this;
        }
    }

    public class GltfExtension : GltfClass {
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

        public GltfExtension(Gltf pRoot, string extensionName, BLogger pLog, IParameters pParams) : base(pRoot, extensionName, pLog, pParams) {
            pRoot.UsingExtension(extensionName);
            values = new GltfAttributes();
            LogGltf("{0} GltfExtension: created empty. ID={1}", "Gltf", ID);
        }

        public override Object AsJSON() {
            return values;
        }

        public int Count() {
            return values.Count;
        }
    }
}
