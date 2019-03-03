using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;

namespace FoundationTests
{
	public sealed class TestUtility
    {
        public static Material GenerateDebugColorMaterial(Color color)
        {
            var name = "Color: " + color;
            var shader = Shader.Find("Unlit/Color");
            if (!shader)
                return null;

            var material = new Material(shader)
            {
                name = name.Replace(':', '_'),
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_Color", color);
            return material;
        }

        public static BrushMesh CreateBox(Vector3 size, SurfaceLayers layers)
        {
            return BrushMeshFactory.CreateBox(Vector3.one, layers);
        }

        public static void ExpectValidBrushWithUserID(ref CSGTreeBrush brush, int userID)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(brush.NodeID).Type;

			Assert.AreEqual(true, brush.Valid);
			Assert.AreNotEqual(0, brush.NodeID);
			Assert.AreEqual(userID, brush.UserID);
			Assert.AreEqual(CSGNodeType.Brush, type);
		}

		public static void ExpectInvalidBrush(ref CSGTreeBrush brush)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(brush.NodeID).Type;

			Assert.AreEqual(false, brush.Valid);
			Assert.AreEqual(0, brush.UserID);
			Assert.AreEqual(CSGNodeType.None, type);
		}

		public static void ExpectValidBranchWithUserID(ref CSGTreeBranch operation, int userID)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(operation.NodeID).Type;

			Assert.AreEqual(true, operation.Valid);
			Assert.AreNotEqual(0, operation.NodeID);
			Assert.AreEqual(userID, operation.UserID);
			Assert.AreEqual(CSGNodeType.Branch, type);
		}

		public static void ExpectInvalidBranch(ref CSGTreeBranch operation)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(operation.NodeID).Type;

			Assert.AreEqual(false, operation.Valid);
			Assert.AreEqual(0, operation.UserID);
			Assert.AreEqual(CSGNodeType.None, type);
		}

		public static void ExpectValidTreeWithUserID(ref CSGTree model, int userID)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(model.NodeID).Type;

			Assert.AreEqual(true, model.Valid);
			Assert.AreNotEqual(0, model.NodeID);
			Assert.AreEqual(userID, model.UserID);
			Assert.AreEqual(CSGNodeType.Tree, type);
		}

		public static void ExpectInvalidTree(ref CSGTree model)
		{
			CSGNodeType type = CSGTreeNode.Encapsulate(model.NodeID).Type;

			Assert.AreEqual(false, model.Valid);
			Assert.AreEqual(0, model.UserID);
			Assert.AreEqual(CSGNodeType.None, type);
		}
	}
}