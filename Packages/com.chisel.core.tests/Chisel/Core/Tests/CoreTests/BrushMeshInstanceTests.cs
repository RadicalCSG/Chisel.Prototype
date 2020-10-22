using UnityEngine;
using NUnit.Framework;
using Chisel;
using Chisel.Core;

namespace FoundationTests
{
    [TestFixture]
    public partial class BrushMeshInstanceTests
    {
        [SetUp]
        public void Init()
        {
            CSGManager.Clear();
        }

        [Test]
        public void CreateBrushMesh()
        {
            TestUtility.CreateBox(Vector3.one, null, out BrushMesh brushMesh);

            Assert.AreNotEqual(null, brushMesh);
            Assert.AreNotEqual(null, brushMesh.vertices);
            Assert.AreNotEqual(null, brushMesh.halfEdges);
            Assert.AreNotEqual(null, brushMesh.polygons);
            Assert.AreEqual(8, brushMesh.vertices.Length);
            Assert.AreEqual(24, brushMesh.halfEdges.Length);
            Assert.AreEqual(6, brushMesh.polygons.Length);
        }

        [Test]
        public void CreateBrushMeshInstanceFromBrushMesh()
        {
            TestUtility.CreateBox(Vector3.one, null, out BrushMesh brushMesh);

            BrushMeshInstance coreBrushMesh = BrushMeshInstance.Create(brushMesh);

            Assert.AreEqual(true, coreBrushMesh.Valid);
        }

        [Test]
        public void DestroyBrushMeshInstance()
        {
            TestUtility.CreateBox(Vector3.one, null, out BrushMesh brushMesh);
            BrushMeshInstance brushMeshInstance = BrushMeshInstance.Create(brushMesh);

            brushMeshInstance.Destroy();

            Assert.AreEqual(false, brushMeshInstance.Valid);
        }


        [Test]
        public void CreateBrushMeshInstance_WithNullBrushMesh_IsNotValid()
        {
            BrushMeshInstance coreBrushMesh = BrushMeshInstance.Create(null);

            Assert.AreEqual(false, coreBrushMesh.Valid);
        }
    }
}