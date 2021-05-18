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
    public partial class RemoveAtTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void BranchWithNoChildren_RemoveAtZero_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);
            LogAssert.Expect(LogType.Error, new Regex("must be between"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void BranchWithSingleChild_RemoveLastNode_IsEmptyBranch()
        {
            const int branchUserID = 10;
            const int brushUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void BranchWithTwoChildren_RemoveLastNode_StillHasFirstNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(1);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
        }

        [Test]
        public void BranchWithTwoChildren_RemoveFirstNode_StillHasLastNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(0);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            Assert.IsTrue(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Parent.Valid);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
        }

        [Test]
        public void BranchWithThreeChildren_RemoveMiddleNode_StillHasFirstAndLastNode()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(1);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsTrue(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Parent.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(2, branch.Count);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
        }

        [Test]
        public void BranchWithThreeChildren_RemoveWithTooLargeIndex_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(3);
            LogAssert.Expect(LogType.Error, new Regex("must be between"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[2]);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
        }

        [Test]
        public void BranchWithThreeChildren_RemoveWithNegativeIndex_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            const int brushUserID3 = 12;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.RemoveAt(-1);
            LogAssert.Expect(LogType.Error, new Regex("must be between"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsFalse(brush3.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[2]);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
        }
    }
}