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
    class SectionManagerTests
    {
        // Note: tests have been written to touch every branch in the code of SectionManager

        [Test]
        public void SectionMananagerWithAllocatedRange_FreeSameRange_NoSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 10;
                var offset = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                // x = free, o = allocated
                // ooooooo

                sectionManager.FreeRange(offset, length);
                
                // xxxxxxx
                Assert.AreEqual(0, sectionManager.Count);
            }
        }

        [Test]
        public void SectionMananagerWithAllocatedRange_FreeRangeAtEnd_OneSmallAllocatedSection()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 10;
                var offset = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                // x = free, o = allocated
                // ooooooo

                sectionManager.FreeRange(offset + 1, length - 1);

                // o xxxxxx
                // o <- last empty section is always removed
                Assert.AreEqual(1, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), 1);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithAllocatedRange_FreeRangeAtBeginning_FreeSectionFollowedByAllocatedSection()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 10;
                var offset = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                // x = free, o = allocated
                // ooooooo

                sectionManager.FreeRange(offset, length - 1);

                // xxxxxx o
                Assert.AreEqual(2, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length - 1);
                Assert.AreEqual(sectionManager.GetSectionStart(1), length - 1);
                Assert.AreEqual(sectionManager.GetSectionLength(1), 1);
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithFreeRangeFollowedByAllocatedRange_FreeRangeAtBeginning_AllocatedSectionFollowedByFreeSectionFollowedByAllocatedSection()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 10;
                var offset1 = sectionManager.AllocateRange(length);
                var offset2 = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                sectionManager.FreeRange(offset1, length);
                Assume.That(sectionManager.Count, Is.EqualTo(2));
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
                // x = free, o = allocated
                // xxxxxxx ooooooo

                // Allocate a section that fits in our empty section
                var offset3 = sectionManager.AllocateRange(length - 1);
                Assume.That(offset3, Is.EqualTo(offset1));

                // oooooo x ooooooo
                Assert.AreEqual(3, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length - 1);
                Assert.AreEqual(sectionManager.GetSectionStart(1), length - 1);
                Assert.AreEqual(sectionManager.GetSectionLength(1), 1);
                Assert.AreEqual(sectionManager.GetSectionStart(2), length);
                Assert.AreEqual(sectionManager.GetSectionLength(2), length);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithFreeRangeFollowedByAllocatedRange_FreeLastAllocatedRange_NoSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 11;
                const int length2 = 12;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                sectionManager.FreeRange(offset1, length1);
                Assume.That(sectionManager.Count, Is.EqualTo(2));
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
                // x = free, o = allocated
                // xxxxxxx ooooooo

                // Allocate a section that fits exactly in our empty section
                sectionManager.FreeRange(offset2, length2);

                // xxxxxxx xxxxxxx
                // xxxxxxxxxxxxxx <- merged
                // <- last section is always removed
                Assert.AreEqual(0, sectionManager.Count);
            }
        }

        [Test]
        public void SectionMananagerWithAllocatedRangeFollowedByFreeRangeFollowedByAllocatedRange_FreeFirstAllocatedRange_TwoSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 11;
                const int length2 = 12;
                const int length3 = 13;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                var originalEnd = sectionManager.GetSectionEnd(0);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                sectionManager.FreeRange(offset2, length2);
                Assume.That(sectionManager.Count, Is.EqualTo(3));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
                // x = free, o = allocated
                // ooooooo xxxxxxx ooooooo

                sectionManager.FreeRange(offset1, length1);

                //  ____ freed
                // |
                // v
                // xxxxxxx xxxxxxx ooooooo
                // xxxxxxxxxxxxxx ooooooo <- free sections merged
                Assert.AreEqual(2, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length1 + length2);
                Assert.AreEqual(sectionManager.GetSectionStart(1), length1 + length2);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length3);
                Assert.AreEqual(sectionManager.GetSectionEnd(1), originalEnd);
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithFreeRangeFollowedByAllocatedRangeFollowedByFreeRangeFollowedByAllocatedRange_FreeFirstAllocatedRange_TwoSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 11;
                const int length2 = 12;
                const int length3 = 13;
                const int length4 = 14;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                var offset4 = sectionManager.AllocateRange(length4);
                var originalEnd = sectionManager.GetSectionEnd(0);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                sectionManager.FreeRange(offset3, length3);
                sectionManager.FreeRange(offset1, length1);
                Assume.That(sectionManager.Count, Is.EqualTo(4));
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
                Assume.That(sectionManager.IsSectionFree(2), Is.True);
                Assume.That(sectionManager.IsSectionFree(3), Is.False);
                // x = free, o = allocated
                // xxxxxxx ooooooo xxxxxxx ooooooo 

                sectionManager.FreeRange(offset2, length2);

                //          ____ freed
                //         |
                //         v
                // xxxxxxx xxxxxxx xxxxxxx ooooooo 
                // xxxxxxxxxxxxxxxxxxxxx ooooooo <- free sections merged
                Assert.AreEqual(2, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length1 + length2 + length3);
                Assert.AreEqual(sectionManager.GetSectionStart(1), length1 + length2 + length3);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length4);
                Assert.AreEqual(sectionManager.GetSectionEnd(1), originalEnd);
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithAllocatedRange_FreeRangeInCenter_ThreeSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 12;
                const int length2 = 11;
                const int length3 = 10;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length1 + length2 + length3));
                var originalEnd = sectionManager.GetSectionEnd(0);
                // x = free, o = allocated
                // ooooooo ooooooo ooooooo 
                // ooooooooooooooooooooo <- allocated sections merged

                sectionManager.FreeRange(offset2 + 1, length2 - 2);
                Assume.That(sectionManager.Count, Is.EqualTo(3));

                //          ____ freed
                //         |
                //         v
                // ooooooo xxxxxxx ooooooo
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionStart(1), offset2 + 1);
                Assert.AreEqual(sectionManager.GetSectionStart(2), offset2 + 1 + length2 - 2);
                Assert.AreEqual(sectionManager.GetSectionEnd(0), sectionManager.GetSectionStart(1) - 1);
                Assert.AreEqual(sectionManager.GetSectionEnd(1), sectionManager.GetSectionStart(2) - 1);
                Assert.AreEqual(sectionManager.GetSectionEnd(2), originalEnd);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length1 + 1);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length2 - 2);
                Assert.AreEqual(sectionManager.GetSectionLength(2), 1 + length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
            }
        }

        [Test]
        public void SectionMananager_ReallocateRangeWithLargerBlockAtStartWithEnoughSpaceInFreeSection_ReturnsSameOffset()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 12;
                const int length2 = 11;
                const int length3 = 10;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length1 + length2 + length3));
                var originalEnd = sectionManager.GetSectionEnd(0);
                sectionManager.FreeRange(offset2, length2);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(3));
                // x = free, o = allocated
                // ooooooo xxxxxxx ooooooo 

                var offset4 = sectionManager.ReallocateRange(offset1, length1, length1 + 3);
                Assume.That(sectionManager.Count, Is.EqualTo(3));

                //          ____ allocated
                //         |
                //         v
                // ooooooo ooo xxxx ooooooo
                // oooooooooo xxxx ooooooo <- allocated sections merged
                Assert.AreEqual(offset4, offset1);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionStart(1), offset2 + 3);
                Assert.AreEqual(sectionManager.GetSectionStart(2), offset2 + 3 + length2 - 3);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length1 + 3);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length2 - 3);
                Assert.AreEqual(sectionManager.GetSectionLength(2), length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
            }
        }

        [Test]
        public void SectionMananager_ReallocateRangeWithSmallerBlockAtStartWithSpaceBehindIt_ReturnsSameOffset()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 12;
                const int length2 = 11;
                const int length3 = 10;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length1 + length2 + length3));
                var originalEnd = sectionManager.GetSectionEnd(0);
                sectionManager.FreeRange(offset2, length2);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(3));
                // x = free, o = allocated
                // ooooooo xxxxxxx ooooooo 

                var lengthDecrease = 3;
                var newLength1 = length1 - lengthDecrease;
                var offset4 = sectionManager.ReallocateRange(offset1, length1, newLength1);
                Assume.That(sectionManager.Count, Is.EqualTo(3));

                //        ____ free
                //       |
                //       v
                // ooooo xxx xxxx ooooooo
                // ooooo xxxxxxx ooooooo <- freed sections merged
                Assert.AreEqual(offset4, offset1);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionStart(1), newLength1);
                Assert.AreEqual(sectionManager.GetSectionStart(2), offset3);
                Assert.AreEqual(sectionManager.GetSectionLength(0), newLength1);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length2 + lengthDecrease);
                Assert.AreEqual(sectionManager.GetSectionLength(2), length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
            }
        }

        [Test]
        public void SectionMananager_ReallocateRangeWithLargerBlockWithNoSpace_ReturnsDifferentOffset()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length1 = 12;
                const int length2 = 3;
                const int length3 = 10;
                var offset1 = sectionManager.AllocateRange(length1);
                var offset2 = sectionManager.AllocateRange(length2);
                var offset3 = sectionManager.AllocateRange(length3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length1 + length2 + length3));
                sectionManager.FreeRange(offset2, length2);
                var originalEnd = sectionManager.GetSectionEnd(2);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(3));
                // x = free, o = allocated
                // ooooooo xxx ooooooo 

                // reallocate to block that is larger than is available
                var newLength = length1 + length2 + 1;
                var offset4 = sectionManager.ReallocateRange(offset1, length1, newLength);
                Assume.That(sectionManager.Count, Is.EqualTo(2));

                // ____ free           ____ allocated
                // |                   |
                // v                   v
                // xxxxxxx xxx ooooooo ooooooooooo
                // xxxxxxxxxx oooooooooooooooooo <- sections merged
                Assert.AreEqual(offset4, originalEnd + 1);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionStart(1), length1 + length2);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length1 + length2);
                Assert.AreEqual(sectionManager.GetSectionLength(1), length3 + newLength);
                Assume.That(sectionManager.IsSectionFree(0), Is.True);
                Assume.That(sectionManager.IsSectionFree(1), Is.False);
            }
        }        

        [Test]
        public void SectionMananager_ReallocateRangeWithLargerBlockAtEnd_ReturnsSameOffset()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 12;
                var offset1 = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                // x = free, o = allocated
                // ooooooo

                var offset4 = sectionManager.ReallocateRange(offset1, length, length + 3);
                Assume.That(sectionManager.Count, Is.EqualTo(1));

                //          ____ allocated
                //         |
                //         v
                // ooooooo ooo 
                // oooooooooo <- allocated sections merged
                Assert.AreEqual(offset4, offset1);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length + 3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
            }
        }

        [Test]
        public void SectionMananager_ReallocateRangeWithSmallerBlock_ReturnsSameOffset()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 12;
                var offset1 = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.GetSectionStart(0), Is.EqualTo(0));
                Assume.That(sectionManager.GetSectionLength(0), Is.EqualTo(length));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                // x = free, o = allocated
                // oooooooo

                var offset4 = sectionManager.ReallocateRange(offset1, length, length - 3);
                Assume.That(sectionManager.Count, Is.EqualTo(1));

                //       ____ free  
                //       |          
                //       v          
                // oooo xx          
                // oooo <- empty section at end is removed
                Assert.AreEqual(offset4, offset1);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length - 3);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
            }
        }

        [Test]
        public void SectionMananagerWithAllocatedRangeFollowedByFreeRangeFollowedByAllocatedRange_AllocateRangeSmallerThanFreeBlock_TwoSections()
        {
            using (var sectionManager = SectionManager.Create(Allocator.Temp))
            {
                const int length = 10;
                var offset1 = sectionManager.AllocateRange(length);
                var offset2 = sectionManager.AllocateRange(length);
                var offset3 = sectionManager.AllocateRange(length);
                Assume.That(sectionManager.Count, Is.EqualTo(1));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                sectionManager.FreeRange(offset2, length);
                Assume.That(sectionManager.Count, Is.EqualTo(3));
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
                // x = free, o = allocated
                // ooooooo xxxxxxx ooooooo 

                var offset4 = sectionManager.AllocateRange(length - 2);
                Assume.That(offset4, Is.EqualTo(offset2));

                //          ____ allocated
                //         |
                //         v
                // ooooooo ooooo xx ooooooo 
                // ooooooooooooo xx ooooooo <- allocated sections merged
                Assert.AreEqual(3, sectionManager.Count);
                Assert.AreEqual(sectionManager.GetSectionStart(0), 0);
                Assert.AreEqual(sectionManager.GetSectionStart(1), offset4 + length - 2);
                Assert.AreEqual(sectionManager.GetSectionStart(2), offset3);
                Assert.AreEqual(sectionManager.GetSectionLength(0), length + length - 2);
                Assert.AreEqual(sectionManager.GetSectionLength(1), 2);
                Assert.AreEqual(sectionManager.GetSectionLength(2), length);
                Assume.That(sectionManager.IsSectionFree(0), Is.False);
                Assume.That(sectionManager.IsSectionFree(1), Is.True);
                Assume.That(sectionManager.IsSectionFree(2), Is.False);
            }
        }

    }

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
            Assume.That(CompactHierarchyManager.GenerateBranch(0, CSGOperationType.Additive, out var oldBranchNodeID), Is.True);
            Assume.That(CompactHierarchyManager.GenerateTree(0, out var treeNodeID), Is.True);
             
            Assume.That(CompactHierarchyManager.IsValidNodeID(oldBranchNodeID), Is.True);
            Assume.That(CompactHierarchyManager.IsValidNodeID(treeNodeID), Is.True);
            var oldBranceCompactNodeID = CompactHierarchyManager.GetCompactNodeID(oldBranchNodeID);

            var newBranchCompactNodeID = CompactHierarchyManager.MoveChildNode(oldBranchNodeID, CompactHierarchyManager.GetHierarchyID(treeNodeID));
            Assert.AreEqual((true, false, newBranchCompactNodeID), 
                            (CompactHierarchyManager.IsValidCompactNodeID(newBranchCompactNodeID), 
                             CompactHierarchyManager.IsValidCompactNodeID(oldBranceCompactNodeID),
                             CompactHierarchyManager.GetCompactNodeID(oldBranchNodeID)));
        }


        [Test]
        public void CompactHierarchyManager_RecycleID_EverythingWorksFine()
        {
            Assume.That(CompactHierarchyManager.GenerateBranch(0, CSGOperationType.Additive, out var oldNodeID), Is.True);
            var oldCompactNodeID = CompactHierarchyManager.GetCompactNodeID(oldNodeID);

            CompactHierarchyManager.DestroyNode(oldNodeID);
            Assume.That(CompactHierarchyManager.GenerateBranch(0, CSGOperationType.Additive, out var newNodeID), Is.True);
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
        }

        const int userID1 = 1;
        const int userID2 = 2;
        const int userID3 = 3;
        const int userID4 = 4;
        void Add3Branches(out NodeID tree, out NodeID child1, out NodeID child2, out NodeID child3)
        { 
            Assume.That(CompactHierarchyManager.GenerateTree(userID1, out tree), Is.True);
            Assume.That(CompactHierarchyManager.GenerateBranch(userID2, CSGOperationType.Additive, out child1), Is.True);
            Assume.That(CompactHierarchyManager.GenerateBranch(userID3, CSGOperationType.Additive, out child2), Is.True);
            Assume.That(CompactHierarchyManager.GenerateBranch(userID4, CSGOperationType.Additive, out child3), Is.True);
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
                        Is.EqualTo((userID1, userID2, userID3, userID4)));
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
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((userID1, userID2, userID3, 0)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, true, false), validNodes);
        }

        [Test]
        public void RootWith3Children_DeleteMiddleChild_FirstChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.DestroyNode(child2);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((userID1, userID2, 0, userID4)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, true, false, true), validNodes);
        }

        [Test]
        public void RootWith3Children_DeleteFirstChild_FirstChildIsNotPartOfParent()
        {
            Add3Branches(out var tree, out var child1, out var child2, out var child3);

            CompactHierarchyManager.DestroyNode(child1);
            Assume.That(2, Is.EqualTo(CompactHierarchyManager.GetChildNodeCount(tree)));
            var userIDs = (CompactHierarchyManager.GetUserIDOfNode(tree),
                           CompactHierarchyManager.GetUserIDOfNode(child1),
                           CompactHierarchyManager.GetUserIDOfNode(child2),
                           CompactHierarchyManager.GetUserIDOfNode(child3));
            Assume.That(userIDs, Is.EqualTo((userID1, 0, userID3, userID4)));
            var validNodes = (CompactHierarchyManager.IsValidNodeID(tree),
                              CompactHierarchyManager.IsValidNodeID(child1),
                              CompactHierarchyManager.IsValidNodeID(child2),
                              CompactHierarchyManager.IsValidNodeID(child3));

            Assert.AreEqual((true, false, true, true), validNodes);
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
            Assume.That(userIDs, Is.EqualTo((userID1, userID2, userID3, userID4)));
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
            Assume.That(userIDs, Is.EqualTo((userID1, userID2, userID3, userID4)));
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
        }

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

        // (dangling) node count / unused nodes



        // ChiselGeneratedComponentManager:214 why is this necessary??
        // tests for all methods dealing with children of node
        // enable/disable node => still visible until something else is modified (no dirty?)
    }
}
