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

    // TODO: how do generator nodes fit into this?
    //      TODO: make generators work with unmanaged hierarchy/brushMeshBlob etc.
    //      TODO: make sure all components work with it
    //      TODO: make sure all editor functionality works with it
    //          TODO: show options
    //          TODO: convert to brush(es)
    //          TODO: get surfaces etc.

    // TODO: need debug visualization (editor window)
    //      TODO: need way to iterate through all valid nodes in hierarchy
    //      TODO: need way to iterate through all "root nodes" (no parent)
    //      TODO: need way to count valid nodes
    //      TODO: need way to count root nodes (no parent)
    //      TODO: need way to count unused elements in hierarchy

    // TODO: need way to be able to serialize hierarchy (so we can cache them w/ generators)

    // TODO: uv-generators / color-generators / normal-generators

    // TODO: properly calculate transformation hierarchy
    // TODO: properly generate wireframes (remove redundant stuff)
    // TODO: move queries to non managed code
    // TODO: need unmanaged Decomposition

    // TODO: need a way to store unique brushMeshes 
    //          use hashes to combine duplicates
    //          hash == id
    //          replace brushManager with this

    // TODO: CompactHierarchy.CreateHierarchy needs to add to CompactHierarchyManager?
    // TODO: implement CompactInternal
    //          -> create new hierarchy, add everything in order, destroy old hierarchy
    // TODO: implement GetHash()

    // TODO: clean up IsValidNodeIDs etc (implicit call to IsValidCompactNodeID)
    // TODO: clean up error handling

    // TODO: find a way to avoid requiring a default hierarchy?

    /*

    --- make things work without NodeIDs, or remove need for compactNodeID

    move hierarchyID out of compactNodeID
    remove need for compactNodeIDs
    (store data directly in data referenced by NodeIDs)
    make CompactHierarchyManager usable with burst
    */


    // TODO: Should be its own container with its own array pointers (fewer indirections)
    [BurstCompatible]
    public partial struct CompactHierarchy : IDisposable
    {
        UnsafeMultiHashMap<int, CompactNodeID> brushMeshToBrush;

        UnsafeList<CompactChildNode> compactNodes;
        UnsafeList<BrushOutline>     brushOutlines;
        IDManager                    idManager;

        
        public CompactNodeID        RootID      { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] internal set; }
        public CompactHierarchyID   HierarchyID { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] private set; }


        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return idManager.IsCreated && compactNodes.IsCreated && brushOutlines.IsCreated && brushMeshToBrush.IsCreated; }
        }

        public bool CheckConsistency()
        {
            if (HierarchyID == default)
            {
                return true;
            }

            if (!IsValidCompactNodeID(RootID))
            {
                Debug.LogError("!IsValidCompactNodeID(RootID)");
                return false;
            }

            if (!idManager.CheckConsistency())
                return false;

            var brushMeshKeyValueArrays = brushMeshToBrush.GetKeyValueArrays(Allocator.Temp);
            var brushMeshKeys           = brushMeshKeyValueArrays.Keys;
            var brushMeshValues         = brushMeshKeyValueArrays.Values;
            try
            {
                for (int i = 0; i < compactNodes.Length; i++)
                {
                    // If the node is not used, all its values should be set to default
                    if (compactNodes[i].compactNodeID == default)
                    {
                        if (compactNodes[i].nodeID != default ||
                            compactNodes[i].parentID != default ||
                            compactNodes[i].childCount != 0 ||
                            compactNodes[i].childOffset != 0 ||
                            compactNodes[i].nodeInformation.brushMeshID != 0 ||
                            compactNodes[i].nodeInformation.userID != 0)
                        {
                            Debug.LogError($"{compactNodes[i].nodeID} != default ||\n{compactNodes[i].parentID} != default ||\n{compactNodes[i].childCount} != 0 ||\n{compactNodes[i].childOffset} != 0 ||\n{compactNodes[i].nodeInformation.brushMeshID != 0} ||\n{compactNodes[i].nodeInformation.userID} != 0");
                            return false;
                        }
                        continue;
                    }

                    if (!IsValidCompactNodeID(compactNodes[i].compactNodeID))
                    {
                        Debug.LogError($"!IsValidCompactNodeID(compactNodes[{i}].compactNodeID)");
                        return false;
                    }

                    if (!CompactHierarchyManager.IsValidNodeID(compactNodes[i].nodeID))
                    {
                        Debug.LogError($"!CompactHierarchyManager.IsValidNodeID({compactNodes[i].nodeID})");
                        return false;
                    }

                    var foundCompactNodeID = CompactHierarchyManager.GetCompactNodeID(compactNodes[i].nodeID);
                    if (foundCompactNodeID != compactNodes[i].compactNodeID)
                    {
                        Debug.LogError($"{foundCompactNodeID} != compactNodes[{i}].compactNodeID");
                        return false;
                    }

                    ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodes[i].compactNodeID.hierarchyID);
                    var foundNodeID = hierarchy.GetNodeID(compactNodes[i].compactNodeID);
                    if (foundNodeID != compactNodes[i].nodeID)
                    {
                        Debug.LogError($"{foundNodeID} != compactNodes[{i}].nodeID");
                        return false;
                    }

                    for (int c = 0; c < compactNodes[i].childCount; c++)
                    {
                        var childIndex = compactNodes[i].childOffset + c;
                        if (childIndex < 0 || childIndex >= compactNodes.Length)
                        {
                            Debug.LogError($"{childIndex} < 0 || {childIndex} >= {compactNodes.Length}");
                            return false;
                        }

                        if (!IsValidCompactNodeID(compactNodes[childIndex].compactNodeID))
                        {
                            Debug.LogError($"!IsValidCompactNodeID(compactNodes[{childIndex}].compactNodeID)");
                            return false;
                        }
                        if (!IsValidCompactNodeID(compactNodes[childIndex].parentID))
                        {
                            Debug.LogError($"!IsValidCompactNodeID(compactNodes[{childIndex}].parentID)");
                            return false;
                        }
                        if (compactNodes[childIndex].parentID != compactNodes[i].compactNodeID)
                        {
                            Debug.LogError($"compactNodes[{childIndex}].parentID != compactNodes[{i}].compactNodeID");
                            return false;
                        }
                    }

                    if (compactNodes[i].nodeInformation.brushMeshID != Int32.MaxValue)
                    {
                        if (!brushMeshKeys.Contains(compactNodes[i].nodeInformation.brushMeshID))
                        {
                            Debug.LogError($"!brushMeshKeys.Contains(compactNodes[{i}].nodeInformation.brushMeshID) {compactNodes[i].nodeInformation.brushMeshID}");
                            return false;
                        }

                        if (!brushMeshValues.Contains(foundCompactNodeID))
                        {
                            Debug.LogError($"!brushMeshValues.Contains(compactNodeID) {foundCompactNodeID}");
                            return false;
                        }
                    }
                }

                for (int index = 0; index < idManager.IndexCount; index++)
                {
                    if (!idManager.IsValidIndex(index, out var value, out var generation))
                    {
                        if (compactNodes[index].compactNodeID != default)
                        {
                            Debug.LogError($"!idManager.IsValidIndex({index}, out var {value}, out var {generation}) && compactNodes[{index}].compactNodeID != default");
                            return false;
                        }
                        continue;
                    }

                    if (compactNodes[index].compactNodeID.value != value ||
                        compactNodes[index].compactNodeID.generation != generation)
                    {
                        Debug.LogError($"compactNodes[{index}].compactNodeID.value ({compactNodes[index].compactNodeID.value}) != {value} || compactNodes[{index}].compactNodeID.generation != {generation}  ({compactNodes[index].compactNodeID.generation})");
                        return false;
                    }

                    if (!idManager.IsValidID(value, generation, out var foundIndex))
                    {
                        Debug.LogError($"!idManager.IsValidID({value}, {generation}, out var {foundIndex})");
                        return false;
                    }

                    if (foundIndex != index)
                    {
                        Debug.LogError($"{foundIndex} != {index}");
                        return false;
                    }
                }


                for (int i = 0; i < brushMeshValues.Length; i++)
                {
                    var compactNodeID = brushMeshValues[i];
                    if (!idManager.IsValidID(compactNodeID.value, compactNodeID.generation, out var foundIndex))
                    {
                        Debug.LogError($"!idManager.IsValidID({compactNodeID.value}, {compactNodeID.generation}, out var {foundIndex})");
                        return false;
                    }
                }
                for (int i = 0; i < brushMeshValues.Length; i++)
                {
                    var compactNodeID = brushMeshValues[i];
                    if (!idManager.IsValidID(compactNodeID.value, compactNodeID.generation, out var foundIndex))
                    {
                        Debug.LogError($"!idManager.IsValidID({compactNodeID.value}, {compactNodeID.generation}, out var {foundIndex})");
                        return false;
                    }
                }
            }
            finally
            {
                brushMeshKeyValueArrays.Dispose();
            }
            return true;
        }


        public void Dispose()
        {
            if (brushMeshToBrush.IsCreated) brushMeshToBrush.Dispose(); brushMeshToBrush = default;
            if (compactNodes.IsCreated) compactNodes.Dispose(); compactNodes = default;

            if (brushOutlines.IsCreated)
            {
                try
                {
                    for (int i = 0; i < brushOutlines.Length; i++)
                    {
                        if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose();
                        brushOutlines[i] = default;
                    }
                }
                finally
                {
                    brushOutlines.Dispose();
                    brushOutlines = default;
                }
            }
            try
            {
                CompactHierarchyManager.FreeHierarchyID(HierarchyID);
            }
            finally
            {
                HierarchyID = CompactHierarchyID.Invalid;
                if (idManager.IsCreated) idManager.Dispose(); idManager = default;
            } 
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactNodeID CreateNode(NodeID nodeID, CompactNode nodeInformation)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            int index = Int32.MaxValue;
            return CreateNode(nodeID, ref index, in nodeInformation, CompactNodeID.Invalid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CompactNodeID CreateNode(NodeID nodeID, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            int index = Int32.MaxValue;
            return CreateNode(nodeID, ref index, in nodeInformation, parentID);
        }

        internal CompactNodeID CreateNode(NodeID nodeID, ref int index, in CompactNode nodeInformation, CompactNodeID parentID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            int id, generation;
            if (index == Int32.MaxValue)
            {
                index = idManager.CreateID(out id, out generation);
            } else
            {
                idManager.GetID(index, out id, out generation);
                Debug.Assert(index >= compactNodes.Length || compactNodes[index].compactNodeID == default);
            }
            var compactNodeID = new CompactNodeID(hierarchyID: HierarchyID, value: id, generation: generation);
            if (index >= compactNodes.Length)
            {
                compactNodes.Resize(index + 1, NativeArrayOptions.ClearMemory);
                brushOutlines.Resize(index + 1, NativeArrayOptions.ClearMemory);
            }
            brushOutlines[index] = default;
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

        struct HashStruct
        {
            public CompactNode  nodeInformation;
            public int          childCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void GetHash(in CompactChildNode node, NativeList<uint> hashes)
        {
            if (!IsValidCompactNodeID(node.compactNodeID))
                return;

            var nodeIndex = HierarchyIndexOfInternal(node.compactNodeID);
            { 
                var hashStruct = new HashStruct
                { 
                    nodeInformation = node.nodeInformation,
                    childCount      = node.childCount
                };
                hashes.Add(math.hash(UnsafeUtility.AddressOf(ref hashStruct), sizeof(HashStruct)));
            }

            var childCount = node.childCount;
            if (childCount == 0)
                return;
            var childOffset = node.childOffset;
            var compactNodesPtr = compactNodes.Ptr;
            if (childOffset < 0 || childOffset + childCount > compactNodes.Length)
                throw new ArgumentOutOfRangeException();
            for (int i = 0; i < childCount; i++)
                GetHash(in compactNodesPtr[childOffset + i], hashes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe uint GetHash(in CompactChildNode node)
        {
            if (!IsValidCompactNodeID(node.compactNodeID))
                return 0;

            using (var hashes = new NativeList<uint>(Allocator.Temp))
            {
                GetHash(in node, hashes);
                return math.hash(hashes.GetUnsafePtr(), sizeof(uint) * hashes.Length);
            }
        }

        public unsafe uint GetHash()
        {
            using (var hashes = new NativeList<uint>(Allocator.Temp))
            {
                var compactNodesPtr = compactNodes.Ptr;
                for (int i = 0; i < compactNodes.Length; i++)
                {
                    if (compactNodesPtr[i].compactNodeID == CompactNodeID.Invalid)
                        continue;
                    if (compactNodesPtr[i].parentID != CompactNodeID.Invalid)
                        continue;
                    GetHash(in compactNodesPtr[i], hashes);
                }
                return math.hash(hashes.GetUnsafePtr(), sizeof(uint) * hashes.Length);
            }
        }

        // Temporary hack
        public void ClearAllOutlines()
        {
            for (int i = 0; i < brushOutlines.Length; i++)
            {
                if (brushOutlines[i].IsCreated)
                    brushOutlines[i].Dispose();
                brushOutlines[i] = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ClearOutline(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var index = HierarchyIndexOfInternal(compactNodeID);
            if (index < 0 || index >= brushOutlines.length)
                throw new ArgumentException(nameof(compactNodeID));

            if (brushOutlines.Ptr[index].IsCreated)
                brushOutlines.Ptr[index].Dispose();
            brushOutlines.Ptr[index] = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref BrushOutline GetOutline(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var index = HierarchyIndexOfInternal(compactNodeID);
            if (index < 0 || index >= brushOutlines.length)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            if (!brushOutlines.Ptr[index].IsCreated)
                brushOutlines.Ptr[index] = BrushOutline.Create();
            return ref UnsafeUtility.ArrayElementAsRef<BrushOutline>(brushOutlines.Ptr, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void FillOutline(CompactNodeID compactNodeID, ref BrushMeshBlob brushMesh)
        {
            Debug.Assert(IsCreated);
            var index = HierarchyIndexOfInternal(compactNodeID);
            if (index < 0 || index >= brushOutlines.length)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            if (!brushOutlines.Ptr[index].IsCreated)
                 brushOutlines.Ptr[index] = BrushOutline.Create();
            brushOutlines.Ptr[index].Fill(ref brushMesh);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Int32 GetBrushMeshID(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var index = HierarchyIndexOfInternal(compactNodeID);
            if (index < 0 || index >= compactNodes.length)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            return compactNodes[index].nodeInformation.brushMeshID;
        }

        #region ID / Memory Management
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

        void FreeIndexRange(int index, int range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = index + range; i < lastNode; i++)
            {
                var compactNodeID   = compactNodes[i].compactNodeID;
                var brushMeshID     = compactNodes[i].nodeInformation.brushMeshID;
                
                RemoveMeshReference(compactNodeID, brushMeshID);

                if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                brushOutlines[i] = default;
                compactNodes[i] = default;
            }

            idManager.FreeIndexRange(index, range);
        }

        void RemoveIndexRange(int parentChildOffset, int parentChildCount, int index, int range)
        {
            if (index < 0 || index + range > compactNodes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = index, lastNode = index + range; i < lastNode; i++)
            {
                var compactNodeID   = compactNodes[i].compactNodeID;
                var brushMeshID     = compactNodes[i].nodeInformation.brushMeshID;

                RemoveMeshReference(compactNodeID, brushMeshID);

                if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                brushOutlines[i] = default; 
                compactNodes[i] = default;
            }

            idManager.RemoveIndexRange(parentChildOffset, parentChildCount, index, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int AllocateChildCount(int parentNodeIndex, int length)
        {
            if (length < 0)
                throw new ArgumentException(nameof(length));

            if (parentNodeIndex < 0 || parentNodeIndex >= compactNodes.Length)
                throw new ArgumentException($"{nameof(parentNodeIndex)} ({parentNodeIndex}) must be between 0 and {compactNodes.Length}", nameof(parentNodeIndex));

            var parentNode = compactNodes[parentNodeIndex];
            if (parentNode.childCount > 0)
                throw new ArgumentException($"{nameof(parentNodeIndex)} already has children", nameof(parentNodeIndex));

            parentNode.childOffset = idManager.AllocateIndexRange(length);
            parentNode.childCount = length;
            compactNodes[parentNodeIndex] = parentNode;
            return parentNode.childOffset;
        }
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int HierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return -1;
            if (compactNodeID.hierarchyID != HierarchyID)
                return -1;
            return idManager.GetIndex(compactNodeID.value, compactNodeID.generation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int HierarchyIndexOfInternalNoErrors(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return -1;
            if (compactNodeID.hierarchyID != HierarchyID)
                return -1;
            return idManager.GetIndexNoErrors(compactNodeID.value, compactNodeID.generation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int UnsafeHierarchyIndexOfInternal(CompactNodeID compactNodeID)
        {
            return idManager.GetIndex(compactNodeID.value, compactNodeID.generation);
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactChildNode SafeGetNodeRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex       = UnsafeHierarchyIndexOfInternal(compactNodeID);
            var compactNodesPtr = compactNodes.Ptr;
            return ref compactNodesPtr[nodeIndex];
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var nodeIndex       = UnsafeHierarchyIndexOfInternal(compactNodeID);
            var compactNodesPtr = compactNodes.Ptr;
            return ref compactNodesPtr[nodeIndex].nodeInformation;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe CompactNodeID GetChildIDAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            var compactNodesPtr = compactNodes.Ptr;
            return compactNodesPtr[nodeIndex].compactNodeID;
        }

        // WARNING: The returned reference will become invalid after modifying the hierarchy!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe ref CompactNode SafeGetChildRefAtInternal(CompactNodeID parentD, int index)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            var parentIndex         = UnsafeHierarchyIndexOfInternal(parentD);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            if (index < 0 || index >= parentChildCount) throw new ArgumentOutOfRangeException(nameof(index));

            var nodeIndex = parentChildOffset + index;
            if (nodeIndex < 0 || nodeIndex >= compactNodes.Length) throw new ArgumentOutOfRangeException(nameof(nodeIndex));

            var compactNodesPtr = compactNodes.Ptr;
            return ref compactNodesPtr[nodeIndex].nodeInformation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int SiblingIndexOfInternal(int parentIndex, int nodeIndex)
        {
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;

            var index = nodeIndex - parentChildOffset;
            Debug.Assert(index >= 0 && index < parentChildCount);

            return index;
        }

        unsafe bool DeleteRangeInternal(int parentIndex, int siblingIndex, int range, bool deleteChildren)
        {
            if (range == 0)
                return false;
            
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildOffset   = parentHierarchy.childOffset;
            var parentChildCount    = parentHierarchy.childCount;
            if (siblingIndex < 0 || siblingIndex + range > parentChildCount)
                throw new ArgumentOutOfRangeException(nameof(siblingIndex));

            if (deleteChildren)
            {
                // Delete all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy  = compactNodes[i];
                    var childCount      = childHierarchy.childCount;
                    DeleteRangeInternal(i, 0, childCount, deleteChildren: true);
                }
            } else
            {
                // Detach all children of the nodes we're going to delete
                for (int i = parentChildOffset + siblingIndex, lastIndex = (parentChildOffset + siblingIndex + range); i < lastIndex; i++)
                {
                    var childHierarchy  = compactNodes[i];
                    var childCount      = childHierarchy.childCount;
                    DetachRangeInternal(i, 0, childCount);
                }
            }
            

            var nodeIndex = siblingIndex + parentChildOffset;


            // Check if we're deleting from the front of the list of children
            if (siblingIndex == 0)
            {
                // Clear the ids of the children we're deleting
                FreeIndexRange(nodeIndex, range);

                for (int i = nodeIndex; i < nodeIndex + range; i++)
                {
                    //if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                    brushOutlines[i] = default; 
                    compactNodes[i]  = default;
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
                    Debug.Assert(parentChildCount > range);
                    parentHierarchy.childCount -= range;
                    parentHierarchy.childOffset += range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're deleting from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // Clear the ids of the children we're deleting
                FreeIndexRange(nodeIndex, range);

                for (int i = nodeIndex; i < nodeIndex + range; i++)
                {
                    //if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                    brushOutlines[i] = default; 
                    compactNodes[i]  = default;
                }

                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }

            // If we get here, it means we're deleting children in the center of the list of children

            // Clear the ids of the children we're deleting
            RemoveIndexRange(parentChildOffset, parentChildCount, nodeIndex, range);

            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            compactNodes.MemMove(nodeIndex, nodeIndex + range, count);
            brushOutlines.MemMove(nodeIndex, nodeIndex + range, count);

            for (int i = nodeIndex + count; i < parentChildOffset + parentChildCount; i++)
            {
                //if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                brushOutlines[i] = default; 
                compactNodes[i] = default;
            }

            parentHierarchy.childCount -= range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }

        // "optimize" - remove holes, reorder them in hierarchy order
        unsafe bool CompactInternal()
        {
            // TODO: implement
            //       could just create a new hierarchy and insert everything in it in order, and replace the hierarchy with this one
            throw new NotImplementedException();
        }

        unsafe bool DetachAllChildrenInternal(int parentIndex)
        {
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildCount    = parentHierarchy.childCount;
            return DetachRangeInternal(parentIndex, 0, parentChildCount);
        }

        unsafe bool DeleteAllChildrenInternal(int parentIndex)
        {
            Debug.Assert(parentIndex >= 0 && parentIndex < compactNodes.Length);
            var parentHierarchy     = compactNodes[parentIndex];
            var parentChildCount    = parentHierarchy.childCount;
            return DeleteRangeInternal(parentIndex, 0, parentChildCount, true);
        }

        unsafe bool DetachRangeInternal(int parentIndex, int siblingIndex, int range)
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
                var compactNodesPtr = compactNodes.Ptr;
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
                    parentHierarchy.childCount -= range;
                    parentHierarchy.childOffset += range;
                    compactNodes[parentIndex] = parentHierarchy;
                }
                return true;
            } else
            // Check if we're detaching from the back of the list of children
            if (siblingIndex == parentChildCount - range)
            {
                // In that case, we can just decrease the number of children in the list
                parentHierarchy.childCount -= range;
                compactNodes[parentIndex] = parentHierarchy;
                return true;
            }
            
            // If we get here, it means we're detaching children in the center of the list of children

            var prevLength = compactNodes.Length;
            // Resize compactNodes to have space for the nodes we're detaching (compactNodes will probably have capacity for this already)
            compactNodes.Resize(prevLength + range, NativeArrayOptions.UninitializedMemory);
            brushOutlines.Resize(prevLength + range, NativeArrayOptions.ClearMemory);

            // Copy the original nodes to behind all our other nodes
            compactNodes.MemMove(prevLength, nodeIndex, range);
            brushOutlines.MemMove(prevLength, nodeIndex, range);

            // Move nodes behind our node on top of the node we're removing
            var count = parentChildCount - (siblingIndex + range);
            compactNodes.MemMove(nodeIndex, nodeIndex + range, count);
            brushOutlines.MemMove(nodeIndex, nodeIndex + range, count);

            // Copy original nodes to behind the new parent child list
            compactNodes.MemMove(lastNodeIndex, prevLength, range);
            brushOutlines.MemMove(lastNodeIndex, prevLength, range);

            // Set the compactNodes length to its original size
            compactNodes.Resize(prevLength, NativeArrayOptions.UninitializedMemory);
            brushOutlines.Resize(prevLength + range, NativeArrayOptions.ClearMemory);

            idManager.SwapIndexRangeToBack(parentChildOffset, parentChildCount, siblingIndex, range);

            parentHierarchy.childCount -= range;
            compactNodes[parentIndex] = parentHierarchy;
            return true;
        }


        unsafe bool AttachInternal(CompactNodeID parentID, int parentIndex, int insertIndex, CompactNodeID compactNodeID)
        {
            Debug.Assert(parentID != CompactNodeID.Invalid);

            var parentHierarchy   = compactNodes[parentIndex];
            var parentChildCount  = parentHierarchy.childCount;
            if (insertIndex < 0 || insertIndex > parentChildCount)
            {
                Debug.LogError($"Index ({insertIndex}) must be between 0 .. {parentChildCount}");
                return false;
            }

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException($"{nameof(CompactNodeID)} is invalid", nameof(compactNodeID));

            // Make a temporary copy of our node in case we need to move it
            var nodeItem            = compactNodes[nodeIndex];
            var brushOutlineItem    = brushOutlines[nodeIndex];
            var oldParentID         = nodeItem.parentID;
            var parentChildOffset   = parentHierarchy.childOffset;
            var desiredIndex        = parentChildOffset + insertIndex;

            // If the node is already a child of a parent, then we need to remove it from that parent
            if (oldParentID != CompactNodeID.Invalid)
            {
                // inline & optimize this
                Detach(compactNodeID);
                if (desiredIndex > nodeIndex)
                {
                    desiredIndex--;
                    insertIndex--;
                }
                Debug.Assert(CheckConsistency());
            }

            // If our new parent doesn't have any child nodes yet, we don't need to move our node and just set 
            // our node as the location for our children
            if (parentChildCount == 0)
            {
                parentHierarchy.childOffset = nodeIndex;
                parentHierarchy.childCount = 1;
                compactNodes[parentIndex] = parentHierarchy;
                
                nodeItem.parentID = parentID;
                brushOutlines[nodeIndex] = brushOutlineItem;
                compactNodes[nodeIndex] = nodeItem;
                return true;
            }
            

            // Check if our node is already a child of the right parent and at the correct position
            if (oldParentID == parentID && desiredIndex == nodeIndex)
                return true;

            // If the desired index of our node is already at the index we want it to be, things are simple
            if (desiredIndex == nodeIndex)
            {
                parentHierarchy.childCount ++;
                compactNodes[parentIndex] = parentHierarchy;

                nodeItem.parentID = parentID;
                brushOutlines[nodeIndex] = brushOutlineItem;
                compactNodes[nodeIndex] = nodeItem;
                return true;
            }

            // If, however, the desired index of our node is NOT at the index we want it to be, 
            // we need to move nodes around


            // If it's a different parent then we need to change the size of our child list
            {
                var originalOffset = parentChildOffset;
                var originalCount  = parentChildCount;

                Debug.Assert(!idManager.IsIndexFree(nodeIndex));

                // Find (or create) a span of enough elements that we can use to copy our children into
                parentChildCount++;
                parentChildOffset = idManager.InsertIntoIndexRange(originalOffset, originalCount, insertIndex, nodeIndex);
                Debug.Assert(parentChildOffset >= 0 && parentChildOffset < compactNodes.Length + parentChildCount);

                if (compactNodes.Length < parentChildOffset + parentChildCount)
                {
                    compactNodes.Resize(parentChildOffset + parentChildCount, NativeArrayOptions.ClearMemory);
                    brushOutlines.Resize(parentChildOffset + parentChildCount, NativeArrayOptions.ClearMemory);
                }

                // We first move the last nodes to the correct new offset ..
                var items = originalCount - insertIndex;
                compactNodes.MemMove(parentChildOffset + insertIndex + 1, originalOffset + insertIndex, items);
                brushOutlines.MemMove(parentChildOffset + insertIndex + 1, originalOffset + insertIndex, items);

                // If our offset is different then the front section will not be in the right location, so we might need to copy this
                // We'd also need to reset the old nodes to invalid, if we don't we'd create new dangling nodes
                if (originalOffset != parentChildOffset)
                {
                    // Then we move the first part (if necesary)
                    items = insertIndex;
                    compactNodes.MemMove(parentChildOffset, originalOffset, items);
                    brushOutlines.MemMove(parentChildOffset, originalOffset, items);
                }

                // Then we copy our node to the new location
                var newNodeIndex = parentChildOffset + insertIndex;
                nodeItem.parentID = parentID;
                brushOutlines[newNodeIndex] = brushOutlineItem;
                compactNodes[newNodeIndex] = nodeItem;

                // Then we set the old indices to 0
                var newCount = originalCount + 1;
                if (nodeIndex < parentChildOffset || nodeIndex >= parentChildOffset + newCount)
                {
                    //if (brushOutlines[nodeIndex].IsCreated) brushOutlines[nodeIndex].Dispose(); 
                    brushOutlines[nodeIndex] = default; 
                    compactNodes[nodeIndex] = default;
                }
                for (int i = originalOffset, lastIndex = (originalOffset + originalCount); i < lastIndex; i++)
                {
                    if (i >= parentChildOffset && i < parentChildOffset + newCount)
                        continue;

                    //if (brushOutlines[i].IsCreated) brushOutlines[i].Dispose(); 
                    brushOutlines[i] = default;
                    compactNodes[i] = default;
                }

                // And fixup the id to index lookup
                for (int i = parentChildOffset, lastIndex = (parentChildOffset + newCount); i < lastIndex; i++)
                {
                    if (compactNodes[i].nodeID != default)
                        CompactHierarchyManager.MoveNodeID(compactNodes[i].nodeID, compactNodes[i].compactNodeID);
                }

                parentHierarchy.childOffset = parentChildOffset; // We make sure we set the parent child offset correctly
                parentHierarchy.childCount++;                    // And we increase the childCount of our parent
                compactNodes[parentIndex] = parentHierarchy;
            }
            return true;
        }

        public unsafe void GetTreeNodes(NativeList<CompactNodeID> nodes, NativeList<CompactNodeID> brushes)
        {
            if (nodes.IsCreated) nodes.Clear();
            if (brushes.IsCreated) brushes.Clear();
            if (!nodes.IsCreated && !brushes.IsCreated)
                return;

            var rootIndex = UnsafeHierarchyIndexOfInternal(RootID);
            if (rootIndex < 0 || rootIndex >= compactNodes.Length)
                return;
            
            if (nodes.IsCreated) nodes.Add(RootID);
            var compactNodesPtr = compactNodes.Ptr;
            var nodeStack = new NativeList<int>(math.max(1, compactNodes.Length), Allocator.Temp);
            try
            {
                nodeStack.Add(rootIndex);
                while (nodeStack.Length > 0)
                {
                    var lastNodeStackIndex = nodeStack.Length - 1;
                    var nodeIndex = nodeStack[lastNodeStackIndex];
                    nodeStack.RemoveAt(lastNodeStackIndex);
                    ref var node = ref compactNodesPtr[nodeIndex];

                    if (!IsValidCompactNodeID(node.compactNodeID))
                        continue;

                    if (nodes.IsCreated &&
                        node.compactNodeID != RootID)
                    {
                        nodes.Add(node.compactNodeID);
                    }
                    if (node.childCount > 0)
                    {
                        for (int i = 0, childIndex = node.childOffset + node.childCount - 1, childCount = node.childCount; i < childCount; i++, childIndex--)
                            nodeStack.Add(childIndex);
                    } else
                    if (node.nodeInformation.brushMeshID != Int32.MaxValue && brushes.IsCreated)
                    {
                        brushes.Add(node.compactNodeID);
                    }
                }
            }
            finally
            {
                nodeStack.Dispose();
            }
        }

        internal unsafe void GetAllNodes(NativeList<CSGTreeNode> nodes)
        {
            if (!nodes.IsCreated)
                return;
            
            nodes.Clear();

            var compactNodesPtr = compactNodes.Ptr;
            for (int i = 0, count = this.compactNodes.Length; i < count; i++)
            {
                ref var node = ref compactNodesPtr[i];
                if (node.nodeID == NodeID.Invalid)
                    continue;

                var treeNode = CSGTreeNode.Find(node.nodeID);
                if (!treeNode.Valid)
                    continue;
                nodes.Add(treeNode);
            }
        }

        /*
        // TODO: when we change brushMeshIDs to be hashes of meshes, 
        //       we need to pass along both the original and the new hash and switch them
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
                    catch (Exception ex) { Debug.LogException(ex); }
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
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
        */

        
        // Temporary workaround until we can switch to hashes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsAnyStatusFlagSet(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return node.flags != NodeStatusFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsStatusFlagSet(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            return (node.flags & flag) == flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags |= flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearAllStatusFlags(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags = NodeStatusFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearStatusFlag(CompactNodeID compactNodeID, NodeStatusFlags flag)
        {
            Debug.Assert(IsCreated, "Hierarchy has not been initialized");
            if (compactNodeID == CompactNodeID.Invalid)
                return;

            ref var node = ref GetChildRef(compactNodeID);
            node.flags &= ~flag;
        }
        
        // This method might be removed/renamed in the future
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNodeDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (RootID != compactNodeID)
                nodeType = (node.brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:  return (node.flags & (NodeStatusFlags.NeedCSGUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Branch: return (node.flags & (NodeStatusFlags.BranchNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Tree:   return (node.flags & (NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate)) != NodeStatusFlags.None;
            }
            return false;
        }

        // This method might be removed/renamed in the future
        internal unsafe bool SetChildrenDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            if (node.brushMeshID != Int32.MaxValue)
                return false;

            var result = true;
            var count = ChildCount(compactNodeID);
            for (int i = 0; i < count; i++)
            {
                var childID = GetChildIDAtInternal(compactNodeID, i);
                result = SetDirty(childID) && result;
            }
            return result;
        }

        // This method might be removed/renamed in the future
        internal bool SetDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var node = ref GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (RootID != compactNodeID)
                nodeType = (node.brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:
                {
                    node.flags |= NodeStatusFlags.NeedFullUpdate;
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true; 
                }
                case CSGNodeType.Branch:
                {
                    node.flags |= NodeStatusFlags.BranchNeedsUpdate;
                    ref var rootNode = ref GetChildRef(RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true; 
                }
                case CSGNodeType.Tree:
                {
                    node.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true;
                }
                default:
                {
                    Debug.LogError("Unknown node type");
                    return false;
                }
            }
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ClearDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            GetChildRef(compactNodeID).flags = NodeStatusFlags.None;
            return true;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CSGNodeType GetTypeOfNode(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGNodeType.None;

            if (RootID == compactNodeID)
                return CSGNodeType.Tree;

            return (GetChildRef(compactNodeID).brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetUserIDOfNode(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return 0;

            return GetChildRef(compactNodeID).userID;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal CSGOperationType GetOperation(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGOperationType.Invalid;                        
            return GetChildRef(compactNodeID).operation;
        }


        internal bool IsDescendant(CompactNodeID parentCompactNodeID, CompactNodeID childCompactNodeID)
        {
            if (parentCompactNodeID.hierarchyID != HierarchyID)
                throw new ArgumentException($"{nameof(parentCompactNodeID)} is not part of this hierarchy", nameof(parentCompactNodeID));

            if (childCompactNodeID.hierarchyID != HierarchyID)
                throw new ArgumentException($"{nameof(childCompactNodeID)} is not part of this hierarchy", nameof(childCompactNodeID));

            var iterator = childCompactNodeID;
            while (iterator != parentCompactNodeID)
            {
                if (iterator == CompactNodeID.Invalid)
                    return false;
                iterator = ParentOf(iterator);
            }
            return true;
        }


        internal MinMaxAABB GetBrushBounds(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            if (nodeRef.brushMeshID == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            return nodeRef.bounds;
        }

        internal MinMaxAABB GetBrushBounds(CompactNodeID compactNodeID, float4x4 transformation)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            ref var nodeRef = ref GetChildRef(compactNodeID);
            if (nodeRef.brushMeshID == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            return BrushMeshManager.CalculateBounds(nodeRef.brushMeshID, in transformation);
        }

        public CompactNodeID GetRootOfNode(CompactNodeID compactNodeID)
        {
            var rootCompactNodeID = RootID;
            if (compactNodeID == rootCompactNodeID)
                return CompactNodeID.Invalid;
            var iterator = compactNodeID;
            while (iterator != CompactNodeID.Invalid)
            {
                if (iterator == rootCompactNodeID)
                    return rootCompactNodeID;
                iterator = ParentOf(iterator);
            }
            return CompactNodeID.Invalid;
        }
    }
}
