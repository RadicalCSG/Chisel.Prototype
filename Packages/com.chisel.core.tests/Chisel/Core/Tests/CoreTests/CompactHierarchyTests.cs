using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Collections;
using Chisel;
using Chisel.Core;

namespace FoundationTests
{
    [TestFixture]
    class CompactHierarchyTests 
    {
        [SetUp]
        public void Init()
        {
            CompactHierarchyManager.Clear();
        }

        void Add2Branches(in CompactHierarchy compactHierarchy, out CompactNodeID branch0_ID, out CompactNodeID branch1_ID, int userID1 = 1, int userID2 = 2)
        {
            Add2Branches(in compactHierarchy, compactHierarchy.RootID, out branch0_ID, out branch1_ID, userID1, userID2);
        }

        void Add2Branches(in CompactHierarchy compactHierarchy, CompactNodeID parentID, out CompactNodeID branch0_ID, out CompactNodeID branch1_ID, int userID1 = 1, int userID2 = 2)
        {
            branch0_ID = compactHierarchy.CreateBranch(NodeID.Invalid, userID: userID1);
            branch1_ID = compactHierarchy.CreateBranch(NodeID.Invalid, userID: userID2);
            compactHierarchy.AttachToParent(parentID, branch0_ID);
            compactHierarchy.AttachToParent(parentID, branch1_ID);
            var siblingIndex0 = compactHierarchy.SiblingIndexOf(branch0_ID);
            var siblingIndex1 = compactHierarchy.SiblingIndexOf(branch1_ID);
            Assume.That((0, 1), Is.EqualTo((siblingIndex0, siblingIndex1)));
            Assume.That((compactHierarchy.ParentOf(branch0_ID), compactHierarchy.ParentOf(branch1_ID)), Is.EqualTo((parentID, parentID)));
            ref var child0 = ref compactHierarchy.GetChildRefAt(parentID, 0);
            ref var child1 = ref compactHierarchy.GetChildRefAt(parentID, 1);
            Assume.That((child0.userID, child1.userID), Is.EqualTo((userID1, userID2)));
        }

        void Add2Brushes(in CompactHierarchy compactHierarchy, CompactNodeID parentID, out CompactNodeID brushID0, out CompactNodeID brushID1, int userID1 = 1, int userID2 = 2, int brushMeshID1 = 1, int brushMeshID2 = 2)
        {
            brushID0 = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID1, userID: userID1);
            brushID1 = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID2, userID: userID2);
            compactHierarchy.AttachToParent(parentID, brushID0);
            compactHierarchy.AttachToParent(parentID, brushID1);
            var siblingIndex0 = compactHierarchy.SiblingIndexOf(brushID0);
            var siblingIndex1 = compactHierarchy.SiblingIndexOf(brushID1);
            Assume.That((0, 1), Is.EqualTo((siblingIndex0, siblingIndex1)));
            Assume.That((compactHierarchy.ParentOf(brushID0), compactHierarchy.ParentOf(brushID1)), Is.EqualTo((parentID, parentID)));
            ref var child0 = ref compactHierarchy.GetChildRefAt(parentID, 0);
            ref var child1 = ref compactHierarchy.GetChildRefAt(parentID, 1);
            Assume.That((child0.userID, child1.userID), Is.EqualTo((userID1, userID2)));
        }

        void Add3Brushes(in CompactHierarchy compactHierarchy, out CompactNodeID brushID0, out CompactNodeID brushID1, out CompactNodeID brushID2, int userID1 = 1, int userID2 = 2, int userID3 = 3, int brushMeshID1 = 1, int brushMeshID2 = 2, int brushMeshID3 = 3)
        {
            Add3Brushes(in compactHierarchy, compactHierarchy.RootID, out brushID0, out brushID1, out brushID2, userID1, userID2, userID3, brushMeshID1, brushMeshID2, brushMeshID3);
        }

        void Add3Brushes(in CompactHierarchy compactHierarchy, CompactNodeID parentID, out CompactNodeID brushID0, out CompactNodeID brushID1, out CompactNodeID brushID2, int userID1 = 1, int userID2 = 2, int userID3 = 3, int brushMeshID1 = 1, int brushMeshID2 = 2, int brushMeshID3 = 3)
        {
            brushID0 = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID1, userID: userID1);
            brushID1 = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID2, userID: userID2);
            brushID2 = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID3, userID: userID3);
            compactHierarchy.AttachToParent(parentID, brushID0);
            compactHierarchy.AttachToParent(parentID, brushID1);
            compactHierarchy.AttachToParent(parentID, brushID2);
            var siblingIndex0 = compactHierarchy.SiblingIndexOf(brushID0);
            var siblingIndex1 = compactHierarchy.SiblingIndexOf(brushID1);
            var siblingIndex2 = compactHierarchy.SiblingIndexOf(brushID2);
            Assume.That((0, 1, 2), Is.EqualTo((siblingIndex0, siblingIndex1, siblingIndex2)));
            Assume.That(compactHierarchy.ChildCount(parentID), Is.EqualTo(3));
            Assume.That((compactHierarchy.ParentOf(brushID0), compactHierarchy.ParentOf(brushID1), compactHierarchy.ParentOf(brushID2)), Is.EqualTo((parentID, parentID, parentID)));
            ref var child0 = ref compactHierarchy.GetChildRefAt(parentID, 0);
            ref var child1 = ref compactHierarchy.GetChildRefAt(parentID, 1);
            ref var child2 = ref compactHierarchy.GetChildRefAt(parentID, 2);
            Assume.That((child0.userID, child1.userID, child2.userID), Is.EqualTo((userID1, userID2, userID3)));
        }

        [Test]
        public void RootWith3Children_GetParentsOfChildren_ParentsAreRoot()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                var parentRoot = compactHierarchy.ParentOf(compactHierarchy.RootID);
                var parent0 = compactHierarchy.ParentOf(brushID0);
                var parent1 = compactHierarchy.ParentOf(brushID1);
                var parent2 = compactHierarchy.ParentOf(brushID2);

                Assert.AreEqual((CompactNodeID.Invalid, compactHierarchy.RootID, compactHierarchy.RootID, compactHierarchy.RootID), 
                                (parentRoot, parent0, parent1, parent2));
            }
        }

        [Test]
        public void RootWith3Children_GetChildCount_ChildCountIs3()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                var childcount = compactHierarchy.ChildCount(compactHierarchy.RootID);

                Assert.AreEqual(3, childcount);
            }
        }

        [Test]
        public void RootWith3Children_GetSiblingIndices_HaveExpectedValues()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                var siblingIndexRoot = compactHierarchy.SiblingIndexOf(compactHierarchy.RootID);
                var siblingIndex0 = compactHierarchy.SiblingIndexOf(brushID0);
                var siblingIndex1 = compactHierarchy.SiblingIndexOf(brushID1);
                var siblingIndex2 = compactHierarchy.SiblingIndexOf(brushID2);

                Assert.AreEqual((-1, 0, 1, 2), (siblingIndexRoot, siblingIndex0, siblingIndex1, siblingIndex2));
            }
        }

        [Test]
        public void RootWith3Children_DetachAllChildrenInFrontAndGetSiblingIndices_SiblingIndicesAreInvalid()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                Assume.That(compactHierarchy.ParentOf(brushID0), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID1), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID2), Is.EqualTo(CompactNodeID.Invalid));
                
                Assert.AreEqual((-1, -1, -1), 
                                (compactHierarchy.SiblingIndexOf(brushID0),
                                 compactHierarchy.SiblingIndexOf(brushID1),
                                 compactHierarchy.SiblingIndexOf(brushID2)));
            }
        }

        [Test]
        public void RootWith3Children_DetachAllChildrenAtBackAndGetSiblingIndices_SiblingIndicesAreInvalid()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 2);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 1);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                Assume.That(compactHierarchy.ParentOf(brushID0), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID1), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID2), Is.EqualTo(CompactNodeID.Invalid));

                Assert.AreEqual((-1, -1, -1), 
                                (compactHierarchy.SiblingIndexOf(brushID0),
                                 compactHierarchy.SiblingIndexOf(brushID1),
                                 compactHierarchy.SiblingIndexOf(brushID2)));
            }
        }

        [Test]
        public void RootWith3Children_DetachAllChildrenInFrontAndGetChildCount_ChildCountIs0()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy, out var brushID0, out var brushID1, out var brushID2);

                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                Assume.That(compactHierarchy.ParentOf(brushID0), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID1), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID2), Is.EqualTo(CompactNodeID.Invalid));

                Assert.AreEqual(0, compactHierarchy.ChildCount(compactHierarchy.RootID));
            }
        }

        [Test]
        public void RootWith3Children_DetachAllChildrenAtBackAndGetChildCount_ChildCountIs0()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add3Brushes(in compactHierarchy,  out var brushID0, out var brushID1, out var brushID2);

                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 2);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 1);
                compactHierarchy.DetachChildFromParentAt(compactHierarchy.RootID, 0);
                Assume.That(compactHierarchy.ParentOf(brushID0), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID1), Is.EqualTo(CompactNodeID.Invalid));
                Assume.That(compactHierarchy.ParentOf(brushID2), Is.EqualTo(CompactNodeID.Invalid));

                Assert.AreEqual(0, compactHierarchy.ChildCount(compactHierarchy.RootID));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_InsertChildInBetweenChildren_SiblingIndicesAreInOrder()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID,        userID1: 1, userID2: 2);
                Add2Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush2_ID, userID1: 3, userID2: 4);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID, userID1: 5, userID2: 6);
                var branch0_brush1_ID = compactHierarchy.CreateBrush(NodeID.Invalid, 7, userID: 7);

                compactHierarchy.AttachToParentAt(branch0_ID, 1, branch0_brush1_ID);
                Assume.That(compactHierarchy.ParentOf(branch0_brush1_ID),         Is.EqualTo(branch0_ID));
                Assume.That(compactHierarchy.ChildCount(branch0_ID),              Is.EqualTo(3));
                Assume.That(compactHierarchy.ChildCount(branch1_ID),              Is.EqualTo(2));
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(2));
                
                Assert.AreEqual((0, 1, 2, 0, 1), 
                                (compactHierarchy.SiblingIndexOf(branch0_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch0_brush1_ID),
                                 compactHierarchy.SiblingIndexOf(branch0_brush2_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_InsertChildInBetweenChildren_ChildrenHaveShifted()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush2_ID, brushMeshID1: 1, brushMeshID2: 3);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID, brushMeshID1: 4, brushMeshID2: 5);
                var branch0_brush1_ID = compactHierarchy.CreateBrush(NodeID.Invalid, brushMeshID: 2);

                compactHierarchy.AttachToParentAt(branch0_ID, 1, branch0_brush1_ID);
                Assume.That(compactHierarchy.ParentOf(branch0_brush1_ID), Is.EqualTo(branch0_ID));
                Assume.That(compactHierarchy.ChildCount(branch0_ID), Is.EqualTo(3));
                
                Assert.AreEqual((1, 2, 3, 4, 5), 
                                (compactHierarchy.GetChild(branch0_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch0_brush1_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch0_brush2_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush1_ID).brushMeshID));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_InsertChildAtEnd_SiblingIndicesAreInOrder()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);
                var branch0_brush2_ID = compactHierarchy.CreateBrush(NodeID.Invalid, 0);

                compactHierarchy.AttachToParentAt(branch0_ID, 2, branch0_brush2_ID);
                Assume.That(compactHierarchy.ParentOf(branch0_brush1_ID), Is.EqualTo(branch0_ID));
                Assume.That(compactHierarchy.ChildCount(branch0_ID), Is.EqualTo(3));

                Assert.AreEqual((0, 1, 2, 0, 1), 
                                (compactHierarchy.SiblingIndexOf(branch0_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch0_brush1_ID),
                                 compactHierarchy.SiblingIndexOf(branch0_brush2_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DetachChildInBetweenChildren_ChildrenHaveShifted()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add3Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID, out var branch0_brush2_ID, brushMeshID1: 1, brushMeshID2: 2, brushMeshID3: 3);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID,                            brushMeshID1: 4, brushMeshID2: 5);

                compactHierarchy.Detach(branch0_brush1_ID);
                Assume.That(compactHierarchy.ParentOf(branch0_brush1_ID), Is.Not.EqualTo(branch0_ID));
                Assume.That(compactHierarchy.ChildCount(branch0_ID), Is.EqualTo(2));

                Assert.AreEqual((1, 3, 4, 5), 
                                (compactHierarchy.GetChild(branch0_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch0_brush2_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush1_ID).brushMeshID));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DetachChildAtEnd_RemainingChildrenHaveBeenUnchanged()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add3Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID, out var branch0_brush2_ID, brushMeshID1: 1, brushMeshID2: 2, brushMeshID3: 3);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID,                            brushMeshID1: 4, brushMeshID2: 5);

                compactHierarchy.DetachChildFromParentAt(branch0_ID, 2);
                Assume.That(compactHierarchy.ParentOf(branch0_brush2_ID), Is.Not.EqualTo(branch0_ID));
                Assume.That(compactHierarchy.ChildCount(branch0_ID), Is.EqualTo(2));

                Assert.AreEqual((1, 2, 4, 5), 
                                (compactHierarchy.GetChild(branch0_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch0_brush1_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush0_ID).brushMeshID,
                                 compactHierarchy.GetChild(branch1_brush1_ID).brushMeshID));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DetachChildInBetweenChildren_SiblingIndicesAreInOrder()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add3Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID, out var branch0_brush2_ID);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);

                compactHierarchy.DetachChildFromParentAt(branch0_ID, 1);

                Assert.AreEqual((0, 1, 0, 1), 
                                (compactHierarchy.SiblingIndexOf(branch0_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch0_brush2_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush0_ID),
                                 compactHierarchy.SiblingIndexOf(branch1_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DetachChild_ChildHasNoParent()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(2));

                compactHierarchy.Detach(branch0_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(1));

                Assert.AreEqual(CompactNodeID.Invalid, compactHierarchy.ParentOf(branch0_ID));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DetachChild_ChildOfChildIsUnchanged()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy, out var branch0_ID, out var branch1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);

                compactHierarchy.Detach(branch0_ID);

                // check if children are still attached
                Assert.AreEqual((branch0_ID, branch0_ID),
                                (compactHierarchy.ParentOf(branch0_brush0_ID), 
                                 compactHierarchy.ParentOf(branch0_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DeleteChild_ChildIDIsNotValid()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy,                       out var branch0_ID,        out var branch1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes (in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(2));

                compactHierarchy.Delete(branch0_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(1));

                Assert.AreEqual((false, true, true), 
                                (compactHierarchy.IsValidCompactNodeID(branch0_ID),
                                 compactHierarchy.IsValidCompactNodeID(branch0_brush0_ID),
                                 compactHierarchy.IsValidCompactNodeID(branch0_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DeleteChildRecursive_ChildIDIsNotValid()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy, out var branch0_ID, out var branch1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(2));

                compactHierarchy.DeleteRecursive(branch0_ID);
                Assume.That(compactHierarchy.ChildCount(compactHierarchy.RootID), Is.EqualTo(1));

                Assert.AreEqual((false, false, false),
                                (compactHierarchy.IsValidCompactNodeID(branch0_ID),
                                 compactHierarchy.IsValidCompactNodeID(branch0_brush0_ID),
                                 compactHierarchy.IsValidCompactNodeID(branch0_brush1_ID)));
            }
        }

        [Test]
        public void HierarchyWithNodesInBranches_DeleteChild_ChildOfChildHasNoParent()
        {
            using (var compactHierarchy = CompactHierarchy.CreateHierarchy(NodeID.Invalid, Allocator.Temp))
            {
                Add2Branches(in compactHierarchy, out var branch0_ID, out var branch1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch0_ID, out var branch0_brush0_ID, out var branch0_brush1_ID);
                Add2Brushes(in compactHierarchy, parentID: branch1_ID, out var branch1_brush0_ID, out var branch1_brush1_ID);

                compactHierarchy.Delete(branch0_ID);
                Assume.That((false, true, true), 
                            Is.EqualTo((compactHierarchy.IsValidCompactNodeID(branch0_ID), 
                                        compactHierarchy.IsValidCompactNodeID(branch0_brush0_ID), 
                                        compactHierarchy.IsValidCompactNodeID(branch0_brush1_ID))));

                // check if children are still attached
                Assert.AreEqual((CompactNodeID.Invalid, CompactNodeID.Invalid),
                                (compactHierarchy.ParentOf(branch0_brush0_ID), 
                                 compactHierarchy.ParentOf(branch0_brush1_ID)));
            }
        }
    }
}
