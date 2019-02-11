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

using OpenSim.Region.Framework.Scenes;

using org.herbal3d.cs.CommonEntitiesUtil;
using org.herbal3d.cs.os.CommonEntities;

using OMV = OpenMetaverse;
using OMVR = OpenMetaverse.Rendering;

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

        ConvoarParams _params;
        BLogger _log;

        [TestFixtureSetUp]
        public void Init() {
            _log = new LoggerConsole();
            _params = new ConvoarParams(_log);
            ConvOAR.Globals = new GlobalContext() {
                log = _log,
                parms = _params
            };
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }

        [TestCase]
        public void ProcessArgsParameter() {
            bool oldExportTextures = _params.P<bool>("ExportTextures");
            string inputOARFileParameterName = "InputOAR";
            string inputOARFile = "AnOARFileToRead.oar";
            string outputDirectory = "this/that";
            string[] args = new string[] {
                "-d", outputDirectory,
                "--exporttextures",
                "--preferredTextureFormat", "GIF",
                "--mergeStaticMeshes",
                "--verticesmaxForBuffer", "1234",
                inputOARFile
            };
            _params.SetParameterValue("ExportTextures", "false");
            _params.SetParameterValue("MergeStaticMeshes", "false");

            Exception exceptionCode = null;
            try {
                _params.MergeCommandLine(args, null, inputOARFileParameterName);
            }
            catch (Exception e) {
                exceptionCode = e;
            }

            if (exceptionCode != null) {
                Assert.Fail("Exception merging parameters: " + exceptionCode.ToString());
            }
            else {
                Assert.AreEqual(outputDirectory, _params.P<string>("OutputDir"), "Output directory specification short form was not set");
                Assert.AreEqual(true, _params.P<bool>("ExportTextures"), "ExportTextures was not parameterized properly");
                Assert.AreEqual("GIF", _params.P<string>("PreferredTextureFormat"), "Preferred texture format was not set");
                Assert.AreEqual(true, _params.P<bool>("MergeStaticMeshes"), "MergeStaticMeshes was not set");
                Assert.AreEqual(1234, _params.P<int>("VerticesMaxForBuffer"), "VerticesMaxForBuffer was not set");
                Assert.AreEqual(inputOARFile, _params.P<string>("InputOAR"), "The trailing filename was not set");
            }
        }

        [TestCase]
        public void ProcessNoParameter() {
            string outputDirectory = "this/that";
            string[] args = new string[] {
                "-d", outputDirectory,
                "--noExportTextures",
                "--preferredTextureFormat", "GIF",
            };
            _params.SetParameterValue("ExportTextures", "true");

            Exception exceptionCode = null;
            try {
                _params.MergeCommandLine(args);
            }
            catch (Exception e) {
                exceptionCode = e;
            }

            if (exceptionCode != null) {
                Assert.Fail("Exception merging parameters: " + exceptionCode.ToString());
            }
            else {
                Assert.AreEqual(false, _params.P<bool>("ExportTextures"), "ExportTextures was not set to false");
            }
        }
    }

    // =========================================================================================
    // Verify that terrain mesh is generated across the whole region area.
    [TestFixture]
    public class TerrainMeshGeneration : ConvoarTestCase {

        ConvoarParams _params;
        BLogger _log;
        MemAssetService _assetService;
        private AssetManager _assetManager = null;
        private OarConverter _converter = null;
        private OMV.Primitive.TextureEntryFace _defaultTexture = null;
        Scene _scene;

        [TestFixtureSetUp]
        public void Init() {
            _log = new LoggerConsole();
            _params = new ConvoarParams(_log);
            ConvOAR.Globals = new GlobalContext() {
                log = _log,
                parms = _params
            };
            _assetService = new MemAssetService();
            _converter = new OarConverter(_log, _params);
            _scene = _converter.CreateScene(_assetService, "convoar-test");
            _assetManager = new OSAssetFetcher(_assetService, _log, _params);
            OMV.UUID defaultTextureID = new OMV.UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
            _defaultTexture = new OMV.Primitive.TextureEntryFace(null) {
                TextureID = defaultTextureID
            };
        }

        [TestFixtureTearDown]
        public void TearDown() {
            _scene.Close();
            _assetManager.Dispose();
            _assetService.Dispose();
        }

        [TestCase]
        public void VerifyHeightmapMatchesMesh() {
        }

        [TestCase(100, 200)]
        public async void VerifyMeshCoversWholeRegion(int heightmapSize, int regionSize) {
            float[,] heightMap = CreateHeightmap(heightmapSize);
            PrimToMesh mesher = new PrimToMesh(_log, _params);
            DisplayableRenderable dr = await mesher.MeshFromHeightMap(heightMap, regionSize, regionSize,
                                    _assetManager, _defaultTexture);
            RenderableMeshGroup rmg = dr as RenderableMeshGroup;
            Assert.IsTrue(rmg != null, "MeshFromHeightMap did not return a RenderableMeshGroup");
            Assert.AreEqual(rmg.meshes.Count, 1, "MeshFromHeightMap returned more than one mesh");
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
    // Test the operations of BHasher and BHash.
    [TestFixture]
    public class BHasherTests : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
        }

        [TestCase(Result = 8511673652397437578L)]
        public ulong BHasherMdjb2TestParams() {
            BHasher hasher = new BHasherMdjb2();
            hasher.Add('c');
            hasher.Add((ushort)6);
            hasher.Add((int)12345);
            hasher.Add(-2345L);
            byte[] byt = Encoding.ASCII.GetBytes("This is a string");
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            // System.Console.WriteLine("BHasher hash output = " + hash.ToString());
            return hash.ToULong();
        }

        [TestCase("This is a string", Result = "1036731341136637329")]
        [TestCase("A long string which is much longer than we are willing to test", Result = "6489722179198911432")]
        [TestCase("A string witA one difference", Result = "254346540298589279")]
        [TestCase("A string witB one difference", Result = "5728349923385932352")]
        [TestCase("A string witC one difference", Result = "11202353306473275425")]
        public string BHasherMdjb2Test(string toHash) {
            BHasher hasher = new BHasherMdjb2();
            byte[] byt = Encoding.ASCII.GetBytes(toHash);
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            // System.Console.WriteLine("BHasherMdjb2 hash output = " + hash.ToString());
            return hash.ToString();
        }

        [TestCase("This is a string", Result = "41FB5B5AE4D57C5EE528ADB00E5E8E74")]
        [TestCase("A long string which is much longer than we are willing to test", Result = "7DA9312B687AEF9E776DD9F441254437")]
        [TestCase("A string witA one difference", Result = "D3E5DC1272277204DEC8B8009AC31CE7")]
        [TestCase("A string witB one difference", Result = "AF4F00FE82C49DA99623911BA79B61A4")]
        [TestCase("A string witC one difference", Result = "B7954F90F15DBEB6EE4974F38895D8C8")]
        public string BHasherMD5Test(string toHash) {
            BHasher hasher = new BHasherMD5();
            byte[] byt = Encoding.ASCII.GetBytes(toHash);
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            System.Console.WriteLine("BHasherMD5 hash output = " + hash.ToString());
            return hash.ToString();
        }

        [TestCase("This is a string", Result = "4E9518575422C9087396887CE20477AB5F550A4AA3D161C5C22A996B0ABB8B35")]
        [TestCase("A long string which is much longer than we are willing to test", Result = "312BB2AFFEF9E276C83C30342A742CD05CE51BDB7E15DB5209B3CCADDBBDF3B5")]
        [TestCase("A string witA one difference", Result = "D0E312AC7CA43B85822A95ADB1D12014EF5DE6B66F4F7A4905078941BB0C27C1")]
        [TestCase("A string witB one difference", Result = "59129922BEBED4D0F9EBF1C450D77C3C3600D6535E0F6E39CB270898A3865369")]
        [TestCase("A string witC one difference", Result = "D260E15C2F3251F1D8AD037418B39311BBE2AA828E914415408A3E60F0AC84EF")]
        public string BHasherSHA256Test(string toHash) {
            BHasher hasher = new BHasherSHA256();
            byte[] byt = Encoding.ASCII.GetBytes(toHash);
            hasher.Add(byt, 0, byt.Length);
            BHash hash = hasher.Finish();
            System.Console.WriteLine("BHasherSHA256 hash output = " + hash.ToString());
            return hash.ToString();
        }

        // Test that passing the hashing input in parts is the same as passing it all at once.
        [TestCase]
        public void BHasherParameterParts() {
            byte[] testBytes = new byte[100];
            Random rand = new Random();
            rand.NextBytes(testBytes);
            BHasher hasher1 = new BHasherMdjb2();
            BHasher hasher2 = new BHasherMdjb2();
            TestHasher("Mdjb2", hasher1, hasher2, testBytes);
            hasher1 = new BHasherMD5();
            hasher2 = new BHasherMD5();
            TestHasher("MD5", hasher1, hasher2, testBytes);
            hasher1 = new BHasherSHA256();
            hasher2 = new BHasherSHA256();
            TestHasher("SHA256", hasher1, hasher2, testBytes);
        }

        private void TestHasher(string name, BHasher hasher1, BHasher hasher2, byte[] testBytes) {
            hasher1.Add(testBytes, 0, testBytes.Length);
            BHash hash1 = hasher1.Finish();
            hasher2.Add(testBytes[0]);
            hasher2.Add(testBytes, 1, 10);
            hasher2.Add(testBytes[11]);
            hasher2.Add(testBytes, 12, testBytes.Length - 12);
            BHash hash2 = hasher2.Finish();
            Assert.AreEqual(hash1.ToString(), hash2.ToString(), "Adding bytes in different order gave different results in " + name);
        }
    }

    // =========================================================================================
    // Test the operations of PersistRules
    [TestFixture]
    public class PersistRulesTest : ConvoarTestCase {

        [TestFixtureSetUp]
        public void Init() {
        }

        [TestFixtureTearDown]
        public void TearDown() {
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
