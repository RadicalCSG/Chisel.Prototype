using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Chisel.Core.New
{
    // TODO: we need to store/manage complete hierarchies somewhere
    // TODO: Make it possible to modify/set the root

    // TODO: create structs around CompactHierarchy to abstract internal mechanisms
    // TODO: also need a way to store unique brushMeshes (use hashes to combine duplicates?)

    // TODO: need way to iterate through all valid nodes in hierarchy
    // TODO: need way to iterate through all "root nodes" (no parent)
    // TODO: need way to count valid nodes
    // TODO: need way to count root nodes (no parent)
    // TODO: need way to count unused elements in hierarchy

    // TODO: implement AttachInternal
    // TODO: implement CompactInternal

    // TODO: how do generator nodes fit into this?
    // TODO: need way to be able to serialize hierarchy (so we can cache them w/ generators)


    public partial struct CompactHierarchy : IDisposable
    {
        CompactHierarchyID hierarchyID;

        // TODO: make this more controlled
        public CompactNodeID RootID;

        [BurstCompatible]
        [DebuggerDisplay("Index = {index}, Generation = {generation}")]
        struct Generation
        {
            public Int32 index;
            public Int32 generation;
        }

        // TODO: Should create own container with its own array pointers (fewer indirections)

        NativeMultiHashMap<int, CompactNodeID> brushMeshToBrush;
        NativeList<CompactNodeID>    unorderedBrushesInTree;

        NativeList<CompactChildNode> compactNodes;
        
        NativeList<Generation>       idToIndex;
        NativeList<int>              freeIDs; // TODO: should work with ranges so we can easily find chunks of available memory

        public CompactHierarchyID ID { get { return hierarchyID; } }

        public void Dispose()
        {
            CompactHierarchyManager.FreeID(hierarchyID);
            hierarchyID = CompactHierarchyID.Invalid;
            if (unorderedBrushesInTree.IsCreated) unorderedBrushesInTree.Dispose(); unorderedBrushesInTree = default;
            if (brushMeshToBrush.IsCreated) brushMeshToBrush.Dispose(); brushMeshToBrush = default;
            if (compactNodes.IsCreated) compactNodes.Dispose(); compactNodes = default;
            if (idToIndex.IsCreated) idToIndex.Dispose(); idToIndex = default;
            if (freeIDs.IsCreated) freeIDs.Dispose(); freeIDs = default;
        }


        public uint GetHash()
        {
            // TODO: ability to generate hash of entire hierarchy (but do not include IDs (somehow) since they might vary from run to run)
            throw new NotImplementedException();
        }

        #region ID / Memory Management
        CompactNodeID CreateID(int index)
        {
            int lastNodeID;
            if (freeIDs.Length > 0)
            {
                var freeID = freeIDs.Length - 1;
                lastNodeID = freeIDs[freeID];
                freeIDs.RemoveAt(freeID);
                var generation = idToIndex[lastNodeID].generation + 1;
                idToIndex[lastNodeID] = new Generation { index = index, generation = generation };
                return new CompactNodeID(hierarchyID: hierarchyID, id: lastNodeID, generation: generation);
            } else
            {
                lastNodeID = idToIndex.Length;
                idToIndex.Add(new Generation { index = index, generation = 0 });
                return new CompactNodeID(hierarchyID: hierarchyID, id: lastNodeID, generation: 0);
            }
        }

        void RemoveIDs(int index, uint range)
        {
            if (index < 0 || index + range >= compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = (int)(index + range); i < lastNode; i++)
            {
                var nodeID = compactNodes[i].nodeID;
                var id = nodeID.ID;
                var brushMeshID = compactNodes[i].nodeInformation.brushMeshID;

                if (brushMeshID != Int32.MaxValue)
                {
                    if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var item, out var iterator))
                    {
                        do
                        {
                            if (item.ID == id)
                            {
                                brushMeshToBrush.Remove(iterator);
                                break;
                            }
                        } while (brushMeshToBrush.TryGetNextValue(out item, ref iterator));
                    }
                    ChiselNativeListExtensions.Remove(unorderedBrushesInTree, nodeID);
                }

                var idLookup = idToIndex[id];
                idLookup.index = -1;
                idToIndex[id] = idLookup;
            }
        }

        void FreeIndexRange(int index, uint range)
        {
            if (index < 0 || index + range >= compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = (int)(index + range); i < lastNode; i++)
            {
                compactNodes[i] = CompactChildNode.Invalid;
                freeIDs.Add(i);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe int ReserveCapacityAndReturnOffset(int parentNodeIndex, int capacity)
        {
            // TODO: use ranges w/ "freeIDs" to find chunks
            // TODO: see if we can safely not set all these nodes to invalid here

            if (capacity < 0)
                throw new ArgumentException(nameof(capacity));

            if (parentNodeIndex < 0 || parentNodeIndex >= compactNodes.Length) 
                throw new ArgumentException(nameof(parentNodeIndex));

            var parentNode  = compactNodes[parentNodeIndex];
            int offset      = parentNode.childOffset;
            int count       = parentNode.childCount;
            if (count > 0)
            { 
                if (capacity < count)
                    throw new Exception(); // we cannot decrease capacity to less than our count

                int extraNodes = capacity - count;

                // Try to see if the nodes behind this list of children are unused, 
                // in which case we can use those
                int backIndex   = offset + count;
                while (extraNodes > 0)
                {
                    if (backIndex == compactNodes.Length)
                        break;

                    if (compactNodes[backIndex].nodeID != CompactNodeID.Invalid)
                        break;

                    extraNodes--;
                    backIndex++;
                }

                // If we still need space, try to see if we can find unused nodes *in front* of our list of children
                int frontIndex = offset;
                while (frontIndex > 0 && extraNodes > 0)
                {
                    if (compactNodes[frontIndex - 1].nodeID != CompactNodeID.Invalid)
                        break;

                    extraNodes--;
                    frontIndex--;
                }

                // See if we're at the end of compactNodes, in which case we can just extend compactNodes and be done with it
                if (extraNodes > 0 && backIndex == compactNodes.Length)
                {
                    var startPos = compactNodes.Length;
                    compactNodes.ResizeUninitialized(compactNodes.Length + extraNodes);

                    // Set all newly created nodes to Invalid
                    for (int i = startPos, lastNode = compactNodes.Length; i < lastNode; i++)
                        compactNodes[i] = CompactChildNode.Invalid;
                    extraNodes = 0;
                }

                // We've managed to find all the extraNodes we need
                if (extraNodes == 0)
                {
                    // Move our list of children backwards if we font any unused nodes there
                    if (frontIndex == offset)
                        return offset;

                    Debug.Assert(frontIndex < offset);
                    return frontIndex;
                }
            }

            // We couldn't find the extra nodes we need and we need to move our node list to the end of compactNodes
            // OR our child count was 0, and we need to create a new list at the end of compactNodes

            var newOffset = compactNodes.Length;
            compactNodes.ResizeUninitialized(compactNodes.Length + capacity);

            // Set all newly created nodes to invalid
            for (int i = newOffset, lastNode = newOffset + capacity; i < lastNode; i++)
                compactNodes[i] = CompactChildNode.Invalid;

            return newOffset;
        }
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int HierarchyIndexOfInternal(CompactNodeID nodeID)
        {
            if (nodeID == CompactNodeID.Invalid)
                return -1;

            var id = nodeID.ID;
            if (id < 0 || id >= idToIndex.Length)
                throw new ArgumentException(nameof(nodeID), "invalid ID");

            var generation = idToIndex[id].generation;
            Debug.Assert(generation == nodeID.generation);
            if (generation != nodeID.generation)
                return -1;

            var nodeIndex = idToIndex[id].index;
            if (nodeIndex == -1)
                return -1;

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                throw new ArgumentException(nameof(nodeID), "internal index is out of bounds");

            return nodeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int SafeHierarchyIndexOfInternal(CompactNodeID nodeID)
        {
            if (nodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(nodeID), "invalid ID");

            var id = nodeID.ID;
            if (id < 0 || id >= idToIndex.Length)
                throw new ArgumentException(nameof(nodeID), "invalid ID");

            var generation = idToIndex[id].generation;
            Debug.Assert(generation == nodeID.generation);
            if (generation != nodeID.generation)
                throw new ArgumentException(nameof(nodeID), "invalid ID");

            var nodeIndex = idToIndex[id].index;
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), "invalid ID");

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                throw new ArgumentException(nameof(nodeID), "internal index is out of bounds");

            return nodeIndex;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = SafeHierarchyIndexOfInternal(nodeID);
            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return ref compactNodesPtr[nodeIndex].nodeInformation;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe CompactNodeID GetChildIDAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex         = SafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return compactNodesPtr[nodeIndex].nodeID;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex         = SafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return ref compactNodesPtr[nodeIndex].nodeInformation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int SiblingIndexOfInternal(int parentIndex, int nodeIndex)
        {
            var parentHierarchy = compactNodes[parentIndex];
            var parentChildOffset = parentHierarchy.childOffset;
            var parentChildCount = parentHierarchy.childCount;

            var index = nodeIndex - parentChildOffset;
            Debug.Assert(index >= 0 && index < parentChildCount);

            return index;
        }

        unsafe bool DeleteRangeInternal(int parentIndex, int siblingIndex, uint range, bool deleteChildren)
        {
            if (range == 0)
                return false;
            
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;
            var lastNodeIndex       = parentChildCount + parentChildOffset - 1;
            if (siblingIndex < 0 || siblingIndex + range > parentChildCount)
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));

            if (deleteChildren)
            {
                // Delete all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (int)(parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy  = compactNodes[i];
                    var childCount      = childHierarchy.childCount;
                    DeleteRangeInternal(i, 0, (uint)childCount, true);
                }
            } else
            {
                // Detach all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (int)(parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy = compactNodes[i];
                    var childCount = childHierarchy.childCount;
                    DetachRangeInternal(i, 0, (uint)childCount);
                }
            }
            

            var nodeIndex = siblingIndex + parentChildOffset;

            // Clear the ids of the children we're deleting
            RemoveIDs(nodeIndex, range);

            // Check if we're deleting from the front of the list of children
            if (siblingIndex == 0)
            {
                // Set our deleted nodes to invalid
                {
                    var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                    for (int i = nodeIndex; i < nodeIndex + range; i++)
                    {
                        compactNodesPtr[i] = CompactChildNode.Invalid;
                    }
                    FreeIndexRange(nodeIndex, range);
                }

                // If the range is identical to the number of children, we're deleting all the children
                if (parentChildCount == range)
                {
                    parentHierarchy.childCount = 0;
                    parentHierarchy.childOffset = 0;
                    compactNodes[parentIndex] = parentHierarchy;
                } else
                // Otherwise, we can just move the start offset of the parents' children forward
                {
                    parentHierarchy.childCount -= (int)range;
                    parentHierarchy.childOffset += (int)range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're deleting from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // Set our deleted nodes to invalid
                {
                    var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                    for (int i = nodeIndex; i < nodeIndex + range; i++)
                    {
                        compactNodesPtr[i] = CompactChildNode.Invalid;
                    }
                    FreeIndexRange(nodeIndex, range);
                }

                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= (int)range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }
            
            // If we get here, it means we're deleting children in the center of the list of children

            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            if (count > 0) // Using a negative value crashes Unity on UnsafeUtility.MemMove
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                UnsafeUtility.MemMove(compactNodesPtr + nodeIndex, compactNodesPtr + nodeIndex + range, count * sizeof(CompactChildNode));
            }


            // Set the left over nodes to invalid
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                for (int i = lastNodeIndex; i < lastNodeIndex + range; i++)
                {
                    compactNodesPtr[i] = CompactChildNode.Invalid;
                }
                FreeIndexRange(lastNodeIndex, range);
            }

            // Fix up the idToIndex lookup table
            for (int i = nodeIndex; i < lastNodeIndex; i++)
            {
                var id = compactNodes[i].nodeID.ID;
                var structure = idToIndex[id];
                structure.index = i;
                idToIndex[id] = structure;
            }

            parentHierarchy.childCount -= (int)range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }

        // "optimize" - remove holes, reorder them in hierarchy order
        unsafe bool CompactInternal()
        {
            // TODO: implement
            throw new NotImplementedException();
        }

        unsafe bool DetachAllInternal(int parentIndex)
        {
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildCount    = parentHierarchy.childCount;
            return DetachRangeInternal(parentIndex, 0, (uint)parentChildCount);
        }
            
        unsafe bool DetachRangeInternal(int parentIndex, int siblingIndex, uint range)
        {
            if (range == 0)
                return false;

            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;
            var lastNodeIndex       = parentChildCount + parentChildOffset - 1;
            if (siblingIndex < 0 || siblingIndex + range > parentChildCount)
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));

            var nodeIndex = siblingIndex + parentChildOffset;

            // Set the parents of our detached nodes to invalid
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                for (int i = nodeIndex; i < nodeIndex + range; i++)
                {
                    compactNodesPtr[i].parentID = CompactNodeID.Invalid;
                }
            }

            // Check if we're detaching from the front of the list of children
            if (siblingIndex == 0)
            {
                // If the range is identical to the number of children, we're detaching all the children
                if (parentChildCount == range)
                {
                    parentHierarchy.childCount = 0;
                    parentHierarchy.childOffset = 0;
                    compactNodes[parentIndex] = parentHierarchy;
                } else
                // Otherwise, we can just move the start offset of the parents' children forward
                {
                    parentHierarchy.childCount -= (int)range;
                    parentHierarchy.childOffset += (int)range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're detaching from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= (int)range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }
            
            // If we get here, it means we're detaching children in the center of the list of children

            var prevLength = compactNodes.Length;
            // Resize compactNodes to have space for the nodes we're detaching (compactNodes will probably have capacity for this already)
            compactNodes.ResizeUninitialized((int)(prevLength + range));

            // Copy the original nodes to behind all our other nodes
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                UnsafeUtility.MemMove(compactNodesPtr + prevLength, compactNodesPtr + nodeIndex, range * sizeof(CompactChildNode));
            }

            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            if (count > 0) // Using a negative value crashes Unity on UnsafeUtility.MemMove
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                UnsafeUtility.MemMove(compactNodesPtr + nodeIndex, compactNodesPtr + nodeIndex + range, count * sizeof(CompactChildNode));
            }

            // Copy original nodes to behind the new parent child list
            {
                var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                UnsafeUtility.MemMove(compactNodesPtr + lastNodeIndex, compactNodesPtr + prevLength, range * sizeof(CompactChildNode));
            }

            // Set the compactNodes length to its original size
            compactNodes.ResizeUninitialized(prevLength);

            // Fix up the idToIndex lookup table
            for (int i = nodeIndex; i <= (int)(lastNodeIndex + range); i++)
            {
                var id = compactNodes[i].nodeID.ID;
                var structure = idToIndex[id];
                structure.index = i;
                idToIndex[id] = structure;
            }

            parentHierarchy.childCount -= (int)range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void AttachInternal(CompactNodeID parentID, int parentIndex, int index, CompactNodeID nodeID)
        {
            Debug.Assert(parentID != CompactNodeID.Invalid);

            var parentHierarchy   = compactNodes[parentIndex];
            var parentChildCount  = parentHierarchy.childCount;
            if (index < 0 || index > parentChildCount) throw new IndexOutOfRangeException();

            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is invalid");


            // Make a temporary copy of our node in case we need to move it
            var nodeItem        = compactNodes[nodeIndex];
            var oldParentID     = nodeItem.parentID;

            // If the node is already a child of a different parent, then we need to remove it from that parent
            if (oldParentID != CompactNodeID.Invalid && oldParentID != parentID)
            { 
                var oldParentIndex       = HierarchyIndexOfInternal(nodeID);
                var oldParent            = compactNodes[oldParentIndex];
                var oldParentChildCount  = oldParent.childCount;
                var oldParentChildOffset = oldParent.childOffset;
                var lastOldNodeIndex     = oldParentChildOffset + oldParentChildCount - 1;

                Debug.Assert(oldParentChildCount > 0);
                Debug.Assert(nodeIndex >= oldParentChildOffset && nodeIndex <= lastOldNodeIndex);

                // If our node is the last node in the parent, we don't need to move any of the existing nodes
                if (nodeIndex < lastOldNodeIndex)
                {
                    // But if it's in not the last node, we need to move all the nodes of our 
                    // parent behind our node backward, overlapping our node
                    var items = oldParentChildCount - nodeIndex;
                    if (items > 0)
                    {
                        var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                        UnsafeUtility.MemMove(compactNodesPtr + nodeIndex, compactNodesPtr + nodeIndex + 1, items * sizeof(CompactChildNode));
                    }

                    // We then fixup the idToIndex table to ensure that our ids keep pointing to the right indices
                    for (int i = nodeIndex; i <= lastOldNodeIndex; i++)
                    {
                        var id = compactNodes[i].nodeID.ID;
                        var structure = idToIndex[id];
                        structure.index = i;
                        idToIndex[id] = structure;
                    }

                    // Finally, we set our node to the index of what used to be the last child of our parent (but is now a duplicate)
                    // We'll copy our node into this index further on
                    nodeIndex = lastOldNodeIndex;
                }

                // We've removed a child from our parent, so we need to decrease the child count
                oldParent.childCount--;
                if (oldParent.childCount == 0) oldParent.childOffset = 0;
                compactNodes[oldParentIndex] = oldParent;

                // Finally, we remove the reference to our old parent (and copy our node into its index, which may have been moved)
                nodeItem.parentID = CompactNodeID.Invalid;
                compactNodes[nodeIndex] = nodeItem;
                oldParentID = CompactNodeID.Invalid;
            }

            // If our new parent doesn't have any child nodes yet, we don't need to move our node and just set 
            // our node as the location for our children
            if (parentChildCount == 0)
            {
                parentHierarchy.childOffset = nodeIndex;
                parentHierarchy.childCount = 1;
                compactNodes[parentIndex] = parentHierarchy;
                
                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;
                return;
            }
            
            var parentChildOffset = parentHierarchy.childOffset;
            var desiredIndex      = parentChildOffset + index;

            // Check if our node is already a child of the right parent and at the correct position
            if (oldParentID == parentID && desiredIndex == nodeIndex)
                return;

            // If the desired index of our node is already at the index we want it to be, things are simple
            if (desiredIndex == nodeIndex)
            {
                parentHierarchy.childCount ++;
                compactNodes[parentIndex] = parentHierarchy;

                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;
                return;
            }

            // If, however, the desired index of our node is NOT at the index we want it to be, 
            // we need to move nodes around

            // If it's attached to the same parent, however, we can just move memory around
            if (oldParentID == parentID)
            {
                // We already know that desiredIndex and nodeIndex are not identical, and we have to move the nodes in between them
                int index1, index2;

                if (nodeIndex < desiredIndex)
                {
                    // nodeIndex    desiredIndex
                    //          ____
                    //         |    |
                    //         |    v
                    //      ,,,i....,,,
                    //            ^
                    // section to move to the left

                    index1 = nodeIndex;
                    index2 = desiredIndex;

                    // We move the section between both the new (desired) and old node index to the left, overlapping the old node
                    var items = index2 - index1;
                    if (items > 0)
                    {
                        var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                        UnsafeUtility.MemMove(compactNodesPtr + parentChildOffset + index1, compactNodesPtr + parentChildOffset + index1 + 1, items * sizeof(CompactChildNode));
                    }
                } else
                {
                    // desiredIndex    nodeIndex
                    //             ____
                    //            |    |
                    //            v    |
                    //         ,,,.....i,,,
                    //              ^
                    // section to move to the right

                    index1 = desiredIndex;
                    index2 = nodeIndex;

                    // We move the section between both the new (desired) and old node index to the right, overlapping the old node
                    var items = index2 - index1;
                    if (items > 0)
                    {
                        var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                        UnsafeUtility.MemMove(compactNodesPtr + parentChildOffset + index1 + 1, compactNodesPtr + parentChildOffset + index1, items * sizeof(CompactChildNode));
                    }
                }

                // We then fixup the idToIndex table to ensure that our ids keep pointing to the right indices
                for (int i = index1; i <= index2; i++)
                {
                    var id = compactNodes[i].nodeID.ID;
                    var structure = idToIndex[id];
                    structure.index = i;
                    idToIndex[id] = structure;
                }

                // Finally, we copy our node to the desired index, which is at the end of the section we just moved
                nodeItem.parentID = parentID;
                compactNodes[desiredIndex] = nodeItem;

                // And we increase the childCount of our parent
                parentHierarchy.childCount++;
                compactNodes[parentIndex] = parentHierarchy;
                return;
            } 
            
            // If it's a different parent then we need to change the size of our child list
            {
                var originalOffset = parentChildOffset;
                var originalCount  = parentChildCount;

                // Find (or create) a span of enough elements that we can use to copy our children into
                parentChildCount++;
                parentChildOffset = ReserveCapacityAndReturnOffset(parentIndex, parentChildCount);
                Debug.Assert(parentChildOffset >= 0 && parentChildOffset < compactNodes.Length + parentChildCount);

                int firstModifiedIndex = index; // We want to keep track of which ids will be invalidated

                // We first move the last nodes to the correct new offset ..
                var items = originalCount - index;
                if (items > 0)
                {
                    var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                    UnsafeUtility.MemMove(compactNodesPtr + parentChildOffset + index + 1, compactNodesPtr + originalOffset + index, items * sizeof(CompactChildNode));
                }

                // If our offset is different then the front section will not be in the right location, so we might need to copy this
                // We'd also need to reset the old nodes to invalid, if we don't we'd create new dangling nodes
                if (originalOffset != parentChildOffset)
                {
                    // Then we move the first part (if necesary)
                    items = index;
                    if (items > 0)
                    {
                        var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                        UnsafeUtility.MemMove(compactNodesPtr + parentChildOffset, compactNodesPtr + originalOffset, items * sizeof(CompactChildNode));
                        firstModifiedIndex = 0;
                    }

                    // Set all old nodes to invalid
                    FreeIndexRange(originalOffset + firstModifiedIndex, (uint)(originalCount - firstModifiedIndex));
                }

                // Then we copy our node to the new location
                nodeIndex = parentChildOffset + index;
                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;

                // We then fixup the idToIndex table to ensure that our ids keep pointing to the right indices
                for (int i = parentChildOffset + firstModifiedIndex, lastNodeIndex = parentChildOffset + parentChildCount; i < lastNodeIndex; i++)
                {
                    var id = compactNodes[i].nodeID.ID;
                    var structure = idToIndex[id];
                    structure.index = i;
                    idToIndex[id] = structure;
                }

                
                parentHierarchy.childOffset = parentChildOffset; // We make sure we set the parent child offset correctly
                parentHierarchy.childCount++;                    // And we increase the childCount of our parent
                compactNodes[parentIndex] = parentHierarchy;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void AttachInternal(CompactNodeID parentID, int parentIndex, int index, CompactHierarchy sourceHierarchy, CompactNodeID nodeID)
        {
            // TODO: implement
            throw new NotImplementedException();
        }

        internal CSGTreeBrush GetChildBrushAtIndex(Int32 index)
        {
            return new CSGTreeBrush { brushNodeID = unorderedBrushesInTree[index] };
        }

        public unsafe Int32 GetNumberOfBrushesInTree()
        {
            return unorderedBrushesInTree.Length;
        }

        // TODO: when we change brushMeshIDs to be hashes of meshes, we need to pass along both the 
        // original and the new hash and switch them
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public void NotifyBrushMeshModified(System.Collections.Generic.HashSet<int> modifiedBrushMeshes)
        {
            bool modified = false;
            foreach (var brushMeshID in modifiedBrushMeshes)
            {
                if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var nodeID, out var iterator))
                {
                    do
                    {
                        try
                        {
                            var node = GetChildRef(nodeID);
                            node.flags |= NodeStatusFlags.NeedFullUpdate;
                            modified = true;
                        }
                        catch (Exception ex) { Debug.LogException(ex); }
                    } while (brushMeshToBrush.TryGetNextValue(out nodeID, ref iterator));
                }
            }
            if (modified)
            {
                ref var rootNode = ref GetChildRef(RootID);
                rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public void NotifyBrushMeshRemoved(int brushMeshID)
        {
            bool modified = false;
            if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var nodeID, out var iterator))
            {
                do
                {
                    try
                    {
                        var node = GetChildRef(nodeID);
                        // TODO: Make it impossible to change this in other places without us detecting this so we can update brushMeshToBrush
                        node.brushMeshID = 0;
                        node.flags |= NodeStatusFlags.NeedFullUpdate;
                        modified = true;
                        // TODO: figure out if this is safe here ...
                        brushMeshToBrush.Remove(iterator);
                        ChiselNativeListExtensions.Remove(unorderedBrushesInTree, nodeID);
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                } while (brushMeshToBrush.TryGetNextValue(out nodeID, ref iterator));
            }
            if (modified)
            {
                ref var rootNode = ref GetChildRef(RootID);
                rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            }
        }
    }
}
