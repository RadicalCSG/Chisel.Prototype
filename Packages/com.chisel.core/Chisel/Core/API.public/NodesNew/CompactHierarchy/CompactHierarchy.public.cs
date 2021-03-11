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
    // Temporary workaround until we can switch to hashes
    public enum NodeStatusFlags : UInt16
    {
        None                        = 0,
        //NeedChildUpdate		    = 1,
        NeedPreviousSiblingsUpdate  = 2,

        BranchNeedsUpdate           = 4,
            
        TreeIsDisabled              = 1024,// TODO: remove, or make more useful
        TreeNeedsUpdate             = 8,
        TreeMeshNeedsUpdate         = 16,

            
        ShapeModified               = 32,
        TransformationModified      = 64,
        HierarchyModified           = 128,
        OutlineModified             = 256,
        NeedAllTouchingUpdated      = 512,	// all brushes that touch this brush need to be updated,
        NeedFullUpdate              = ShapeModified | TransformationModified | OutlineModified | HierarchyModified,
        NeedCSGUpdate               = ShapeModified | TransformationModified | HierarchyModified,
        NeedUpdateDirectOnly        = TransformationModified | OutlineModified,
    };


    [BurstCompatible]
    public readonly struct CompactNodeID : IComparable<CompactNodeID>, IEquatable<CompactNodeID>
    {
        public static readonly CompactNodeID Invalid = default;

        public readonly Int32 value;
        public readonly Int32 generation;

        public readonly CompactHierarchyID hierarchyID;

        internal CompactNodeID(CompactHierarchyID hierarchyID, Int32 value, Int32 generation = 0) { this.hierarchyID = hierarchyID; this.value = value; this.generation = generation; }

        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override string ToString() { return $"NodeID = {value}, Generation = {generation}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactNodeID left, CompactNodeID right) { return left.value == right.value && left.generation == right.generation && left.hierarchyID == right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactNodeID left, CompactNodeID right) { return left.value != right.value || left.generation != right.generation || left.hierarchyID != right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is CompactNodeID) return this == ((CompactNodeID)obj);
            return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return value; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int CompareTo(CompactNodeID other)
        {
            var diff = hierarchyID.CompareTo(other.hierarchyID);
            if (diff != 0)
                return diff;

            diff = value - other.value;
            if (diff != 0)
                return diff;

            return generation - other.generation;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CompactNodeID other)
        {
            return value == other.value && generation == other.generation && hierarchyID == other.hierarchyID;
        }
        #endregion
    }

    [Serializable]
    [BurstCompatible]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("BrushMeshID = {brushMeshID}, Operation = {operation}, UserID = {userID}, Transformation = {transformation}")]
    public struct CompactNode
    {
        public Int32                userID;

        public CSGOperationType     operation;
        public float4x4             transformation;
        public NodeStatusFlags      flags;          // TODO: replace with using hashes to compare changes
        public MinMaxAABB           bounds;         // TODO: move this somewhere else, depends on brushMeshID
        
        public Int32                brushMeshID;    // TODO: use hash of mesh as "ID"
    }

    [BurstCompatible]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("NodeID = {nodeID.ID}, Parent = {parentID.ID}, UserID = {nodeInformation.userID}, ChildCount = {childCount}, ChildOffset = {childOffset}, BrushMeshID = {nodeInformation.brushMeshID}, Operation = {nodeInformation.operation}, Transformation = {nodeInformation.transformation}")]
    public struct CompactChildNode // TODO: rename
    {
        // TODO: probably need to split this up into multiple pieces, figure out how this will actually be used in practice first

        public CompactNode      nodeInformation;
        public NodeID           nodeID;         // TODO: figure out how to get rid of this
        public CompactNodeID    compactNodeID;     
        public CompactNodeID    parentID;       // TODO: figure out how to get rid of these IDs and use index instead
        public Int32            childCount;
        public Int32            childOffset;

        public static readonly CompactChildNode Invalid = default;
    }

    [BurstCompatible]
    [DebuggerDisplay("NodeCount = {Count}")]
    public partial struct CompactHierarchy : IDisposable
    {
        #region CreateHierarchy
        public static CompactHierarchy CreateHierarchy(NodeID nodeID, Int32 userID, Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, nodeID, userID, allocator);
        }

        public static CompactHierarchy CreateHierarchy(NodeID nodeID, Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, nodeID, 0, allocator);
        }

        internal static CompactHierarchy CreateHierarchy(CompactHierarchyID hierarchyID, NodeID nodeID, Int32 userID, Allocator allocator)
        {
            var compactHierarchy = new CompactHierarchy
            {
                brushMeshToBrush = new NativeMultiHashMap<int, CompactNodeID>(16384, allocator),
                compactNodes     = new NativeList<CompactChildNode>(allocator),
                sectionManager   = SectionManager.Create(allocator),
                idToIndex        = new NativeList<IndexLookup>(allocator),
                freeIDs          = new NativeList<int>(allocator),
                hierarchyID      = hierarchyID
            };
            compactHierarchy.RootID = compactHierarchy.CreateNode(nodeID, new CompactNode
            {
                userID          = userID,
                operation       = CSGOperationType.Additive,
                transformation  = float4x4.identity,
                brushMeshID     = Int32.MaxValue
            });
            return compactHierarchy;
        } 
        #endregion

        #region CreateBranch
        public CompactNodeID CreateBranch(NodeID nodeID, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0) { return CreateBranch(nodeID, float4x4.identity, operation, userID); }

        public CompactNodeID CreateBranch(NodeID nodeID, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0)
        {
            return CreateNode(nodeID, new CompactNode
            {
                userID          = userID,
                operation       = operation,
                transformation  = transformation,
                brushMeshID     = Int32.MaxValue
            });
        }
        #endregion

        #region CreateBrush
        public CompactNodeID CreateBrush(NodeID nodeID, Int32 brushMeshID, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0) { return CreateBrush(nodeID, brushMeshID, float4x4.identity, operation, userID); }
        
        public CompactNodeID CreateBrush(NodeID nodeID, Int32 brushMeshID, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0)
        {
            return CreateNode(nodeID, new CompactNode
            {
                userID          = userID,
                operation       = operation,
                transformation  = transformation,
                brushMeshID     = brushMeshID, 
                bounds          = CalculateBounds(brushMeshID, in transformation)
            });
        }
        #endregion

        public int Count
        {
            get
            {
                return compactNodes.Length - freeIDs.Length;
            }
        }

        public bool IsCreated
        {
            get
            {
                return idToIndex.IsCreated && freeIDs.IsCreated && compactNodes.IsCreated && sectionManager.IsCreated;
            }
        }

        public bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            var valueIndex = compactNodeID.value - 1;
            if (valueIndex < 0 || valueIndex >= idToIndex.Length)
                return false;

            var nodeIndex = idToIndex[valueIndex].index;
            if (nodeIndex == -1)
                return false;

            var generation = idToIndex[valueIndex].generation;
            if (generation != compactNodeID.generation)
                return false;

            return sectionManager.IsAllocatedIndex(nodeIndex);
        }

        public int SiblingIndexOf(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

            var parentID = compactNodes[nodeIndex].parentID;
            if (parentID == CompactNodeID.Invalid)
                return -1;

            var parentIndex = HierarchyIndexOfInternal(parentID);
            Debug.Assert(parentIndex != -1);
            return SiblingIndexOfInternal(parentIndex, nodeIndex);
        }
        
        public NodeID GetNodeID(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                return NodeID.Invalid;

            return compactNodes[nodeIndex].nodeID;
        }

        public CompactNodeID ParentOf(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is an invalid node");

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                return CompactNodeID.Invalid;

            return compactNodes[nodeIndex].parentID;
        }

        public int ChildCount(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            return compactNodes[nodeIndex].childCount;
        }

        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref CompactChildNode GetNodeRef(CompactNodeID compactNodeID) { return ref SafeGetNodeRefAtInternal(compactNodeID); }


        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactNode GetChildRef(CompactNodeID compactNodeID) { return ref SafeGetChildRefAtInternal(compactNodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChild(CompactNodeID compactNodeID) { return SafeGetChildRefAtInternal(compactNodeID); }

        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactNode GetChildRefAt(CompactNodeID compactNodeID, int index) { return ref SafeGetChildRefAtInternal(compactNodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChildAt(CompactNodeID compactNodeID, int index) { return SafeGetChildRefAtInternal(compactNodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID GetChildIDAt(CompactNodeID compactNodeID, int index) { return GetChildIDAtInternal(compactNodeID, index); }

        public bool Compact()
        {
            Debug.Assert(IsCreated);
            return CompactInternal();
        }

        public bool Delete(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
            {
                // node doesn't have a parent, so it cannot be removed from its parent
                var childCount = ChildCount(compactNodeID);
                if (childCount > 0) DetachRangeInternal(nodeIndex, 0, (uint)childCount);
                RemoveIDs(nodeIndex, 1);
                FreeIndexRange(nodeIndex, 1);
                return true;
            }

            var index = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DeleteRangeInternal(parentIndex, index, range: 1, false);
        }

        public bool DeleteRecursive(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
            {
                // node doesn't have a parent, so it cannot be removed from its parent
                var childCount = ChildCount(compactNodeID);
                if (childCount > 0) DeleteRangeInternal(nodeIndex, 0, (uint)childCount, true);
                RemoveIDs(nodeIndex, 1);
                FreeIndexRange(nodeIndex, 1);
                return true; 
            }

            var index = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DeleteRangeInternal(parentIndex, index, range: 1, true);
        }

        public bool DeleteChildFromParentAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range: 1, false);
        }

        public bool DeleteChildFromParentRecursiveAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range: 1, true);
        }

        public bool DeleteChildrenFromParentAt(CompactNodeID parentID, int index, uint range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range, false);
        }

        public bool DeleteChildrenFromParentRecursiveAt(CompactNodeID parentID, int index, uint range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DeleteRangeInternal(parentIndex, index, range, true);
        }

        public bool Detach(CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (compactNodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(compactNodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(compactNodeID), $"{nameof(compactNodeID)} is invalid");

            var parentID    = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                return false; // node doesn't have a parent, so it cannot be removed from its parent

            var index       = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DetachRangeInternal(parentIndex, index, range: 1);
        }

        public bool DetachChildFromParentAt(CompactNodeID parentID, int index)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachRangeInternal(parentIndex, index, range: 1);
        }

        public bool DetachChildrenFromParentAt(CompactNodeID parentID, int index, uint range)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachRangeInternal(parentIndex, index, range);
        }

        public bool DetachAllChildrenFromParent(CompactNodeID parentID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex < 0)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            return DetachAllInternal(parentIndex);
        }

        public void AttachToParent(CompactNodeID parentID, CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var parentHierarchy = compactNodes[parentIndex];
            var parentChildCount = parentHierarchy.childCount;
            AttachInternal(parentID, parentIndex, parentChildCount, compactNodeID);
        }

        public void AttachToParentAt(CompactNodeID parentID, int index, CompactNodeID compactNodeID)
        {
            Debug.Assert(IsCreated);
            if (index < 0)
                throw new IndexOutOfRangeException();
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            if (!IsValidCompactNodeID(compactNodeID))
                return;

            AttachInternal(parentID, parentIndex, index, compactNodeID);
        }
    }
}
