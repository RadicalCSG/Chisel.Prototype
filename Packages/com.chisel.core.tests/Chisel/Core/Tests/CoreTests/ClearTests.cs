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
    [TestFixture]
    public partial class ClearTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void TreeWithoutChildren_Clear_TreeIsEmpty()
        {
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            tree.Clear();

            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void BranchWithoutChildren_Clear_BranchIsEmpty()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            branch.Clear();

            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void TreeWithChildBranch_Clear_TreeIsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { branch });
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            tree.Clear();

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWithChildBrush_Clear_TreeIsEmpty()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            tree.Clear();

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void BranchWithChildBranch_Clear_BranchIsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);

            branch2.Clear();

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


        [Test]
        public void BranchWithChildBrush_Clear_BranchIsEmpty()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            branch.Clear();

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.IsFalse(branch.Parent.Valid);
            Assert.IsFalse(branch.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }
    }
}