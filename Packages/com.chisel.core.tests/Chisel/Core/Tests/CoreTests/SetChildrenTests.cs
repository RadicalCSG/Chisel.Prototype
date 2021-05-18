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
    public partial class SetChildrenTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void Branch_SetChildrenWithAncestore_DoesNotContainAncestor()
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

            branch3.InsertRange(0, new CSGTreeNode[] { branch1 });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a descendant"));

            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidBranchWithUserID(ref branch3, branchUserID3);
            Assert.IsFalse(branch1.Dirty);
            Assert.IsFalse(branch2.Dirty);
            Assert.IsFalse(branch3.Dirty);
            Assert.IsFalse(branch3.Tree.Valid); 
            Assert.IsFalse(branch2.Tree.Valid);
            Assert.IsFalse(branch1.Parent.Valid);
            Assert.IsFalse(branch1.Tree.Valid);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual(0, branch3.Count);
            Assert.AreEqual(1, branch1.Count);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)branch3.Parent);
            Assert.AreEqual((CSGTreeNode)branch1, (CSGTreeNode)branch2.Parent);
        }


        [Test]
        public void Branch_SetChildrenWithBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            branch.InsertRange(0, new CSGTreeNode[] { brush });

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
        }


        [Test]
        public void Branch_SetChildrenWithTree_DoesNotContainTree()
        {
            const int treeUserID = 10;
            const int branchUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            branch.InsertRange(0, new CSGTreeNode[] { tree });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
            Assert.AreEqual(0, branch.Count);
        }


        [Test]
        public void Tree_SetChildrenWithTree_DoesNotContainTree()
        {
            const int treeUserID1 = 10;
            const int treeUserID2 = 11;
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            tree2.InsertRange(0, new CSGTreeNode[] { tree1 });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsFalse(tree1.Dirty);
            Assert.IsFalse(tree2.Dirty);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(0, tree2.Count);
        }

        [Test]
        public void Branch_SetChildrenWithSelf_DoesNotContainSelf()
        {
            const int branchUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            bool result = branch.InsertRange(0, new CSGTreeNode[] { branch });
            LogAssert.Expect(LogType.Error, new Regex("cannot be its own child"));

            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(result);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }


        [Test]
        public void Tree_SetChildrenWithBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree);

            tree.InsertRange(0, new CSGTreeNode[] { brush });

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
        }


        [Test]
        public void Tree_SetChildrenWithSelf_DoesNotContainsSelf()
        {
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            tree.InsertRange(0, new CSGTreeNode[] { tree });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void Branch_SetChildrenWithBranch_ContainsBranch()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);

            branch2.InsertRange(0, new CSGTreeNode[] { branch1 });

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
        public void Tree_SetChildrenWithBranch_ContainsBranch()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            tree.InsertRange(0, new CSGTreeNode[] { branch });

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
        public void Tree_SetChildrenWithBranchWithBrush_ContainsBranchThatContainsBrush()
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

            branch.InsertRange(0, new CSGTreeNode[] { brush });
            tree.InsertRange(0, new CSGTreeNode[] { branch });

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
        }        

       [Test]
        public void Tree_SetChildrenWithBranchWithBrushReversed_ContainsBranchThatContainsBrush()
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

            tree.InsertRange(0, new CSGTreeNode[] { branch });
            branch.InsertRange(0, new CSGTreeNode[] { brush });

            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)branch.Tree);
        }        
    }
}