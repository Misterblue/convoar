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

        [TestCase]
        public void VerifyMeshCoversWholeRegion() {
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
