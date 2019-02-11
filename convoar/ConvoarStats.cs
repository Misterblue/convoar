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
using System.Text;
using System.Collections.Generic;

using log4net;

using OpenSim.Region.Framework.Scenes;

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

namespace org.herbal3d.convoar {
    public class ConvoarStats {

        public int numSimplePrims = 0;
        public int numMeshAssets = 0;
        public int numSculpties = 0;


        public Scene m_scene;
    #pragma warning disable 414
        private static readonly string _logHeader = "[Stats]";
    #pragma warning restore 414

        public ConvoarStats() {
        }

        /*  TODO: figure out what statistics are wanted
        // Gather statistics
        public void ExtractStatistics(BasilModule.ReorganizedScene reorgScene) {
            staticStats = StatsFromEntityGroupList("static", reorgScene.staticEntities);
            nonStaticStats = StatsFromEntityGroupList("nonStatic", reorgScene.nonStaticEntities);
            rebuiltStats = StatsFromEntityGroupList("rebuilt", reorgScene.rebuiltFaceEntities);
            rebuiltNonStaticStats = StatsFromEntityGroupList("rebuiltNonStatic", reorgScene.rebuiltNonStaticEntities);
        }
        */

        /*
        public class EntityGroupStats {
            public int numEntities = 0;
            public int numMeshes = 0;
            public int numLinksets = 0;
            public int numIndices = 0;
            public int numVertices = 0;
            public int numMaterials = 0;
            public int numTextures = 0;
        }

        EntityGroupStats staticStats = null;
        EntityGroupStats nonStaticStats = null;
        EntityGroupStats rebuiltStats = null;
        EntityGroupStats rebuiltNonStaticStats = null;


        public EntityGroupStats StatsFromEntityGroupList(string listName, EntityGroupList entityList) {
            EntityGroupStats egs = new EntityGroupStats();
            try {
                List<OMV.Primitive.TextureEntryFace> TEFs = new List<OMV.Primitive.TextureEntryFace>();
                List<OMV.UUID> TEXs = new List<OMV.UUID>();
                egs.numEntities = entityList.Count;
                entityList.ForEach(entity => {
                    if (entity.Count > 1) {
                        egs.numLinksets++;
                    }
                    entity.ForEach(epg => {
                        var ep = epg.primaryExtendePrim;
                        egs.numMeshes += ep.faces.Count;
                        foreach (FaceInfo fi in ep.faces) {
                            egs.numIndices += fi.indices.Count;
                            egs.numVertices += fi.vertexs.Count;
                            if (!TEFs.Contains(fi.textureEntry)) {
                                TEFs.Add(fi.textureEntry);
                            }
                            if (fi.textureID != null && !TEXs.Contains((OMV.UUID)fi.textureID)) {
                                TEXs.Add((OMV.UUID)fi.textureID);
                            }
                        }
                    });
                });
                egs.numMaterials = TEFs.Count;
                egs.numTextures = TEXs.Count;
            }
            catch (Exception e) {
                m_log.ErrorFormat("{0}: Exception computing {1} stats: {2}", "StatsFromEntityGroupList", listName, e);
            }

            return egs;
        }


        // Output the non entitiy list info
        public void Log(string header) {
            m_log.DebugFormat("{0} numSimplePrims={1}", header, numSimplePrims);
            m_log.DebugFormat("{0} numSculpties={1}", header, numSculpties);
            m_log.DebugFormat("{0} numMeshAssets={1}", header, numMeshAssets);
        }

        public void Log(EntityGroupStats stats, string header) {
            m_log.DebugFormat("{0} numEntities={1}", header, stats.numEntities);
            m_log.DebugFormat("{0} numMeshes={1}", header, stats.numMeshes);
            m_log.DebugFormat("{0} numLinksets={1}", header, stats.numLinksets);
            m_log.DebugFormat("{0} numIndices={1}", header, stats.numIndices);
            m_log.DebugFormat("{0} numVertices={1}", header, stats.numVertices);
            m_log.DebugFormat("{0} numMaterials={1}", header, stats.numMaterials);
            m_log.DebugFormat("{0} numTextures={1}", header, stats.numTextures);
        }

        public void LogAll(string header) {
            Log(header + " " + m_scene.Name);

            if (staticStats != null) {
                Log(staticStats, header + " " + m_scene.Name + " static");
            }
            if (nonStaticStats != null) {
                Log(nonStaticStats, header + " " + m_scene.Name + " nonStatic");
            }
            if (rebuiltStats != null) {
                Log(rebuiltStats, header + " " + m_scene.Name + " rebuilt");
            }
            if (rebuiltNonStaticStats != null) {
                Log(rebuiltNonStaticStats, header + " " + m_scene.Name + " rebuiltNonStatic");
            }
        }

        private const int indentStep = 2;
        private string MI(int indent) { // short for "MakeIndent"
            return _logHeader + "                                                                                          ".Substring(0, indent * indentStep);
        }
        public void DumpDetailed(EntityGroupList egl) {
            DumpDetailed(1, egl);
        }
        public void DumpDetailed(int indent, EntityGroupList egl) {
            m_log.Debug(MI(indent) + "EntityGroupList. Num entities=" + egl.Count);
            egl.ForEach(eg => {
                DumpDetailed(indent+1, eg);
            });
        }
        public void DumpDetailed(int indent, EntityGroup eg) {
            m_log.Debug(MI(indent) + "EntityGroup. NumExtendedPrims=" + eg.Count);
            eg.ForEach(epg => {
                DumpDetailed(indent+1, epg);
            });
        }
        public void DumpDetailed(int indent, ExtendedPrimGroup epg) {
            m_log.Debug(MI(indent) + "ExtendedPrimGroup");
            foreach (PrimGroupType lodKey in epg.Keys) {
                DumpDetailed(indent+1, lodKey, epg[lodKey]);
            }
        }
        public void DumpDetailed(int indent, PrimGroupType lodKey, ExtendedPrim ep) {
            m_log.Debug(MI(indent) + "LOD=" + lodKey);
            DumpDetailed(indent+1, ep);
        }
        public void DumpDetailed(int indent, ExtendedPrim ep) {
            var name = ep.fromOS.SOG == null ? "TERRAIN" : ep.fromOS.SOG.Name;
            m_log.Debug(MI(indent) + "name=" + name);
            StringBuilder buff = new StringBuilder();
            buff.Append(MI(indent) + "faces=" + ep.faces.Count);
            foreach (FaceInfo face in ep.faces) {
                buff.Append("(");
                buff.Append(face.vertexs.Count.ToString());
                buff.Append("/");
                buff.Append(face.indices.Count.ToString());
                buff.Append("/");
                var hash = face.textureEntry == null ? 0 : face.GetTextureHash();
                buff.Append(hash.ToString());
                if (face.textureID != null 
                        && face.textureID != OMV.UUID.Zero
                        && face.textureID != OMV.Primitive.TextureEntry.WHITE_TEXTURE) {
                    buff.Append("T");
                }
                buff.Append(") ");
            }
            m_log.Debug(buff.ToString());
        }
    */
    }
}
