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
    public partial class InsertRangeTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }


        [Test]
        public void TreeWithNoChildren_InsertRangeWithInvalidNode_TreeStaysEmpty()
        {
            const int treeUserID = 13;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.InsertRange(0, new[] { CSGTreeNode.Invalid });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add an invalid child"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void BranchWithNoChildren_InsertRangeWithInvalidNode_BranchStaysEmpty()
        {
            const int branchUserID = 13;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.InsertRange(0, new[] { CSGTreeNode.Invalid });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add an invalid child"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void TreeWithNoChildren_InsertRangeWithTree_TreeStaysEmpty()
        {
            const int treeUserID1 = 13;
            const int treeUserID2 = 14;
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = tree1.InsertRange(0, new CSGTreeNode[] { tree2 });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
            Assert.IsFalse(tree1.Dirty);
            Assert.IsFalse(tree2.Dirty);
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(0, tree2.Count);
        }

        [Test]
        public void BranchWithNoChildren_InsertRangeWithTree_BranchStaysEmpty()
        {
            const int branchUserID = 13;
            const int treeUserID = 14;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result = branch.InsertRange(0, new CSGTreeNode[] { tree });
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            Assert.IsFalse(result);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, tree.Count);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void TreeWithNoChildren_InsertRangeWithBrushes_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.InsertRange(0, new CSGTreeNode[] { brush1, brush2, brush3 });

            Assert.IsTrue(result);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.AreEqual(3, tree.Count);

            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);

            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)tree[0]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)tree[1]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)tree[2]);

            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void TreeWithAChild_InsertRangeWithBrushesAtStart_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.InsertRange(0, new CSGTreeNode[] { brush1, brush2 });

            Assert.IsTrue(result);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.AreEqual(3, tree.Count);

            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);

            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)tree[0]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)tree[1]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)tree[2]);

            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void TreeWithAChild_InsertRangeWithBrushesAtEnd_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.InsertRange(1, new CSGTreeNode[] { brush1, brush2 });

            Assert.IsTrue(result);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);

            Assert.AreEqual(3, tree.Count);

            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);

            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)tree[0]);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)tree[1]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)tree[2]);

            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }

        [Test]
        public void TreeWithTwoChildren_InsertRangeWithBrushesInMiddle_TreeContainsBrushesInOrder()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int brushUserID4 = 13;
            const int treeUserID = 14;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var brush4 = CSGTreeBrush.Create(userID: brushUserID4);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush3, brush4 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(brush4);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.InsertRange(1, new CSGTreeNode[] { brush1, brush2 });

            Assert.IsTrue(result);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsFalse(brush4.Dirty);
            Assert.AreEqual(4, tree.Count);

            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush4.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush4.Tree);

            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)tree[0]);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)tree[1]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)tree[2]);
            Assert.AreEqual((CSGTreeNode)brush4, (CSGTreeNode)tree[3]);

            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBrushWithUserID(ref brush4, brushUserID4);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }
    }
}