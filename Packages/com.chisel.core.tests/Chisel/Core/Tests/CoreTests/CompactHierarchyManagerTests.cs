using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Collections;
using Chisel;
using Chisel.Core;

namespace FoundationTests
{
    // TODO: add tests for IDManager

    // Test Delete -> completely remove it from hierarchy (children would be dangling)
    // Test DeleteRecursive -> delete all child nodes as well

    // Test iterating through all (dangling) nodes in hierarchy
    // Test iterating through children of branch (GetChildAt)

    // Test modifying the root

    // Test adding nodeids to wrong hierarchy

    // Tests attaching brushes that are already a child of another branch

    // Add reordering tests
    // Add tests for id generations

    // Add tests for hierarchy compacting
    // Add tests to merge in hierarchies as branches

    // tests for all methods dealing with children of node
    // enable/disable node => still visible until something else is modified (no dirty?)

    [TestFixture]
    class CompactHierarchyManagerTests
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        [Test]
        public void CompactHierarchyManager_MoveChildNodeBetweenHierarchies_ChildNodeOnlyExistsInDestinationHierarchy()
        {
            var oldBranchNodeID = CompactHierarchyManager.CreateBranch();
            var treeNodeID      = CompactHierarchyManager.CreateTree();
            CompactHierarchyManager.CheckConsistency();

            Assume.That(CompactHierarchyManager.IsValidNodeID(oldBranchNodeID), Is.True);
            Assume.That(CompactHierarchyManager.IsValidNodeID(treeNodeID), Is.True);
            var oldBranceCompactNodeID = CompactHierarchyManager.GetCompactNodeID(oldBranchNodeID);

            var newBranchCompactNodeID = CompactHierarchyManager.MoveChildNode(oldBranchNodeID, CompactHierarchyManager.GetHierarchyID(treeNodeID));
            Assert.AreEqual((true, false, newBranchCompactNodeID), 
                            (CompactHierarchyManager.IsValidCompactNodeID(newBranchCompactNodeID), 
                             CompactHierarchyManager.IsValidCompactNodeID(oldBranceCompactNodeID),
                             CompactHierarchyManager.GetCompactNodeID(oldBranchNodeID)));
            CompactHierarchyManager.CheckConsistency();
        }


        [Test]
        public void CompactHierarchyManager_RecycleID_EverythingWorksFine()
        {
            var oldNodeID           = CompactHierarchyManager.CreateBranch();
            var oldCompactNodeID    = CompactHierarchyManager.GetCompactNodeID(oldNodeID);
            CompactHierarchyManager.CheckConsistency();

            CompactHierarchyManager.DestroyNode(oldNodeID);
            var newNodeID           = CompactHierarchyManager.CreateBranch();
            var newCompactNodeID = CompactHierarchyManager.GetCompactNodeID(newNodeID);
            Assume.That(newNodeID.value, Is.EqualTo(oldNodeID.value));
            Assume.That(newNodeID.generation, Is.EqualTo(oldNodeID.generation + 1));
            Assume.That(newCompactNodeID.value, Is.EqualTo(oldCompactNodeID.value));
            Assume.That(newCompactNodeID.generation, Is.EqualTo(oldCompactNodeID.generation + 1));

            Assert.AreEqual((true, true, false, false),
                            (CompactHierarchyManager.IsValidNodeID(newNodeID),
                             CompactHierarchyManager.IsValidCompactNodeID(newCompactNodeID),
                             CompactHierarchyManager.IsValidNodeID(oldNodeID),
                             CompactHierarchyManager.IsValidCompactNodeID(oldCompactNodeID)));
            CompactHierarchyManager.CheckConsistency();
        }

        const int treeUserID = 1;
        const int branchUserID1 = 2;
        const int branchUserID2 = 3;
        const int branchUserID3 = 4;
        const int branchUserID4 = 5;
        const int branchUserID5 = 6;
        const int branchUserID6 = 7;
        const int branchUserID7 = 5;
        const int branchUserID8 = 6;
        const int branchUserID9 = 7;
        void Add3Branches(out NodeID tree, out NodeID child1, out NodeID child2, out NodeID child3)
        { 
            tree    = CompactHierarchyManager.CreateTree(treeUserID);
            child1  = CompactHierarchyManager.CreateBranch(branchUserID1);
            child2  = CompactHierarchyManager.CreateBranch(branchUserID2);
            child3  = CompactHierarchyManager.CreateBranch(branchUserID3);
            CompactHierarchyManager.CheckConsistency();


            CompactHierarchyManager.AddChildNode(tree, child1);
            CompactHierarchyManager.AddChildNode(tree, child2);
            CompactHierarchyManager.AddChildNode(tree, child3);
            var child1_parent = CompactHierarchyManager.GetParentOfNode(child1);
            var child2_parent = CompactHierarchyManager.GetParentOfNode(child2);
            var child3_parent = CompactHierarchyManager.GetParentOfNode(child3);
            Assume.That((child1_parent, child2_parent, child3_parent), Is.EqualTo((tree, tree, tree)));
            Assume.That(3, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var child1_siblingIndex = CompactHierarchyManager.SiblingIndexOf(tree, child1);
            var child2_siblingIndex = CompactHierarchyManager.SiblingIndexOf(tree, child2);
            var child3_siblingIndex = CompactHierarchyManager.SiblingIndexOf(tree, child3);
            Assume.That((0, 1, 2), Is.EqualTo((child1_siblingIndex, child2_siblingIndex, child3_siblingIndex)));
            var tree_userID = CompactHierarchyManager.GetUserIDOfNode(tree);
            var child1_userID = CompactHierarchyManager.GetUserIDOfNode(child1);
            var child2_userID = CompactHierarchyManager.GetUserIDOfNode(child2);
            var child3_userID = CompactHierarchyManager.GetUserIDOfNode(child3);
            Assume.That((tree_userID, child1_userID, child2_userID, child3_userID), 
                        Is.EqualTo((treeUserID, branchUserID1, branchUserID2, branchUserID3)));
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void RootWith3Children_GetParentsOfChildren_ParentsAreRoot()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child2),
                           CompactHierarchyManager.GetParentOfNode(child3));

            Assert.AreEqual((NodeID.Invalid, tree, tree, tree), parents);
        }

        [Test]
        public void RootWith3Children_DeleteLastChild_LastChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.DestroyNode(child3);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child2));
            Assume.That(parents, Is.EqualTo((NodeID.Invalid, tree, tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((treeUserID, branchUserID1, branchUserID2, 0)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, true, false), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void RootWith3Children_DeleteMiddleChild_FirstChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.DestroyNode(child2);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child3));
            Assume.That(parents, Is.EqualTo((NodeID.Invalid, tree, tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((treeUserID, branchUserID1, 0, branchUserID3)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, false, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void RootWith3Children_DeleteFirstChild_FirstChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.DestroyNode(child1);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child2),
                           CompactHierarchyManager.GetParentOfNode(child3));
            Assume.That(parents, Is.EqualTo((NodeID.Invalid, tree, tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((treeUserID, 0, branchUserID2, branchUserID3)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, false, true, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void RootWith3Children_RemoveLastChild_LastChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.RemoveChildNodeAt(tree, 2);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((treeUserID, branchUserID1, branchUserID2, branchUserID3)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));
            Assume.That((true, true, true, true), Is.EqualTo(validNodes));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child2),
                           CompactHierarchyManager.GetParentOfNode(child3));

            Assert.AreEqual((NodeID.Invalid, tree, tree, NodeID.Invalid), parents);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void RootWith3Children_RemoveMiddleChild_FirstChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.RemoveChildNodeAt(tree, 1);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((treeUserID, branchUserID1, branchUserID2, branchUserID3)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));
            Assume.That((true, true, true, true), Is.EqualTo(validNodes));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child2),
                           CompactHierarchyManager.GetParentOfNode(child3));

            Assert.AreEqual((NodeID.Invalid, tree, NodeID.Invalid, tree), parents);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void ChildWithChild_AddedToRoot_HierarchyIsCorrect()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.AddChildNode(child2, child3);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(child2)));
            CompactHierarchyManager.AddChildNode(child1, child2);
            Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(child1)));
            Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(child2)));
            //CompactHierarchyManager.AddChildNode(tree, child1);
            //Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            //Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(child1)));
            //Assume.That(1, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(child2)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));
            Assume.That((true, true, true, true), Is.EqualTo(validNodes));
            var parents = (CompactHierarchyManager.GetParentOfNode(tree),
                           CompactHierarchyManager.GetParentOfNode(child1),
                           CompactHierarchyManager.GetParentOfNode(child2),
                           CompactHierarchyManager.GetParentOfNode(child3));

            Assert.AreEqual((NodeID.Invalid, tree, child1, child2), parents);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void AddBranchWithChildrenToTree_AllNodesAreValid()
        {
            var tree = CompactHierarchyManager.CreateTree(treeUserID);
            var branch1 = CompactHierarchyManager.CreateBranch(branchUserID7);
            var child1 = CompactHierarchyManager.CreateBranch(branchUserID1);
            var child2 = CompactHierarchyManager.CreateBranch(branchUserID2);
            CompactHierarchyManager.CheckConsistency();

            CompactHierarchyManager.CheckConsistency();
            CompactHierarchyManager.AddChildNode(branch1, child1);
            CompactHierarchyManager.AddChildNode(branch1, child2);
            CompactHierarchyManager.CheckConsistency();
            CompactHierarchyManager.AddChildNode(tree, branch1);

            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2));

            Assert.AreEqual((true, true, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void AddChildrenToBranchWithGap_AllNodesAreValid()
        {
            var branch1 = CompactHierarchyManager.CreateBranch(branchUserID7);
            var child1 = CompactHierarchyManager.CreateBranch(branchUserID1);
            var child2 = CompactHierarchyManager.CreateBranch(branchUserID2);
            var child3 = CompactHierarchyManager.CreateBranch(branchUserID3);
            CompactHierarchyManager.CheckConsistency();

            CompactHierarchyManager.AddChildNode(branch1, child1);
            CompactHierarchyManager.AddChildNode(branch1, child3);
            CompactHierarchyManager.CheckConsistency();

            var validNodes = (CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void AddChildOfBranchToAnotherBranch_AllNodesAreValid()
        {
            var branch2 = CompactHierarchyManager.CreateBranch(branchUserID8);
            var branch3 = CompactHierarchyManager.CreateBranch(branchUserID9);
            var child1 = CompactHierarchyManager.CreateBranch(branchUserID1);
            var child2 = CompactHierarchyManager.CreateBranch(branchUserID2);
            var child3 = CompactHierarchyManager.CreateBranch(branchUserID3);
            CompactHierarchyManager.CheckConsistency();

            CompactHierarchyManager.AddChildNode(branch2, child1);
            CompactHierarchyManager.AddChildNode(branch2, child2);
            CompactHierarchyManager.AddChildNode(branch3, child3);
            CompactHierarchyManager.AddChildNode(branch3, child1);

            var validNodes = (CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }

        [Test]
        public void AddBranchesWithChildrenToTreeInRandomOrder_AllNodesAreValid()
        {
            var tree = CompactHierarchyManager.CreateTree(treeUserID);
            var branch1 = CompactHierarchyManager.CreateBranch(branchUserID7);
            var branch2 = CompactHierarchyManager.CreateBranch(branchUserID8);
            var branch3 = CompactHierarchyManager.CreateBranch(branchUserID9);
            var child1 = CompactHierarchyManager.CreateBranch(branchUserID1);
            var child2 = CompactHierarchyManager.CreateBranch(branchUserID2);
            var child3 = CompactHierarchyManager.CreateBranch(branchUserID3);
            var child4 = CompactHierarchyManager.CreateBranch(branchUserID4);
            var child5 = CompactHierarchyManager.CreateBranch(branchUserID5);
            var child6 = CompactHierarchyManager.CreateBranch(branchUserID6);
            CompactHierarchyManager.CheckConsistency();

            CompactHierarchyManager.AddChildNode(branch2, child5);
            CompactHierarchyManager.AddChildNode(branch2, child6);
            CompactHierarchyManager.AddChildNode(branch3, child4);
            CompactHierarchyManager.AddChildNode(branch3, child3);
            CompactHierarchyManager.AddChildNode(branch1, child1);
            CompactHierarchyManager.AddChildNode(branch1, child2);
            CompactHierarchyManager.AddChildNode(tree, branch1);
            CompactHierarchyManager.AddChildNode(tree, branch3);
            CompactHierarchyManager.AddChildNode(tree, branch2);

            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3),
                              CompactHierarchyManager.IsValidNodeID(child4),
                              CompactHierarchyManager.IsValidNodeID(child5),
                              CompactHierarchyManager.IsValidNodeID(child6));

            Assert.AreEqual((true, true, true, true, true, true, true), validNodes);
            CompactHierarchyManager.CheckConsistency();
        }
    }
}
