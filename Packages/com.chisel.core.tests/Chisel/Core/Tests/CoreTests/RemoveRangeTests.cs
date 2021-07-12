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
    public partial class RemoveRangeTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }


        [Test]
        public void TreeWithNoChildren_RemoveRange_ReturnsFalse()
        {
            const int treeUserID = 13;
            var tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(0, 1);
            LogAssert.Expect(LogType.Error, new Regex("must be below or equal to"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAt0WithCount2_TreeContainsLastBrush()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);
            
            var result = tree.RemoveRange(index:0, count:2);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsTrue(tree.Dirty);

            Assert.IsFalse(brush1.Parent.Valid);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)tree[0]);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAt1WithCount2_TreeContainsFirstBrush()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(1, 2);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsFalse(brush3.Parent.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.AreEqual(1, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)tree[0]);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAt0WithCount3_TreeIsEmpty()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(0, 3);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.IsTrue(tree.Dirty);
            Assert.IsFalse(brush1.Parent.Valid);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Parent.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(0, tree.Count);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAtWithNegativeIndex_ReturnsFalse()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(-1, 3);
            LogAssert.Expect(LogType.Error, new Regex("must be positive"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAt0WithTooLargeCount_ReturnsFalse()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(0, 4);
            LogAssert.Expect(LogType.Error, new Regex("must be below or equal to"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeAt1WithTooLargeCount_ReturnsFalse()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(1, 3);
            LogAssert.Expect(LogType.Error, new Regex("must be below or equal to"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeWithCount0AndValidIndex_ReturnsTrueButRemovesNothing()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(0, 0);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
        }


        [Test]
        public void TreeWith3Children_RemoveRangeWithCount0AndInvalidIndex_ReturnsTrue()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var result = tree.RemoveRange(3, 0);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
        }
    }
}