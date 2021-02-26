﻿using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using Chisel.Core;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
/*
namespace FoundationTests
{
    [TestFixture]
    public partial class CreateTests
    {
        [SetUp]
        public void Init()
        {
            CSGManager.Clear();
        }

        [Test]
        public void CreateBrush_WithUserID()
        {
            const int brushUserID = 10;

            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBranch_WithUserID()
        {
            const int branchUserID = 10;

            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
        }

        [Test]
        public void CreateTree_WithUserID()
        {
            const int treeUserID = 10;

            CSGTree tree = CSGTree.Create(treeUserID);

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
        }

        [Test]
        public void CreateBrush_WithoutUserID()
        {
            CSGTreeBrush brush = CSGTreeBrush.Create();

            TestUtility.ExpectValidBrushWithUserID(ref brush, 0);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBranch_WithoutUserID()
        {
            CSGTreeBranch branch = CSGTreeBranch.Create();

            TestUtility.ExpectValidBranchWithUserID(ref branch, 0);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
        }

        [Test]
        public void CreateTree_WithoutUserID()
        {
            CSGTree tree = CSGTree.Create();

            TestUtility.ExpectValidTreeWithUserID(ref tree, 0);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
        }

        [Test]
        public void CreateBranchWithChildren()
        {
            const int brushUserID = 10;
            const int branchUserID1 = 11;
            const int branchUserID2 = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGTreeBranch branch1 = CSGTreeBranch.Create(branchUserID1);
            CSGManager.ClearDirty(brush.NodeID);
            CSGManager.ClearDirty(branch1.NodeID);


            CSGTreeBranch branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { brush, branch1 });


            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(branch2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(0, brush.Tree.NodeID);
            Assert.AreEqual(branch2.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(0, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(0, branch2.Parent.NodeID);
            Assert.AreEqual(0, branch2.Tree.NodeID);
            Assert.AreEqual(2, branch2.Count);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(2, CSGManager.TreeBranchCount, "Expected 2 TreeBranches to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
        }

        [Test]
        public void CreateTreeWithChildren()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            const int treeUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CSGManager.ClearDirty(brush.NodeID);
            CSGManager.ClearDirty(branch.NodeID);


            CSGTree tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush, branch });


            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(0, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(2, tree.Count);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
        }

        [Test]
        public void CreateTreeWithDuplicateChildren()
        {
            const int brushUserID = 10;
            const int treeUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGManager.ClearDirty(brush.NodeID);
             

            LogAssert.Expect(LogType.Error, new Regex("Have duplicate child"));
            CSGTree tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush, brush });


            TestUtility.ExpectInvalidTree(ref tree);
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(0, brush.Tree.NodeID);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBranchWithDuplicateChildren()
        {
            const int brushUserID = 10;
            const int branchUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGManager.ClearDirty(brush.NodeID);

            LogAssert.Expect(LogType.Error, new Regex("Have duplicate child"));
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush, brush });


            TestUtility.ExpectInvalidBranch(ref branch); 
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(0, brush.Tree.NodeID);
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateTreeWithNullChildren()
        {
            const int treeUserID = 12;

            CSGTree tree = CSGTree.Create(treeUserID, null);

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBranchWithNullChildren()
        {
            const int branchUserID = 12;

            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, null);

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBranchWithInvalidChildren()
        {
            const int treeUserID = 10;
            const int branchUserID = 11;
            CSGTree tree = CSGTree.Create(treeUserID);
            CSGManager.ClearDirty(tree.NodeID);


            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { CSGTreeNode.InvalidNode, tree });


            TestUtility.ExpectInvalidBranch(ref branch);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateTreeWithInvalidChildren()
        {
            const int treeUserID1 = 10;
            const int treeUserID2 = 11;
            CSGTree tree1 = CSGTree.Create(treeUserID1);
            CSGManager.ClearDirty(tree1.NodeID);


            CSGTree tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { CSGTreeNode.InvalidNode, tree1 });


            TestUtility.ExpectInvalidTree(ref tree2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            Assert.AreEqual(false, tree1.Dirty);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
            Assert.AreEqual(1, CSGManager.TreeCount, "Expected 1 Tree to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void CreateBrush_Multiple()
        {
            const int brushUserID0 = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;

            var brush0 = CSGTreeBrush.Create(userID: brushUserID0);
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);

            TestUtility.ExpectValidBrushWithUserID(ref brush0, brushUserID0);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            Assert.AreEqual(true, brush0.Dirty);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(3, CSGManager.TreeBrushCount, "Expected 3 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBranchCount, "Expected 0 TreeBranches to Exist");
        }

        [Test]
        public void CreateBranch_Multiple()
        {
            const int branchUserID0 = 10;
            const int branchUserID1 = 11;
            const int branchUserID2 = 12;

            var branch0 = CSGTreeBranch.Create(branchUserID0);
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);

            TestUtility.ExpectValidBranchWithUserID(ref branch0, branchUserID0);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.AreEqual(true, branch0.Dirty);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
            Assert.AreEqual(3, CSGManager.TreeBranchCount, "Expected 3 TreeBranches to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
        }

        [Test]
        public void CreateTree_Multiple()
        {
            const int treeUserID0 = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;

            var tree0 = CSGTree.Create(treeUserID0);
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            Assert.AreEqual(true, tree0.Dirty);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
            TestUtility.ExpectValidTreeWithUserID(ref tree0, treeUserID0);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
        }

    }
}*/