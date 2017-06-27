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
        String _logHeader = "[Basil.PrimToMesh]";

        public PrimToMesh() {
            m_mesher = new OMVR.MeshmerizerR();
        }

        /// <summary>
        /// Create and return a set of meshes/materials that make the passed SOP.
        /// This just deals the making a mesh from the SOP and getting the material/texture of the meshes
        ///    into the caches.
        /// </summary>
        public IPromise<Displayable> CreateMeshResource(SceneObjectGroup sog, SceneObjectPart sop,
                    OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<Displayable>();

            try {
                if (prim.Sculpt != null) {
                    if (prim.Sculpt.Type == OMV.SculptType.Mesh) {
                        // ConvOAR.Globals.log.DebugFormat("{0}: CreateMeshResource: creating mesh", _logHeader);
                        ConvOAR.Globals.stats.numMeshAssets++;
                        MeshFromPrimMeshData(sog, sop, prim, assetFetcher, lod)
                            .Catch(e => {
                                prom.Reject(e);
                            })
                            .Then(dispable => {
                                prom.Resolve(new Displayable(dispable, sop));
                            });
                    }
                    else {
                        // ConvOAR.Globals.log.DebugFormat("{0}: CreateMeshResource: creating sculpty", _logHeader);
                        ConvOAR.Globals.stats.numSculpties++;
                        MeshFromPrimSculptData(sog, sop, prim, assetFetcher, lod)
                            .Catch(e => {
                                prom.Reject(e);
                            })
                            .Then(dispable => {
                                prom.Resolve(new Displayable(dispable, sop));
                            });
                    }
                }
                else {
                    // ConvOAR.Globals.log.DebugFormat("{0}: CreateMeshResource: creating primshape", _logHeader);
                    ConvOAR.Globals.stats.numSimplePrims++;
                    DisplayableRenderable dispable = MeshFromPrimShapeData(sog, sop, prim, assetFetcher, lod);
                    prom.Resolve(new Displayable(dispable, sop));
                }
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        private DisplayableRenderable MeshFromPrimShapeData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            BHash primHash = new BHashULong(prim.GetHashCode());
            DisplayableRenderable renderable = assetFetcher.GetRenderable(primHash, () => {
                OMVR.FacetedMesh mesh = m_mesher.GenerateFacetedMesh(prim, lod);
                return ConvertFacetedMeshToDisplayable(assetFetcher, mesh, prim.Textures.DefaultTexture, prim.Scale);
            });
            return renderable;
        }

        private IPromise<DisplayableRenderable> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<DisplayableRenderable>();

            // Get the asset that the sculpty is built on
            EntityHandle texHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
            assetFetcher.FetchTexture(texHandle)
                .Then((bm) => {
                    OMVR.FacetedMesh fMesh = m_mesher.GenerateFacetedSculptMesh(prim, bm.Image.ExportBitmap(), lod);
                    DisplayableRenderable dr = ConvertFacetedMeshToDisplayable(assetFetcher, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                    prom.Resolve(dr);
                })
                .Catch((e) => {
                    ConvOAR.Globals.log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}: {2}", _logHeader, texHandle, e);
                    prom.Reject(e);
                });

            return prom;
        }

        private IPromise<DisplayableRenderable> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            var prom = new Promise<DisplayableRenderable>();

            // Get the asset that the mesh is built on
            EntityHandle meshHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
            try {
                assetFetcher.FetchRawAsset(meshHandle)
                    .Then(meshBytes => {
                        OMVA.AssetMesh meshAsset = new OMVA.AssetMesh(prim.ID, meshBytes);
                        OMVR.FacetedMesh fMesh;
                        if (OMVR.FacetedMesh.TryDecodeFromAsset(prim, meshAsset, lod, out fMesh)) {
                            DisplayableRenderable dr = ConvertFacetedMeshToDisplayable(assetFetcher, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                            prom.Resolve(dr);
                        }
                        else {
                            prom.Reject(new Exception("MeshFromPrimMeshData: could not decode mesh information from asset. ID="
                                            + prim.ID.ToString()));
                        }
                    })
                    .Catch((e) => {
                        ConvOAR.Globals.log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}", _logHeader, e);
                        prom.Reject(e);
                    });
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        /// <summary>
        /// Given a FacetedMesh, create a DisplayableRenderable (a list of RenderableMesh's with materials).
        /// This also creates underlying MesnInfo, MaterialInfo, and ImageInfo in the AssetFetcher.
        /// </summary>
        /// <param name="assetFetcher"></param>
        /// <param name="fmesh">The FacetedMesh to convert into Renderables</param>
        /// <param name="defaultTexture">If a face doesn't have a texture defined, use this one.
        /// This is an OMV.Primitive.TextureEntryFace that includes a lot of OpenSimulator material info.</param>
        /// <param name="primScale">Scaling for the base prim that is used when appliying any texture
        /// to the face (updating UV).</param>
        /// <returns></returns>
        private DisplayableRenderable ConvertFacetedMeshToDisplayable(IAssetFetcher assetFetcher, OMVR.FacetedMesh fmesh,
                        OMV.Primitive.TextureEntryFace defaultTexture, OMV.Vector3 primScale) {
            RenderableMeshGroup ret = new RenderableMeshGroup();
            foreach (OMVR.Face face in fmesh.Faces) {
                RenderableMesh rmesh = ConvertFaceToRenderableMesh(face, assetFetcher, defaultTexture, primScale);
                ret.meshes.Add(rmesh);
            }
            // ConvOAR.Globals.log.DebugFormat("{0} ConvertFacetedMeshToDisplayable: complete. numMeshes={1}", _logHeader, ret.meshes.Count);
            return ret;
        }

        private RenderableMesh ConvertFaceToRenderableMesh(OMVR.Face face, IAssetFetcher assetFetcher,
                        OMV.Primitive.TextureEntryFace defaultTexture, OMV.Vector3 primScale) {
            RenderableMesh rmesh = new RenderableMesh();
            rmesh.num = face.ID;

            // Copy one face's mesh imformation from the FacetedMesh into a MeshInfo
            MeshInfo meshInfo = new MeshInfo();
            meshInfo.vertexs = face.Vertices;
            meshInfo.indices = new List<int>();
            face.Indices.ForEach(ind => { meshInfo.indices.Add((int)ind); });
            meshInfo.faceCenter = face.Center;
            // ConvOAR.Globals.log.DebugFormat("{0} ConvertFaceToRenderableMesh: faceId={1}, numVert={2}, numInd={3}",
            //     _logHeader, face.ID, meshInfo.vertexs.Count, meshInfo.indices.Count);

            if (!ConvOAR.Globals.parms.DisplayTimeScaling) {
                ScaleMeshes(meshInfo, primScale);
            }

            // Find or create the MaterialInfo for this face.
            MaterialInfo matInfo = new MaterialInfo(face, defaultTexture);
            if (matInfo.textureID != null
                        && matInfo.textureID != OMV.UUID.Zero
                        && matInfo.textureID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                // Textures/images use the UUID from OpenSim and the hash is just the hash of the UUID
                EntityHandleUUID textureHandle = new EntityHandleUUID((OMV.UUID)matInfo.textureID);
                BHash textureHash = new BHashULong(textureHandle.GetUUID().GetHashCode());
                ImageInfo lookupImageInfo = assetFetcher.GetImageInfo(textureHash, () => {
                    // The image is not in the cache yet so create an ImageInfo entry for it
                    ImageInfo imageInfo = new ImageInfo();
                    assetFetcher.FetchTextureAsImage(textureHandle)
                        .Then( img => {
                            imageInfo.SetImage(img);
                        });
                    imageInfo.handle = textureHandle;
                    return imageInfo;
                });

                // Update the UV information for the texture mapping
                m_mesher.TransformTexCoords(meshInfo.vertexs, meshInfo.faceCenter, face.TextureFace,  primScale);
            }

            MaterialInfo lookupMatInfo = assetFetcher.GetMaterialInfo(matInfo.GetHash(), () => { return matInfo; });
            rmesh.material = lookupMatInfo.handle;

            MeshInfo lookupMeshInfo = assetFetcher.GetMeshInfo(meshInfo.GetHash(), () => { return meshInfo; });
            rmesh.mesh = lookupMeshInfo.handle;

            // ConvOAR.Globals.log.DebugFormat("{0} ConvertFaceToRenderableMesh: rmesh.mesh={1}, rmesh.material={2}",
            //                 _logHeader, rmesh.mesh.GetUUID(), rmesh.material.GetUUID());

            return rmesh;
        }

        // Returns an ExtendedPrimGroup with a mesh for the passed heightmap.
        // Note that the returned EPG does not include any face information -- the caller must add a texture.
        public DisplayableRenderable MeshFromHeightMap( float[,] pHeightMap, int regionSizeX, int regionSizeY,
                    IAssetFetcher assetFetcher, OMV.Primitive.TextureEntryFace defaultTexture) {

            // OMVR.Face rawMesh = m_mesher.TerrainMesh(pHeightMap, 0, pHeightMap.GetLength(0)-1, 0, pHeightMap.GetLength(1)-1);
            ConvOAR.Globals.log.DebugFormat("{0} MeshFromHeightMap: heightmap=<{1},{2}>, regionSize=<{3},{4}>",
                    _logHeader, pHeightMap.GetLength(0), pHeightMap.GetLength(1), regionSizeX, regionSizeY);
            OMVR.Face rawMesh = ConvoarTerrain.TerrainMesh(pHeightMap, (float)regionSizeX, (float)regionSizeY);

            RenderableMesh rm = ConvertFaceToRenderableMesh(rawMesh, assetFetcher, defaultTexture, new OMV.Vector3(1, 1, 1));

            RenderableMeshGroup rmg = new RenderableMeshGroup();
            rmg.meshes.Add(rm);

            return rmg;
        }

        public void Dispose() {
            m_mesher = null;
        }

        // Walk through all the vertices and scale the included meshes
        public static void ScaleMeshes(MeshInfo meshInfo, OMV.Vector3 scale) {
            if (scale.X != 1.0 || scale.Y != 1.0 || scale.Z != 1.0) {
                for (int ii = 0; ii < meshInfo.vertexs.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[ii];
                    aVert.Position *= scale;
                    meshInfo.vertexs[ii] = aVert;
                }
            }
        }
        // Loop over all the vertices in an ExtendedPrim and perform some operation on them
        public delegate void OperateOnVertex(ref OMVR.Vertex vert);
        public static void OnAllVertex(MeshInfo mi, OperateOnVertex vertOp) {
            for (int jj = 0; jj < mi.vertexs.Count; jj++) {
                OMVR.Vertex aVert = mi.vertexs[jj];
                vertOp(ref aVert);
                mi.vertexs[jj] = aVert;
            }
        }

    }
}
