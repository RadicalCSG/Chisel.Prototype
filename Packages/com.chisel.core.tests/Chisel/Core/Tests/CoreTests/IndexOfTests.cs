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
    public partial class IndexOfTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }


        [Test]
        public void Tree_AddBrushes_HaveInOrderIndices()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID);
            var result1 = tree.Add(brush1);
            var result2 = tree.Add(brush2);
            var result3 = tree.Add(brush3);
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(tree);

            var index1 = tree.IndexOf(brush1);
            var index2 = tree.IndexOf(brush2);
            var index3 = tree.IndexOf(brush3);

            Assert.AreEqual(0, index1);//2
            Assert.AreEqual(1, index2);//3
            Assert.AreEqual(2, index3);//0
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
            Assert.IsFalse(tree.Dirty);
            Assert.AreEqual(3, tree.Count);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
        }


        [Test]
        public void Branch_AddBrushes_HaveInOrderIndices()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID);
            var result1 = branch.Add(brush1);
            var result2 = branch.Add(brush2);
            var result3 = branch.Add(brush3);
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var index1 = branch.IndexOf(brush1);
            var index2 = branch.IndexOf(brush2);
            var index3 = branch.IndexOf(brush3);

            Assert.AreEqual(0, index1);
            Assert.AreEqual(1, index2);
            Assert.AreEqual(2, index3);
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
            Assert.IsTrue(result3);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
        }


        [Test]
        public void Tree_IndexOfNonChildBrush_IsMinusOne()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int treeUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var tree = CSGTree.Create(treeUserID);
            tree.Add(brush1);
            tree.Add(brush2);
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(tree);

            var index = tree.IndexOf(brush3);
            
            Assert.AreEqual(-1, index);
        }

    }
}