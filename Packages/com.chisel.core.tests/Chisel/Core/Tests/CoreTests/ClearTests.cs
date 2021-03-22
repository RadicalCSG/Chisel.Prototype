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
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            tree.Clear();

            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void BranchWithoutChildren_Clear_BranchIsEmpty()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            branch.Clear();

            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void TreeWithChildBranch_Clear_TreeIsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { branch });
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            tree.Clear();

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
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
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            tree.Clear();

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void BranchWithChildBranch_Clear_BranchIsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            branch2.Clear();

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


        [Test]
        public void BranchWithChildBrush_Clear_BranchIsEmpty()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            branch.Clear();

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }
    }
}