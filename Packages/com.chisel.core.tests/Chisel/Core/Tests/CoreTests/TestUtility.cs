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
using Unity.Collections;

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

            Assert.IsTrue(brush.Valid);
            Assert.AreEqual(userID, brush.UserID);
            Assert.AreEqual(CSGNodeType.Brush, type);
        }

        public static void ExpectInvalidBrush(ref CSGTreeBrush brush)
        {
            CSGNodeType type = ((CSGTreeNode)brush).Type;

            Assert.IsFalse(brush.Valid);
            Assert.AreEqual(0, brush.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }

        public static void ExpectValidBranchWithUserID(ref CSGTreeBranch branch, int userID)
        {
            CSGNodeType type = ((CSGTreeNode)branch).Type;

            Assert.IsTrue(branch.Valid);
            Assert.AreEqual(userID, branch.UserID);
            Assert.AreEqual(CSGNodeType.Branch, type);
        }

        public static void ExpectInvalidBranch(ref CSGTreeBranch branch)
        {
            CSGNodeType type = ((CSGTreeNode)branch).Type;

            Assert.IsFalse(branch.Valid);
            Assert.AreEqual(0, branch.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }

        public static void ExpectValidTreeWithUserID(ref CSGTree tree, int userID)
        {
            CSGNodeType type = ((CSGTreeNode)tree).Type;

            Assert.IsTrue(tree.Valid);
            Assert.AreEqual(userID, tree.UserID);
            Assert.AreEqual(CSGNodeType.Tree, type);
        }

        public static void ExpectInvalidTree(ref CSGTree tree)
        {
            CSGNodeType type = ((CSGTreeNode)tree).Type;

            Assert.IsFalse(tree.Valid);
            Assert.AreEqual(0, tree.UserID);
            Assert.AreEqual(CSGNodeType.None, type);
        }
        
        public static int CountOfBrushesInTree(CSGTree tree)
        {
            using (var brushes = new NativeList<CSGTreeBrush>(Allocator.Temp))
            {
                CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, brushes);
                return brushes.Length;
            }
        }


        public static bool IsInTree(CSGTree tree, CSGTreeBrush brush)
        {
            using (var brushes = new NativeList<CSGTreeBrush>(Allocator.Temp))
            {
                CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, brushes);
                return brushes.IndexOf(brush) != -1;
            }
        }

        public static int GetChildCount(NodeID parent)
        {
            var parentNodeCompactID = CompactHierarchyManager.GetCompactNodeID(parent);
            var hierarchyID         = parentNodeCompactID.hierarchyID;
            ref var treeHierarchy = ref CompactHierarchyManager.GetHierarchy(hierarchyID);
            return treeHierarchy.ChildCount(parentNodeCompactID);
        }

        public static NodeID GetParentOfNode(NodeID parent)
        {
            var parentNodeCompactID = CompactHierarchyManager.GetCompactNodeID(parent);
            if (parentNodeCompactID == CompactNodeID.Invalid)
                return NodeID.Invalid;
            var hierarchyID = parentNodeCompactID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return NodeID.Invalid;
            ref var treeHierarchy = ref CompactHierarchyManager.GetHierarchy(hierarchyID);
            var parentNodeID = treeHierarchy.ParentOf(parentNodeCompactID);
            if (parentNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;
            return treeHierarchy.GetNodeID(parentNodeID);
        }

    }
}