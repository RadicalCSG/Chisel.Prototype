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
    public partial class CreateTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void CreateBrush_WithUserID()
        {
            const int brushUserID = 10;

            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
        }

        [Test]
        public void CreateBranch_WithUserID()
        {
            const int branchUserID = 10;

            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, branch.Dirty);
        }

        [Test]
        public void CreateTree_WithUserID()
        {
            const int treeUserID = 10;

            CSGTree tree = CSGTree.Create(treeUserID);

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
        }

        [Test]
        public void CreateBrush_WithoutUserID()
        {
            CSGTreeBrush brush = CSGTreeBrush.Create();

            TestUtility.ExpectValidBrushWithUserID(ref brush, 0);
            Assert.AreEqual(true, brush.Dirty);
        }

        [Test]
        public void CreateBranch_WithoutUserID()
        {
            CSGTreeBranch branch = CSGTreeBranch.Create();

            TestUtility.ExpectValidBranchWithUserID(ref branch, 0);
            Assert.AreEqual(true, branch.Dirty);
        }

        [Test]
        public void CreateTree_WithoutUserID()
        {
            CSGTree tree = CSGTree.Create();

            TestUtility.ExpectValidTreeWithUserID(ref tree, 0);
            Assert.AreEqual(true, tree.Dirty);
        }

        [Test]
        public void CreateBranchWithChildren()
        {
            const int brushUserID = 10;
            const int branchUserID1 = 11;
            const int branchUserID2 = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGTreeBranch branch1 = CSGTreeBranch.Create(branchUserID1);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);


            CSGTreeBranch branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { brush, branch1 });


            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(branch2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(branch2.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(NodeID.Invalid, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(2, branch2.Count);
        }

        [Test]
        public void CreateTreeWithChildren()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            const int treeUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);


            CSGTree tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush, branch });


            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(tree.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(2, tree.Count);
        }

        [Test]
        public void CreateTreeWithDuplicateChildren()
        {
            const int brushUserID = 10;
            const int treeUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
             

            LogAssert.Expect(LogType.Error, new Regex("Have duplicate child"));
            CSGTree tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush, brush });


            TestUtility.ExpectInvalidTree(ref tree);
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
        }

        [Test]
        public void CreateBranchWithDuplicateChildren()
        {
            const int brushUserID = 10;
            const int branchUserID = 12;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);

            LogAssert.Expect(LogType.Error, new Regex("Have duplicate child"));
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush, brush });


            TestUtility.ExpectInvalidBranch(ref branch); 
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
        }

        [Test]
        public void CreateTreeWithNullChildren()
        {
            const int treeUserID = 12;

            CSGTree tree = CSGTree.Create(treeUserID, null);

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void CreateBranchWithNullChildren()
        {
            const int branchUserID = 12;

            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, null);

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void CreateBranchWithInvalidChildren()
        {
            const int treeUserID = 10;
            const int branchUserID = 11;
            CSGTree tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);


            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { CSGTreeNode.InvalidNode, tree });


            TestUtility.ExpectInvalidBranch(ref branch);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void CreateTreeWithInvalidChildren()
        {
            const int treeUserID1 = 10;
            const int treeUserID2 = 11;
            CSGTree tree1 = CSGTree.Create(treeUserID1);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);


            CSGTree tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { CSGTreeNode.InvalidNode, tree1 });


            TestUtility.ExpectInvalidTree(ref tree2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            Assert.AreEqual(false, tree1.Dirty);
            Assert.AreEqual(0, tree1.Count);
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
            TestUtility.ExpectValidTreeWithUserID(ref tree0, treeUserID0);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(true, tree0.Dirty);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
        }

    }
}