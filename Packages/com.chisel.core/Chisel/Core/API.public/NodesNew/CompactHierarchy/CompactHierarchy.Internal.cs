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

namespace Chisel.Core
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

    // TODO: implement CompactInternal

    // TODO: how do generator nodes fit into this?
    // TODO: need way to be able to serialize hierarchy (so we can cache them w/ generators)

    /*
    make IDManager record ranges in freeIndices + easy lookup of desired range (but prefer reallocating range)
    use IDManager for compactNodeIDs

    --- make things work without NodeIDs, or remove need for compactNodeID

    move hierarchyID out of compactNodeID
    remove need for compactNodeIDs
    (store data directly in data referenced by NodeIDs)
    make CompactHierarchyManager usable with burst
    */


    public partial struct CompactHierarchy : IDisposable
    {
        CompactHierarchyID hierarchyID;

        // TODO: make this more controlled
        public CompactNodeID RootID;

        [BurstCompatible]
        [DebuggerDisplay("Index = {index}, Generation = {generation}")]
        struct IndexLookup
        {
            public Int32 index;
            public Int32 generation;
        }

        // TODO: Should create own container with its own array pointers (fewer indirections)

        NativeMultiHashMap<int, CompactNodeID> brushMeshToBrush;

        NativeList<CompactChildNode> compactNodes;
        
        NativeList<IndexLookup>      idToIndex;
        SectionManager               sectionManager;
        NativeList<int>              freeIDs; // TODO: should work with ranges so we can easily find chunks of available memory

        public CompactHierarchyID HierarchyID { get { return hierarchyID; } }

        public void Dispose()
        {
            //Debug.Log($"Dispose (value: {hierarchyID.value}, generation {hierarchyID.generation})");
            if (brushMeshToBrush.IsCreated) brushMeshToBrush.Dispose(); brushMeshToBrush = default;
            if (compactNodes.IsCreated) compactNodes.Dispose(); compactNodes = default;
            if (idToIndex.IsCreated) idToIndex.Dispose(); idToIndex = default;

            try
            {
                CompactHierarchyManager.FreeHierarchyID(hierarchyID);
            }
            finally
            {
                hierarchyID = CompactHierarchyID.Invalid;
                if (freeIDs.IsCreated) freeIDs.Dispose(); freeIDs = default;
                if (sectionManager.IsCreated) sectionManager.Dispose(); sectionManager = default;
            } 
        }

        internal CompactNodeID CreateNode(NodeID nodeID, CompactNode nodeInformation)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            return CreateNode(nodeID, Int32.MaxValue, in nodeInformation, CompactNodeID.Invalid);
        }

        internal CompactNodeID CreateNode(NodeID nodeID, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            return CreateNode(nodeID, Int32.MaxValue, in nodeInformation, parentID);
        }

        internal CompactNodeID CreateNode(NodeID nodeID, int index, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            if (index == Int32.MaxValue)
                index = sectionManager.AllocateRange(1);
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var compactNodeID = CreateID(index);
            if (index >= compactNodes.Length)
                compactNodes.Resize(index + 1, NativeArrayOptions.ClearMemory);
            compactNodes[index] = new CompactChildNode
            {
                nodeInformation = nodeInformation,
                nodeID          = nodeID,
                compactNodeID   = compactNodeID,
                parentID        = parentID,
                childOffset     = 0,
                childCount      = 0
            };
            if (nodeInformation.brushMeshID != Int32.MaxValue)
                brushMeshToBrush.Add(nodeInformation.brushMeshID, compactNodeID);
            Debug.Assert(IsValidCompactNodeID(compactNodeID), "newly created ID is invalid");
            Debug.Assert(GetChildRef(compactNodeID).userID == nodeInformation.userID, "newly created ID is invalid");
            return compactNodeID;
        }

        // TODO: move somewhere else
        public static MinMaxAABB CalculateBounds(int brushMeshID, in float4x4 transformation)
        {
            if (!BrushMeshManager.IsBrushMeshIDValid(brushMeshID))
                return default;

            var brushMesh = BrushMeshManager.GetBrushMesh(brushMeshID);
            if (brushMesh == null)
                return default;

            var vertices = brushMesh.vertices;
            if (vertices == null ||
                vertices.Length == 0)
                return default;

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            
            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = math.mul(transformation, new float4(vertices[i], 1)).xyz;
                min = math.min(min, vert);
                max = math.max(max, vert); 
            }
            return new MinMaxAABB { Min = min, Max = max };
        }

        public uint GetHash()
        {
            // TODO: ability to generate hash of entire hierarchy (but do not include IDs (somehow) since they might vary from run to run)
            throw new NotImplementedException();
        }

        #region ID / Memory Management
        CompactNodeID CreateID(int index)
        {
            int lastID;
            if (freeIDs.Length > 0)
            {
                var freeID = freeIDs.Length - 1;
                lastID = freeIDs[freeID];
                freeIDs.RemoveAt(freeID);
                if (lastID >= idToIndex.Length)
                {
                    var prevLength = idToIndex.Length;
                    idToIndex.ResizeUninitialized(lastID + 1);
                    for (int i = prevLength; i < idToIndex.Length; i++)
                        idToIndex[i] = new IndexLookup { index = -1, generation = 0 };
                }
                var generation = idToIndex[lastID].generation + 1;
                idToIndex[lastID] = new IndexLookup { index = index, generation = generation };

                //Debug.Log($"Created id (value: {lastNodeValue}, generation: {generation}) at index ({index}) on hierarchy (value: {hierarchyID.value}, generation: {hierarchyID.generation})");
                return new CompactNodeID(hierarchyID: hierarchyID, value: lastID + 1, generation: generation);
            } else
            {
                lastID = idToIndex.Length;
                var generation = 1;
                idToIndex.Add(new IndexLookup { index = index, generation = generation });
                //Debug.Log($"Created id (value: {lastNodeValue}, generation: {generation}) at index ({index}) on hierarchy (value: {hierarchyID.value}, generation: {hierarchyID.generation})");
                return new CompactNodeID(hierarchyID: hierarchyID, value: lastID + 1, generation: generation);
            } 
        }

        void RemoveMeshReference(CompactNodeID compactNodeID, int brushMeshID)
        {
            if (brushMeshID == Int32.MaxValue)
                return;
            var value = compactNodeID.value;

            bool found = true;
            while (found)
            {
                found = false;
                if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var item, out var iterator))
                {
                    do
                    {
                        if (item.value == value)
                        {
                            found = true;
                            brushMeshToBrush.Remove(iterator);
                            break;
                        }
                    } while (brushMeshToBrush.TryGetNextValue(out item, ref iterator));
                }
            }
        }

        void RemoveIDs(int index, uint range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = (int)(index + range); i < lastNode; i++)
            {
                var compactNodeID   = compactNodes[i].compactNodeID;
                var brushMeshID     = compactNodes[i].nodeInformation.brushMeshID;
                
                var valueIndex = compactNodeID.value - 1;

                //Debug.Log($"Removed id (value: {value}, generation: {compactNodeID.generation}) at index ({index}) on hierarchy (value: {hierarchyID.value}, generation: {hierarchyID.generation})");
                RemoveMeshReference(compactNodeID, brushMeshID);

                var idLookup = idToIndex[valueIndex];
                idLookup.index = -1;
                idToIndex[valueIndex] = idLookup;

                Debug.Assert(!IsValidCompactNodeID(compactNodeID), "destroyed ID is still valid");
            }
        }

        void FreeIndexRange(int index, uint range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            sectionManager.FreeRange(index, (int)range);
            for (int i = index, lastNode = (int)(index + range); i < lastNode; i++)
            {
                compactNodes[i] = CompactChildNode.Invalid;
                freeIDs.Add(i);
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SetSize(int parentNodeIndex, int length)
        {
            if (length < 0)
                throw new ArgumentException(nameof(length));

            if (parentNodeIndex < 0 || parentNodeIndex >= compactNodes.Length)
                throw new ArgumentException(nameof(parentNodeIndex));

            var node = compactNodes[parentNodeIndex];
            node.childOffset = sectionManager.ReallocateRange(node.childOffset, node.childCount, (int)length);
            node.childCount = length;
            compactNodes[parentNodeIndex] = node;
            return node.childOffset;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe int ReserveCapacityAndReturnOffset(int parentNodeIndex, int desiredLength)
        {
            // TODO: use ranges w/ "freeIDs" to find chunks
            // TODO: see if we can safely not set all these nodes to invalid here

            if (desiredLength < 0)
                throw new ArgumentException(nameof(desiredLength));

            if (parentNodeIndex < 0 || parentNodeIndex >= compactNodes.Length) 
                throw new ArgumentException(nameof(parentNodeIndex));

            var parentNode  = compactNodes[parentNodeIndex];
            int offset      = parentNode.childOffset;
            int count       = parentNode.childCount;

            var newOffset   = sectionManager.ReallocateRange(offset, count, (int)desiredLength);
            /*
            if (count > 0 && newOffset != offset)
            {
                // We first move the last nodes to the correct new offset ..
                var items = count;
                if (items > 0)
                {
                    var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
                    UnsafeUtility.MemMove(compactNodesPtr + newOffset, compactNodesPtr + offset, items * sizeof(CompactChildNode));
                }
            }

            for (int i = offset, lastNode = offset + count; i < lastNode; i++)
            {
                if (i >= newOffset && i <= newOffset + desiredLength)
                    continue;
                compactNodes[i] = CompactChildNode.Invalid;
            }

            // Set all newly created nodes to invalid
            for (int i = newOffset, lastNode = newOffset + desiredLength; i < lastNode; i++)
                compactNodes[i] = CompactChildNode.Invalid;
            */

            return newOffset;
        }
        #endregion


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        int HierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return -1;

            var valueIndex = compactNodeID.value - 1;
            if (valueIndex < 0 || valueIndex >= idToIndex.Length)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {valueIndex}, generation: {compactNodeID.generation}) out of bounds [0..{idToIndex.Length}], are you using an old reference?", nameof(compactNodeID));

            var nodeIndex = idToIndex[valueIndex].index;
            if (nodeIndex == -1)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {valueIndex}, generation: {compactNodeID.generation}, index: {nodeIndex}) doesn't lead to a valid index ({nodeIndex}), are you using an old reference?", nameof(compactNodeID));

            var expectedGeneration = idToIndex[valueIndex].generation;
            if (expectedGeneration != compactNodeID.generation)
                throw new ArgumentException($"Generation mismatch on {nameof(CompactNodeID)} (value: {valueIndex}, index: {nodeIndex}) whose generation ({compactNodeID.generation}), is not the expected generation ({expectedGeneration}) in the hierarchy (value: {hierarchyID.value}, generation: {hierarchyID.generation}), are you using an old reference?", nameof(compactNodeID));

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                throw new ArgumentException("internal index is out of bounds", nameof(compactNodeID));

            return nodeIndex;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        int SafeHierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException($"{nameof(CompactNodeID)} is invalid", nameof(compactNodeID));

            var valueIndex = compactNodeID.value - 1;
            if (valueIndex < 0 || valueIndex >= idToIndex.Length)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {valueIndex}, generation: {compactNodeID.generation}) out of bounds [0..{idToIndex.Length}], are you using an old reference?", nameof(compactNodeID));

            var nodeIndex = idToIndex[valueIndex].index;
            if (nodeIndex == -1)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {valueIndex}, generation: {compactNodeID.generation}, index: {nodeIndex}) doesn't lead to a valid index ({nodeIndex}), are you using an old reference?", nameof(compactNodeID));

            var expectedGeneration = idToIndex[valueIndex].generation;
            if (expectedGeneration != compactNodeID.generation)
                throw new ArgumentException($"Generation mismatch on {nameof(CompactNodeID)} (value: {valueIndex}, index: {nodeIndex}) whose generation ({compactNodeID.generation}), is not the expected generation ({expectedGeneration}) in the hierarchy (value: {hierarchyID.value}, generation: {hierarchyID.generation}), are you using an old reference?", nameof(compactNodeID));

            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                throw new ArgumentException("Internal index is out of bounds", nameof(compactNodeID));

            return nodeIndex;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactChildNode SafeGetNodeRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex = SafeHierarchyIndexOfInternal(compactNodeID);
            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return ref compactNodesPtr[nodeIndex];
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex = SafeHierarchyIndexOfInternal(compactNodeID);
            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return ref compactNodesPtr[nodeIndex].nodeInformation;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe CompactNodeID GetChildIDAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = SafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            return compactNodesPtr[nodeIndex].compactNodeID;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
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

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                FreeIndexRange(nodeIndex, range);

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
                FreeIndexRange(nodeIndex, range);

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
            FreeIndexRange(lastNodeIndex, range);

            // Fix up the idToIndex lookup table
            for (int i = nodeIndex; i < lastNodeIndex; i++)
            {
                var value = compactNodes[i].compactNodeID.value;
                if (value == 0)
                    continue;

                var valueIndex = value - 1;
                var structure = idToIndex[valueIndex];
                structure.index = i;
                idToIndex[valueIndex] = structure;
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
            for (int i = nodeIndex; i < (int)(lastNodeIndex + range); i++)
            {
                var value = compactNodes[i].compactNodeID.value;
                if (value == 0)
                    continue;

                var valueIndex = value - 1;
                var structure = idToIndex[valueIndex];
                structure.index = i;
                idToIndex[valueIndex] = structure;
            }

            parentHierarchy.childCount -= (int)range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void AttachInternal(CompactNodeID parentID, int parentIndex, int index, CompactNodeID compactNodeID)
        {
            Debug.Assert(parentID != CompactNodeID.Invalid);

            var parentHierarchy   = compactNodes[parentIndex];
            var parentChildCount  = parentHierarchy.childCount;
            if (index < 0 || index > parentChildCount) throw new IndexOutOfRangeException();

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException($"{nameof(CompactNodeID)} is invalid", nameof(compactNodeID));

            // Make a temporary copy of our node in case we need to move it
            var nodeItem        = compactNodes[nodeIndex];
            var oldParentID     = nodeItem.parentID;

            // If the node is already a child of a different parent, then we need to remove it from that parent
            if (oldParentID != CompactNodeID.Invalid && oldParentID != parentID)
            { 
                var oldParentIndex       = HierarchyIndexOfInternal(oldParentID);
                var oldParent            = compactNodes[oldParentIndex];
                var oldParentChildCount  = oldParent.childCount;
                var oldParentChildOffset = oldParent.childOffset;
                var lastOldNodeIndex     = oldParentChildOffset + oldParentChildCount - 1;

                Debug.Assert(oldParentChildCount > 0);
                Debug.Assert(oldParent.compactNodeID == oldParentID);
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
                        var value = compactNodes[i].compactNodeID.value;
                        if (value == 0)
                            continue;

                        var valueIndex = value - 1;
                        var structure = idToIndex[valueIndex];
                        structure.index = i;
                        idToIndex[valueIndex] = structure;
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
                for (int i = index1; i < index2; i++)
                {
                    var value = compactNodes[i].compactNodeID.value;
                    if (value == 0)
                        continue;

                    var valueIndex = value - 1;
                    var structure = idToIndex[valueIndex];
                    structure.index = i;
                    idToIndex[valueIndex] = structure;
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

                if (compactNodes.Length < parentChildOffset + parentChildCount)
                    compactNodes.Resize(parentChildOffset + parentChildCount, NativeArrayOptions.ClearMemory);

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
                }

                // Then we copy our node to the new location
                nodeIndex = parentChildOffset + index;
                nodeItem.parentID = parentID;
                compactNodes[nodeIndex] = nodeItem;

                // We then fixup the idToIndex table to ensure that our ids keep pointing to the right indices
                for (int i = parentChildOffset + firstModifiedIndex, lastNodeIndex = parentChildOffset + parentChildCount; i < lastNodeIndex; i++)
                {
                    var value = compactNodes[i].compactNodeID.value;
                    if (value == 0)
                        continue;

                    var valueIndex = value - 1;
                    var structure = idToIndex[valueIndex];
                    structure.index = i;
                    idToIndex[valueIndex] = structure;
                }
                
                parentHierarchy.childOffset = parentChildOffset; // We make sure we set the parent child offset correctly
                parentHierarchy.childCount++;                    // And we increase the childCount of our parent
                compactNodes[parentIndex] = parentHierarchy;
            }
        }

        internal void GetBrushesInOrder(System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (brushes == null)
                return;
            UpdateTreeNodeList(null, brushes);
        }

        internal unsafe void UpdateTreeNodeList(System.Collections.Generic.List<CompactNodeID> nodes, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (nodes != null) nodes.Clear();
            if (brushes != null) brushes.Clear();
            if (nodes == null && brushes == null)
                return;

            var nodeIndex       = SafeHierarchyIndexOfInternal(RootID);
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length)
                return;
            
            if (nodes != null) nodes.Add(RootID);
            var compactNodesPtr = (CompactChildNode*)compactNodes.GetUnsafePtr();
            ref var parent = ref compactNodesPtr[nodeIndex];
            RecursiveAddTreeChildren(compactNodesPtr, ref parent, nodes, brushes);
        }

        // TODO: rewrite to be non-recursive
        unsafe void RecursiveAddTreeChildren(CompactChildNode* compactNodesPtr, ref CompactChildNode parent,
                                             System.Collections.Generic.List<CompactNodeID> nodes,
                                             System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            for (int i = 0, childIndex = parent.childOffset, childCount = parent.childCount; i < childCount; i++, childIndex++)
            {
                ref var child = ref compactNodesPtr[childIndex];
                if (nodes != null)
                    nodes.Add(child.compactNodeID);
                if (child.childCount > 0)
                {
                    RecursiveAddTreeChildren(compactNodesPtr, ref child,
                                             nodes,
                                             brushes);
                } else
                if (child.nodeInformation.brushMeshID != Int32.MaxValue && brushes != null)
                    brushes.Add(new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(child.compactNodeID) });
            }
        }


        // TODO: when we change brushMeshIDs to be hashes of meshes, we need to pass along both the 
        // original and the new hash and switch them
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public void NotifyBrushMeshModified(System.Collections.Generic.HashSet<int> modifiedBrushMeshes)
        {
            bool modified = false;
            var failedItems = new System.Collections.Generic.List<(CompactNodeID,int)>();
            foreach (var brushMeshID in modifiedBrushMeshes)
            {
                if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var item, out var iterator))
                {
                    do
                    {
                        try
                        {
                            var node = GetChildRef(item);
                            node.flags |= NodeStatusFlags.NeedFullUpdate;
                            modified = true;
                        }
                        catch (Exception ex) { Debug.LogException(ex); failedItems.Add((item, brushMeshID)); }
                    } while (brushMeshToBrush.TryGetNextValue(out item, ref iterator));
                }
            }
            if (modified)
            {
                try
                {
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            if (failedItems.Count > 0)
            {
                for (int i = 0; i < failedItems.Count; i++)
                {
                    var (item, brushMeshID) = failedItems[i];
                    RemoveMeshReference(item, brushMeshID);
                }
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public void NotifyBrushMeshRemoved(int brushMeshID)
        {
            bool modified = false;
            bool found = false;
            while (found)
            {
                found = false;
                if (brushMeshToBrush.TryGetFirstValue(brushMeshID, out var item, out var iterator))
                {
                    try
                    {
                        var node = GetChildRef(item);
                        // TODO: Make it impossible to change this in other places without us detecting this so we can update brushMeshToBrush
                        node.brushMeshID = 0;
                        node.flags |= NodeStatusFlags.NeedFullUpdate;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                    modified = true;
                    found = true;
                    brushMeshToBrush.Remove(iterator);
                }
            }
            if (modified)
            {
                try
                {
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }


        internal int CompactNodeCount
        {
            get
            {
                return compactNodes.Length;
            }
        }

        // Temporary workaround until we can switch to hashes
        internal bool IsAnyStatusFlagSet(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return node.flags != NodeStatusFlags.None;
        }

        internal bool IsStatusFlagSet(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return (node.flags & flag) == flag;
        }

        internal void SetStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags |= flag;
        }

        internal void ClearAllStatusFlags(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags = NodeStatusFlags.None;
        }

        internal void ClearStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags &= ~flag;
        }

    }
}
