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
    public partial class RemoveAtTests
    {
        [SetUp]
        public void Init()
        {
            CSGManager.Clear();
        }

        [Test]
        public void BranchWithNoChildren_RemoveAtZero_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }

        [Test]
        public void BranchWithSingleChild_RemoveLastNode_IsEmptyBranch()
        {
            const int branchUserID = 10;
            const int brushUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CSGManager.ClearDirty(brush);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(0, brush.Parent.NodeID);
            Assert.AreEqual(0, brush.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(1, CSGManager.TreeBrushCount, "Expected 1 TreeBrush to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(2, CSGManager.TreeNodeCount, "Expected 2 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithTwoChildren_RemoveLastNode_StillHasFirstNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CSGManager.ClearDirty(brush1);
            CSGManager.ClearDirty(brush2);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(1);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(0, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(brush1.NodeID, branch[0].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithTwoChildren_RemoveFirstNode_StillHasLastNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CSGManager.ClearDirty(brush1);
            CSGManager.ClearDirty(brush2);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            Assert.AreEqual(true, brush1.Dirty);
            Assert.AreEqual(false, brush2.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(0, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(brush2.NodeID, branch[0].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(2, CSGManager.TreeBrushCount, "Expected 2 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(3, CSGManager.TreeNodeCount, "Expected 3 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithThreeChildren_RemoveMiddleNode_StillHasFirstAndLastNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CSGManager.ClearDirty(brush1);
            CSGManager.ClearDirty(brush2);
            CSGManager.ClearDirty(brush3);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(1);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(0, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(0, brush3.Tree.NodeID);
            Assert.AreEqual(2, branch.Count);
            Assert.AreEqual(brush1.NodeID, branch[0].NodeID);
            Assert.AreEqual(brush3.NodeID, branch[1].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(3, CSGManager.TreeBrushCount, "Expected 3 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithThreeChildren_RemoveWithTooLargeIndex_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CSGManager.ClearDirty(brush1);
            CSGManager.ClearDirty(brush2);
            CSGManager.ClearDirty(brush3);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(3);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(false, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(0, brush3.Tree.NodeID);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual(brush1.NodeID, branch[0].NodeID);
            Assert.AreEqual(brush2.NodeID, branch[1].NodeID);
            Assert.AreEqual(brush3.NodeID, branch[2].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(3, CSGManager.TreeBrushCount, "Expected 3 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithThreeChildren_RemoveWithNegativeIndex_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CSGManager.ClearDirty(brush1);
            CSGManager.ClearDirty(brush2);
            CSGManager.ClearDirty(brush3);
            CSGManager.ClearDirty(branch);

            var result = branch.RemoveAt(-1);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(false, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(0, brush3.Tree.NodeID);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual(brush1.NodeID, branch[0].NodeID);
            Assert.AreEqual(brush2.NodeID, branch[1].NodeID);
            Assert.AreEqual(brush3.NodeID, branch[2].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(3, CSGManager.TreeBrushCount, "Expected 3 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
        }
    }
}