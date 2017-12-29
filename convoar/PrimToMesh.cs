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
using System.Linq;
using System.IO;
using System.Text;

using log4net;

using RSG;

using Assimp;

using OMV = OpenMetaverse;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace org.herbal3d.convoar {

    public class PrimToMesh {
        private OMVR.MeshmerizerR _mesher;
        String _logHeader = "[PrimToMesh]";

        public PrimToMesh() {
            _mesher = new OMVR.MeshmerizerR();
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
                        BConverterOS.LogBProgress("{0}: CreateMeshResource: creating mesh", _logHeader);
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
                        BConverterOS.LogBProgress("{0}: CreateMeshResource: creating sculpty", _logHeader);
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
                    BConverterOS.LogBProgress("{0}: CreateMeshResource: creating primshape", _logHeader);
                    ConvOAR.Globals.stats.numSimplePrims++;
                    MeshFromPrimShapeData(sog, sop, prim, assetFetcher, lod)
                        .Catch(e => {
                            prom.Reject(e);
                        })
                        .Then(dispable => {
                            BConverterOS.LogBProgress("{0} CreateMeshResource: prim created", _logHeader);
                            prom.Resolve(new Displayable(dispable, sop));
                        });
                }
            }
            catch (Exception e) {
                prom.Reject(e);
            }

            return prom;
        }

        private Promise<DisplayableRenderable> MeshFromPrimShapeData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {
            return new Promise<DisplayableRenderable>((resolve, reject) => {
                OMVR.FacetedMesh mesh = _mesher.GenerateFacetedMesh(prim, lod);
                DisplayableRenderable dr = ConvertFacetedMeshToDisplayable(assetFetcher, mesh, prim.Textures.DefaultTexture, prim.Scale);
                BHash drHash = dr.GetBHash();
                DisplayableRenderable realDR = assetFetcher.GetRenderable(drHash, () => { return dr; });
                BConverterOS.LogBProgress("{0} MeshFromPrimShapeData. numGenedMeshed={1}",
                        _logHeader, ((RenderableMeshGroup)realDR).meshes.Count);
                resolve(realDR);
            });
        }

        private Promise<DisplayableRenderable> MeshFromPrimSculptData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

            return new Promise<DisplayableRenderable>((resolve, reject) => {
                // Get the asset that the sculpty is built on
                EntityHandleUUID texHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
                assetFetcher.FetchTexture(texHandle)
                    .Catch((e) => {
                        ConvOAR.Globals.log.ErrorFormat("{0} MeshFromPrimSculptData: Rejected FetchTexture: {1}: {2}", _logHeader, texHandle, e);
                        reject(null);
                    })
                    .Then((bm) => {
                        OMVR.FacetedMesh fMesh = _mesher.GenerateFacetedSculptMesh(prim, bm.Image.ExportBitmap(), lod);
                        DisplayableRenderable dr =
                                ConvertFacetedMeshToDisplayable(assetFetcher, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                        BHash drHash = dr.GetBHash();
                        DisplayableRenderable realDR = assetFetcher.GetRenderable(drHash, () => { return dr; });
                        BConverterOS.LogBProgress("{0} MeshFromPrimSculptData. numFaces={1}, numGenedMeshed={2}",
                                _logHeader, fMesh.Faces.Count, ((RenderableMeshGroup)realDR).meshes.Count);
                        resolve(realDR);
                    });
            });
        }

        private Promise<DisplayableRenderable> MeshFromPrimMeshData(SceneObjectGroup sog, SceneObjectPart sop,
                                OMV.Primitive prim, IAssetFetcher assetFetcher, OMVR.DetailLevel lod) {

                EntityHandleUUID meshHandle = new EntityHandleUUID(prim.Sculpt.SculptTexture);
                return new Promise<DisplayableRenderable>((resolve, reject) => {
                    assetFetcher.FetchRawAsset(meshHandle)
                        .Catch(e => {
                            ConvOAR.Globals.log.ErrorFormat("{0} MeshFromPrimMeshData: exception: {1}", _logHeader, e);
                            reject(e);
                        })
                        .Then(meshBytes => {
                            // OMVA.AssetMesh meshAsset = new OMVA.AssetMesh(prim.ID, meshBytes);
                            // if (OMVR.FacetedMesh.TryDecodeFromAsset(prim, meshAsset, lod, out fMesh)) {
                            OMVR.FacetedMesh fMesh = null;
                            try
                            {
                                fMesh = _mesher.GenerateFacetedMeshMesh(prim, meshBytes);
                            }
                            catch (Exception e)
                            {
                                ConvOAR.Globals.log.ErrorFormat("{0} Exception in GenerateFacetedMeshMesh: {1}", _logHeader, e);
                            }
                            if (fMesh != null) {
                                DisplayableRenderable dr = ConvertFacetedMeshToDisplayable(assetFetcher, fMesh, prim.Textures.DefaultTexture, prim.Scale);
                                // Don't know the hash of the DisplayableRenderable until after it has been created.
                                // Now use the hash to see if this has already been done.
                                // If this DisplayableRenderable has already been built, use the other one and throw this away.
                                BHash drHash = dr.GetBHash();
                                DisplayableRenderable realDR = assetFetcher.GetRenderable(drHash, () => { return dr; });
                                resolve(realDR);
                            }
                            else {
                                reject(new Exception("MeshFromPrimMeshData: could not decode mesh information from asset. ID="
                                                + prim.ID.ToString()));
                            }
                        });
                });
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
            ret.meshes.AddRange(fmesh.Faces.Select(face => {
                return ConvertFaceToRenderableMesh(face, assetFetcher, defaultTexture, primScale);
            }));
            // ConvOAR.Globals.log.DebugFormat("{0} ConvertFacetedMeshToDisplayable: complete. numMeshes={1}", _logHeader, ret.meshes.Count);
            return ret;
        }

        private RenderableMesh ConvertFaceToRenderableMesh(OMVR.Face face, IAssetFetcher assetFetcher,
                        OMV.Primitive.TextureEntryFace defaultTexture, OMV.Vector3 primScale) {
            RenderableMesh rmesh = new RenderableMesh();
            rmesh.num = face.ID;

            // Copy one face's mesh imformation from the FacetedMesh into a MeshInfo
            MeshInfo meshInfo = new MeshInfo();
            meshInfo.vertexs = new List<OMVR.Vertex>(face.Vertices);
            meshInfo.indices = face.Indices.ConvertAll(ii => (int)ii);
            meshInfo.faceCenter = face.Center;
            meshInfo.scale = primScale;
            BConverterOS.LogBProgress("{0} ConvertFaceToRenderableMesh: faceId={1}, numVert={2}, numInd={3}",
                 _logHeader, face.ID, meshInfo.vertexs.Count, meshInfo.indices.Count);

            if (!ConvOAR.Globals.parms.P<bool>("DisplayTimeScaling")) {
                if (ScaleMeshes(meshInfo, primScale)) {
                    BConverterOS.LogBProgress("{0} ConvertFaceToRenderableMesh: scaled mesh to {1}",
                             _logHeader, primScale);
                }
                meshInfo.scale = OMV.Vector3.One;
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
                    // Note that image gets the same UUID as the OpenSim texture
                    ImageInfo imageInfo = new ImageInfo(textureHandle);
                    assetFetcher.FetchTextureAsImage(textureHandle)
                        .Catch( e => {
                            // Failure getting the image
                            ConvOAR.Globals.log.ErrorFormat("{0} Failure fetching texture. id={1}. {2}",
                                        _logHeader, matInfo.textureID, e);
                            // In this case, ImageInfo.image remains 'null' and everyone has to check if it's set.
                        })
                        .Then( img => {
                            imageInfo.SetImage(img);
                        });
                    imageInfo.imageIdentifier = (OMV.UUID)matInfo.textureID;
                    BConverterOS.LogBProgress("{0} ConvertFaceToRenderableMesh: create ImageInfo. hash={1}, id={2}",
                                    _logHeader, textureHash, imageInfo.handle);
                    return imageInfo;
                });
                matInfo.image = lookupImageInfo;

                // Update the UV information for the texture mapping
                BConverterOS.LogBProgress("{0} ConvertFaceToRenderableMesh: Converting tex coords using {1} texture",
                             _logHeader, face.TextureFace == null ? "default" : "face");
                _mesher.TransformTexCoords(meshInfo.vertexs, meshInfo.faceCenter,
                        face.TextureFace == null ? defaultTexture : face.TextureFace,  primScale);
            }

            // See that the material is in the cache
            MaterialInfo lookupMatInfo = assetFetcher.GetMaterialInfo(matInfo.GetBHash(), () => { return matInfo; });
            rmesh.material = lookupMatInfo;

            // See that the mesh is in the cache
            MeshInfo lookupMeshInfo = assetFetcher.GetMeshInfo(meshInfo.GetBHash(), () => { return meshInfo; });
            rmesh.mesh = lookupMeshInfo;

            BConverterOS.LogBProgress("{0} ConvertFaceToRenderableMesh: rmesh.mesh={1}, rmesh.material={2}",
                             _logHeader, rmesh.mesh, rmesh.material);

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

        // Walk through all the vertices and scale the included meshes
        // Returns 'true' of the mesh was changed.
        public static bool ScaleMeshes(MeshInfo meshInfo, OMV.Vector3 scale) {
            bool ret = false;
            if (scale.X != 1.0 || scale.Y != 1.0 || scale.Z != 1.0) {
                ret = true;
                for (int ii = 0; ii < meshInfo.vertexs.Count; ii++) {
                    OMVR.Vertex aVert = meshInfo.vertexs[ii];
                    aVert.Position *= scale;
                    meshInfo.vertexs[ii] = aVert;
                }
            }
            return ret;
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
