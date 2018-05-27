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
    public class ConvoarTerrain {

        private static string LogHeader = "ConvoarTerrain";

        // Create a mesh for the terrain of the current scene
        public static BInstance CreateTerrainMesh(
                            Scene scene,
                            PrimToMesh assetMesher, IAssetFetcher assetFetcher) {

            ITerrainChannel terrainDef = scene.Heightmap;
            int XSize = terrainDef.Width;
            int YSize = terrainDef.Height;

            float[,] heightMap = new float[XSize, YSize];
            if (ConvOAR.Globals.parms.P<bool>("HalfRezTerrain")) {
                ConvOAR.Globals.log.DebugFormat("{0}: CreateTerrainMesh. creating half sized terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
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
                ConvOAR.Globals.log.DebugFormat("{0}: CreateTerrainMesh. creating terrain sized <{1},{2}>", LogHeader, XSize/2, YSize/2);
                for (int xx = 0; xx < XSize; xx++) {
                    for (int yy = 0; yy < YSize; yy++) {
                        heightMap[xx, yy] = terrainDef.GetHeightAtXYZ(xx, yy, 26);
                    }
                }
            }

            // Number found in RegionSettings.cs as DEFAULT_TERRAIN_TEXTURE_3
            OMV.UUID convoarID = new OMV.UUID(ConvOAR.Globals.parms.P<string>("ConvoarID"));

            OMV.UUID defaultTextureID = new OMV.UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
            OMV.Primitive.TextureEntryFace terrainFace = new OMV.Primitive.TextureEntryFace(null);
            terrainFace.TextureID = defaultTextureID;

            EntityHandleUUID terrainTextureHandle = new EntityHandleUUID();
            MaterialInfo terrainMaterialInfo = new MaterialInfo(terrainFace);

            if (ConvOAR.Globals.parms.P<bool>("CreateTerrainSplat")) {
                // Use the OpenSim maptile generator to create a texture for the terrain
                var terrainRenderer = new TexturedMapTileRenderer();
                Nini.Config.IConfigSource config = new Nini.Config.IniConfigSource();
                terrainRenderer.Initialise(scene, config);

                var mapbmp = new Bitmap(terrainDef.Width, terrainDef.Height,
                                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                terrainRenderer.TerrainToBitmap(mapbmp);

                // Place the newly created image into the Displayable caches
                ImageInfo terrainImageInfo = new ImageInfo();
                terrainImageInfo.handle = terrainTextureHandle;
                terrainImageInfo.image = mapbmp;
                terrainImageInfo.resizable = false; // terrain image resolution is not reduced
                assetFetcher.Images.Add(new BHashULong(terrainTextureHandle.GetHashCode()), terrainTextureHandle, terrainImageInfo);
                // Store the new image into the asset system so it can be read later.
                assetFetcher.StoreTextureImage(terrainTextureHandle, scene.Name + " Terrain", convoarID, mapbmp);
                // Link this image to the material
                terrainFace.TextureID = terrainTextureHandle.GetUUID();
            }
            else {
                // Use the default texture code for terrain
                terrainTextureHandle = new EntityHandleUUID(defaultTextureID);
                BHash terrainHash = new BHashULong(defaultTextureID.GetHashCode());
                assetFetcher.GetImageInfo(terrainHash, () => {
                    ImageInfo terrainImageInfo = new ImageInfo();
                    terrainImageInfo.handle = terrainTextureHandle;
                    assetFetcher.FetchTextureAsImage(terrainTextureHandle)
                    .Then(img => {
                        terrainImageInfo.image = img;
                    });
                    return terrainImageInfo;
                });
            }

            // The above has created a MaterialInfo for the terrain texture

            ConvOAR.Globals.log.DebugFormat("{0}: CreateTerrainMesh. calling MeshFromHeightMap", LogHeader);
            DisplayableRenderable terrainDisplayable = assetMesher.MeshFromHeightMap(heightMap,
                            terrainDef.Width, terrainDef.Height, assetFetcher, terrainFace);

            BInstance terrainInstance = new BInstance();
            Displayable terrainDisp = new Displayable(terrainDisplayable);
            terrainDisp.name = "Terrain";
            terrainDisp.baseUUID = OMV.UUID.Random();
            terrainInstance.Representation = terrainDisp;

            return terrainInstance;
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
        public static OMVR.Face TerrainMesh(float[,] heights, float realSizeX, float realSizeY) {

            List<ushort> indices = new List<ushort>();

            int sizeX = heights.GetLength(0);
            int sizeY = heights.GetLength(1);

            // build the vertices in an array for computing normals and eventually for
            //    optimizations.
            Vert[,] vertices = new Vert[sizeX, sizeY];

            float stepX = (realSizeX) / (float)sizeX;    // the real dimension step for each heightmap step
            float stepY = (realSizeY) / (float)sizeY;
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
            // Pass over the far edges and make sure the mesh streaches the whole area
            for (int xx = 0; xx < sizeX; xx++) {
                vertices[xx, sizeY - 1].Position.Y = realSizeY + 1;
            }
            for (int yy = 0; yy < sizeY; yy++) {
                vertices[sizeX - 1, yy].Position.X = realSizeY + 1;
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
