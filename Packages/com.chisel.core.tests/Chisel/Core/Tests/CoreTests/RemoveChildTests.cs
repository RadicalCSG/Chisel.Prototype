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
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Remove(CSGTreeNode.Invalid);
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.IsFalse(result);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Tree_RemoveUnknownBrush_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Remove(brush);

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsFalse(TestUtility.IsInTree(tree, brush));
            Assert.IsFalse(tree.Dirty);
            Assert.IsFalse(brush.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Branch_RemoveInvalidNode_ReturnsFalse()
        {
            const int branchUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Remove(CSGTreeNode.Invalid);
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.IsFalse(result);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_RemoveUnknownBrush_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Remove(brush);

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsFalse(brush.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }


        [Test]
        public void TreeWithChildBrush_RemoveBrush_IsEmpty()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, brush);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Remove(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree, brush));
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWithChildBrush_RemoveSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            var result1 = tree.Remove(brush);
            var result2 = tree.Remove(brush);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree, brush));
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWithChildBranch_RemoveBranch_IsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { branch });
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Remove(branch);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Remove(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void BranchWithChildBrush_RemoveSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result1 = branch.Remove(brush);
            var result2 = branch.Remove(brush);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
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
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Remove(brush2);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(2, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[1]);
        }

        [Test]
        public void BranchWithChildBranch_RemoveBranch_IsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);

            var result = branch2.Remove(branch1);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsFalse(branch1.Parent.Valid);
            Assert.IsFalse(branch1.Tree.Valid);
            Assert.IsFalse(branch2.Parent.Valid);
            Assert.IsFalse(branch2.Tree.Valid);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(0, branch2.Count);
        }
    }
}