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
using System.IO;
using System.Text;
using System.Threading.Tasks;

using OMV = OpenMetaverse;

namespace org.herbal3d.convoar {
    // Some static routines for creating JSON output
    public class JSONHelpers {
        //====================================================================
        // Useful routines for creating the JSON output
        public static string ParamsToJSONArray(params Object[] vals) {
            StringBuilder buff = new StringBuilder();
            buff.Append(" [ ");
            bool first = true;
            foreach (object obj in vals) {
                if (!first) buff.Append(", ");
                buff.Append(obj.ToString());
                first = false;
            }
            buff.Append(" ] ");
            return buff.ToString();
        }

        public static string ArrayToJSONArray(float[] vals) {
            StringBuilder buff = new StringBuilder();
            buff.Append(" [ ");
            bool first = true;
            foreach (object obj in vals) {
                if (!first) buff.Append(", ");
                buff.Append(obj.ToString());
                first = false;
            }
            buff.Append(" ]");
            return buff.ToString();
        }

        public static string Indent(int level) {
            return "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t".Substring(0, level);
        }

        // Used to output lines of JSON values. Used in the pattern:
        //    public void ToJSON(StreamWriter outt, int level) {
        //        outt.Write(" { ");
        //        bool first = true;
        //        foreach (KeyValuePair<string, Object> kvp in this) {
        //            first = WriteJSONValueLine(outt, level, first, kvp.Key, kvp.Value);
        //        }
        //        outt.Write("\n" + GltfClass.Indent(level) + "}\n");
        //    }
        public static void WriteJSONValueLine(StreamWriter outt, int level, ref bool first, string key, Object val) {
            if (val != null) {
                WriteJSONLineEnding(outt, ref first);
                outt.Write(JSONHelpers.Indent(level) + "\"" + key + "\": " + CreateJSONValue(val));
            }
        }

        // Used to end the last line of output JSON. If there was something before, a comma is needed
        public static void WriteJSONLineEnding(StreamWriter outt, ref bool first) {
            if (first)
                outt.Write("\n");
            else
                outt.Write(",\n");
            first = false;
        }

        // Examines passed object and creates the correct form of a JSON value.
        // Strings are closed in quotes, arrays get square bracketed, and numbers are stringified.
        public static string CreateJSONValue(Object val) {
            string ret = String.Empty;
            if (val is string) {
                ret = "\"" + val + "\""; 
            }
            else if (val is bool) {
                ret = (bool)val ? "true" : "false";
            }
            else if (val is OMV.Color4) {
                OMV.Color4 col = (OMV.Color4)val;
                ret = ParamsToJSONArray(col.R, col.G, col.B, col.A);
            }
            else if (val is OMV.Matrix4) {
                OMV.Matrix4 mat = (OMV.Matrix4)val;
                ret = ParamsToJSONArray(
                    mat[0,0], mat[0,1], mat[0,2], mat[0,3],
                    mat[1,0], mat[1,1], mat[1,2], mat[1,3],
                    mat[2,0], mat[2,1], mat[2,2], mat[2,3],
                    mat[3,0], mat[3,1], mat[3,2], mat[3,3]
                );
            }
            else if (val is OMV.Vector3) {
                OMV.Vector3 vect = (OMV.Vector3)val;
                ret = ParamsToJSONArray(vect.X, vect.Y, vect.Z);
            }
            else if (val is OMV.Quaternion) {
                OMV.Quaternion quan = (OMV.Quaternion)val;
                ret = ParamsToJSONArray(quan.X, quan.Y, quan.Z, quan.W);
            }
            else if (val.GetType().IsArray) {
                ret = " [ ";
                object[] values = (object[])val;
                bool first = true;
                for (int ii = 0; ii < values.Length; ii++) {
                    if (!first) ret += ",";
                    first = false;
                    ret += CreateJSONValue(values[ii]);
                }
                ret += " ]";
            }
            else {
                ret = val.ToString();
            }
            return ret;
        }
    }
}
