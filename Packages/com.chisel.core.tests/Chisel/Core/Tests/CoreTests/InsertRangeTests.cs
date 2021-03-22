﻿using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;

namespace FoundationTests
{
    [TestFixture]
    public partial class InsertRangeTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }


        [Test]
        public void TreeWithNoChildren_InsertRangeWithInvalidNode_TreeStaysEmpty()
        {
            const int treeUserID = 13;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.InsertRange(0, new[] { CSGTreeNode.InvalidNode });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add an invalid child"));

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void BranchWithNoChildren_InsertRangeWithInvalidNode_BranchStaysEmpty()
        {
            const int branchUserID = 13;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.InsertRange(0, new[] { CSGTreeNode.InvalidNode });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add an invalid child"));

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void TreeWithNoChildren_InsertRangeWithTree_TreeStaysEmpty()
        {
            const int treeUserID1 = 13;
            const int treeUserID2 = 14;
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = tree1.InsertRange(0, new CSGTreeNode[] { tree2 });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(false, tree1.Dirty);
            Assert.AreEqual(false, tree2.Dirty);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(0, tree2.Count);
        }

        [Test]
        public void BranchWithNoChildren_InsertRangeWithTree_BranchStaysEmpty()
        {
            const int branchUserID = 13;
            const int treeUserID = 14;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = branch.InsertRange(0, new CSGTreeNode[] { tree });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void TreeWithNoChildren_InsertRangeWithBrushes_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);
            CompactHierarchyManager.ClearDirty(brush3.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.InsertRange(0, new CSGTreeNode[] { brush1, brush2, brush3 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(true, brush3.Dirty);
            Assert.AreEqual(tree.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush1.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Tree.NodeID);

            Assert.AreEqual(brush1.NodeID, tree[0].NodeID);
            Assert.AreEqual(brush2.NodeID, tree[1].NodeID);
            Assert.AreEqual(brush3.NodeID, tree[2].NodeID);

            Assert.AreEqual(3, tree.Count);
        }

        [Test]
        public void TreeWithAChild_InsertRangeWithBrushesAtStart_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3 });
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);
            CompactHierarchyManager.ClearDirty(brush3.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.InsertRange(0, new CSGTreeNode[] { brush1, brush2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(tree.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush1.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Tree.NodeID);

            Assert.AreEqual(brush1.NodeID, tree[0].NodeID);
            Assert.AreEqual(brush2.NodeID, tree[1].NodeID);
            Assert.AreEqual(brush3.NodeID, tree[2].NodeID);

            Assert.AreEqual(3, tree.Count);
        }

        [Test]
        public void TreeWithAChild_InsertRangeWithBrushesAtEnd_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3 });
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);
            CompactHierarchyManager.ClearDirty(brush3.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.InsertRange(1, new CSGTreeNode[] { brush1, brush2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(tree.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush1.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Tree.NodeID);

            Assert.AreEqual(brush3.NodeID, tree[0].NodeID);
            Assert.AreEqual(brush1.NodeID, tree[1].NodeID);
            Assert.AreEqual(brush2.NodeID, tree[2].NodeID);

            Assert.AreEqual(3, tree.Count);
        }

        [Test]
        public void TreeWithTwoChildren_InsertRangeWithBrushesInMiddle_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int brushUserID4 = 13;
            const int treeUserID = 14;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var brush4 = CSGTreeBrush.Create(userID: brushUserID4);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3, brush4 });
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);
            CompactHierarchyManager.ClearDirty(brush3.NodeID);
            CompactHierarchyManager.ClearDirty(brush4.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.InsertRange(1, new CSGTreeNode[] { brush1, brush2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBrushWithUserID(ref brush4, brushUserID4);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(false, brush4.Dirty);
            Assert.AreEqual(tree.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush1.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush2.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush3.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, brush4.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush4.Tree.NodeID);

            Assert.AreEqual(brush3.NodeID, tree[0].NodeID);
            Assert.AreEqual(brush1.NodeID, tree[1].NodeID);
            Assert.AreEqual(brush2.NodeID, tree[2].NodeID);
            Assert.AreEqual(brush4.NodeID, tree[3].NodeID);

            Assert.AreEqual(4, tree.Count);
        }
    }
}