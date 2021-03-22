using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using Chisel;
using UnityEditor.SceneManagement;
using Chisel.Core;

namespace FoundationTests
{
    [TestFixture]
    public partial class AddChildTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void Branch_AddAncestor_ReturnsFalse()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            const int branchUserID3 = 12;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            var branch3 = CSGTreeBranch.Create(branchUserID3);
            branch1.Add(branch2);
            branch2.Add(branch3);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);
            CompactHierarchyManager.ClearDirty(branch3.NodeID);

            var result = branch3.Add(branch1);

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidBranchWithUserID(ref branch3, branchUserID3);
            Assert.AreEqual(branch2.NodeID, branch3.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch3.Tree.NodeID);
            Assert.AreEqual(0, branch3.Count);
            Assert.AreEqual(false, branch3.Dirty);
            Assert.AreEqual(branch1.NodeID, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual(false, branch2.Dirty);
            Assert.AreEqual(NodeID.Invalid, branch1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch1.Tree.NodeID);
            Assert.AreEqual(1, branch1.Count);
            Assert.AreEqual(false, branch1.Dirty);
        }

        [Test]
        public void Branch_AddInvalidNode_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Add(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, "Cannot add invalid node as child");

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddInvalidNode_ReturnsFalse()
        {
            const int treeUserID = 10;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Add(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, "Cannot add invalid node as child");

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void Branch_AddSelf_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Add(branch);
            LogAssert.Expect(LogType.Error, "Cannot add self as child");

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddSelf_ReturnsFalse()
        {
            const int treeUserID = 10;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Add(tree);
            LogAssert.Expect(LogType.Error, "Cannot add self as child");

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Branch_AddTree_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = branch.Add(tree);
            LogAssert.Expect(LogType.Error, "Cannot add a tree as a child");


            Assert.AreEqual(false, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(false, tree.Dirty);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddTree_ReturnsFalse()
        {
            const int treeUserID1 = 10;
            const int treeUserID2 = 11;
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = tree1.Add(tree2);
            LogAssert.Expect(LogType.Error, "Cannot add a tree as a child");

            Assert.AreEqual(false, result);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(false, tree1.Dirty);
            Assert.AreEqual(false, tree2.Dirty);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(0, tree2.Count);
        }

        [Test]
        public void Branch_AddBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(userID: branchUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(branch.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
        }

        [Test]
        public void Tree_AddBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(tree.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(1, tree.Count);
        }


        [Test]
        public void Branch_AddChildBrushOfOtherBranch_MovesBrushToBranch()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID1 = 13;
            const int branchUserID2 = 14;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            var tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { branch2 });
            Assume.That(branch2.Parent.NodeID, Is.EqualTo(tree2.NodeID));
            var branch1 = CSGTreeBranch.Create(branchUserID1, new CSGTreeNode[] { brush });
            Assume.That(brush.Parent.NodeID, Is.EqualTo(branch1.NodeID));
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { branch1 });
            Assume.That(branch1.Parent.NodeID, Is.EqualTo(tree1.NodeID));
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = branch2.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(false, TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(true, TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(tree2.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(branch2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree1.NodeID, branch1.Tree.NodeID);
            Assert.AreEqual(tree1.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(tree2.NodeID, branch2.Tree.NodeID);
            Assert.AreEqual(tree2.NodeID, branch2.Parent.NodeID);
            Assert.AreEqual(1, branch2.Count);
        }


        [Test]
        public void Tree_AddChildBrushOfOtherTree_MovesBrushToTree()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree2 = CSGTree.Create(treeUserID2);
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { brush });
            Assume.That(brush.Parent.NodeID, Is.EqualTo(tree1.NodeID));
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = tree2.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(false, TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(true, TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(tree2.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(tree2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, tree2.Count);
        }


        [Test]
        public void Branch_AddChildBrushOfOtherTree_MovesBrushToTree()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID1 = 13;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree2 = CSGTree.Create(treeUserID2);
            var branch1 = CSGTreeBranch.Create(branchUserID1, new CSGTreeNode[] { brush });
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);
            
            var result = tree2.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(false, TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(true, TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(tree2.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(tree2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree1.NodeID, branch1.Tree.NodeID);
            Assert.AreEqual(tree1.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, tree2.Count);
        }


        [Test]
        public void Tree_AddChildBrushOfOtherBranch_MovesBrushToBranch()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID2 = 14;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            var tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { branch2 });
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = branch2.Add(brush);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.AreEqual(true, tree1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(true, tree2.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(tree2.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(branch2.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree2.NodeID, branch2.Tree.NodeID);
            Assert.AreEqual(tree2.NodeID, branch2.Parent.NodeID);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual(false, TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(true,  TestUtility.IsInTree(tree2, brush));
        }


        [Test]
        public void Branch_AddSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result1 = branch.Add(brush);
            var result2 = branch.Add(brush);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, brush.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
        }

        [Test]
        public void Tree_AddSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = tree.Add(brush);
            var result2 = tree.Add(brush);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(tree.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void Branch_AddBranch_ContainsBranch()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            var result = branch2.Add(branch1);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(branch2.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(NodeID.Invalid, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(1, branch2.Count);
        }

        [Test]
        public void Tree_AddBranch_ContainsBranch()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Add(branch);

            Assert.AreEqual(true, result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(tree.NodeID, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(1, tree.Count);
        }


        [Test]
        public void Branch_AddSameBranchTwice_ReturnsFalse()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            var result1 = branch2.Add(branch1);
            var result2 = branch2.Add(branch1);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.AreEqual(true, branch1.Dirty);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(branch2.NodeID, branch1.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch1.Tree.NodeID);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(NodeID.Invalid, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(1, branch2.Count);
        }

        [Test]
        public void Tree_AddSameBranchTwice_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = tree.Add(branch);
            var result2 = tree.Add(branch);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(tree.NodeID, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(1, tree.Count);
        }


        [Test]
        public void Tree_AddBranchAddBrush_ContainsBranchThatContainsBrush()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            const int treeUserID = 12;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = branch.Add(brush);
            var result2 = tree.Add(branch);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(true, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(branch.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
        }

        [Test]
        public void Tree_AddBranchAddBrushReversed_ContainsBranchThatContainsBrush()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            const int treeUserID = 12;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = tree.Add(branch);
            var result2 = branch.Add(brush);

            Assert.AreEqual(true, result1);
            Assert.AreEqual(true, result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(true, brush.Dirty);
            Assert.AreEqual(true, TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(branch.NodeID, brush.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, brush.Tree.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Parent.NodeID);
            Assert.AreEqual(tree.NodeID, branch.Tree.NodeID);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
        }
    }
}