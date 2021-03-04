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
        public static readonly CompactNodeID Invalid = new CompactNodeID(hierarchyID: CompactHierarchyID.Invalid, id: -1);

        public readonly Int32 ID;
        public readonly Int32 generation;

        public readonly CompactHierarchyID hierarchyID;

        internal CompactNodeID(CompactHierarchyID hierarchyID, Int32 id, Int32 generation = 0) { this.hierarchyID = hierarchyID; this.ID = id; this.generation = generation; }

        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override string ToString() { return $"NodeID = {ID}, Generation = {generation}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactNodeID left, CompactNodeID right) { return left.ID == right.ID && left.generation == right.generation && left.hierarchyID == right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactNodeID left, CompactNodeID right) { return left.ID != right.ID || left.generation != right.generation || left.hierarchyID != right.hierarchyID; }
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override bool Equals(object obj)
        {
            if (obj is CompactNodeID) return this == ((CompactNodeID)obj);
            return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override int GetHashCode() { return ID.GetHashCode(); }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int CompareTo(CompactNodeID other)
        {
            var diff = hierarchyID.CompareTo(other.hierarchyID);
            if (diff != 0)
                return diff;

            diff = ID - other.ID;
            if (diff != 0)
                return diff;

            return generation - other.generation;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CompactNodeID other)
        {
            return ID == other.ID && generation == other.generation && hierarchyID == other.hierarchyID;
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
        public CompactNodeID    nodeID;     
        public CompactNodeID    parentID;       // TODO: figure out how to get rid of these IDs and use index instead
        public Int32            childCount;
        public Int32            childOffset;

        public static readonly CompactChildNode Invalid = new CompactChildNode
                {
                    nodeID   = CompactNodeID.Invalid,
                    parentID = CompactNodeID.Invalid,
                };
    }

    [BurstCompatible]
    [DebuggerDisplay("NodeCount = {Count}")]
    public partial struct CompactHierarchy : IDisposable
    {
        #region CreateHierarchy
        public static CompactHierarchy CreateHierarchy(Int32 userID, Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, userID, allocator);
        }

        public static CompactHierarchy CreateHierarchy(Allocator allocator)
        {
            return CreateHierarchy(CompactHierarchyID.Invalid, 0, allocator);
        }

        internal static CompactHierarchy CreateHierarchy(CompactHierarchyID hierarchyID, Int32 userID, Allocator allocator)
        {
            var rootID = new CompactNodeID(hierarchyID: hierarchyID, id: 0);
            var compactHierarchy = new CompactHierarchy
            {
                brushMeshToBrush        = new NativeMultiHashMap<int, CompactNodeID>(16384, allocator),
                compactNodes            = new NativeList<CompactChildNode>(allocator),
                idToIndex               = new NativeList<Generation>(allocator),
                freeIDs                 = new NativeList<int>(allocator),
                hierarchyID             = hierarchyID,
                RootID                  = rootID,
            };
            compactHierarchy.compactNodes.Add(new CompactChildNode
            {
                nodeInformation = new CompactNode
                {
                    userID          = userID,

                    operation       = CSGOperationType.Additive,
                    transformation  = float4x4.identity,

                    brushMeshID     = Int32.MaxValue
                },
                nodeID          = rootID,
                parentID        = CompactNodeID.Invalid,
                childOffset     = 0,
                childCount      = 0
            });
            compactHierarchy.idToIndex.Add(new Generation { index = 0, generation = 0 });
            return compactHierarchy;
        }
        #endregion

        #region CreateBranch
        public CompactNodeID CreateBranch(CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0) { return CreateBranch(float4x4.identity, operation, userID); }

        public CompactNodeID CreateBranch(float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0)
        {
            Debug.Assert(IsCreated);
            var nodeID = CreateID(compactNodes.Length);
            compactNodes.Add(new CompactChildNode
            {
                nodeInformation = new CompactNode
                {
                    userID          = userID,

                    operation       = operation,
                    transformation  = transformation,
                
                    brushMeshID     = Int32.MaxValue
                },
                nodeID          = nodeID,
                parentID        = CompactNodeID.Invalid,
                childOffset     = 0,
                childCount      = 0
            });
            return nodeID;
        }
        #endregion

        #region CreateBrush
        public CompactNodeID CreateBrush(Int32 brushMeshID, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0) { return CreateBrush(brushMeshID, float4x4.identity, operation, userID); }
        
        public CompactNodeID CreateBrush(Int32 brushMeshID, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, Int32 userID = 0)
        {
            Debug.Assert(IsCreated);
            var nodeID = CreateID(compactNodes.Length);
            compactNodes.Add(new CompactChildNode
            {
                nodeInformation = new CompactNode
                {
                    userID          = userID,

                    operation       = operation,
                    transformation  = transformation,

                    brushMeshID     = brushMeshID
                },
                nodeID          = nodeID,
                parentID        = CompactNodeID.Invalid,
                childOffset     = 0,
                childCount      = 0
            });
            brushMeshToBrush.Add(brushMeshID, nodeID);
            return nodeID;
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
                return idToIndex.IsCreated && freeIDs.IsCreated && compactNodes.IsCreated;
            }
        }

        public bool IsValidNodeID(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (nodeID == CompactNodeID.Invalid)
                return false;

            var lookupIDIndex = nodeID.ID;
            if (lookupIDIndex < 0 || lookupIDIndex >= idToIndex.Length)
                return false;

            var generation = idToIndex[lookupIDIndex].generation;
            Debug.Assert(generation == nodeID.generation);
            if (generation != nodeID.generation)
                return false;

            var nodeIndex = idToIndex[lookupIDIndex].index;
            return nodeIndex >= 0 && nodeIndex < compactNodes.Length;
        }

        public int SiblingIndexOf(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is an invalid node");

            var parentID = compactNodes[nodeIndex].parentID;
            if (parentID == CompactNodeID.Invalid)
                return -1;

            var parentIndex = HierarchyIndexOfInternal(parentID);
            Debug.Assert(parentIndex != -1);
            return SiblingIndexOfInternal(parentIndex, nodeIndex);
        }
        
        public CompactNodeID ParentOf(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (nodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is an invalid node");

            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                return CompactNodeID.Invalid;

            return compactNodes[nodeIndex].parentID;
        }

        public int ChildCount(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is invalid");

            return compactNodes[nodeIndex].childCount;
        }


        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactNode GetChildRef(CompactNodeID nodeID) { return ref SafeGetChildRefAtInternal(nodeID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChild(CompactNodeID nodeID) { return SafeGetChildRefAtInternal(nodeID); }

        /// <summary>
        /// WARNING: The returned reference will become invalid after modifying the hierarchy!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref CompactNode GetChildRefAt(CompactNodeID nodeID, int index) { return ref SafeGetChildRefAtInternal(nodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNode GetChildAt(CompactNodeID nodeID, int index) { return SafeGetChildRefAtInternal(nodeID, index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactNodeID GetChildIDAt(CompactNodeID nodeID, int index) { return GetChildIDAtInternal(nodeID, index); }

        public bool Compact()
        {
            Debug.Assert(IsCreated);
            return CompactInternal();
        }

        public bool Delete(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (nodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is invalid");

            var parentID = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                return false; // node doesn't have a parent, so it cannot be removed from its parent

            var index = SiblingIndexOfInternal(parentIndex, nodeIndex);
            return DeleteRangeInternal(parentIndex, index, range: 1, false);
        }

        public bool DeleteRecursive(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (nodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is invalid");

            var parentID = compactNodes[nodeIndex].parentID;
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                return false; // node doesn't have a parent, so it cannot be removed from its parent

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

        public bool Detach(CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (nodeID == RootID) // Cannot remove root
                return false;

            var nodeIndex = HierarchyIndexOfInternal(nodeID);
            if (nodeIndex == -1)
                throw new ArgumentException(nameof(nodeID), $"{nameof(nodeID)} is invalid");

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

        public void AttachToParent(CompactNodeID parentID, CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            var parentHierarchy = compactNodes[parentIndex];
            var parentChildCount = parentHierarchy.childCount;
            AttachInternal(parentID, parentIndex, parentChildCount, nodeID);
        }

        public void AttachToParentAt(CompactNodeID parentID, int index, CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (index < 0)
                throw new IndexOutOfRangeException();
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            AttachInternal(parentID, parentIndex, index, nodeID);
        }

        public void AttachToParent(CompactNodeID parentID, CompactHierarchy sourceHierarchy, CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");

            var parentHierarchy = compactNodes[parentIndex];
            var parentChildCount = parentHierarchy.childCount;
            AttachInternal(parentID, parentIndex, parentChildCount, sourceHierarchy, nodeID);
        }

        public void AttachToParentAt(CompactNodeID parentID, int index, CompactHierarchy sourceHierarchy, CompactNodeID nodeID)
        {
            Debug.Assert(IsCreated);
            if (index < 0)
                throw new IndexOutOfRangeException();
            var parentIndex = HierarchyIndexOfInternal(parentID);
            if (parentIndex == -1)
                throw new ArgumentException(nameof(parentID), $"{nameof(parentID)} is invalid");
            AttachInternal(parentID, parentIndex, index, sourceHierarchy, nodeID);
        }
    }
}
