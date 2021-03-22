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
    public partial class DestroyTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        internal static bool DestroyNodes(CSGTreeNode[] nodeIDs)
        {
            bool success = true;
            for (int i = 0; i < nodeIDs.Length; i++)
            {
                success = nodeIDs[i].Destroy() && success;
            }
            return success;
        }

        [Test]
        public void DestroyInvalidNode()
        {
            var invalidNode = CSGTreeNode.InvalidNode;

            var result = invalidNode.Destroy();
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(false, result);
        }


        [Test]
        public void DestroyNode_InvalidNode_Multiple()
        {
            var result = DestroyNodes(new CSGTreeNode[] { CSGTreeNode.InvalidNode, CSGTreeNode.InvalidNode });
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(false, result);
        }

        [Test]
        public void DestroyNode_Duplicates()
        {
            const int brushUserID0 = 10;
            const int brushUserID1 = 11;
            var brush0 = CSGTreeBrush.Create(userID: brushUserID0);
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            CompactHierarchyManager.ClearDirty(brush0.NodeID);
            CompactHierarchyManager.ClearDirty(brush1.NodeID);

            var result = DestroyNodes(new CSGTreeNode[] { brush0, brush1, brush1 });
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(false, result);
            TestUtility.ExpectInvalidBrush(ref brush0);
            TestUtility.ExpectInvalidBrush(ref brush1);
        }

        [Test]
        public void DestroyBrush()
        {
            const int brushUserID = 10;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
        }

        [Test]
        public void DestroyBranch()
        {
            const int branchUserID = 10;
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = branch.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch);
        }

        [Test]
        public void DestroyTree()
        {
            const int treeUserID = 10;
            CSGTree tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = tree.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidTree(ref tree);
        }

        [Test]
        public void DestroyBrush_Twice_ReturnsFalse()
        {
            const int brushUserID = 10;
            CSGTreeBrush brush = CSGTreeBrush.Create(userID: brushUserID);
            CompactHierarchyManager.ClearDirty(brush.NodeID);

            var result1 = brush.Destroy();
            var result2 = brush.Destroy();
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidBrush(ref brush);
        }

        [Test]
        public void DestroyBranch_Twice_ReturnsFalse()
        {
            const int branchUserID = 10;
            CSGTreeBranch branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result1 = branch.Destroy();
            var result2 = branch.Destroy();
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidBranch(ref branch);
        }

        [Test]
        public void DestroyTree_Twice_ReturnsFalse()
        {
            const int treeUserID = 10;
            CSGTree tree = CSGTree.Create(treeUserID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result1 = tree.Destroy();
            var result2 = tree.Destroy();
            LogAssert.Expect(LogType.Error, new Regex("is invalid"));

            Assert.AreEqual(true, result1);
            Assert.AreEqual(false, result2);
            TestUtility.ExpectInvalidTree(ref tree);
        }


        [Test]
        public void DestroyBrush_Multiple()
        {
            const int brushUserID0 = 10;
            const int brushUserID1 = 11;
            const int brushUserID2 = 12;
            var brush0 = CSGTreeBrush.Create(userID: brushUserID0);
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2);
            CompactHierarchyManager.ClearDirty(brush0.NodeID);
            CompactHierarchyManager.ClearDirty(brush1.NodeID);
            CompactHierarchyManager.ClearDirty(brush2.NodeID);

            var result = DestroyNodes(new CSGTreeNode[] { brush0, brush1, brush2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush0);
            TestUtility.ExpectInvalidBrush(ref brush1);
            TestUtility.ExpectInvalidBrush(ref brush2);
        }

        [Test]
        public void DestroyBranch_Multiple()
        {
            const int branchUserID0 = 10;
            const int branchUserID1 = 11;
            const int branchUserID2 = 12;
            var branch0 = CSGTreeBranch.Create(branchUserID0);
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            CompactHierarchyManager.ClearDirty(branch0.NodeID);
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            var result = DestroyNodes(new CSGTreeNode[] { branch0, branch1, branch2 });

            TestUtility.ExpectInvalidBranch(ref branch0);
            TestUtility.ExpectInvalidBranch(ref branch1);
            TestUtility.ExpectInvalidBranch(ref branch2);
            Assert.AreEqual(true, result);
        }

        [Test]
        public void DestroyTree_Multiple()
        {
            const int treeUserID0 = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            var tree0 = CSGTree.Create(treeUserID0);
            var tree1 = CSGTree.Create(treeUserID1);
            var tree2 = CSGTree.Create(treeUserID2);
            CompactHierarchyManager.ClearDirty(tree0.NodeID);
            CompactHierarchyManager.ClearDirty(tree1.NodeID);
            CompactHierarchyManager.ClearDirty(tree2.NodeID);

            var result = DestroyNodes(new CSGTreeNode[] { tree0, tree1, tree2 });

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidTree(ref tree0);
            TestUtility.ExpectInvalidTree(ref tree1);
            TestUtility.ExpectInvalidTree(ref tree2);
        }

        [Test]
        public void BranchWithBrush_DestroyBrush_BranchIsEmpty()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(branch.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(true, branch.Dirty);
            Assert.AreEqual(false, brush.Valid);
            Assert.AreEqual(NodeID.Invalid, branch.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch.Tree.NodeID);
            Assert.AreEqual(0, branch.Count);
        }


        [Test]
        public void BranchWithBranch_DestroyBranch_BranchIsEmpty()
        {
            const int branchUserID1 = 10;
            const int branchUserID2 = 11;
            var branch1 = CSGTreeBranch.Create(branchUserID1);
            var branch2 = CSGTreeBranch.Create(branchUserID2, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(branch1.NodeID);
            CompactHierarchyManager.ClearDirty(branch2.NodeID);

            var result = branch1.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch1);
            Assert.AreEqual(true, branch2.Dirty);
            Assert.AreEqual(false, branch1.Valid);
            Assert.AreEqual(NodeID.Invalid, branch2.Parent.NodeID);
            Assert.AreEqual(NodeID.Invalid, branch2.Tree.NodeID);
            Assert.AreEqual(0, branch2.Count);
        }

        [Test]
        public void TreeWithBranch_DestroyBranch_TreeIsEmpty()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var branch = CSGTreeBranch.Create(branchUserID);
            var tree = CSGTree.Create(treeUserID);
            tree.InsertRange(0, new CSGTreeNode[] { branch });
            Assume.That(tree.Count, Is.EqualTo(1));
            Assume.That(branch.Parent.NodeID, Is.EqualTo(tree.NodeID));
            CompactHierarchyManager.ClearDirty(branch.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = branch.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBranch(ref branch);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(false, branch.Valid);
            Assert.AreEqual(0, tree.Count);
        }

        [Test]
        public void TreeWithBrush_DestroyBrush_TreeIsEmpty()
        {
            const int brushUserID = 10;
            const int treeUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID);
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush.NodeID);
            CompactHierarchyManager.ClearDirty(tree.NodeID);

            var result = brush.Destroy();

            Assert.AreEqual(true, result);
            TestUtility.ExpectInvalidBrush(ref brush);
            Assert.AreEqual(true, tree.Dirty);
            Assert.AreEqual(false, brush.Valid);
            Assert.AreEqual(0, tree.Count);
        }

    }
}