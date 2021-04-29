using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    // TODO: make sure everything is covered in tests
    // TODO: use native containers, make hierarchy use this as well
    struct IDManager : IDisposable
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
        

        public bool CheckConsistency()
        {
            for (int id = 0; id < idToIndex.Length; id++)
            {
                var index = idToIndex[id].index;
                if (index == -1)
                {
                    if (!freeIDs.Contains(id))
                    {
                        Debug.LogError($"!freeIDs.Contains({id})");
                        return false;
                    }
                    continue;
                }

                if (index < 0 || index >= indexToID.Length)
                {
                    Debug.LogError($"{index} < 0 || {index} >= {indexToID.Length}");
                    return false;
                }

                if ((indexToID[index] - 1) != id)
                {
                    Debug.LogError($"indexToID[{index}] - 1 ({(indexToID[index] - 1)}) == {id}");
                    return false;
                }

                if (sectionManager.IsIndexFree(index))
                {
                    Debug.LogError($"sectionManager.IsIndexFree({index})");
                    return false;
                }
            }

            for (int index = 0; index < indexToID.Length; index++)
            {
                var id = indexToID[index];
                if (id == 0)
                {
                    if (!sectionManager.IsIndexFree(index))
                    {
                        Debug.LogError($"!sectionManager.IsIndexFree({index})");
                        return false;
                    }
                    continue;
                }

                id--;

                if (id < 0 || id >= idToIndex.Length)
                {
                    Debug.LogError($"{id} < 0 || {id} >= {idToIndex.Length}");
                    return false;
                }

                if (idToIndex[id].index != index)
                {
                    Debug.LogError($"idToIndex[{id}].index ({idToIndex[id].index}) == {index}");
                    return false;
                }

                if (sectionManager.IsIndexFree(index))
                {
                    Debug.LogError($"sectionManager.IsIndexFree({index})");
                    return false;
                }
            }
            return true;
        }

        // Note: not all indices might be in use
        public int IndexCount   { get { return indexToID.Length; } }

        public bool IsIndexFree(int index) { return sectionManager.IsIndexFree(index); }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public bool IsValidID(int id, int generation, out int index)
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
        public bool IsValidIndex(int index, out int id, out int generation)
        {
            id = default; //out
            generation = default;//out

            if (!sectionManager.IsAllocatedIndex(index))
                return false;

            var idInternal = indexToID[index] - 1;
            if (idInternal < 0 || idInternal >= idToIndex.Length)
                return false;

            generation = idToIndex[idInternal].generation;
            if (idToIndex[idInternal].index != index)
                return false;

            id = idInternal + 1;//out
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndex(int id, int generation)
        {
            Debug.Assert(IsCreated);
            var idInternal = id - 1; // We don't want 0 to be a valid id

            if (idInternal < 0 || idInternal >= idToIndex.Length)
            {
                Debug.LogError($"{nameof(id)} ({id}) must be between 1 and {1 + idToIndex.Length}");
                return -1;
            }

            var idLookup = idToIndex[idInternal];
            if (idLookup.generation != generation)
            {
                Debug.LogError($"The given generation ({generation}) was not identical to the expected generation ({idLookup.generation}), are you using an old reference?");
                return -1;
            }

            var index = idLookup.index;
            if (index < 0 || index >= indexToID.Length)
            {
                if (indexToID.Length == 0)
                    Debug.LogError($"{nameof(id)} ({id}) does not point to an valid index. This lookup table does not contain any valid indices at the moment.");
                else
                    Debug.LogError($"{nameof(id)} ({id}) does not point to an valid index. It must be >= 0 and < {indexToID.Length}.");
                return -1;
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

            var index = sectionManager.AllocateRange(range);
            AllocateIndexRange(index, range);
            return index;
        }

        internal void AllocateIndexRange(int index, int range)
        {
            if ((index + range) > indexToID.Length)
                indexToID.Resize((index + range), NativeArrayOptions.ClearMemory);

            int idInternal, generation;
            // TODO: should make it possible to allocate ids in a range as well, for cache locality
            if (freeIDs.Length >= range)
            {
                var childIndex = index;
                while (range > 0)
                {
                    var freeID = freeIDs.Length - 1;
                    idInternal = freeIDs[freeID];
                    freeIDs.RemoveAt(freeID);
                    
                    generation = idToIndex[idInternal].generation + 1;
                    idToIndex[idInternal] = new IndexLookup { index = childIndex, generation = generation };

                    indexToID[childIndex] = idInternal + 1;

                    range--;
                    childIndex++;
                }
            }

            if (range <= 0)
                return;
            
            generation = 1;
            idInternal = idToIndex.Length;
            idToIndex.Resize((idInternal + range), NativeArrayOptions.ClearMemory);

            for (int childIndex = index, lastID = (idInternal + range); idInternal < lastID; idInternal++, childIndex++)
            {
                indexToID[childIndex] = idInternal + 1;
                idToIndex[idInternal] = new IndexLookup { index = childIndex, generation = generation };                    
            }
        }

        public void SwapIndexRangeToBack(int sectionIndex, int sectionLength, int swapIndex, int swapRange)
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
            // Make space for these indices, hopefully the index list already has the capacity for 
            // this and no allocation needs to be made
            indexToID.ResizeUninitialized(tempOffset + swapRange);
            indexToID.MemMove(tempOffset, sectionIndex + swapIndex, swapRange);
            
            // aaaaaabbbbcc .... bbbb
            // aaaaaaccbbcc .... bbbb
            //        ^  |       
            //        |__|       

            // Move indices behind our swapIndex/swapRange on top of where our swap region begins
            var count = lengthBehindSwapIndex - swapRange;
            indexToID.MemMove(sectionIndex + swapIndex, sectionIndex + swapIndex + swapRange, count);
            
            // aaaaaaccbbcc .... bbbb
            // aaaaaaccbbbb .... bbbb
            //         ^           |
            //         |___________|

            // Copy the original indices to the end
            indexToID.MemMove(sectionIndex + swapIndex + count, tempOffset, swapRange);

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
            var range = insertIndex;
            if (range > 0)
                indexToID.MemMove(newOffset, offset, range);

            // Then we move the back part to the correct new offset (when necesary) ..
            range = count - insertIndex;
            if (range > 0)
                indexToID.MemMove(newOffset + insertIndex + 1, offset + insertIndex, range);

            // Then we copy srcIndex to the new location
            var newNodeIndex = newOffset + insertIndex;
            indexToID[newNodeIndex] = originalID;

            // Then we set the old indices to 0
            if (srcIndex < newOffset || srcIndex >= newOffset + newCount)
            {
                sectionManager.FreeRange(srcIndex, 1);
                indexToID[srcIndex] = default;
            }
            for (int index = offset, lastIndex = (offset + count); index < lastIndex; index++)
            {
                // TODO: figure out if there's an off by one here
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

        internal unsafe void RemoveIndexRange(int offset, int count, int removeIndex, int removeRange)
        {
            if (offset < 0) throw new ArgumentException($"{nameof(offset)} must be positive");
            if (count < 0) throw new ArgumentException($"{nameof(count)} must be positive");
            if (removeIndex < 0) throw new ArgumentException($"{nameof(removeIndex)} must be positive");
            if (removeRange < 0) throw new ArgumentException($"{nameof(removeRange)} must be positive");
            if (removeRange == 0) throw new ArgumentException($"{nameof(removeRange)} must be above 0");
            if (count == 0) throw new ArgumentException($"{nameof(count)} must be above 0");
            if (removeIndex < offset)
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) < {nameof(offset)} ({offset})");
            if (removeIndex + removeRange > offset + count) 
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) + {nameof(removeRange)} ({removeRange}) > {nameof(count)} ({count})");

            // Remove the range of indices we want to remove
            for (int i = removeIndex, lastIndex = removeIndex + removeRange; i < lastIndex; i++)
            {
                var idInternal = indexToID[i] - 1;

                Debug.Assert(!freeIDs.Contains(idInternal));
                freeIDs.Add(idInternal);

                var idLookup = idToIndex[idInternal];
                idLookup.index = -1;
                idToIndex[idInternal] = idLookup;

                indexToID[i] = 0;
            }

            var leftOver = (offset + count) - (removeIndex + removeRange);
            if (leftOver < 0)
            {
                throw new ArgumentException($"{nameof(leftOver)} ({leftOver}) < 0");
            }
            if (removeIndex + leftOver > indexToID.Length)
            {
                throw new ArgumentException($"{nameof(removeIndex)} ({removeIndex}) + {nameof(leftOver)} ({leftOver}) < {nameof(indexToID)}.Length ({indexToID.Length})");
            }
            indexToID.MemMove(removeIndex, removeIndex + removeRange, leftOver);

            // Then fixup the id to index lookup
            for (int i = removeIndex, lastIndex = removeIndex + leftOver; i < lastIndex; i++)
            {
                var idInternal = indexToID[i] - 1;

                var idLookup = idToIndex[idInternal];
                idLookup.index = i;
                idToIndex[idInternal] = idLookup;
            }

            // And we set the old indices to 0
            for (int i = offset + count - removeRange; i < offset + count; i++)
                indexToID[i] = 0;

            sectionManager.FreeRange(offset + count - removeRange, removeRange);
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
            var lastIndex = startIndex + range;
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
            sectionManager.FreeRange(startIndex, range);
        }
    }
}