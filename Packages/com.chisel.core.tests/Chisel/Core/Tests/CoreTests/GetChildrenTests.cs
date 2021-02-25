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
    public partial class GetChildrenTests
    {
        [SetUp]
        public void Init()
        {
            CSGManager.Clear();
        }

        [Test]
        public void BranchWith3Children_GetChildren_Returns3Children()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2, brush3 });
            CSGManager.ClearDirty(brush1.NodeID);
            CSGManager.ClearDirty(brush2.NodeID);
            CSGManager.ClearDirty(brush3.NodeID);
            CSGManager.ClearDirty(branch.NodeID);

            var result = branch.ChildrenToArray();

            Assert.AreNotEqual(null, result);
            Assert.AreEqual(3, result.Length);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(false, brush1.Dirty);
            Assert.AreEqual(false, brush2.Dirty);
            Assert.AreEqual(false, brush3.Dirty);
            Assert.AreEqual(branch.NodeID, brush1.Parent.NodeID);
            Assert.AreEqual(0, brush1.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush3.Parent.NodeID);
            Assert.AreEqual(0, brush3.Tree.NodeID);
            Assert.AreEqual(branch.NodeID, brush2.Parent.NodeID);
            Assert.AreEqual(0, brush2.Tree.NodeID);
            Assert.AreEqual(0, branch.Parent.NodeID);
            Assert.AreEqual(0, branch.Tree.NodeID);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual(branch[0].NodeID, result[0].NodeID);
            Assert.AreEqual(branch[1].NodeID, result[1].NodeID);
            Assert.AreEqual(branch[2].NodeID, result[2].NodeID);
            Assert.AreEqual(brush1.NodeID, result[0].NodeID);
            Assert.AreEqual(brush2.NodeID, result[1].NodeID);
            Assert.AreEqual(brush3.NodeID, result[2].NodeID);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(3, CSGManager.TreeBrushCount, "Expected 3 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(4, CSGManager.TreeNodeCount, "Expected 4 TreeNodes to Exist");
        }

        [Test]
        public void BranchWithNoChildren_GetChildren_Returns0Children()
        {
            const int branchUserID = 13;
            var branch = CSGTreeBranch.Create(branchUserID);
            CSGManager.ClearDirty(branch.NodeID);

            var result = branch.ChildrenToArray();

            Assert.AreNotEqual(null, result);
            Assert.AreEqual(0, result.Length);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.AreEqual(false, branch.Dirty);
            Assert.AreEqual(0, branch.Parent.NodeID);
            Assert.AreEqual(0, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(0, CSGManager.TreeCount, "Expected 0 Trees to Exist");
            Assert.AreEqual(0, CSGManager.TreeBrushCount, "Expected 0 TreeBrushes to Exist");
            Assert.AreEqual(1, CSGManager.TreeBranchCount, "Expected 1 TreeBranch to Exist");
            Assert.AreEqual(1, CSGManager.TreeNodeCount, "Expected 1 TreeNode to Exist");
        }
    }
}