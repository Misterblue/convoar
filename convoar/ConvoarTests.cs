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
using System.Text;
using System.Threading.Tasks;

using org.herbal3d.convoar;

using NUnit.Framework;

namespace org.herbal3d.convoar.tests {

    [TestFixture]
    public class ConvoarTestCase {
        // Documentation on attributes: http://www.nunit.org/index.php?p=attributes&r=2.6.1
        // Documentation on assertions: http://www.nunit.org/index.php?p=assertions&r=2.6.1

        [SetUp]
        public virtual void Setup() {
        }
    }

    // =========================================================================================
    [TestFixture]
    public class ParameterParsing : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }
    }

    // =========================================================================================
    // Verify that terrain mesh is generated across the whole region area.
    [TestFixture]
    public class TerrainMeshGeneration : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }

        [TestCase]
        public void VerifyHeightmapMatchesMesh() {
        }

        [TestCase(100.0, 200.0)]
        public void VerifyMeshCoversWholeRegion(float pHeightmapSize, float pRegionSize) {
            int heightmapSize = (int)pHeightmapSize;
            int regionSize = (int)pRegionSize;
            float[,] heightMap = CreateHeightmap(heightmapSize);

            // ExtendedPrimGroup epg = assetMesher.MeshFromHeightMap(heightMap, regionSize, regionSize);
        }

        // Creates a heightmap of specificed size with a simple gradient from on corner to the
        //    opposite corner.
        private float[,] CreateHeightmap(int pHeightmapSize) {
            float[,] heightmap = new float[pHeightmapSize, pHeightmapSize];
            int xSize = heightmap.GetLength(0);
            int ySize = heightmap.GetLength(1);
            float xStep = 1.0f / (float)xSize;
            float yStep = 1.0f / (float)ySize;
            for (int xx = 0; xx < xSize; xx++) {
                for (int yy = 0; yy < ySize; yy++) {
                    heightmap[xx, yy] = (xStep * (float)xx) + (yStep * (float)yy);
                }
            }
            return heightmap;
        }
    }

    // =========================================================================================
    // Test whether the coordinates and child rotations happen properly when
    //    converting linksets.
    [TestFixture]
    public class BHasherTests : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }

        [TestCase]
        public void BHasherMdjb2TestParams() {
            BHasher hasher = new BHasherMdjb2();
            hasher.Add('c');
            hasher.Add((ushort)6);
            hasher.Add((int)12345);
            hasher.Add(-2345L);
            byte[] byt = Encoding.ASCII.GetBytes("This is a string");
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            System.Console.WriteLine("BHasher hash output = " + hash.ToString());
        }

        [TestCase("This is a string", Result = 12345)]
        [TestCase("A long string which is much longer than we are willing to test", Result = 12345)]
        [TestCase("A string witA one difference", Result = 12345)]
        [TestCase("A string witB one difference", Result = 12345)]
        [TestCase("A string witC one difference", Result = 12345)]
        public void BHasherMdjb2Test(string toHash) {
            BHasher hasher = new BHasherMdjb2();
            byte[] byt = Encoding.ASCII.GetBytes(toHash);
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            System.Console.WriteLine("BHasher hash output = " + hash.ToString());
        }

        [TestCase]
        public void BHasherMD5Test() {
        }

        [TestCase]
        public void BHasherSHA256Test() {
        }
    }

    // =========================================================================================
    // Test whether the coordinates and child rotations happen properly when
    //    converting linksets.
    [TestFixture]
    public class LinksetToEntity : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }
    }

}
