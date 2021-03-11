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
    // TODO: have some way to test consistency of the data, use that in tests
    // TODO: make sure everything is covered in tests
    // TODO: use native containers, make hierarchy use this as well
    internal struct IDManager : IDisposable
    {
        [DebuggerDisplay("Index = {index}, Generation = {generation}")]
        struct IndexLookup
        {
            public Int32 index;
            public Int32 generation;
        }

        NativeList<IndexLookup> idToIndex;
        NativeList<int>         indexToID;
        SectionManager          sectionManager;
        NativeList<int>         freeIDs; // TODO: use SectionManager, or something like that, so we can easily allocate ids/id ranges in order
        
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return idToIndex.IsCreated &&
                       indexToID.IsCreated &&
                       sectionManager.IsCreated &&
                       freeIDs.IsCreated;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDManager Create(Allocator allocator)
        {
            return new IDManager
            {
                idToIndex       = new NativeList<IndexLookup>(allocator),
                indexToID       = new NativeList<int>(allocator),
                sectionManager  = SectionManager.Create(allocator),
                freeIDs         = new NativeList<int>(allocator)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (idToIndex.IsCreated) idToIndex.Clear();
            if (indexToID.IsCreated) indexToID.Clear();
            if (sectionManager.IsCreated) sectionManager.Clear();
            if (freeIDs.IsCreated) freeIDs.Clear();
        }

        public void Dispose()
        {
            if (idToIndex.IsCreated) idToIndex.Dispose(); idToIndex = default;
            if (indexToID.IsCreated) indexToID.Dispose(); indexToID = default;
            if (sectionManager.IsCreated) sectionManager.Dispose(); sectionManager = default;
            if (freeIDs.IsCreated) freeIDs.Dispose(); freeIDs = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetID(int index, out int id, out int generation)
        {
            id = default; //out
            generation = default;//out

            if (!sectionManager.IsAllocatedIndex(index))
                throw new ArgumentOutOfRangeException($"{nameof(index)} ({index}) must be allocated and lie between 0 ... {indexToID.Length}");

            var idInternal = indexToID[index] - 1;
            if (idInternal < 0 || idInternal >= idToIndex.Length)
                throw new IndexOutOfRangeException($"{nameof(id)} ({id}) must be between 1 ... {1 + idToIndex.Length}");

            generation = idToIndex[idInternal].generation;
            if (idToIndex[idInternal].index != index)
                throw new FieldAccessException($"Internal mismatch of ids and indices");

            id = idInternal + 1;//out
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid(int id, int generation, out int index)
        {
            var idInternal = id - 1; // We don't want 0 to be a valid id

            index = -1;
            if (idInternal < 0 || idInternal >= idToIndex.Length)
                return false;

            var idLookup = idToIndex[idInternal];
            if (idLookup.generation != generation)
                return false;

            index = idLookup.index;
            return sectionManager.IsAllocatedIndex(idLookup.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int id, int generation)
        {
            var idInternal = id - 1; // We don't want 0 to be a valid id

            if (idInternal < 0 || idInternal >= idToIndex.Length)
                throw new ArgumentOutOfRangeException($"{nameof(id)} ({id}) must be between 1 and {1 + idToIndex.Length}");

            var idLookup = idToIndex[idInternal];
            if (idLookup.generation != generation)
                throw new ArgumentException($"The given generation ({generation}) was not identical to the expected generation ({idLookup.generation}), are you using an old reference?");

            var index = idLookup.index;
            if (index < 0 || index >= indexToID.Length)
            {
                if (indexToID.Length == 0)
                    throw new ArgumentException($"{nameof(id)} ({id}) does not point to an valid index. This lookup table does not contain any valid indices at the moment.");
                throw new ArgumentException($"{nameof(id)} ({id}) does not point to an valid index. It must be above 0 and below {indexToID.Length + 1}.");
            }

            return idLookup.index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CreateID(out int id, out int generation)
        {
            var index = sectionManager.AllocateRange(1);
            AllocateIndexRange(index, 1);
            GetID(index, out id, out generation);
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateID(int index, out int id, out int generation)
        {
            AllocateIndexRange(index, 1);
            GetID(index, out id, out generation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AllocateIndexRange(int range)
        {
            if (range == 0)
                return -1;

            var index = sectionManager.AllocateRange((int)range);
            AllocateIndexRange(index, range);
            return index;
        }

        internal void AllocateIndexRange(int index, int range)
        {
            if ((int)(index + range) > indexToID.Length)
                indexToID.Resize((int)(index + range), NativeArrayOptions.ClearMemory);

            int idInternal, generation;
            // TODO: should make it possible to allocate ids in a range as well, for cache locality
            if (freeIDs.Length > 0)
            {
                while (range > 0)
                {
                    var freeID = freeIDs.Length - 1;
                    idInternal = freeIDs[freeID];
                    freeIDs.RemoveAt(freeID);
                    
                    generation = idToIndex[idInternal].generation + 1;
                    idToIndex[idInternal] = new IndexLookup { index = index, generation = generation };

                    indexToID[index] = idInternal + 1;

                    range--;
                    index++;
                }
            }

            if (range <= 0)
                return;
            
            generation = 1;
            idInternal = idToIndex.Length;
            idToIndex.Resize((int)(idInternal + range), NativeArrayOptions.ClearMemory);

            for (var lastID = (int)(idInternal + range); idInternal < lastID; idInternal++)
            {
                indexToID[index] = idInternal + 1;
                idToIndex[idInternal] = new IndexLookup { index = index, generation = generation };
            }
        }

        public unsafe void SwapIndexRangeToBack(int sectionIndex, int sectionLength, int swapIndex, int swapRange)
        {
            if (sectionIndex < 0)
                throw new ArgumentException($"{nameof(sectionIndex)} must be 0 or higher.");

            if (sectionLength < 0)
                throw new ArgumentException($"{nameof(sectionLength)} must be 0 or higher.");

            if (swapIndex < 0)
                throw new ArgumentException($"{nameof(swapIndex)} must be positive.");

            if (swapIndex + swapRange > sectionLength)
                throw new ArgumentException($"{nameof(swapIndex)} ({swapIndex}) + {nameof(swapRange)} ({swapRange}) must be smaller than {nameof(sectionIndex)} ({sectionIndex}).");

            if (sectionLength == 0 || swapRange == 0)
                return;

            var lengthBehindSwapIndex = sectionLength - swapIndex;
            var tempOffset = indexToID.Length;

            // aaaaaabbbbcc ....
            // aaaaaabbbbcc .... bbbb
            //        |           ^
            //        |___________|

            // Copy the original indices to beyond the end of the list
            {
                // Make space for these indices, hopefully the index list already has the capacity for 
                // this and no allocation needs to be made
                indexToID.ResizeUninitialized(tempOffset + swapRange);
                var indexToIDPtr = ((int*)indexToID.GetUnsafePtr());
                UnsafeUtility.MemMove(indexToIDPtr + tempOffset, indexToIDPtr + sectionIndex + swapIndex, swapRange * sizeof(int));
            }

            // aaaaaabbbbcc .... bbbb
            // aaaaaaccbbcc .... bbbb
            //        ^  |       
            //        |__|       

            // Move indices behind our swapIndex/swapRange on top of where our swap region begins
            var count = lengthBehindSwapIndex - swapRange;
            {
                var indexToIDPtr = ((int*)indexToID.GetUnsafePtr()) + sectionIndex + swapIndex;
                UnsafeUtility.MemMove(indexToIDPtr, indexToIDPtr + swapRange, count * sizeof(int));
            }

            // aaaaaaccbbcc .... bbbb
            // aaaaaaccbbbb .... bbbb
            //         ^           |
            //         |___________|

            // Copy the original indices to the end
            {
                var indexToIDPtr = ((int*)indexToID.GetUnsafePtr());
                UnsafeUtility.MemMove(indexToIDPtr + sectionIndex + swapIndex + count, indexToIDPtr + tempOffset, swapRange * sizeof(int));
            }

            // aaaaaaccbbbb .... bbbb
            // aaaaaaccbbbb .... 

            // Resize indices list to remove the temporary data, this is basically just 
            //   indexToID.length = tempOffset
            indexToID.ResizeUninitialized(tempOffset);

            for (int index = sectionIndex + swapIndex, lastIndex = sectionIndex + sectionLength; index < lastIndex; index++)
            {
                var idInternal = indexToID[index] - 1;

                var idLookup = idToIndex[idInternal];
                idLookup.index = index;
                idToIndex[idInternal] = idLookup;
            }
        }

        internal unsafe int InsertIntoIndexRange(int offset, int count, int insertIndex, int srcIndex)
        {
            var newCount = count + 1;
            var newOffset = sectionManager.ReallocateRange(offset, count, newCount);
            if (newOffset < 0)
                throw new ArgumentException($"{nameof(newOffset)} must be 0 or higher.");

            if (indexToID.Length < newOffset + newCount)
                indexToID.Resize(newOffset + newCount, NativeArrayOptions.ClearMemory);

            var originalID = indexToID[srcIndex];
            indexToID[srcIndex] = 0;

            // We first move the front part (when necesary)
            var items = insertIndex;
            if (items > 0)
            {
                var indexToIDPtr = (int*)indexToID.GetUnsafePtr();
                UnsafeUtility.MemMove(indexToIDPtr + newOffset, indexToIDPtr + offset, items * sizeof(int));
            }

            // Then we move the back part to the correct new offset (when necesary) ..
            items = count - insertIndex;
            if (items > 0)
            {
                var indexToIDPtr = (int*)indexToID.GetUnsafePtr();
                UnsafeUtility.MemMove(indexToIDPtr + newOffset + insertIndex + 1, indexToIDPtr + offset + insertIndex, items * sizeof(int));
            }

            // Then we copy srcIndex to the new location
            var newNodeIndex = newOffset + insertIndex;
            indexToID[newNodeIndex] = originalID;

            // Then we set the old indices to 0
            for (int index = offset, lastIndex = (offset + count); index < lastIndex; index++)
            {
                if (index >= newOffset && index < newOffset + newCount)
                    continue;

                indexToID[index] = 0;
            }

            // And fixup the id to index lookup
            for (int index = newOffset, lastIndex = (newOffset + newCount); index < lastIndex; index++)
            {
                var idInternal = indexToID[index] - 1;

                var idLookup = idToIndex[idInternal];
                idLookup.index = index;
                idToIndex[idInternal] = idLookup;
            }

            return newOffset;
        }

        public int FreeID(int id, int generation)
        {
            int index = GetIndex(id, generation);
            if (index < 0)
                return -1;

            var idInternal = id - 1; // We don't want 0 to be a valid id

            sectionManager.FreeRange(index, 1);

            Debug.Assert(!freeIDs.Contains(idInternal));
            freeIDs.Add(idInternal);

            var idLookup = idToIndex[idInternal];
            indexToID[index] = 0;
            idLookup.index = -1;
            idToIndex[idInternal] = idLookup;
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FreeIndex(int index)
        {
            FreeIndexRange(index, 1);
        }

        public void FreeIndexRange(int startIndex, int range)
        {
            var lastIndex = (int)(startIndex + range);
            if (startIndex < 0 || lastIndex > indexToID.Length)
                throw new ArgumentOutOfRangeException($"StartIndex {startIndex} with range {range}, must be between 0 and {indexToID.Length}");

            if (range == 0)
                return; // nothing to do

            for (int index = startIndex; index < lastIndex; index++)
            {
                var idInternal = indexToID[index] - 1;
                indexToID[index] = 0;

                Debug.Assert(!freeIDs.Contains(idInternal)); 
                freeIDs.Add(idInternal);

                var idLookup = idToIndex[idInternal];
                idLookup.index = -1;
                idToIndex[idInternal] = idLookup;
            }

            sectionManager.FreeRange(startIndex, (int)range);
        }
    }
}