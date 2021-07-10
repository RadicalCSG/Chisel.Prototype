using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Collections;
using Chisel;
using Chisel.Core;

namespace FoundationTests
{
    // TODO: add tests to ensure robustness with invalid data
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
}
