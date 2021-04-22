using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;

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

        public static void CreateBox(Vector3 size, int descriptionIndex, out BrushMesh box)
        {
            BrushMeshFactory.CreateBox(size, descriptionIndex, out box);
        }

        public static void ExpectValidBrushWithUserID(ref CSGTreeBrush brush, int userID)
        {
            CSGNodeType type = ((CSGTreeNode)brush).Type;

            Assert.AreEqual(true, brush.Valid);
            Assert.AreNotEqual(0, brush.NodeID);
            Assert.AreEqual(userID, brush.UserID);
            Assert.AreEqual(CSGNodeType.Brush, type);
        }

        public static void ExpectInvalidBrush(ref CSGTreeBrush brush)
        {
            CSGNodeType type = ((CSGTreeNode)brush).Type;

            Assert.AreEqual(false, brush.Valid);
            Assert.AreEqual(0, brush.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }

        public static void ExpectValidBranchWithUserID(ref CSGTreeBranch branch, int userID)
        {
            CSGNodeType type = ((CSGTreeNode)branch).Type;

            Assert.AreEqual(true, branch.Valid);
            Assert.AreNotEqual(0, branch.NodeID);
            Assert.AreEqual(userID, branch.UserID);
            Assert.AreEqual(CSGNodeType.Branch, type);
        }

        public static void ExpectInvalidBranch(ref CSGTreeBranch branch)
        {
            CSGNodeType type = ((CSGTreeNode)branch).Type;

            Assert.AreEqual(false, branch.Valid);
            Assert.AreEqual(0, branch.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }

        public static void ExpectValidTreeWithUserID(ref CSGTree tree, int userID)
        {
            CSGNodeType type = ((CSGTreeNode)tree).Type;

            Assert.AreEqual(true, tree.Valid);
            Assert.AreNotEqual(0, tree.NodeID);
            Assert.AreEqual(userID, tree.UserID);
            Assert.AreEqual(CSGNodeType.Tree, type);
        }

        public static void ExpectInvalidTree(ref CSGTree tree)
        {
            CSGNodeType type = ((CSGTreeNode)tree).Type;

            Assert.AreEqual(false, tree.Valid);
            Assert.AreEqual(0, tree.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }
        
        static readonly List<CSGTreeBrush> brushes = new List<CSGTreeBrush>();

        public static int CountOfBrushesInTree(CSGTree tree)
        {
            brushes.Clear();
            CompactHierarchyManager.GetTreeNodes(tree.NodeID, null, brushes);
            return brushes.Count;
        }


        public static bool IsInTree(CSGTree tree, CSGTreeBrush brush)
        {
            brushes.Clear();
            CompactHierarchyManager.GetTreeNodes(tree.NodeID, null, brushes);
            return brushes.Contains(brush);
        }

    }
}