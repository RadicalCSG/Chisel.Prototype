using UnityEngine;
using NUnit.Framework;
using Chisel;
using Chisel.Core;

namespace FoundationTests
{
    [TestFixture]
    public partial class MeshGenerationTests
    {
        static Material material1;
        static Material material2;
        static int      materialID1 = -1;
        static int      materialID2  = -1;

        const VertexChannelFlags invalidVertexChannels = (VertexChannelFlags)0;
        readonly static MeshQuery[] simpleMeshTypes = new MeshQuery[]
        {
            new MeshQuery(LayerUsageFlags.Collidable, vertexChannels: VertexChannelFlags.Position | VertexChannelFlags.UV0)
        };
        readonly static MeshQuery[] materialMeshTypes = new MeshQuery[]
        {
            new MeshQuery(LayerUsageFlags.Renderable, parameterIndex: LayerParameterIndex.RenderMaterial, vertexChannels: VertexChannelFlags.Position | VertexChannelFlags.UV0)
        };

        const int boxIndexCount  = 6 * (2 * 3); // 6 sides, 2 triangles per side, 3 indices per triangle
        const int boxVertexCount = 6 * (4    ); // 6 sides, 4 vertices per side
        

        [SetUp]
        public void Init()
        {
            material1 = TestUtility.GenerateDebugColorMaterial(Color.blue);
            material2 = TestUtility.GenerateDebugColorMaterial(Color.red);

            Assert.NotNull(material1);
            Assert.NotNull(material2);

            materialID1 = material1.GetInstanceID();
            materialID2 = material2.GetInstanceID();
            CSGManager.Clear();
        }

        #region Helpers
        static BrushMeshInstance CreateBox(Vector3 size, CSGOperationType operation = CSGOperationType.Additive, Material material = null)
        {
            if (material == null)
                material = material2;
            var layers = new SurfaceLayers
            {
                layerUsage = LayerUsageFlags.All,
                layerParameter1 = (material) ? material.GetInstanceID() : 0
            };
            BrushMesh brushMesh = TestUtility.CreateBox(size, layers);
            return BrushMeshInstance.Create(brushMesh);
        }

        static CSGTreeBrush CreateBoxBrush(CSGOperationType operation = CSGOperationType.Additive, Material material = null)
        {
            return CreateBoxBrush(Vector3.one, operation, material);
        }

        static CSGTreeBrush CreateBoxBrush(Vector3 size, CSGOperationType operation = CSGOperationType.Additive, Material material = null)
        {
            return CSGTreeBrush.Create(operation: operation, brushMesh: CreateBox(size, operation, material ?? material2));
        }


        static GeneratedMeshContents GeneratedMeshAndValidate(CSGTree tree, MeshQuery[] meshTypes, bool expectEmpty = false)
        {
            GeneratedMeshContents generatedMesh = null;
            GeneratedMeshDescription[] meshDescriptions = null;
            bool treeWasDirtyBefore = false;
            bool treeIsDirtyAfter = true;

            tree.SetDirty();
            bool haveChanges = CSGManager.Flush(); // Note: optional
            if (haveChanges)
            {
                treeWasDirtyBefore = tree.Dirty; // Note: optional
                if (treeWasDirtyBefore)
                {
                    meshDescriptions = tree.GetMeshDescriptions(meshTypes);
                    if (meshDescriptions != null)
                    {
                        var meshDescription = meshDescriptions[0];
                        generatedMesh = tree.GetGeneratedMesh(meshDescription);
                    }
                    treeIsDirtyAfter = tree.Dirty;
                }
            }

            Assert.IsTrue(haveChanges);
            Assert.IsTrue(treeWasDirtyBefore);
            Assert.IsFalse(treeIsDirtyAfter);
            if (expectEmpty)
            {
                Assert.Null(meshDescriptions);
                Assert.Null(generatedMesh);
            } else
            {
                Assert.NotNull(meshDescriptions);
                Assert.NotNull(generatedMesh);
                Assert.AreEqual(meshDescriptions[0].meshQuery, meshTypes[0]);
                Assert.AreEqual(simpleMeshTypes.Length, meshDescriptions.Length);
                Assert.IsTrue(generatedMesh.description.vertexCount > 0 &&
                              generatedMesh.description.indexCount > 0);
            }
            return generatedMesh;
        }

        static void ValidateIsCorrectBox(GeneratedMeshContents generatedMesh)
        {
            Assert.NotNull(generatedMesh.indices);
            Assert.NotNull(generatedMesh.positions);
            Assert.AreEqual(boxIndexCount, generatedMesh.indices.Length);      // 6 sides, 2 triangles per side, 3 indices per triangle
            Assert.AreEqual(boxVertexCount, generatedMesh.positions.Length);   // 6 sides, 4 vertices per side

            var vertexChannels = generatedMesh.description.meshQuery.UsedVertexChannels;
            //		if ((vertexChannels & VertexChannelFlags.Color) == VertexChannelFlags.Color)
            //		{
            //			Assert.NotNull(generatedMesh.colors);
            //			Assert.AreEqual(generatedMesh.positions.Length, generatedMesh.colors.Length);
            //		} else
            //			Assert.Null(generatedMesh.colors);

            if ((vertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent)
            {
                Assert.NotNull(generatedMesh.tangents);
                Assert.AreEqual(generatedMesh.positions.Length, generatedMesh.tangents.Length);
            } else
                Assert.Null(generatedMesh.tangents);

            if ((vertexChannels & VertexChannelFlags.Normal) == VertexChannelFlags.Normal)
            {
                Assert.NotNull(generatedMesh.normals);
                Assert.AreEqual(generatedMesh.positions.Length, generatedMesh.normals.Length);
            } else
                Assert.Null(generatedMesh.normals);

            if ((vertexChannels & VertexChannelFlags.UV0) == VertexChannelFlags.UV0)
            {
                Assert.NotNull(generatedMesh.uv0);
                Assert.AreEqual(generatedMesh.positions.Length, generatedMesh.uv0.Length);
            } else
                Assert.Null(generatedMesh.uv0);
        }
        #endregion


        [Test]
        public void Mesh_CreateAdditiveBoxBrush_RetrieveMesh_RetrievedMeshIsACube()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Additive)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, simpleMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
        }

        [Test]
        public void Mesh_CreateSubtractiveBoxBrush_RetrieveMesh_RetrievedMeshIsEmpty()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Subtractive)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, simpleMeshTypes, expectEmpty: true);

            Assert.Null(generatedMesh);
        }

        [Test]
        public void Mesh_CreateIntersectionBoxBrush_RetrieveMesh_RetrievedMeshIsEmpty()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Intersecting)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, simpleMeshTypes, expectEmpty: true);

            Assert.Null(generatedMesh);
        }

        [Test]
        public void Mesh_AdditiveBoxBrushWithSubtractiveBoxBrush_RetrievedMeshIsEmpty()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Additive),
                CreateBoxBrush(operation: CSGOperationType.Subtractive)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, simpleMeshTypes, expectEmpty: true);

            Assert.Null(generatedMesh);
        }

        [Test]
        public void Mesh_SubtractiveBoxBrushWithAdditiveBoxBrush_RetrievedMeshIsBox()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Subtractive, material: material2),
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material1)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, materialMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
            Assert.AreEqual(materialID1, generatedMesh.description.surfaceParameter);
        }

        [Test]
        public void Mesh_IntersectingBoxBrushWithAdditiveBoxBrush_RetrievedMeshIsEmpty()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Intersecting, material: material1),
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material2)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, materialMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
            Assert.AreEqual(materialID2, generatedMesh.description.surfaceParameter);
        }

        [Test]
        public void Mesh_AdditiveBoxBrushWithAdditiveBoxBrush_RetrievedMeshHasLastBrushMaterial()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material2),
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material1)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, materialMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
            Assert.AreEqual(materialID1, generatedMesh.description.surfaceParameter);
        }

        [Test]
        public void Mesh_AdditiveBoxBrushOverlapsWithIntersectingBoxBrush_RetrievedMeshHasIntersectingBoxMaterial()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material2),
                CreateBoxBrush(operation: CSGOperationType.Intersecting, material: material1)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, materialMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
            Assert.AreEqual(materialID2, generatedMesh.description.surfaceParameter);
        }

        [Test]
        public void Mesh_LargeAdditiveBoxBrushWithIntersectingBoxBrush_RetrievedMeshHasIntersectingBoxMaterial()
        {
            var tree = CSGTree.Create(
                CreateBoxBrush(operation: CSGOperationType.Additive, material: material2, size: Vector3.one * 2),
                CreateBoxBrush(operation: CSGOperationType.Intersecting, material: material1)
            );

            GeneratedMeshContents generatedMesh = GeneratedMeshAndValidate(tree, materialMeshTypes, expectEmpty: false);

            ValidateIsCorrectBox(generatedMesh);
            Assert.AreEqual(materialID1, generatedMesh.description.surfaceParameter);
        }
    }
}