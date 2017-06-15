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
using System.Linq;
using System.Drawing;
using System.Text;

using log4net;

using OpenSim.Region.CoreModules.World.LegacyMap;

using OMV = OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OMVS = OpenMetaverse.StructuredData;
using OMVA = OpenMetaverse.Assets;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.convoar {
    public class BasilTerrain {

        private static string LogHeader = "BasilTerrain";

        // Create a mesh for the terrain of the current scene
        public static EntityGroup CreateTerrainMesh(GlobalContext context,
                            Scene scene,
                            PrimToMesh assetMesher, IAssetFetcher assetFetcher) {

            ITerrainChannel terrainDef = scene.Heightmap;
            int XSize = terrainDef.Width;
            int YSize = terrainDef.Height;

            float[,] heightMap = new float[XSize, YSize];
            if (context.parms.HalfRezTerrain) {
                context.log.LogDebug("{0}: CreateTerrainMesh. creating half sized terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
                // Half resolution mesh that approximates the heightmap
                heightMap = new float[XSize/2, YSize/2];
                for (int xx = 0; xx < XSize; xx += 2) {
                    for (int yy = 0; yy < YSize; yy += 2) {
                        float here = terrainDef.GetHeightAtXYZ(xx+0, yy+0, 26);
                        float ln = terrainDef.GetHeightAtXYZ(xx+1, yy+0, 26);
                        float ll = terrainDef.GetHeightAtXYZ(xx+0, yy+1, 26);
                        float lr = terrainDef.GetHeightAtXYZ(xx+1, yy+1, 26);
                        heightMap[xx/2, yy/2] = (here + ln + ll + lr) / 4;
                    }
                }
            }
            else {
                context.log.LogDebug("{0}: CreateTerrainMesh. creating terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
                for (int xx = 0; xx < XSize; xx++) {
                    for (int yy = 0; yy < YSize; yy++) {
                        heightMap[xx, yy] = terrainDef.GetHeightAtXYZ(xx, yy, 26);
                    }
                }
            }

            context.log.LogDebug("{0}: CreateTerrainMesh. calling MeshFromHeightMap", LogHeader);
            ExtendedPrimGroup epg = assetMesher.MeshFromHeightMap(heightMap,
                            terrainDef.Width, terrainDef.Height);

            // Number found in RegionSettings.cs as DEFAULT_TERRAIN_TEXTURE_3
            OMV.UUID defaultTextureID = new OMV.UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
            OMV.Primitive.TextureEntry te = new OMV.Primitive.TextureEntry(defaultTextureID);

            if (context.parms.CreateTerrainSplat) {
                // Use the OpenSim maptile generator to create a texture for the terrain
                var terrainRenderer = new TexturedMapTileRenderer();
                terrainRenderer.Initialise(scene, null);    // doesn't use config param

                var mapbmp = new Bitmap(terrainDef.Width, terrainDef.Height,
                                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                terrainRenderer.TerrainToBitmap(mapbmp);

                // The built terrain mesh will have one face in the mesh
                OMVR.Face aFace = epg.primaryExtendePrim.fromOS.facetedMesh.Faces.First();
                FaceInfo fi = new FaceInfo(0, epg.primaryExtendePrim, aFace, te.CreateFace(0));
                fi.textureID = OMV.UUID.Random();
                fi.faceImage = mapbmp;
                fi.hasAlpha = false;
                fi.persist = new BasilPersist(Gltf.MakeAssetURITypeImage, fi.textureID.ToString(), context);
                epg.primaryExtendePrim.faces.Add(fi);
            }
            else {
                // Fabricate a texture
                // The built terrain mesh will have one face in the mesh
                OMVR.Face aFace = epg.primaryExtendePrim.fromOS.facetedMesh.Faces.First();
                FaceInfo fi = new FaceInfo(0, epg.primaryExtendePrim, aFace, te.CreateFace(0));
                fi.textureID = defaultTextureID;
                assetFetcher.FetchTextureAsImage(new EntityHandle(defaultTextureID))
                    .Catch(e => {
                        context.log.LogError("{0} CreateTerrainMesh: unable to fetch default terrain texture: id={1}: {2}",
                                    LogHeader, defaultTextureID, e);
                    })
                    .Then(theImage => {
                        // This will happen later so hopefully soon enough for anyone using the image
                        fi.faceImage = theImage;
                    });
                fi.hasAlpha = false;
                epg.primaryExtendePrim.faces.Add(fi);
            }

            EntityGroup eg = new EntityGroup();
            eg.Add(epg);

            return eg;
        }

        // A structure to hold vertex information that also includes the index for building indices.
        private struct Vert : IEquatable<Vert>{
            public OMV.Vector3 Position;
            public OMV.Vector3 Normal;
            public OMV.Vector2 TexCoord;
            public uint index;
            // Methods so this will work in a Dictionary
            public override int GetHashCode() {
                int hash = Position.GetHashCode();
                hash = hash * 31 + Normal.GetHashCode();
                hash = hash * 31 + TexCoord.GetHashCode();
                return hash;
            }
            public bool Equals(Vert other) {
                return Position == other.Position
                    && Normal == other.Normal
                    && TexCoord == other.TexCoord;
            }
        }

        // PrimMesher has a terrain mesh generator but it doesn't compute normals.
        // TODO: Optimize by removing vertices that are just mid points.
        //    Having a vertex for every height is very inefficient especially for flat areas.
        public static OMVR.Face TerrainMesh(float[,] heights, float realSizeX, float realSizeY, ILog log) {

            List<ushort> indices = new List<ushort>();

            int sizeX = heights.GetLength(0);
            int sizeY = heights.GetLength(1);

            // build the vertices in an array for computing normals and eventually for
            //    optimizations.
            Vert[,] vertices = new Vert[sizeX, sizeY];

            float stepX = (realSizeX+1f) / (float)sizeX;    // the real dimension step for each heightmap step
            float stepY = (realSizeY+1f) / (float)sizeY;
            float coordStepX = 1.0f / (float)sizeX;    // the coordinate dimension step for each heightmap step
            float coordStepY = 1.0f / (float)sizeY;

            uint index = 0;
            for (int xx = 0; xx < sizeX; xx++) {
                for (int yy = 0; yy < sizeY; yy++) {
                    Vert vert = new Vert();
                    vert.Position = new OMV.Vector3(stepX * xx, stepY * yy, heights[xx, yy]);
                    vert.Normal = new OMV.Vector3(0f, 1f, 0f);  // normal pointing up for the moment
                    vert.TexCoord = new OMV.Vector2(coordStepX * xx, coordStepY * yy);
                    vert.index = index++;
                    vertices[xx, yy] = vert;
                }
            }

            // Compute the normals
            // Take three corners of each quad and calculate the normal for the vector
            //   a--b--e--...
            //   |  |  |
            //   d--c--h--...
            // The triangle a-b-d calculates the normal for a, etc
            for (int xx = 0; xx < sizeX-1; xx++) {
                for (int yy = 0; yy < sizeY-1; yy++) {
                    vertices[xx,yy].Normal = MakeNormal(vertices[xx, yy], vertices[xx + 1, yy], vertices[xx, yy + 1]);
                }
            }
            // The vertices along the edges need an extra pass to compute the normals
            for (int xx = 0; xx < sizeX-1 ; xx++) {
                vertices[xx, sizeY - 1].Normal = MakeNormal(vertices[xx, sizeY - 1], vertices[xx + 1, sizeY - 1], vertices[xx, sizeY - 2]);
            }
            for (int yy = 0; yy < sizeY - 1; yy++) {
                vertices[sizeX -1, yy].Normal = MakeNormal(vertices[sizeX -1 , yy], vertices[sizeX - 1, yy + 1], vertices[sizeX - 2, yy]);
            }
            vertices[sizeX -1, sizeY - 1].Normal = MakeNormal(vertices[sizeX -1 , sizeY - 1], vertices[sizeX - 2, sizeY - 1], vertices[sizeX - 1, sizeY - 2]);

            // Convert our vertices into the format expected by the caller
            List<OMVR.Vertex> vertexList = new List<OMVR.Vertex>();
            for (int xx = 0; xx < sizeX; xx++) {
                for (int yy = 0; yy < sizeY; yy++) {
                    Vert vert = vertices[xx, yy];
                    OMVR.Vertex oVert = new OMVR.Vertex();
                    oVert.Position = vert.Position;
                    oVert.Normal = vert.Normal;
                    oVert.TexCoord = vert.TexCoord;
                    vertexList.Add(oVert);
                }
            }

            // Make indices for all the vertices.
            // Pass over the matrix and create two triangles for each quad
            //
            //   00-----01
            //   | f1  /|
            //   |   /  |
            //   | / f2 |
            //   10-----11
            //
            // Counter Clockwise
            for (int xx = 0; xx < sizeX - 1; xx++) {
                for (int yy = 0; yy < sizeY - 1; yy++) {
                    indices.Add((ushort)vertices[xx + 0, yy + 0].index);
                    indices.Add((ushort)vertices[xx + 1, yy + 0].index);
                    indices.Add((ushort)vertices[xx + 0, yy + 1].index);
                    indices.Add((ushort)vertices[xx + 0, yy + 1].index);
                    indices.Add((ushort)vertices[xx + 1, yy + 0].index);
                    indices.Add((ushort)vertices[xx + 1, yy + 1].index);
                }
            }

            OMVR.Face aface = new OMVR.Face();
            aface.Vertices = vertexList;
            aface.Indices = indices;
            return aface;
        }

        // Given a root (aa) and two adjacent vertices (bb, cc), computer the normal for aa
        private static OMV.Vector3 MakeNormal(Vert aa, Vert bb, Vert cc) {
            OMV.Vector3 mm = aa.Position - bb.Position;
            OMV.Vector3 nn = aa.Position - cc.Position;
            OMV.Vector3 theNormal = OMV.Vector3.Cross(mm, nn);
            theNormal.Normalize();
            return theNormal;
        }
    }
}
