using UnityEngine;
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
    public partial class RemoveChildTests
    { 
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void Tree_RemoveInvalidNode_ReturnsFalse()
        {
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Remove(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(false, result);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Tree_RemoveUnknownBrush_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Remove(brush);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(false, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Branch_RemoveInvalidNode_ReturnsFalse()
        {
            const int branchUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Remove(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(false, result);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_RemoveUnknownBrush_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Remove(brush);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(false, brush.Dirty);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }


        [Test]
        public void TreeWithChildBrush_RemoveBrush_IsEmpty()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, brush);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Remove(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(false, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWithChildBrush_RemoveSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = tree.Remove(brush);
            var result2 = tree.Remove(brush);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(false, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWithChildBranch_RemoveBranch_IsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { branch });
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Remove(branch);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void BranchWithChildBrush_RemoveBrush_IsEmpty()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Remove(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void BranchWithChildBrush_RemoveSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result1 = branch.Remove(brush);
            var result2 = branch.Remove(brush);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void BranchWithChildBrushes_RemoveMiddleBrush_OtherBrushesRemain()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);
            CompactHierarchyManager.ClearDirty(brush3.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Remove(brush2);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(true, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush1.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush3.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush2.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(2, branch.Count);
            Assert.AreEqual(brush1.NodeID, branch[0].NodeID);
            Assert.AreEqual(brush3.NodeID, branch[1].NodeID);
        }

        [Test]
        public void BranchWithChildBranch_RemoveBranch_IsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            var result = branch2.Remove(branch1);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(NodeID.Invalid, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(0, branch2.Count);
        }
    }
}