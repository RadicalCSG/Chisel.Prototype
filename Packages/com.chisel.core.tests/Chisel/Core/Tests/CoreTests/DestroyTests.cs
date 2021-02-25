﻿using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;

namespace FoundationTests
{
    [TestFixture]
    public partial class DestroyTests
    {
        [SetUp]
        public void Init()
        {
            CSGManager.Clear();
        }


        [Test]
        public void DestroyInvalidNode()
        {
            var invalidNode = CSGTreeNode.InvalidNode;

            var result = invalidNode.Destroy();

            Assert.AreEqual(false, result);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }


        [Test]
        public void DestroyNode_InvalidNode_Multiple()
        {
            var result = CSGManager.Destroy(new CSGTreeNode[] { CSGTreeNode.InvalidNode, CSGTreeNode.InvalidNode });

            Assert.AreEqual(false, result);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void DestroyNode_Duplicates()
        {
            const int brushUserID0 = 10;
            const int brushUserID1 = 11;
            var brush0 = CSGTreeBrush.Create(userID: brushUserID0);
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            CSGManager.ClearDirty(brush0.NodeID);
            CSGManager.ClearDirty(brush1.NodeID);

            var result = CSGManager.Destroy(new CSGTreeNode[] { brush0, brush1, brush1 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush0);
            TestUtility.ExpectInvalidBrush(ref brush1);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void DestroyBrush()
        {
            const int brushUserID = 10;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGManager.ClearDirty(brush.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }

        [Test]
        public void DestroyBranch()
        {
            const int branchUserID = 10;
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CSGManager.ClearDirty(branch.NodeID);

            var result = branch.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }

        [Test]
        public void DestroyTree()
        {
            const int treeUserID = 10;
            CSGTree tree = CSGTree.Create(treeUserID);
            CSGManager.ClearDirty(tree.NodeID);

            var result = tree.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidTree(ref tree);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }

        [Test]
        public void DestroyBrush_Twice_ReturnsFalse()
        {
            const int brushUserID = 10;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGManager.ClearDirty(brush.NodeID);

            var result1 = brush.Destroy();
            var result2 = brush.Destroy();

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }

        [Test]
        public void DestroyBranch_Twice_ReturnsFalse()
        {
            const int branchUserID = 10;
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CSGManager.ClearDirty(branch.NodeID);

            var result1 = branch.Destroy();
            var result2 = branch.Destroy();

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidBranch(ref branch);
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }

        [Test]
        public void DestroyTree_Twice_ReturnsFalse()
        {
            const int treeUserID = 10;
            CSGTree tree = CSGTree.Create(treeUserID);
            CSGManager.ClearDirty(tree.NodeID);

            var result1 = tree.Destroy();
            var result2 = tree.Destroy();

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidTree(ref tree);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
        }


        [Test]
        public void DestroyBrush_Multiple()
        {
            const int brushUserID0 = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush0 = CSGTreeBrush.Create(userID: brushUserID0);
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            CSGManager.ClearDirty(brush0.NodeID);
            CSGManager.ClearDirty(brush1.NodeID);
            CSGManager.ClearDirty(brush2.NodeID);

            var result = CSGManager.Destroy(new CSGTreeNode[] { brush0, brush1, brush2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush0);
            TestUtility.ExpectInvalidBrush(ref brush1);
            TestUtility.ExpectInvalidBrush(ref brush2);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void DestroyBranch_Multiple()
        {
            const int branchUserID0 = 10;
            const int branchUserID1 = 11;
            const int branchUserID2 = 12;
            var branch0 = CSGTreeBranch.Create(branchUserID0);
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CSGManager.ClearDirty(branch0.NodeID);
            CSGManager.ClearDirty(branch1.NodeID);
            CSGManager.ClearDirty(branch2.NodeID);

            var result = CSGManager.Destroy(new CSGTreeNode[] { branch0, branch1, branch2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch0);
            TestUtility.ExpectInvalidBranch(ref branch1);
            TestUtility.ExpectInvalidBranch(ref branch2);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void DestroyTree_Multiple()
        {
            const int treeUserID0 = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            var tree0 = CSGTree.Create(treeUserID0);
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CSGManager.ClearDirty(tree0.NodeID);
            CSGManager.ClearDirty(tree1.NodeID);
            CSGManager.ClearDirty(tree2.NodeID);

            var result = CSGManager.Destroy(new CSGTreeNode[] { tree0, tree1, tree2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidTree(ref tree0);
            TestUtility.ExpectInvalidTree(ref tree1);
            TestUtility.ExpectInvalidTree(ref tree2);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeNodeCount, "Expected 0 TreeNodes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void BranchWithBrush_DestroyBrush_BranchIsEmpty()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CSGManager.ClearDirty(brush.NodeID);
            CSGManager.ClearDirty(branch.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(0, brush.Tree.NodeID);
            Assert.AreEqual(0, branch.Parent.NodeID);
            Assert.AreEqual(0, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void TreeWithBrush_DestroyBrush_TreeIsEmpty()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush });
            CSGManager.ClearDirty(brush.NodeID);
            CSGManager.ClearDirty(tree.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(0, tree.CountOfBrushesInTree);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }


        [Test]
        public void BranchWithBranch_DestroyBranch_BranchIsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CSGManager.ClearDirty(branch1.NodeID);
            CSGManager.ClearDirty(branch2.NodeID);

            var result = branch1.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch1);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(0, branch1.Parent.NodeID);
            Assert.AreEqual(0, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch2.Parent.NodeID);
            Assert.AreEqual(0, branch2.Tree.NodeID);
            Assert.AreEqual(0, branch2.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void TreeWithBranch_DestroyBranch_TreeIsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            tree.InsertRange(0, new CSGTreeNode[] { branch });
            CSGManager.ClearDirty(branch.NodeID);
            CSGManager.ClearDirty(tree.NodeID);

            var result = branch.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(0, branch.Parent.NodeID);
            Assert.AreEqual(0, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

    }
}