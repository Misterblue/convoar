/*
 * Copyright (c) 2016 Robert Adams
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
using System.Drawing;
using System.IO;
using System.Text;

using log4net;

using RSG;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.convoar {

    public class PrimToMesh : IDisposable {
        private OMVR.MeshmerizerR m_mesher;
        ILog _log;
        String _logHeader = "[Basil.PrimToMesh]";

        public PrimToMesh(ILog logger) {
            m_mesher = new OMVR.MeshmerizerR();
            _log = logger;
        }

        /// <summary>
        /// Create and return a faceted mesh.
        /// </summary>
        public IPromise<ExtendedPrimGroup> CreateMeshResource(SceneObjectGroup sog, SceneObjectPart sop,
                    OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod, BasilStats stats) {

            var prom = new Promise<ExtendedPrimGroup>();

            ExtendedPrimGroup mesh;
            try {
                if (prim.Sculpt != null) {
                    if (prim.Sculpt.Type == OMV.SculptType.Mesh) {
                        // m_log.DebugFormat("{0}: CreateMeshResource: creating mesh", LogHeader);
                        stats.numMeshAssets++;
                        MeshFromPrimMeshData(sog, sop, prim, assetFetcher, lod)
                            .Catch(e => {
                                prom.Reject(e);
                            })
                            .Then(ePrimGroup => {
                                prom.Resolve(ePrimGroup);
                            });
                    }
                    else {
                        // m_log.DebugFormat("{0}: CreateMeshResource: creating sculpty", LogHeader);
                        stats.numSculpties++;
                        MeshFromPrimSculptData(sog, sop, prim, assetFetcher, lod)
                            .Catch(e => {
                                prom.Reject(e);
                            })
                            .Then(fm => {
                                prom.Resolve(fm);
                            });
                    }
                }
                else {
                    // m_log.DebugFormat("{0}: CreateMeshResource: creating primshape", LogHeader);
                    stats.numSimplePrims++;
                    mesh = MeshFromPrimShapeData(sog, sop, prim, lod);
                    prom.Resolve(mesh);
                }
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        private ExtendedPrimGroup MeshFromPrimShapeData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, OMVR.DetailLevel lod) {
            OMVR.FacetedMesh mesh;

            mesh = m_mesher.GenerateFacetedMesh(prim, lod);

            ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, mesh);
            ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup(extPrim);

            return extPrimGroup;
        }

        private IPromise<ExtendedPrimGroup> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<ExtendedPrimGroup>();

            // Get the asset that the sculpty is built on
            EntityHandle texHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            assetFetcher.FetchTexture(texHandle)
                .Then((bm) => {
                    OMVR.FacetedMesh fMesh = m_mesher.GenerateFacetedSculptMesh(prim, bm.Image.ExportBitmap(), lod);

                    ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, fMesh);
                    ExtendedPrimGroup extPrimGroup = new ExtendedPrimGroup(extPrim);

                    prom.Resolve(extPrimGroup);
                })
                .Catch((e) => {
                    _log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}: {2}", _logHeader, texHandle, e);
                    prom.Reject(e);
                });

            return prom;
        }

        private IPromise<ExtendedPrimGroup> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<ExtendedPrimGroup>();

            // Get the asset that the mesh is built on
            EntityHandle meshHandle = new EntityHandle(prim.Sculpt.SculptTexture);
            try {
                assetFetcher.FetchRawAsset(meshHandle)
                    .Then(meshBytes => {
                        OMVA.AssetMesh meshAsset = new OMVA.AssetMesh(prim.ID, meshBytes);
                        OMVR.FacetedMesh fMesh;
                        if (OMVR.FacetedMesh.TryDecodeFromAsset(prim, meshAsset, lod, out fMesh)) {
                            ExtendedPrim extPrim = new ExtendedPrim(sog, sop, prim, fMesh);
                            ExtendedPrimGroup eGroup = new ExtendedPrimGroup(extPrim);
                            prom.Resolve(eGroup);
                        }
                        else {
                            prom.Reject(new Exception("MeshFromPrimMeshData: could not decode mesh information from asset. ID="
                                            + prim.ID.ToString()));
                        }
                    })
                    .Catch((e) => {
                        _log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}", _logHeader, e);
                        prom.Reject(e);
                    });
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        // Returns an ExtendedPrimGroup with a mesh for the passed heightmap.
        // Note that the returned EPG does not include any face information -- the caller must add a texture.
        public ExtendedPrimGroup MeshFromHeightMap( float[,] pHeightMap, int regionSizeX, int regionSizeY) {

            // OMVR.Face rawMesh = m_mesher.TerrainMesh(pHeightMap, 0, pHeightMap.GetLength(0)-1, 0, pHeightMap.GetLength(1)-1);
            _log.DebugFormat("{0} MeshFromHeightMap: heightmap=<{1},{2}>, regionSize=<{3},{4}>",
                    _logHeader, pHeightMap.GetLength(0), pHeightMap.GetLength(1), regionSizeX, regionSizeY);
            OMVR.Face rawMesh = BasilTerrain.TerrainMesh(pHeightMap, (float)regionSizeX, (float)regionSizeY, _log);
            OMVR.FacetedMesh facetMesh = new OMVR.FacetedMesh();
            facetMesh.Faces = new List<OMVR.Face>() { rawMesh };

            ExtendedPrim ep = new ExtendedPrim(null, null, null, facetMesh);
            ep.faces = new List<FaceInfo>();

            ExtendedPrimGroup epg = new ExtendedPrimGroup(ep);

            return epg;
        }


        public void Dispose() {
            m_mesher = null;
        }

        public void UpdateCoords(FaceInfo faceInfo, OMV.Primitive prim) {
            if (faceInfo.vertexs != null) {
                m_mesher.TransformTexCoords(faceInfo.vertexs, faceInfo.faceCenter, faceInfo.textureEntry,  prim.Scale);
            }
        }

        // Walk through all the vertices and scale the included meshes
        public static void ScaleMeshes(ExtendedPrimGroup ePG) {
            foreach (ExtendedPrim ep in ePG.Values) {
                OMV.Vector3 scale = ep.fromOS.primitive.Scale;
                if (scale.X != 1.0 || scale.Y != 1.0 || scale.Z != 1.0) {
                    OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                        vert.Position *= scale;
                    });
                }
            }
        }

        // Loop over all the vertices in an ExtendedPrim and perform some operation on them
        public delegate void OperateOnVertex(ref OMVR.Vertex vert);
        public static void OnAllVertex(ExtendedPrim ep, OperateOnVertex vertOp) {
            foreach (FaceInfo aFace in ep.faces) {
                for (int jj = 0; jj < aFace.vertexs.Count; jj++) {
                    OMVR.Vertex aVert = aFace.vertexs[jj];
                    vertOp(ref aVert);
                    aFace.vertexs[jj] = aVert;
                }
            }
        }


    }
}
