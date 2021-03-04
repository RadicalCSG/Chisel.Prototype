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

namespace Chisel.Core.New
{
    public struct CompactHierarchyID : IComparable<CompactHierarchyID>, IEquatable<CompactHierarchyID>
    {
        public static readonly CompactHierarchyID Invalid = new CompactHierarchyID(id: -1);

        public readonly Int32 ID;
        public readonly Int32 generation;
        internal CompactHierarchyID(Int32 id, Int32 generation = 0) { this.ID = id; this.generation = generation; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string ToString() { return $"HierarchyID = {ID}, Generation = {generation}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactHierarchyID left, CompactHierarchyID right) { return left.ID == right.ID && left.generation == right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactHierarchyID left, CompactHierarchyID right) { return left.ID != right.ID || left.generation != right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override bool Equals(object obj)
        {
            if (obj is CompactHierarchyID) return this == ((CompactHierarchyID)obj);
            return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override int GetHashCode() { return ID.GetHashCode(); }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int CompareTo(CompactHierarchyID other)
        {
            var diff = ID - other.ID;
            if (diff != 0)
                return diff;

            return generation - other.generation;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CompactHierarchyID other)
        {
            return ID == other.ID && generation == other.generation;
        }
        #endregion
    }

    // TODO: use struct
    // TODO: rename
    public partial class CompactHierarchyManager
    {
        [DebuggerDisplay("Index = {index}, Generation = {generation}")]
        struct Generation
        {
            public Int32 index;
            public Int32 generation;
        }

        // TODO: use native containers
        static readonly List<CompactHierarchy>   hierarchies     = new List<CompactHierarchy>();

        static readonly List<Generation>         idToIndex       = new List<Generation>();
        static readonly List<int>                freeIDs         = new List<int>();




        internal sealed class BrushOutlineState : IDisposable
        {
            public BrushOutline brushOutline;
            public void Dispose()
            {
                if (brushOutline.IsCreated)
                    brushOutline.Dispose();
                brushOutline = default;
            }
        }

        static readonly Dictionary<int, BrushOutlineState> brushOutlineStates = new Dictionary<int, BrushOutlineState>();


        // Temporary hack
        public static void ClearOutlines()
        {
            foreach(var value in brushOutlineStates.Values)
                value.Dispose();
            brushOutlineStates.Clear();
        }



        public static void GetAllTrees(List<CSGTree> allTrees)
        {
            allTrees.Clear();

            var hierarchyCount = hierarchies.Count;

            if (hierarchyCount == 0)
                return;

            if (allTrees.Capacity < hierarchyCount)
                allTrees.Capacity = hierarchyCount;

            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                allTrees.Add(new Chisel.Core.New.CSGTree { treeNodeID = hierarchies[i].RootID });
            }
        }

        public static void GetBrushesInOrder(CSGTree tree, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (!IsValidNodeID(tree.NodeID))
                throw new ArgumentNullException(nameof(tree));
            GetHierarchy(tree.NodeID).GetBrushesInOrder(brushes);
        }

        public static void UpdateTreeNodeList(CompactNodeID treeNodeID, System.Collections.Generic.List<CompactNodeID> nodes, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (!IsValidNodeID(treeNodeID))
                throw new ArgumentNullException(nameof(treeNodeID));
            GetHierarchy(treeNodeID).UpdateTreeNodeList(nodes, brushes);
        }

        public static void Clear() 
        {
            ClearOutlines();

            var oldHierarchies = hierarchies.ToArray();
            foreach (var hierarchy in oldHierarchies)
                hierarchy.Dispose();
            defaultHierarchy = CompactHierarchyID.Invalid;
            hierarchies.Clear();
            idToIndex.Clear();
            freeIDs.Clear();
            brushSelectableState.Clear();
        }
        
        #region Inspector State
#if UNITY_EDITOR
        enum BrushVisibilityState
        {
            None            = 0,
            Visible         = 1,
            PickingEnabled  = 2,
            Selectable      = Visible | PickingEnabled
        }
        static Dictionary<CompactNodeID, BrushVisibilityState> brushSelectableState = new Dictionary<CompactNodeID, BrushVisibilityState>();

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static VisibilityState SetBrushState(CompactNodeID nodeID, bool visible, bool pickingEnabled)
        {
            if (!IsValidNodeID(nodeID))
                return VisibilityState.Unknown;

            var state = (visible        ? BrushVisibilityState.Visible        : BrushVisibilityState.None) |
                        (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            brushSelectableState[nodeID] = state;

            if (visible)
                return VisibilityState.AllVisible;
            else
                return VisibilityState.AllInvisible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetVisibility(CompactNodeID nodeID, bool visible)
        {
            if (!IsValidNodeID(nodeID))
                return;

            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(nodeID, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);
            brushSelectableState[nodeID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetPickingEnabled(CompactNodeID nodeID, bool pickingEnabled)
        {
            if (!IsValidNodeID(nodeID))
                return;

            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(nodeID, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[nodeID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushVisible(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushPickingEnabled(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushSelectable(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.Selectable) != BrushVisibilityState.None;
        }
#endif
        #endregion

        public static ref BrushOutline GetBrushOutline(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                throw new ArgumentNullException(nameof(nodeID));
            if (!brushOutlineStates.ContainsKey(nodeID.ID))
                brushOutlineStates[nodeID.ID] = new BrushOutlineState
                    { 
                        brushOutline = BrushOutline.Create()
                    };
            return ref brushOutlineStates[nodeID.ID].brushOutline; 
        }

        #region CreateHierarchy
        public static CompactHierarchy CreateHierarchy(Int32 userID = 0)
        {
            var id = CreateID(hierarchies.Count);
            var hierarchy = CompactHierarchy.CreateHierarchy(id, userID, Allocator.Persistent);
            hierarchies.Add(hierarchy);
            return hierarchy;
        }
        #endregion

        // TODO: make this work with ref *somehow*
        static CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException(nameof(hierarchyID));

            var id = hierarchyID.ID;
            if (id < 0 || id >= idToIndex.Count)
                throw new ArgumentException(nameof(hierarchyID));

            var idLookup    = idToIndex[id];
            var index       = idLookup.index;
            if (index < 0 || index >= hierarchies.Count)
                throw new Exception();

            return hierarchies[index];
        }

        static CompactHierarchy GetHierarchy(CompactNodeID nodeID)
        {
            if (nodeID == CompactNodeID.Invalid)
                throw new ArgumentException(nameof(nodeID));
            
            var hierarchyID = nodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException(nameof(nodeID));

            return GetHierarchy(hierarchyID);
        }

        static CompactHierarchyID CreateID(int index)
        {
            int lastNodeID;
            if (freeIDs.Count > 0)
            {
                var freeID = freeIDs.Count - 1;
                lastNodeID = freeIDs[freeID];
                freeIDs.RemoveAt(freeID);
                var generation = idToIndex[lastNodeID].generation + 1;
                idToIndex[lastNodeID] = new Generation { index = index, generation = generation };
                return new CompactHierarchyID(id: lastNodeID, generation: generation);
            } else
            {
                lastNodeID = idToIndex.Count;
                idToIndex.Add(new Generation { index = index, generation = 0 });
                return new CompactHierarchyID(id: lastNodeID, generation: 0);
            }
        }

        internal static void FreeID(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                return;

            var id = hierarchyID.ID;
            if (id < 0 || id >= idToIndex.Count)
                return;
            
            var idLookup    = idToIndex[id];
            var index       = idLookup.index;
            if (index < 0 || index >= hierarchies.Count)
                return;

            if (!freeIDs.Contains(idLookup.index))
                freeIDs.Add(idLookup.index);
            idLookup.index = -1;
            idToIndex[id] = idLookup;

            hierarchies[index] = default;
        }

        static CompactHierarchyID defaultHierarchy = CompactHierarchyID.Invalid;

        static void CreateDefaultHierarchy()
        {
            defaultHierarchy = CreateHierarchy().ID;
        }

        internal static bool GenerateTree(Int32 userID, out CompactNodeID generatedTreeNodeID)
        {
            var newHierarchy = CreateHierarchy(userID);
            generatedTreeNodeID = newHierarchy.RootID;
            return true;
        }
        
        // TODO: switch userID and operationType
        internal static bool GenerateBranch(Int32 userID, CSGOperationType operation, out CompactNodeID generatedBranchNodeID)
        {
            // TODO: modify API to not require default hierarchy
            if (defaultHierarchy == CompactHierarchyID.Invalid)
                CreateDefaultHierarchy();
            generatedBranchNodeID = GetHierarchy(defaultHierarchy).CreateBranch(operation, userID);
            return true;
        }

        internal static bool GenerateBrush(Int32 userID, float4x4 localTransformation, BrushMeshInstance brushMesh, CSGOperationType operation, out CompactNodeID generatedBrushNodeID)
        {
            // TODO: modify API to not require default hierarchy
            if (defaultHierarchy == CompactHierarchyID.Invalid)
                CreateDefaultHierarchy();
            generatedBrushNodeID = GetHierarchy(defaultHierarchy).CreateBrush(brushMesh.brushMeshID, operation, userID);
            return true;
        }
        

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public static void NotifyBrushMeshModified(HashSet<int> modifiedBrushMeshes)
        {
            var hierarchyCount = hierarchies.Count;
            if (hierarchyCount == 0)
                return;

            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                hierarchies[i].NotifyBrushMeshModified(modifiedBrushMeshes);
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [BurstDiscard]
        public static void NotifyBrushMeshRemoved(int brushMeshID)
        {
            var hierarchyCount = hierarchies.Count;
            if (hierarchyCount == 0)
                return;

            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                hierarchies[i].NotifyBrushMeshRemoved(brushMeshID);
            }
        }

        #region Dirty
        internal static bool IsNodeDirty(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;

            var hierarchy = GetHierarchy(nodeID);
            ref var node = ref hierarchy.GetChildRef(nodeID);
            CSGNodeType nodeType;
            if (hierarchy.RootID != nodeID)
                nodeType = (node.brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:		return (node.flags & (NodeStatusFlags.NeedCSGUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Branch:	return (node.flags & (NodeStatusFlags.BranchNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate)) != NodeStatusFlags.None;
                case CSGNodeType.Tree:		return (node.flags & (NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate)) != NodeStatusFlags.None;
            }
            return false;
        }
        
        internal static bool SetDirty(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;

            var hierarchy = GetHierarchy(nodeID);
            ref var node = ref hierarchy.GetChildRef(nodeID);
            CSGNodeType nodeType;
            if (hierarchy.RootID != nodeID)
                nodeType = (node.brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
            else
                nodeType = CSGNodeType.Tree;

            switch (nodeType)
            {
                case CSGNodeType.Brush:
                {
                    node.flags |= NodeStatusFlags.NeedFullUpdate;
                    ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    return true; 
                }
                case CSGNodeType.Branch:
                {
                    node.flags |= NodeStatusFlags.BranchNeedsUpdate;
                    ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
                    rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
                    return true; 
                }
                case CSGNodeType.Tree:
                {
                    node.flags |= NodeStatusFlags.TreeNeedsUpdate;
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
        public static bool ClearDirty(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;

            GetHierarchy(nodeID).GetChildRef(nodeID).flags = NodeStatusFlags.None;
            return true;
        }
        #endregion

        internal static CSGNodeType GetTypeOfNode(CompactNodeID nodeID)
        {
            var hierarchy = GetHierarchy(nodeID);
            if (hierarchy.RootID == nodeID)
                return CSGNodeType.Tree;

            return (hierarchy.GetChildRef(nodeID).brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
        }


        internal static bool IsValidNodeID(CompactNodeID nodeID)
        {
            if (nodeID == CompactNodeID.Invalid)
                return false;

            var hierarchyID = nodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;

            return GetHierarchy(hierarchyID).IsValidNodeID(nodeID);
        }

        internal static int GetUserIDOfNode(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).GetChildRef(nodeID).userID;
        }

        #region Transformations
        internal static float4x4 GetNodeLocalTransformation(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).GetChildRef(nodeID).transformation;
        }

        internal static bool SetNodeLocalTransformation(CompactNodeID nodeID, ref float4x4 result)
        {
            GetHierarchy(nodeID).GetChildRef(nodeID).transformation = result;
            return true;
        }

        internal static bool GetTreeToNodeSpaceMatrix(CompactNodeID nodeID, out float4x4 result)
        {
            // TODO: fix temporary "solution"
            result = GetHierarchy(nodeID).GetChildRef(nodeID).transformation;
            return true;
        }

        internal static bool GetNodeToTreeSpaceMatrix(CompactNodeID nodeID, out float4x4 result)
        {
            // TODO: fix temporary "solution"
            result = math.inverse(GetHierarchy(nodeID).GetChildRef(nodeID).transformation);
            return true;
        }
        #endregion

        #region BrushMeshID
        internal static Int32 GetBrushMeshID(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).GetChildRef(nodeID).brushMeshID;
        }
        
        internal static bool SetBrushMeshID(CompactNodeID nodeID, Int32 brushMeshID)
        {
            GetHierarchy(nodeID).GetChildRef(nodeID).brushMeshID = brushMeshID;
            return true;
        }
        #endregion

        #region Operation
        internal static CSGOperationType GetNodeOperationType(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).GetChildRef(nodeID).operation;
        }

        internal static bool SetNodeOperationType(CompactNodeID nodeID, CSGOperationType operation)
        {
            GetHierarchy(nodeID).GetChildRef(nodeID).operation = operation;
            return true;
        }
        #endregion

        internal static void DestroyOutline(CompactNodeID nodeID)
        {
            if (brushOutlineStates.ContainsKey(nodeID.ID))
            {
                brushOutlineStates[nodeID.ID].Dispose();
                brushOutlineStates.Remove(nodeID.ID);
            }
        }

        internal static bool DestroyNode(CompactNodeID nodeID)
        {
            DestroyOutline(nodeID);
            return GetHierarchy(nodeID).Delete(nodeID);
        }

        internal static CompactNodeID GetParentOfNode(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).ParentOf(nodeID);
        }

        internal static CompactNodeID GetRootOfNode(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).RootID;
        }

        internal static Int32 GetChildNodeCount(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).ChildCount(nodeID);
        }

        internal static CompactNodeID GetChildNodeAtIndex(CompactNodeID parent, int index)
        {
            return GetHierarchy(parent).GetChildIDAt(parent, index);
        }

        internal static MinMaxAABB GetBrushBounds(CompactNodeID nodeID)
        {
            return GetHierarchy(nodeID).GetChildRef(nodeID).bounds;
        }

        internal static bool AddChildNode(CompactNodeID parent, CompactNodeID item)
        {
            if (parent == CompactNodeID.Invalid ||
                item == CompactNodeID.Invalid)
                return false;

            var parentHierarchyID = parent.hierarchyID;
            if (parentHierarchyID == CompactHierarchyID.Invalid)
                return false;

            var itemHierarchyID = item.hierarchyID;
            if (itemHierarchyID == CompactHierarchyID.Invalid)
                return false;

            if (itemHierarchyID != parentHierarchyID)
                return false;

            var hierarchy = GetHierarchy(parent);
            hierarchy.AttachToParent(parent, item);
            return true;
        }

        internal static unsafe bool InsertChildNodeRange(CompactNodeID parent, int index, CSGTreeNode* arrayPtr, int arrayLength)
        {
            var hierarchy = GetHierarchy(parent);
            for (int i = index, lastNodex = index + arrayLength; i < lastNodex; i++)
                hierarchy.AttachToParentAt(parent, i, arrayPtr[i].NodeID);
            return true;
        }

        internal static bool InsertChildNode(CompactNodeID parent, int index, CompactNodeID item)
        {
            GetHierarchy(parent).AttachToParentAt(parent, index, item);
            return true;
        }

        internal static unsafe bool SetChildNodes(CompactNodeID parent, CSGTreeNode* arrayPtr, int arrayLength)
        {
            var hierarchy = GetHierarchy(parent);
            hierarchy.DetachAllChildrenFromParent(parent);

            if (arrayLength == 0)
                return true;

            for (int i = 0; i < arrayLength; i++)
                hierarchy.AttachToParentAt(parent, i, arrayPtr[i].NodeID);

            return true;
        }

        internal static bool RemoveChildNode(CompactNodeID parent, CompactNodeID item)
        {
            var hierarchy = GetHierarchy(parent);
            if (hierarchy.ParentOf(parent) != parent)
                return false;

            return hierarchy.Detach(item);
        }

        internal static bool RemoveChildNodeAt(CompactNodeID parent, int index)
        {
            return GetHierarchy(parent).DetachChildFromParentAt(parent, index);
        }

        internal static bool RemoveChildNodeRange(CompactNodeID parent, int index, int range)
        {
            if (range <= 0)
                return false;

            return GetHierarchy(parent).DetachChildrenFromParentAt(parent, index, (uint)range);
        }

        internal static void ClearChildNodes(CompactNodeID parent)
        {
            var hierarchy = GetHierarchy(parent);
            hierarchy.DetachAllChildrenFromParent(parent);
        }

        internal static int IndexOfChildNode(CompactNodeID parent, CompactNodeID item)
        {
            var hierarchy = GetHierarchy(parent);
            if (hierarchy.ParentOf(parent) != parent)
                throw new ArgumentException(nameof(parent));

            return hierarchy.SiblingIndexOf(item);
        }


        // Temporary workaround until we can switch to hashes
        internal static bool IsAnyStatusFlagSet(CompactNodeID nodeID)
        {
            var hierarchy = GetHierarchy(nodeID);
            return hierarchy.IsAnyStatusFlagSet(nodeID);
        }

        internal static bool IsStatusFlagSet(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var hierarchy = GetHierarchy(nodeID);
            return hierarchy.IsStatusFlagSet(nodeID, flag);
        }

        internal static void SetStatusFlag(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var hierarchy = GetHierarchy(nodeID);
            hierarchy.SetStatusFlag(nodeID, flag);
        }

        internal static void ClearAllStatusFlags(CompactNodeID nodeID)
        {
            var hierarchy = GetHierarchy(nodeID);
            hierarchy.ClearAllStatusFlags(nodeID);
        }

        internal static void ClearStatusFlag(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var hierarchy = GetHierarchy(nodeID);
            hierarchy.ClearStatusFlag(nodeID, flag);
        }
        

        internal static NodeTransformations GetNodeTransformation(CompactNodeID nodeID)
        {
            var node = new CSGTreeNode { nodeID = nodeID };
            return new NodeTransformations
            {
                nodeToTree = node.NodeToTreeSpaceMatrix,
                treeToNode = node.TreeToNodeSpaceMatrix
            };
        }
    }
}