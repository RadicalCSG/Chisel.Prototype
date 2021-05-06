using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    // TODO: make sure everything is covered in tests
    public struct SectionManager : IDisposable
    {
        // TODO: use uints instead?
        [StructLayout(LayoutKind.Sequential)]
        struct Section
        {
            public int  start;
            public int  length; // TODO: make this explicit as the difference between start and the next integer
            public int  end { get { return start + length - 1; } }
        }
        NativeList<Section> sections; // TODO: use UnsafeList instead, so we can more easily store them without getting 
                                      //       "lists in lists" problems

        // We merge all allocated and free sections, which means they alternate between being free and allocated
        // We then store if the first element is allocated or not, with that we can determine if an even or odd 
        // section index must also be allocated or free
        bool firstElementFree;
                
        public int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return sections.Length; } }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSectionFree(int sectionIndex) { return ((sectionIndex & 1) == 0) ? firstElementFree : !firstElementFree; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSectionStart(int sectionIndex) { return sections[sectionIndex].start; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSectionLength(int sectionIndex) { return sections[sectionIndex].length; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSectionEnd(int sectionIndex) { return sections[sectionIndex].end; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIndexFree(int index)
        {
            if (!FindSectionByOffset(index, out var sectionIndex))
                return true;
            return ((sectionIndex & 1) == 0) ? firstElementFree : !firstElementFree; 
        }

        public bool IsCreated { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return sections.IsCreated; } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SectionManager Create(Allocator allocator) { return new SectionManager { sections = new NativeList<Section>(allocator) }; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (sections.IsCreated) sections.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (sections.IsCreated) sections.Dispose(); sections = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int CompareSection(int sectionIndex, int compareOffset)
        {
            var section = sections[sectionIndex];
            if (compareOffset < section.start)
                return -1;
            if (compareOffset >= (section.start + section.length))
                return 1;
            return 0;
        }

        public bool IsAllocatedIndex(int index)
        {
            return FindSectionByOffset(index, out _);
        }

        struct SectionFindStack { public int first, last; }

        // Find section by offset using binary search
        unsafe bool FindSectionByOffset(int findOffset, out int foundSection)
        {
            foundSection = -1;
            var sectionsLength = sections.Length;
            if (sectionsLength == 0)
                return false;

            var sectionsPtr = (Section*)sections.GetUnsafePtr();
            if (findOffset < 0 || findOffset > sectionsPtr[sectionsLength - 1].end)
                return false;

            var searchStack = stackalloc SectionFindStack[8]; // should be be more than enough, we'd be running into integer size issues before then
            int searchLength;

            searchStack[0] = new SectionFindStack { first = 0, last = sectionsLength - 1 };
            searchLength = 1;

            while (searchLength > 0)
            {
                var sectionFirstIndex = searchStack[searchLength - 1].first;
                var sectionLastIndex  = searchStack[searchLength - 1].last;
                searchStack[searchLength - 1] = default;
                searchLength--;

                var centerSectionIndex  = sectionFirstIndex + ((sectionLastIndex - sectionFirstIndex) / 2);
                var section             = sectionsPtr[centerSectionIndex];
                var difference          = (findOffset < section.start) ? -1 : 
                                          (findOffset > section.end  ) ?  1 : 
                                          0;

                // Check if this is the section we need 
                if (difference == 0)
                {
                    foundSection = centerSectionIndex;
                    return true;
                }

                // If we still have ranges to check on the left or right, add them to the stack
                if (difference < 0)
                {
                    if (centerSectionIndex == sectionFirstIndex)
                        break;

                    var firstNode = sectionFirstIndex;
                    var lastNode  = centerSectionIndex - 1;

                    searchStack[searchLength] = new SectionFindStack { first = firstNode, last = lastNode };
                    searchLength++;
                } else
                {
                    if (centerSectionIndex == sectionLastIndex)
                        break;

                    var firstNode = centerSectionIndex + 1;
                    var lastNode  = sectionLastIndex;

                    searchStack[searchLength] = new SectionFindStack { first = firstNode, last = lastNode };
                    searchLength++;
                }
            } 
            return false;
        }

        public void FreeRange(int offset, int length)
        {
            if (offset < 0) throw new ArgumentException($"{nameof(offset)} cannot be negative", nameof(offset));
            if (length < 0) throw new ArgumentException($"{nameof(length)} cannot be negative", nameof(length));
            if (length == 0)
                return;

            var found = FindSectionByOffset(offset, out var sectionIndex);
            if (!found)
                throw new IndexOutOfRangeException("Could not find section for given offset");

            if (IsSectionFree(sectionIndex))
                throw new IndexOutOfRangeException("Cannot free section that's already free");

            if (GetSectionLength(sectionIndex) < length)
                throw new IndexOutOfRangeException("Free section at offset is not large enough");

            SetRange(sectionIndex, offset, length, desiredFree: true);
            if (sections.Length > 0 && 
                IsSectionFree(sections.Length - 1))
                sections.RemoveAt(sections.Length - 1);
        }

        public int ReallocateRange(int offset, int originalLength, int newLength)
        {
            if (originalLength == 0)
                return AllocateRange(newLength);

            var found = FindSectionByOffset(offset, out var sectionIndex);
            if (!found)
                throw new IndexOutOfRangeException("Could not find section for given offset");

            if (IsSectionFree(sectionIndex))
                throw new IndexOutOfRangeException("Cannot reallocate section because it's not allocated");

            var sectionLength = GetSectionLength(sectionIndex);
            if (sectionLength < originalLength)
                throw new IndexOutOfRangeException("Cannot reallocate section because it's not completely allocated");

            var requiredExtraLength = newLength - originalLength;
            if (requiredExtraLength == 0)
                return offset; // nothing to do

            // If we decrease in size, that's fine we just free the range at the end
            if (requiredExtraLength < 0)
            {
                FreeRange(offset + newLength, -requiredExtraLength);
                return offset;
            }

            // Check if our section is followed by other data, In which case we need to find another place to put our data
            if ((sectionLength > originalLength) ||
                // If the next block doesn't have the required space, we cannot use it and we need to move our data as well
                (sectionIndex + 1 < sections.Length && GetSectionLength(sectionIndex + 1) < requiredExtraLength))
            {
                FreeRange(offset, originalLength);
                return AllocateRange(newLength);
            }

            // Allocate required range behind our section (will be merged with previous section)
            SetRange(sectionIndex + 1, offset + originalLength, requiredExtraLength, false);
            return offset;
        }

        public int AllocateRange(int length)
        {
            if (length < 0)
                throw new ArgumentException("Length cannot be negative");
            if (length == 0)
                return -1;

            // TODO: have some way to quickly find a free section with a given size without going through ALL free sections

            // Since we alternate between free and allocated sections, we can quickly find all free sections by skipping the allocated sections
            for (int sectionIndex = firstElementFree ? 0 : 1; sectionIndex < sections.Length; sectionIndex += 2)
            {
                Debug.Assert(IsSectionFree(sectionIndex));

                var section = sections[sectionIndex];
                if (length > section.length)
                    continue;

                SetRange(sectionIndex, section.start, length, false);
                return section.start;
            }

            // Create a new range at the end
            var lastSection = (sections.Length == 0) ? new Section { } : sections[sections.Length - 1];
            var offset      = lastSection.start + lastSection.length;
            SetRange(sections.Length, offset, length, false);
            return offset;
        }

        void SetRange(int sectionIndex, int offset, int length, bool desiredFree)
        {
            if (sectionIndex < 0) throw new ArgumentException($"{nameof(sectionIndex)} cannot be negative", nameof(sectionIndex));
            if (offset < 0) throw new ArgumentException($"{nameof(offset)} cannot be negative", nameof(offset));
            if (length < 0) throw new ArgumentException($"{nameof(length)} cannot be negative", nameof(length));
            if (length == 0) return;
            if (sectionIndex > sections.Length) throw new ArgumentException($"{nameof(sectionIndex)} cannot be higher than number of sections", nameof(sectionIndex));
            
            // Check if we need to add a new one instead
            if (sectionIndex == sections.Length)
            {   
                if (sectionIndex == 0                                   // If sections is empty, just add a new section
                    || IsSectionFree(sectionIndex - 1) != desiredFree   // If the previous section is different than desiredFree, we need to add a new section too
                    )
                {
                    Debug.Assert(// either this is the first section and offset needs to be 0 
                                 (sectionIndex == 0 && offset == 0) || 
                                 // or it connects to the previous section
                                 (sectionIndex > 0 && offset == sections[sectionIndex - 1].start + sections[sectionIndex - 1].length));
                    sections.Add(new Section
                    {
                        start  = offset,
                        length = length
                    });
                    if (sectionIndex == 0)
                        firstElementFree = desiredFree;
                    Debug.Assert(IsSectionFree(sectionIndex) == desiredFree);
                } else
                {
                    // Otherwise, we can merge our allocation with the previous allocated section ...
                    Debug.Assert(IsSectionFree(sectionIndex - 1) == desiredFree);
                    var previousSection = sections[sectionIndex - 1];
                    Debug.Assert(previousSection.start + previousSection.length == offset);
                    previousSection.length += length;
                    sections[sectionIndex - 1] = previousSection;
                }
                return;
            }

            if (IsSectionFree(sectionIndex) == desiredFree)
            {
                if (desiredFree)
                    throw new ArgumentException("Cannot free section because it's already free");
                else
                    throw new ArgumentException("Cannot allocate section because it's already allocated");
            }

            var section = sections[sectionIndex];
            if (offset + length > section.start + section.length)
                throw new IndexOutOfRangeException("Length of requested section to free is larger than found section");

            // Note: Free and allocated sections follow each other since they always get merged 
            // with allocated or free sections next to them

            // Check if our section is exactly the free range we need
            if (section.start == offset && section.length == length)
            {
                // Check if our section is the last section in the list, which means there's no next section
                if (sectionIndex + 1 == sections.Length)
                {
                    // If we're the first section, then there's no previous section
                    if (sectionIndex == 0)
                    {
                        // This means this is the only section in the list and we can just modify it
                        sections[sectionIndex] = section;
                        if (sectionIndex == 0)
                            firstElementFree = desiredFree;
                    } else
                    {
                        // Otherwise, we can merge our allocation with the previous allocated section ...
                        Debug.Assert(IsSectionFree(sectionIndex - 1) == desiredFree);
                        var previousSection = sections[sectionIndex - 1];
                        Debug.Assert(previousSection.start + previousSection.length == section.start);
                        previousSection.length += length;
                        sections[sectionIndex - 1] = previousSection;

                        // ... and remove the last item in the list (the found section)
                        sections.RemoveAt(sectionIndex);
                    }
                } else
                // We know that there's a next section. 
                {
                    // If we're the first section, then there's no previous section
                    if (sectionIndex == 0)
                    {
                        // Merge our allocation with the next section ...
                        Debug.Assert(IsSectionFree(sectionIndex + 1) == desiredFree);
                        var nextSection = sections[sectionIndex + 1];
                        Debug.Assert(nextSection.start == section.start + section.length);
                        nextSection.start -= length;
                        nextSection.length += length;
                        sections[sectionIndex + 1] = nextSection;

                        // ... and remove the first item in the list (the found section)
                        sections.RemoveAt(sectionIndex);
                        
                        firstElementFree = desiredFree;
                    } else
                    {
                        // We have both a previous and a next section, and we can merge all 
                        // three sections together into one section
                        Debug.Assert(IsSectionFree(sectionIndex - 1) == desiredFree);
                        var previousSection = sections[sectionIndex - 1];
                        Debug.Assert(previousSection.start + previousSection.length == section.start);
                        Debug.Assert(IsSectionFree(sectionIndex + 1) == desiredFree);
                        var nextSection = sections[sectionIndex + 1];
                        Debug.Assert(nextSection.start == section.start + section.length);
                        previousSection.length = previousSection.length + length + nextSection.length;
                        sections[sectionIndex - 1] = previousSection;

                        // ... and we remove the two entries we don't need anymore
                        sections.RemoveRangeWithBeginEnd(sectionIndex, sectionIndex + 2);
                    }
                }
            } else
            // If our allocation doesn't match the section exactly, we need to keep a leftover
            {
                var firstSectionLength  = offset - section.start;
                Debug.Assert(firstSectionLength >= 0);
                var middleSectionLength = length;
                var lastSectionLength   = (section.start + section.length) - (offset + length);
                Debug.Assert(lastSectionLength >= 0);

                if (firstSectionLength == 0)
                {
                    Debug.Assert(lastSectionLength > 0);

                    if (sectionIndex == 0)
                    {
                        // Modify the existing section to hold the middle section
                        sections[sectionIndex] = new Section
                        {
                            start   = offset,
                            length  = middleSectionLength
                        };

                        // Insert a new section behind it to hold the leftover
                        sections.InsertAt(sectionIndex + 1, new Section
                        {
                            start   = offset + middleSectionLength,
                            length  = lastSectionLength
                        });

                        firstElementFree = desiredFree;
                    } else
                    {
                        // Modify the existing section to hold the left-over
                        sections[sectionIndex] = new Section
                        {
                            start   = offset + middleSectionLength,
                            length  = lastSectionLength
                        };

                        // Merge middle section with the previous section
                        Debug.Assert(IsSectionFree(sectionIndex - 1) == desiredFree);
                        var previousSection = sections[sectionIndex - 1];
                        Debug.Assert(previousSection.start + previousSection.length == section.start);
                        previousSection.length += middleSectionLength;
                        sections[sectionIndex - 1] = previousSection;
                    }
                } else
                if (lastSectionLength == 0)
                {
                    Debug.Assert(firstSectionLength > 0);
                    // Modify the existing section to hold the left-over
                    sections[sectionIndex] = new Section
                    {
                        start   = section.start,
                        length  = firstSectionLength
                    };
                    if (sectionIndex == 0)
                        firstElementFree = !desiredFree;

                    if (sectionIndex + 1 == sections.Length)
                    {
                        // Insert a new section to hold the middle section
                        sections.InsertAt(sectionIndex + 1, new Section
                        {
                            start   = offset,
                            length  = middleSectionLength
                        });
                    } else
                    {
                        // Merge middle section with the next section
                        Debug.Assert(IsSectionFree(sectionIndex + 1) == desiredFree);
                        var nextSection = sections[sectionIndex + 1];
                        Debug.Assert(nextSection.start == section.start + section.length);
                        nextSection.start -= middleSectionLength;
                        nextSection.length += middleSectionLength;
                        sections[sectionIndex + 1] = nextSection;
                    }
                } else
                {
                    // Modify the existing section to hold the first left-over
                    sections[sectionIndex] = new Section
                    {
                        start   = section.start,
                        length  = firstSectionLength
                    };
                    if (sectionIndex == 0)
                        firstElementFree = !desiredFree;

                    // Add the middle section
                    sections.InsertAt(sectionIndex + 1, new Section
                    {
                        start   = offset,
                        length  = middleSectionLength
                    });

                    var lastSection = new Section
                    {
                        start   = offset + middleSectionLength,
                        length  = lastSectionLength
                    };

                    // Add the last left-over
                    sections.InsertAt(sectionIndex + 2, lastSection);
                }
            }
        }
    }
}