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
    [BurstCompatible]
    public readonly struct NodeID : IComparable<NodeID>, IEquatable<NodeID>
    {
        public static readonly NodeID Invalid = default;

        public readonly Int32 value;
        public readonly Int32 generation;
        internal NodeID(Int32 value, Int32 generation = 0) { this.value = value; this.generation = generation; }

        #region Overhead
        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard] public override string ToString() { return $"NodeID = {value}"; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator ==(NodeID left, NodeID right) { return left.value == right.value && left.generation == right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator !=(NodeID left, NodeID right) { return left.value != right.value || left.generation != right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never)] public override bool Equals(object obj) { if (obj is NodeID) return this == ((NodeID)obj); return false; }
        [EditorBrowsable(EditorBrowsableState.Never)] public override int GetHashCode() { return value; }
        [EditorBrowsable(EditorBrowsableState.Never)] public int CompareTo(NodeID other) { var diff = value - other.value; if (diff != 0) return diff; return generation - other.generation; }
        [EditorBrowsable(EditorBrowsableState.Never)] public bool Equals(NodeID other) { return value == other.value && generation == other.generation; }
        #endregion
    }

    public struct CompactHierarchyID : IComparable<CompactHierarchyID>, IEquatable<CompactHierarchyID>
    {
        public static readonly CompactHierarchyID Invalid = default;

        public readonly Int32 value;
        public readonly Int32 generation;
        internal CompactHierarchyID(Int32 value, Int32 generation = 0) { this.value = value; this.generation = generation; }

        [EditorBrowsable(EditorBrowsableState.Never), BurstDiscard]
        public override string ToString() { return $"HierarchyID = {value}, Generation = {generation}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CompactHierarchyID left, CompactHierarchyID right) { return left.value == right.value && left.generation == right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CompactHierarchyID left, CompactHierarchyID right) { return left.value != right.value || left.generation != right.generation; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) { if (obj is CompactHierarchyID) return this == ((CompactHierarchyID)obj); return false; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return value; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int CompareTo(CompactHierarchyID other)
        {
            var diff = value - other.value;
            if (diff != 0)
                return diff;

            return generation - other.generation;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CompactHierarchyID other)
        {
            return value == other.value && generation == other.generation;
        }
        #endregion
    }

    // TODO: use struct
    // TODO: rename
    public partial class CompactHierarchyManager
    {
        static readonly List<CompactHierarchy>  hierarchies         = new List<CompactHierarchy>();
        static IDManager                        hierarchyIDLookup;
        
        static readonly List<CompactNodeID>     nodes               = new List<CompactNodeID>();
        static IDManager                        nodeIDLookup;

        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod]
        static void StaticInitialize()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
#endif
        }
         
#if UNITY_EDITOR
        static void OnBeforeAssemblyReload()
        {
            Dispose();
        }

        // TODO: need a runtime equivalent
        static void OnAfterAssemblyReload()
        {
            Initialize();
        }
#endif

        public static void Initialize()
        {
            Dispose();
            hierarchyIDLookup   = IDManager.Create(Allocator.Persistent);
            nodeIDLookup        = IDManager.Create(Allocator.Persistent);
        }

        public static void Dispose()
        {
            Clear();
            if (hierarchyIDLookup.IsCreated) hierarchyIDLookup.Dispose(); hierarchyIDLookup = default;
            if (nodeIDLookup.IsCreated) nodeIDLookup.Dispose(); nodeIDLookup = default;
        }

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


        /// <summary>Updates all pending changes to all <see cref="Chisel.Core.CSGTree"/>s.</summary>
        /// <returns>True if any <see cref="Chisel.Core.CSGTree"/>s have been updated, false if no changes have been found.</returns>
        public static bool Flush(FinishMeshUpdate finishMeshUpdates) 
        { 
            if (!UpdateAllTreeMeshes(finishMeshUpdates, out JobHandle handle)) 
                return false; 
            handle.Complete(); 
            return true; 
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
                allTrees.Add(new CSGTree { treeNodeID = CompactHierarchyManager.GetNodeID(hierarchies[i].RootID) });
            }
        }

        public static void GetBrushesInOrder(CSGTree tree, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            GetHierarchy(tree.NodeID).GetBrushesInOrder(brushes);
        }

        public static void UpdateTreeNodeList(NodeID treeNodeID, System.Collections.Generic.List<CompactNodeID> nodes, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (!IsValidNodeID(treeNodeID))
                throw new ArgumentException(nameof(treeNodeID));
            GetHierarchy(treeNodeID).UpdateTreeNodeList(nodes, brushes);
        }

        public static void Clear()
        {
            ClearOutlines();

            var oldHierarchies = hierarchies.ToArray();
            foreach (var hierarchy in oldHierarchies)
            {
                if (hierarchy.HierarchyID != default)
                    hierarchy.Dispose();
            }
            defaultHierarchyID = CompactHierarchyID.Invalid;
            
            hierarchies.Clear();
            hierarchyIDLookup.Clear();

            nodes.Clear();
            nodeIDLookup.Clear();

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
        static Dictionary<NodeID, BrushVisibilityState> brushSelectableState = new Dictionary<NodeID, BrushVisibilityState>();

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetVisibility(NodeID nodeID, bool visible)
        {
            if (!IsValidNodeID(nodeID))
                return;

            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(nodeID, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);
            brushSelectableState[nodeID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetPickingEnabled(NodeID nodeID, bool pickingEnabled)
        {
            if (!IsValidNodeID(nodeID))
                return;

            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(nodeID, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[nodeID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushVisible(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushPickingEnabled(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushSelectable(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            if (!brushSelectableState.TryGetValue(nodeID, out var result))
                return false;
            return (result & BrushVisibilityState.Selectable) != BrushVisibilityState.None;
        }
#endif
        #endregion

        public static ref BrushOutline GetBrushOutline(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));
            if (!brushOutlineStates.ContainsKey(nodeID.value))
                brushOutlineStates[nodeID.value] = new BrushOutlineState
                    { 
                        brushOutline = BrushOutline.Create()
                    };
            return ref brushOutlineStates[nodeID.value].brushOutline; 
        }

        #region CreateHierarchy
        public static CompactHierarchy CreateHierarchy(Int32 userID = 0)
        {
            var rootNodeID = CreateNodeID(out var rootNodeIndex);
            var hierarchyID = CreateHierarchyID(out var hierarchyIndex);
            var hierarchy = CompactHierarchy.CreateHierarchy(hierarchyID, rootNodeID, userID, Allocator.Persistent);
            hierarchies[hierarchyIndex] = hierarchy;
            nodes[rootNodeIndex] = hierarchy.RootID;
            return hierarchy;
        }
        #endregion

        // TODO: make this work with ref *somehow*
        internal static CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            var index = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (index < 0 || index >= hierarchies.Count)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {index} has an invalid hierarchy (out of bounds [0...{hierarchies.Count}]), are you using an old reference?", nameof(hierarchyID));

            return hierarchies[index];
        }

        static CompactHierarchy GetHierarchy(CompactNodeID compactNodeID)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid.", nameof(compactNodeID));

            return GetHierarchy(compactNodeID.hierarchyID);
        } 

        static CompactHierarchy GetHierarchy(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            return GetHierarchy(nodes[index].hierarchyID);
        }

        public static CompactHierarchyID GetHierarchyID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid, are you using an old reference?", nameof(compactNodeID));

            return compactNodeID.hierarchyID;
        }

        static CompactHierarchyID CreateHierarchyID(out int index)
        {
            index = hierarchyIDLookup.CreateID(out var id, out var generation);
            while (index >= hierarchies.Count)
                hierarchies.Add(default);
            return new CompactHierarchyID(value: id, generation: generation);
        }

        internal static void FreeHierarchyID(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                return;

            var index = hierarchyIDLookup.FreeID(hierarchyID.value, hierarchyID.generation);
            if (index < 0 || index >= hierarchies.Count)
                return;

            hierarchies[index] = default;
        }

        static CompactHierarchyID defaultHierarchyID = CompactHierarchyID.Invalid;

        static void CreateDefaultHierarchy()
        {
            Initialize();
            defaultHierarchyID = CreateHierarchy().HierarchyID;
        }

        static NodeID CreateNodeID(out int index)
        {
            index = nodeIDLookup.CreateID(out var id, out var generation);
            while (index >= nodes.Count)
                nodes.Add(CompactNodeID.Invalid);
            return new NodeID(value: id, generation: generation);
        }

        internal static void FreeNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var index = nodeIDLookup.FreeID(nodeID.value, nodeID.generation);
            if (index < 0 || index >= nodes.Count)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            nodes[index] = CompactNodeID.Invalid;
        }
        
        public static CompactNodeID GetCompactNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            var index = nodeIDLookup.GetIndex(nodeID.value, nodeID.generation);
            if (index < 0 || index >= nodes.Count)
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) points to an invalid index, are you using an old reference?", nameof(nodeID));

            return nodes[index];
        }

        public static NodeID GetNodeID(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;

            return GetHierarchy(compactNodeID).GetNodeID(compactNodeID);
        }

        internal static bool IsValidNodeID(NodeID nodeID, out int index)
        {
            index = -1;
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsValid(nodeID.value, nodeID.generation, out index))
                return false;

            if (index < 0 || index >= nodes.Count)
            {
                index = -1;
                return false;
            }

            return true;
        }

        public static bool IsValidNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsValid(nodeID.value, nodeID.generation, out var index))
                return false;

            if (index < 0 || index >= nodes.Count)
                return false;

            return true;
        }

        public static bool GenerateTree(Int32 userID, out NodeID generatedTreeNodeID)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                CreateDefaultHierarchy();
            var newHierarchy = CreateHierarchy(userID);
            generatedTreeNodeID = GetNodeID(newHierarchy.RootID);
            return true;
        }
        
        // TODO: switch userID and operationType
        public static bool GenerateBranch(Int32 userID, CSGOperationType operation, out NodeID generatedBranchNodeID)
        {
            // TODO: modify API to not require default hierarchy
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                CreateDefaultHierarchy();
            generatedBranchNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBranch(generatedBranchNodeID, operation, userID);
            return true;
        }

        public static bool GenerateBrush(Int32 userID, float4x4 localTransformation, BrushMeshInstance brushMesh, CSGOperationType operation, out NodeID generatedBrushNodeID)
        {
            // TODO: modify API to not require default hierarchy
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                CreateDefaultHierarchy();
            generatedBrushNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBrush(generatedBrushNodeID, brushMesh.brushMeshID, localTransformation, operation, userID);
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
        internal static bool IsNodeDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var hierarchy = GetHierarchy(compactNodeID);
            ref var node = ref hierarchy.GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (hierarchy.RootID != compactNodeID)
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
        
        internal static bool SetDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var hierarchy = GetHierarchy(compactNodeID);
            ref var node = ref hierarchy.GetChildRef(compactNodeID);
            CSGNodeType nodeType;
            if (hierarchy.RootID != compactNodeID)
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
        public static bool ClearDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            GetHierarchy(compactNodeID).GetChildRef(compactNodeID).flags = NodeStatusFlags.None;
            return true;
        }
        #endregion

        internal static CSGNodeType GetTypeOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return CSGNodeType.None;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGNodeType.None;

            var hierarchy = GetHierarchy(compactNodeID);
            if (hierarchy.RootID == compactNodeID)
                return CSGNodeType.Tree;

            return (hierarchy.GetChildRef(compactNodeID).brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
        }


        public static bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;

            if (!hierarchyIDLookup.IsValid(compactNodeID.hierarchyID.value, compactNodeID.hierarchyID.generation, out var index))
                return false;

            if (index < 0 || index >= hierarchies.Count)
                return false;

            var hierarchy = hierarchies[index];
            return hierarchy.IsValidCompactNodeID(compactNodeID);
        }

        public static int GetUserIDOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return 0;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return 0;

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;
        }

        #region Transformations
        internal static float4x4 GetNodeLocalTransformation(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).transformation;
        }


        internal static NodeTransformations GetNodeTransformation(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var node = new CSGTreeNode { nodeID = nodeID };
            var localTransformations = node.LocalTransformation;
            return new NodeTransformations
            {
                nodeToTree = localTransformations,
                treeToNode = math.inverse(localTransformations)
            };
        }

        internal static bool SetNodeLocalTransformation(NodeID nodeID, in float4x4 result)
        {   
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var hierarchy = GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            nodeRef.transformation = result;
            nodeRef.bounds = CompactHierarchy.CalculateBounds(nodeRef.brushMeshID, in nodeRef.transformation);

            nodeRef.flags |= NodeStatusFlags.TransformationModified;
            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }

        internal static bool GetTreeToNodeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: fix temporary "solution"
            result = math.inverse(GetHierarchy(compactNodeID).GetChildRef(compactNodeID).transformation);
            return true;
        }

        internal static bool GetNodeToTreeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: fix temporary "solution"
            result = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).transformation;
            return true;
        }
        #endregion

        #region BrushMeshID
        internal static Int32 GetBrushMeshID(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).brushMeshID;
        }
        
        internal static bool SetBrushMeshID(NodeID nodeID, Int32 brushMeshID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            nodeRef.brushMeshID = brushMeshID;
            nodeRef.bounds = CompactHierarchy.CalculateBounds(nodeRef.brushMeshID, in nodeRef.transformation);

            nodeRef.flags |= NodeStatusFlags.ShapeModified | NodeStatusFlags.NeedAllTouchingUpdated;
            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }
        #endregion

        #region Operation
        internal static CSGOperationType GetNodeOperationType(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));
            
            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            ref var nodeRef = ref GetHierarchy(compactNodeID).GetChildRef(compactNodeID);
            return nodeRef.operation;
        }

        internal static bool SetNodeOperationType(NodeID nodeID, CSGOperationType operation)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            nodeRef.operation = operation;

            nodeRef.flags |= NodeStatusFlags.NeedAllTouchingUpdated | NodeStatusFlags.NeedPreviousSiblingsUpdate;
            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }
        #endregion

        internal static void DestroyOutline(CompactNodeID compactNodeID)
        {
            if (brushOutlineStates.ContainsKey(compactNodeID.value))
            {
                brushOutlineStates[compactNodeID.value].Dispose();
                brushOutlineStates.Remove(compactNodeID.value);
            }
        }
        
        public static bool DestroyNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            FreeNodeID(nodeID);

            DestroyOutline(compactNodeID);

            var currHierarchy = GetHierarchy(compactNodeID);
            if (currHierarchy.RootID == compactNodeID)
            {
                if (defaultHierarchyID == CompactHierarchyID.Invalid)
                    CreateDefaultHierarchy();
                var defaultHierarchy = GetHierarchy(defaultHierarchyID);

                for (int c = 0, childCount = currHierarchy.ChildCount(compactNodeID); c < childCount; c++)
                {
                    var child = currHierarchy.GetChildIDAt(compactNodeID, c);
                    MoveChildNode(child, ref currHierarchy, ref defaultHierarchy, true);
                }

                currHierarchy.Dispose();
                return true;
            }
            return currHierarchy.Delete(compactNodeID);
        }

        internal static CompactNodeID GetParentOfCompactNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).ParentOf(compactNodeID);
        }

        public static NodeID GetParentOfNode(NodeID nodeID)
        {
            return GetNodeID(GetParentOfCompactNode(nodeID));
        }

        internal static CompactNodeID GetRootOfCompactNode(NodeID nodeID)
        {
            return GetHierarchy(nodeID).RootID;
        }

        public static NodeID GetRootOfNode(NodeID nodeID)
        {
            return GetNodeID(GetHierarchy(nodeID).RootID);
        }

        public static Int32 GetChildNodeCount(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return GetHierarchy(compactNodeID).ChildCount(compactNodeID);
        }

        internal static CompactNodeID GetChildCompactNodeAtIndex(NodeID parent, int index)
        {
            if (!IsValidNodeID(parent, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            return GetHierarchy(parentCompactNodeID).GetChildIDAt(parentCompactNodeID, index);
        }

        internal static NodeID GetChildNodeAtIndex(NodeID parent, int index)
        {
            return GetNodeID(GetChildCompactNodeAtIndex(parent, index));
        }

        internal static MinMaxAABB GetBrushBounds(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var nodeRef = ref GetHierarchy(compactNodeID).GetChildRef(compactNodeID);
            if (nodeRef.brushMeshID == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return nodeRef.bounds;
        }

        
        static CompactNodeID DeepMove(int nodeIndex, CompactNodeID destinationParentCompactNodeID, ref CompactHierarchy destinationHierarchy, ref CompactHierarchy sourceHierarchy, ref CompactChildNode sourceNode)
        {
            Debug.Assert(destinationHierarchy.IsCreated, "Hierarchy has not been initialized");
            var srcCompactNodeID    = sourceNode.compactNodeID;
            var nodeID              = sourceNode.nodeID;
            var newCompactNodeID    = destinationHierarchy.CreateNode(sourceNode.nodeID, nodeIndex, in sourceNode.nodeInformation, destinationParentCompactNodeID);
            var childCount          = sourceHierarchy.ChildCount(srcCompactNodeID);
            if (childCount > 0)
            {
                var offset = destinationHierarchy.AllocateSize(nodeIndex, childCount);
                for (int i = 0; i < childCount; i++)
                {
                    var srcChildID = sourceHierarchy.GetChildIDAt(srcCompactNodeID, i);
                    ref var srcChild = ref sourceHierarchy.GetNodeRef(srcChildID);
                    var childNodeID = srcChild.nodeID;
                    var newChildCompactID = DeepMove(offset + i, newCompactNodeID, ref destinationHierarchy, ref sourceHierarchy, ref srcChild);
                    if (childNodeID != NodeID.Invalid)
                    {
                        var childNodeindex = nodeIDLookup.GetIndex(childNodeID.value, childNodeID.generation);
                        nodes[childNodeindex] = newChildCompactID;
                    }
                }
            }

            if (nodeID != NodeID.Invalid)
            {
                var nodeindex = nodeIDLookup.GetIndex(nodeID.value, nodeID.generation);
                nodes[nodeindex] = newCompactNodeID;
            }
            return newCompactNodeID;
        }

        // unchecked
        static CompactNodeID MoveChildNode(CompactNodeID compactNodeID, ref CompactHierarchy sourceHierarchy, ref CompactHierarchy destinationHierarchy, bool recursive = true)
        {
            Debug.Assert(sourceHierarchy.IsCreated, "Source hierarchy has not been initialized");
            Debug.Assert(destinationHierarchy.IsCreated, "Destination hierarchy has not been initialized");

            ref var sourceNode = ref sourceHierarchy.GetNodeRef(compactNodeID);
            if (recursive)
                return DeepMove(Int32.MaxValue, CompactNodeID.Invalid, ref destinationHierarchy, ref sourceHierarchy, ref sourceNode);

            return destinationHierarchy.CreateNode(sourceNode.nodeID, sourceNode.nodeInformation);
        }

        // Move nodes from one hierarchy to another
        public static CompactNodeID MoveChildNode(NodeID nodeID, CompactHierarchyID destinationParentID, bool recursive = true)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            if (destinationParentID == CompactHierarchyID.Invalid)
                throw new ArgumentException(nameof(destinationParentID));

            if (compactNodeID == CompactNodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var itemHierarchyID = compactNodeID.hierarchyID;
            if (itemHierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            // nothing to do
            if (itemHierarchyID == destinationParentID)
                return compactNodeID;

            var newParentHierarchy = GetHierarchy(destinationParentID);
            var oldParentHierarchy = GetHierarchy(itemHierarchyID);

            // Create new copy of item in new hierarchy
            var newCompactNodeID = MoveChildNode(compactNodeID, ref oldParentHierarchy, ref newParentHierarchy, recursive);

            nodes[index] = newCompactNodeID;

            // Delete item in old hierarchy
            oldParentHierarchy.DeleteRecursive(compactNodeID);
            return newCompactNodeID;
        }

        public static bool AddChildNode(NodeID parent, NodeID childNode)
        {
            if (parent == NodeID.Invalid)
            {
                Debug.LogError("parent == NodeID.Invalid");
                return false;
            }
            if (childNode == NodeID.Invalid)
            {
                Debug.LogError("childNode == NodeID.Invalid");
                return false;
            }

            if (!IsValidNodeID(parent, out var parentIndex))
            {
                Debug.LogError("!IsValidNodeID(parent, out var index)");
                return false;
            }

            var newParentCompactNodeID = nodes[parentIndex];
            if (!IsValidCompactNodeID(newParentCompactNodeID))
                return false;

            if (!IsValidNodeID(childNode, out var childIndex))
                return false;

            var childCompactNodeID = nodes[childIndex];
            if (!IsValidCompactNodeID(childCompactNodeID))
                return false;

            var newParentHierarchyID = newParentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
                return false;
             
            var currParentHierarchyID = childCompactNodeID.hierarchyID;
            if (currParentHierarchyID == CompactHierarchyID.Invalid)
                return false;

            var newParentHierarchy = GetHierarchy(newParentHierarchyID);
            if (currParentHierarchyID != newParentHierarchyID)
            {
                var oldParentHierarchy = GetHierarchy(currParentHierarchyID);

                // Create new copy of item in new hierarchy
                var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                nodes[childIndex] = newCompactNodeID;

                // Delete item in old hierarchy
                oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                childCompactNodeID = newCompactNodeID;
            }

            newParentHierarchy.AttachToParent(newParentCompactNodeID, childCompactNodeID);
            return true;
        }

        internal static unsafe bool InsertChildNodeRange(NodeID parent, int index, CSGTreeNode* arrayPtr, int arrayLength)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var hierarchy = GetHierarchy(parent);
            for (int i = index, lastNodex = index + arrayLength; i < lastNodex; i++)
                hierarchy.AttachToParentAt(parentCompactNodeID, i, GetCompactNodeID(arrayPtr[i].NodeID));
            SetDirty(parent);
            return true;
        }

        internal static bool InsertChildNode(NodeID parent, int index, NodeID item)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            if (!IsValidNodeID(item, out var itemNodeIndex))
                throw new ArgumentException(nameof(item));

            var itemCompactNodeID = nodes[itemNodeIndex];
            if (!IsValidCompactNodeID(itemCompactNodeID))
                throw new ArgumentException(nameof(item));

            GetHierarchy(parentCompactNodeID).AttachToParentAt(parentCompactNodeID, index, itemCompactNodeID);
            SetDirty(parent);
            return true;
        }

        internal static unsafe bool SetChildNodes(NodeID parent, CSGTreeNode* arrayPtr, int arrayLength)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var hierarchy = GetHierarchy(parentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(parentCompactNodeID);

            if (arrayLength == 0)
                return true;

            for (int i = 0; i < arrayLength; i++)
                hierarchy.AttachToParentAt(parentCompactNodeID, i, GetCompactNodeID(arrayPtr[i].NodeID));

            SetDirty(parent);
            return true;
        }

        public static bool RemoveChildNode(NodeID parent, NodeID item)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            if (!IsValidNodeID(item, out var itemNodeIndex))
                throw new ArgumentException(nameof(item));

            var itemCompactNodeID = nodes[itemNodeIndex];
            if (!IsValidCompactNodeID(itemCompactNodeID))
                throw new ArgumentException(nameof(item));

            var hierarchy = GetHierarchy(parentCompactNodeID);
            if (hierarchy.ParentOf(parentCompactNodeID) != parentCompactNodeID)
                return false;

            var result = hierarchy.Detach(itemCompactNodeID);
            if (result)
                SetDirty(parent);
            return result;
        }

        public static bool RemoveChildNodeAt(NodeID parent, int index)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var hierarchy = GetHierarchy(parentCompactNodeID);
            var result = hierarchy.DetachChildFromParentAt(parentCompactNodeID, index);
            if (result)
                SetDirty(parent);
            return result;
        }

        internal static bool RemoveChildNodeRange(NodeID parent, int index, int range)
        {
            if (range <= 0)
                return false;

            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var hierarchy = GetHierarchy(parentCompactNodeID);
            var result = hierarchy.DetachChildrenFromParentAt(parentCompactNodeID, index, range);
            if (result)
                SetDirty(parent);
            return result;
        }

        internal static void ClearChildNodes(NodeID parent)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            var hierarchy = GetHierarchy(parentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parent);
        }

        public static int SiblingIndexOf(NodeID parent, NodeID child)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The parameter {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid, are you using an old reference?", nameof(parent));
            
            if (!IsValidNodeID(child, out var childNodeIndex))
                throw new ArgumentException($"The parameter {nameof(NodeID)} {nameof(child)} (value: {child.value}, generation: {child.generation}) is invalid, are you using an old reference?", nameof(child));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The parameter {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid, are you using an old reference?", nameof(parent));

            var childCompactNodeID = nodes[childNodeIndex];
            if (!IsValidCompactNodeID(childCompactNodeID))
                throw new ArgumentException($"The parameter {nameof(CompactNodeID)} {nameof(child)} (value: {childCompactNodeID.value}, generation: {childCompactNodeID.generation}) is invalid, are you using an old reference?", nameof(child));

            var hierarchy = GetHierarchy(parentCompactNodeID);
            if (parentCompactNodeID != hierarchy.ParentOf(childCompactNodeID))
                throw new ArgumentException($"The parameter {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is not the parent of {nameof(NodeID)} {nameof(child)} (value: {child.value}, generation: {child.generation})", nameof(parent));

            return hierarchy.SiblingIndexOf(childCompactNodeID);
        }

        public static int SiblingIndexOf(NodeID item)
        {
            return SiblingIndexOf(GetParentOfNode(item), item);
        }

        // Temporary workaround until we can switch to hashes
        internal static bool IsAnyStatusFlagSet(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            return hierarchy.IsAnyStatusFlagSet(compactNodeID);
        }

        internal static bool IsStatusFlagSet(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException("NodeID is not valid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException("CompactNodeID is not valid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            return hierarchy.IsStatusFlagSet(compactNodeID, flag);
        }

        internal static void SetStatusFlag(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            hierarchy.SetStatusFlag(compactNodeID, flag);
        }

        internal static void ClearAllStatusFlags(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            hierarchy.ClearAllStatusFlags(compactNodeID);
        }

        internal static void ClearStatusFlag(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var hierarchy = GetHierarchy(compactNodeID);
            hierarchy.ClearStatusFlag(compactNodeID, flag);
        }
    }
}