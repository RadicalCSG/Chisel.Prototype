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
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(branch3);

            var result = branch3.Add(branch1);

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidBranchWithUserID(ref branch3, branchUserID3);
            Assert.IsFalse(branch3.Tree.Valid);
            Assert.IsFalse(branch3.Dirty);
            Assert.IsFalse(branch2.Tree.Valid);
            Assert.IsFalse(branch2.Dirty);
            Assert.IsFalse(branch1.Parent.Valid);
            Assert.IsFalse(branch1.Tree.Valid);
            Assert.IsFalse(branch1.Dirty);
            Assert.AreEqual(0, branch3.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual(1, branch1.Count);            
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)branch3.Parent);
            Assert.AreEqual((CSGTreeNode)branch1, (CSGTreeNode)branch2.Parent);
        }

        [Test]
        public void Branch_AddInvalidNode_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Add(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, "Cannot add invalid node as child");

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddInvalidNode_ReturnsFalse()
        {
            const int treeUserID = 10;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Add(CSGTreeNode.InvalidNode);
            LogAssert.Expect(LogType.Error, "Cannot add invalid node as child");

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void Branch_AddSelf_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Add(branch);
            LogAssert.Expect(LogType.Error, "Cannot add self as child");

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddSelf_ReturnsFalse()
        {
            const int treeUserID = 10;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Add(tree);
            LogAssert.Expect(LogType.Error, "Cannot add self as child");

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Branch_AddTree_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result = branch.Add(tree);
            LogAssert.Expect(LogType.Error, "Cannot add a tree as a child");


            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Tree_AddTree_ReturnsFalse()
        {
            const int treeUserID1 = 10;
            const int treeUserID2 = 11;
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = tree1.Add(tree2);
            LogAssert.Expect(LogType.Error, "Cannot add a tree as a child");

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsFalse(tree1.Dirty);
            Assert.IsFalse(tree2.Dirty);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
        }

        [Test]
        public void Tree_AddBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
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
            Assume.That((CSGTreeNode)branch2.Parent, Is.EqualTo((CSGTreeNode)tree2));
            var branch1 = CSGTreeBranch.Create(branchUserID1, new CSGTreeNode[] { brush });
            Assume.That((CSGTreeNode)brush.Parent, Is.EqualTo((CSGTreeNode)branch1));
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { branch1 });
            Assume.That((CSGTreeNode)branch1.Parent, Is.EqualTo((CSGTreeNode)tree1));
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = branch2.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Tree);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Parent);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Parent);
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
            Assume.That((CSGTreeNode)brush.Parent, Is.EqualTo((CSGTreeNode)tree1));
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = tree2.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, tree2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Parent);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);
            
            var result = tree2.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, tree2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Tree);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Parent);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = branch2.Add(brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Parent);
        }


        [Test]
        public void Branch_AddSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result1 = branch.Add(brush);
            var result2 = branch.Add(brush);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
        }

        [Test]
        public void Tree_AddSameBrushTwice_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            var result1 = tree.Add(brush);
            var result2 = tree.Add(brush);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
        }

        [Test]
        public void Branch_AddBranch_ContainsBranch()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);

            var result = branch2.Add(branch1);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsFalse(branch1.Tree.Valid);
            Assert.IsFalse(branch2.Parent.Valid);
            Assert.IsFalse(branch2.Tree.Valid);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)branch1.Parent);
        }

        [Test]
        public void Tree_AddBranch_ContainsBranch()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.Add(branch);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
        }


        [Test]
        public void Branch_AddSameBranchTwice_ReturnsFalse()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);

            var result1 = branch2.Add(branch1);
            var result2 = branch2.Add(branch1);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsFalse(branch1.Tree.Valid);
            Assert.IsFalse(branch2.Parent.Valid);
            Assert.IsFalse(branch2.Tree.Valid);
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)branch1.Parent);
        }

        [Test]
        public void Tree_AddSameBranchTwice_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result1 = tree.Add(branch);
            var result2 = tree.Add(branch);

            Assert.IsTrue(result1);
            Assert.IsFalse(result2);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result1 = branch.Add(brush);
            var result2 = tree.Add(branch);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
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
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result1 = tree.Add(branch);
            var result2 = branch.Add(brush);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
        }
    }
}