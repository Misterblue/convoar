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

namespace org.herbal3d.convoar {
    // World coordinates.
    // Type double so it can be anywhere on the globe.
    public class BCoordinateBase<T> {
        public T x;
        public T y;
        public T z;
        public CoordSystem coordSystem;
        public RotationSystem rotSystem;
        public CoordAxis coordAxis;
    }

    public class BCoordinate : BCoordinateBase<Double> {
        public BCoordinate() : base() {
            x = 0;
            y = 0;
            z = 0;
            coordSystem = CoordSystem.WSG86;
            rotSystem = RotationSystem.WORLD;
            coordAxis = new CoordAxis(CoordAxis.RightHand_Yup);
        }
    }

    public class BCoordinateF : BCoordinateBase<float> {
        public BCoordinateF() : base() {
            x = 0;
            y = 0;
            z = 0;
            coordSystem = CoordSystem.WSG86;
            rotSystem = RotationSystem.WORLD;
            coordAxis = new CoordAxis(CoordAxis.RightHand_Yup);
        }
    }

    public enum CoordSystem {
        WSG86 = 1,  // WSG86 earth coordinates
        FOR,        // coordiates relative to current frame of reference
        CAMERA,     // camera relative coords (-1..1 range, zero center)
        CAMERAABS,  // absolute coords relative to camera position (zero center)
        VIRTUAL,    // zero-based, unrooted coordinates
        MOON,       // moon coordinates
        MARS,       // mars coordinates
        REL1,       // mutually agreed base coordinates
        REL2,
        REL3
    }

    public enum RotationSystem {
        WORLD = 1,  // rotation relative to world coordinates
        FOR,        // rotation relative to current frame of reference
        CAMERA      // rotation relative to camera direction
    }


    // Capturing the processing of the coordinate system for a mesh
    public class CoordAxis {
        private static string _logHeader = "[CoordAxis]";

        public const int Handedness = 0x200;    // bit that specifies the handedness
        public const int UpDimension = 0x00F;   // field that specifies the up dimension
        public const int UVOrigin = 0x030;      // field that specifies UV origin location
        public const int RightHand = 0x000;
        public const int LeftHand = 0x200;
        public const int Yup = 0x001;
        public const int Zup = 0x002;
        public const int UVOriginUpperLeft = 0x000; // most of the world has origin in upper left
        public const int UVOriginLowerLeft = 0x010; // OpenGL specifies UV origin in lower left
        public const int RightHand_Yup = RightHand + Yup;
        public const int LeftHand_Yup = LeftHand + Yup;
        public const int RightHand_Zup = RightHand + Zup;
        public const int LeftHand_Zup = LeftHand + Zup;
        // RightHand_Zup: SL
        // RightHand_Yup: OpenGL
        // LeftHand_Yup: DirectX, Babylon, Unity

        public int system;
        public CoordAxis() {
            system = RightHand_Zup; // default to SL
        }
        public CoordAxis(int initCoord) {
            system = initCoord;
        }
        public int getUpDimension { get  { return system & UpDimension; } }
        public int getHandedness { get  { return system & Handedness; } }
        public int getUVOrigin { get  { return system & UVOrigin; } }
        public bool isHandednessChanging(CoordAxis nextSystem) {
            return (system & Handedness) != (nextSystem.system & Handedness);
        }
        public string SystemName { get { return SystemNames[system]; } }
        public static Dictionary<int, string> SystemNames = new Dictionary<int, string>() {
            { RightHand_Yup, "RightHand,Y-up" },
            { RightHand_Zup, "RightHand,Z-up" },
            { LeftHand_Yup, "LeftHand,Y-up" },
            { LeftHand_Zup, "LeftHand,Z-up" }
        };
        public CoordAxis clone() {
            return new CoordAxis(this.system);
        }
        // Convert the positions and all the vertices in an ExtendedPrim from one
        //     coordinate space to another. ExtendedPrim.coordSpace gives the current
        //     coordinates and we specify a new one here.
        // This is not a general solution -- it pretty much only works to convert
        //     right-handed,Z-up coordinates (OpenSimulator) to right-handed,Y-up
        //     (OpenGL).
        public static void FixCoordinates(BInstance inst, CoordAxis newCoords) {
            // true if need to flip the V in UV (origin from top left to bottom left)
            bool flipV = false;

            if (inst.coordAxis.system != newCoords.system) {

                OMV.Matrix4 coordTransform = OMV.Matrix4.Identity;
                OMV.Quaternion coordTransformQ = OMV.Quaternion.Identity;
                if (inst.coordAxis.getUpDimension == CoordAxis.Zup
                    && newCoords.getUpDimension == CoordAxis.Yup) {
                    // The one thing we know to do is change from Zup to Yup
                    coordTransformQ = OMV.Quaternion.CreateFromAxisAngle(1.0f, 0.0f, 0.0f, -(float)Math.PI / 2f);
                    // Make a clean matrix version.
                    // The libraries tend to create matrices with small numbers (1.119093e-07) for zero.
                    coordTransform = new OMV.Matrix4(
                                    1, 0, 0, 0,
                                    0, 0, -1, 0,
                                    0, 1, 0, 0,
                                    0, 0, 0, 1);
                }
                if (inst.coordAxis.getUVOrigin != newCoords.getUVOrigin) {
                    flipV = true;
                }

                OMV.Vector3 oldPos = inst.Position;   // DEBUG DEBUG
                OMV.Quaternion oldRot = inst.Rotation;   // DEBUG DEBUG
                // Fix the location in space
                inst.Position = inst.Position * coordTransformQ;
                inst.Rotation = coordTransformQ * inst.Rotation;

                ConvOAR.Globals.log.DebugFormat("{0} FixCoordinates. dispID={1}, oldPos={2}, newPos={3}, oldRot={4}, newRot={5}",
                    _logHeader, inst.handle, oldPos, inst.Position, oldRot, inst.Rotation);

                // Go through all the vertices and change the UV coords if necessary
                if (flipV) {
                    // TODO: Is this needed?
                    // PrimToMesh.OnAllVertex(ep, delegate (ref OMVR.Vertex vert) {
                    //     vert.TexCoord.Y = 1f - vert.TexCoord.Y;
                    // });
                }

                inst.coordAxis = newCoords;
            }
            else {
                ConvOAR.Globals.log.DebugFormat("FixCoordinates. Not converting coord system. dispID={0}", inst.handle);
            }
        }

    }
}
