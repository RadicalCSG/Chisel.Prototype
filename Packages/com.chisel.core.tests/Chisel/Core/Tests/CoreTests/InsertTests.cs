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
    public partial class InsertTests
    {
        BrushMeshInstance dummyBrushMeshInstance;

        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
            BrushMeshFactory.CreateBox(Vector3.one, 0, out var brushMesh);
            var surfaceDefinition = new ChiselSurfaceDefinition();
            surfaceDefinition.EnsureSize(6);
            var brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh, surfaceDefinition);
            dummyBrushMeshInstance = new BrushMeshInstance { brushMeshHash = brushMeshHash };
        }

        //[Test]
        //public void InvalidNode_InsertBranch_ReturnsFalse()
        //{
         //   const int branchUserID = 10;
         //   var branch = CSGTreeBranch.Create(branchUserID);
         //   var invalidNode = CSGTreeNode.InvalidNode;
         //   CSGUtility.ClearDirty(branch);

         //   var result = invalidNode.Insert(0, branch);

            //Assert.IsFalse(result);
            //TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            //Assert.IsFalse(branch.Dirty);
            //Assert.AreEqual(0, branch.Count);
            //Assert.AreEqual(0, CSGUtility.TreeBrushCount);
            //Assert.AreEqual(0, CSGUtility.TreeCount);
            //Assert.AreEqual(1, CSGUtility.TreeBranchCount);
            //Assert.AreEqual(1, CSGUtility.TreeNodeCount);
        //}

        [Test]
        public void Branch_InsertInvalidNode_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(0, CSGTreeNode.Invalid);
            LogAssert.Expect(LogType.Error, new Regex("Cannot add an invalid child"));
            

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_InsertSelf_ReturnsFalse()
        {
            const int branchUserID = 10;
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(0, branch);
            LogAssert.Expect(LogType.Error, new Regex("A node cannot be its own child"));            

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_InsertTree_ReturnsFalse()
        {
            const int branchUserID = 10;
            const int treeUserID = 11;
            var tree = CSGTree.Create(treeUserID);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(tree);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(0, tree);
            LogAssert.Expect(LogType.Error, new Regex("Cannot add a tree as a child"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree, treeUserID);
            Assert.IsFalse(tree.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_InsertBrushTooLargeIndex_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(1, brush);
            LogAssert.Expect(LogType.Error, new Regex("index is invalid"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_InsertBrushNegativeIndex_ReturnsFalse()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(-1, brush);
            LogAssert.Expect(LogType.Error, new Regex("index is invalid"));

            Assert.IsFalse(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsFalse(brush.Dirty);
            Assert.IsFalse(branch.Dirty);
            Assert.IsFalse(brush.Parent.Valid);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(0, branch.Count);
        }

        [Test]
        public void Branch_InsertBrush_ContainsBrush()
        {
            const int brushUserID = 10;
            const int branchUserID = 11;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID);
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(0, brush);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush.Tree.Valid);
            Assert.AreEqual(1, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush.Parent);
        }

        [Test]
        public void Branch_InsertBrushInMiddle_HasBrushInMiddle()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1, dummyBrushMeshInstance);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2, dummyBrushMeshInstance);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(1, brush3);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[2]);
        }

        [Test]
        public void Branch_InsertBrushAtZero_HasBrushInFront()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1, dummyBrushMeshInstance);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2, dummyBrushMeshInstance);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);

            var result = branch.Insert(0, brush3);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.IsFalse(brush1.Tree.Valid);
            Assert.IsFalse(brush2.Tree.Valid);
            Assert.IsFalse(brush3.Tree.Valid);
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[2]);
        }

        [Test]
        public void Branch_InsertBrushAtEnd_HasBrushInBack()
        {
            const int brushUserID1 = 10;
            const int brushUserID2 = 11;
            const int brushUserID3 = 12;
            const int branchUserID = 13;
            const int treeUserID = 14;
            var brush1 = CSGTreeBrush.Create(userID: brushUserID1, dummyBrushMeshInstance);
            var brush2 = CSGTreeBrush.Create(userID: brushUserID2, dummyBrushMeshInstance);
            var brush3 = CSGTreeBrush.Create(userID: brushUserID3, dummyBrushMeshInstance);
            var branch = CSGTreeBranch.Create(branchUserID, new CSGTreeNode[] { brush1, brush2 });
            var tree = CSGTree.Create(treeUserID, new CSGTreeNode[] { branch });
            CompactHierarchyManager.ClearDirty(brush1);
            CompactHierarchyManager.ClearDirty(brush2);
            CompactHierarchyManager.ClearDirty(brush3);
            CompactHierarchyManager.ClearDirty(branch);
            CompactHierarchyManager.ClearDirty(tree);

            var result = branch.Insert(2, brush3);

            Assert.IsTrue(result);
            TestUtility.ExpectValidBrushWithUserID(ref brush1, brushUserID1);
            TestUtility.ExpectValidBrushWithUserID(ref brush2, brushUserID2);
            TestUtility.ExpectValidBrushWithUserID(ref brush3, brushUserID3);
            TestUtility.ExpectValidBranchWithUserID(ref branch, branchUserID);
            Assert.IsTrue(tree.Dirty);
            Assert.IsTrue(branch.Dirty);
            Assert.IsFalse(brush1.Dirty);
            Assert.IsFalse(brush2.Dirty);
            Assert.IsTrue(brush3.Dirty);
            Assert.IsTrue(TestUtility.IsInTree(tree, brush1));
            Assert.IsTrue(TestUtility.IsInTree(tree, brush2));
            Assert.IsTrue(TestUtility.IsInTree(tree, brush3));
            Assert.AreEqual(3, TestUtility.CountOfBrushesInTree(tree));
            Assert.AreEqual(3, branch.Count);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush1.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush2.Parent);
            Assert.AreEqual((CSGTreeNode)branch, (CSGTreeNode)brush3.Parent);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush1.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush2.Tree);
            Assert.AreEqual((CSGTreeNode)tree, (CSGTreeNode)brush3.Tree);
            Assert.AreEqual((CSGTreeNode)brush1, (CSGTreeNode)branch[0]);
            Assert.AreEqual((CSGTreeNode)brush2, (CSGTreeNode)branch[1]);
            Assert.AreEqual((CSGTreeNode)brush3, (CSGTreeNode)branch[2]);
        }


        [Test]
        public void Branch_InsertChildBrushOfOtherBranch_MovesBrushToBranch()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID1 = 13;
            const int branchUserID2 = 14;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            var tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { branch2 });
            var branch1 = CSGTreeBranch.Create(branchUserID1, new CSGTreeNode[] { brush });
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = branch2.Insert(0, brush);

            Assert.IsTrue(result);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree1));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree2));
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Tree);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Parent);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Parent);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
        }


        [Test]
        public void Tree_InsertChildBrushOfOtherTree_MovesBrushToTree()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var tree2 = CSGTree.Create(treeUserID2);
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = tree2.Insert(0, brush);

            Assert.IsTrue(result);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree1));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree2));
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, tree2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Parent);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
        }


        [Test]
        public void Branch_InsertChildBrushOfOtherTree_MovesBrushToTree()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID1 = 13;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var tree2 = CSGTree.Create(treeUserID2);
            var branch1 = CSGTreeBranch.Create(branchUserID1, new CSGTreeNode[] { brush });
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { branch1 });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch1);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = tree2.Insert(0, brush);

            Assert.IsTrue(result);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch1.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree1));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree2));
            Assert.AreEqual(0, branch1.Count);
            Assert.AreEqual(1, tree2.Count);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Tree);
            Assert.AreEqual((CSGTreeNode)tree1, (CSGTreeNode)branch1.Parent);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch1, branchUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
        }


        [Test]
        public void Tree_InsertChildBrushOfOtherBranch_MovesBrushToBranch()
        {
            const int brushUserID = 10;
            const int treeUserID1 = 11;
            const int treeUserID2 = 12;
            const int branchUserID2 = 14;
            var brush = CSGTreeBrush.Create(userID: brushUserID, dummyBrushMeshInstance);
            var branch2 = CSGTreeBranch.Create(branchUserID2);
            var tree2 = CSGTree.Create(treeUserID2, new CSGTreeNode[] { branch2 });
            var tree1 = CSGTree.Create(treeUserID1, new CSGTreeNode[] { brush });
            CompactHierarchyManager.ClearDirty(brush);
            CompactHierarchyManager.ClearDirty(branch2);
            CompactHierarchyManager.ClearDirty(tree1);
            CompactHierarchyManager.ClearDirty(tree2);

            var result = branch2.Insert(0, brush);

            Assert.IsTrue(result);
            Assert.IsTrue(brush.Dirty);
            Assert.IsTrue(branch2.Dirty);
            Assert.IsTrue(tree1.Dirty);
            Assert.IsTrue(tree2.Dirty);
            Assert.IsFalse(TestUtility.IsInTree(tree1, brush));
            Assert.IsTrue(TestUtility.IsInTree(tree2, brush));
            Assert.AreEqual(0, TestUtility.CountOfBrushesInTree(tree1));
            Assert.AreEqual(1, TestUtility.CountOfBrushesInTree(tree2));
            Assert.AreEqual(0, tree1.Count);
            Assert.AreEqual(1, branch2.Count);
            Assert.AreEqual((CSGTreeNode)branch2, (CSGTreeNode)brush.Parent);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)brush.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Tree);
            Assert.AreEqual((CSGTreeNode)tree2, (CSGTreeNode)branch2.Parent);
            TestUtility.ExpectValidBrushWithUserID(ref brush, brushUserID);
            TestUtility.ExpectValidBranchWithUserID(ref branch2, branchUserID2);
            TestUtility.ExpectValidTreeWithUserID(ref tree1, treeUserID1);
            TestUtility.ExpectValidTreeWithUserID(ref tree2, treeUserID2);
        }

    }
}