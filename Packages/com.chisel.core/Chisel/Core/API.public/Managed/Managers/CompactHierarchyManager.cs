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
    // TODO: unify/clean up error messages
    // TODO: use struct, make this non static somehow?
    // TODO: rename
    public partial class CompactHierarchyManager
    {
        static NativeList<CompactHierarchy>     hierarchies;
        static IDManager                        hierarchyIDLookup;
        
        static NativeList<CompactNodeID>        nodes;
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

        // TODO: need a runtime equivalent
        public static void Initialize()
        {
            Dispose();
            hierarchyIDLookup   = IDManager.Create(Allocator.Persistent);
            nodeIDLookup        = IDManager.Create(Allocator.Persistent);
            hierarchies         = new NativeList<CompactHierarchy>(Allocator.Persistent);
            nodes               = new NativeList<CompactNodeID>(Allocator.Persistent);
            allTrees            = new NativeList<CSGTree>(Allocator.Persistent);
            updatedTrees        = new NativeList<CSGTree>(Allocator.Persistent);
            defaultHierarchyID  = CreateHierarchy().HierarchyID;
        }

        public static void Dispose()
        {
            Clear();
            if (hierarchyIDLookup.IsCreated) hierarchyIDLookup.Dispose(); hierarchyIDLookup = default;
            if (nodeIDLookup.IsCreated) nodeIDLookup.Dispose(); nodeIDLookup = default;
            if (hierarchies.IsCreated) hierarchies.Dispose(); hierarchies = default;
            if (nodes.IsCreated) nodes.Dispose(); nodes = default;
            if (allTrees.IsCreated) allTrees.Dispose(); allTrees = default;
            if (updatedTrees.IsCreated) updatedTrees.Dispose(); updatedTrees = default;
        }

        public static bool CheckConsistency()
        {
            for (int i = 0; i < hierarchies.Length; i++)
            {
                if (!hierarchies[i].CheckConsistency())
                    return false;
            }
            return true;
        }


        // Temporary hack
        public static void ClearOutlines()
        {
            if (hierarchies.IsCreated)
            { 
                for (int i = 0; i < hierarchies.Length; i++)
                    hierarchies[i].ClearAllOutlines();
            }
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
        /*
        public static int GetTreeCount()
        {
            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return 0;

            int counter = 0;
            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                counter++;
            }
            return counter;
        }

        public static CSGTree GetDefaultTree()
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                return new CSGTree { treeNodeID = NodeID.Invalid };

            ref var hierarchy = ref GetHierarchy(defaultHierarchyID);            
            return new CSGTree { treeNodeID = CompactHierarchyManager.GetNodeID(hierarchy.RootID) };
        }
        */
        public static void GetAllTreeNodes(List<CSGTreeNode> allNodes)
        {
            allNodes.Clear();

            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return;

            if (allNodes.Capacity < hierarchyCount)
                allNodes.Capacity = hierarchyCount;

            var tempNodes = new List<CSGTreeNode>();
            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                allNodes.Add(new CSGTree { treeNodeID = CompactHierarchyManager.GetNodeID(hierarchies[i].RootID) });
                tempNodes.Clear();
                GetHierarchyNodes(hierarchies[i].HierarchyID, tempNodes);
                allNodes.AddRange(tempNodes);
            }
        }

        public static void GetAllTrees(NativeList<CSGTree> allTrees)
        {
            allTrees.Clear();

            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return;

            if (allTrees.Capacity < hierarchyCount)
                allTrees.Capacity = hierarchyCount;

            for (int i = 0; i < hierarchyCount; i++)
            {
                if (!hierarchies[i].IsCreated)
                    continue;
                var tree = new CSGTree { treeNodeID = CompactHierarchyManager.GetNodeID(hierarchies[i].RootID) };
                if (!tree.Valid)
                    continue;
                allTrees.Add(tree);
            }
        }

        public static void GetBrushesInOrder(CSGTree tree, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            GetHierarchy(tree.NodeID).GetBrushesInOrder(brushes);
        }

        public static void GetTreeNodes(CSGTree tree, System.Collections.Generic.List<CompactNodeID> nodes, System.Collections.Generic.List<CSGTreeBrush> brushes)
        {
            if (!IsValidNodeID(tree))
                throw new ArgumentException(nameof(tree));
            GetHierarchy(tree.NodeID).GetTreeNodes(nodes, brushes);
        }

        public static void GetTreeNodes(CSGTree tree, ref NativeList<CompactNodeID> nodes, ref NativeList<CSGTreeBrush> brushes)
        {
            if (!IsValidNodeID(tree))
                throw new ArgumentException(nameof(tree));
            GetHierarchy(tree.NodeID).GetTreeNodes(nodes, brushes);
        }

        public static void GetHierarchyNodes(CompactHierarchyID hierarchyID, System.Collections.Generic.List<CSGTreeNode> nodes)
        {
            if (!IsValidHierarchyID(hierarchyID))
                throw new ArgumentException(nameof(hierarchyID));
            GetHierarchy(hierarchyID).GetAllNodes(nodes);
        }

        public static void Clear()
        {
            if (hierarchies.IsCreated)
            {
                var tempHierarchyList = new NativeArray<CompactHierarchy>(hierarchies.Length, Allocator.Temp);
                for (int i = 0; i < hierarchies.Length; i++)
                    tempHierarchyList[i] = hierarchies[i];

                using (tempHierarchyList)
                {
                    for (int i = 0; i < tempHierarchyList.Length; i++)
                    {
                        if (tempHierarchyList[i].IsCreated)
                        {
                            try
                            {
                                // Note: calling dispose will remove it from hierarchies, 
                                // which is why we need to copy the list to dispose them all efficiently
                                tempHierarchyList[i].Dispose();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogException(ex);
                            }
                        }
                    }
                }
            }
            defaultHierarchyID = CompactHierarchyID.Invalid;

            if (hierarchies.IsCreated) hierarchies.Clear();
            if (hierarchyIDLookup.IsCreated) hierarchyIDLookup.Clear();

            if (nodes.IsCreated) nodes.Clear();
            if (nodeIDLookup.IsCreated) nodeIDLookup.Clear();

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
        static Dictionary<int, BrushVisibilityState> brushSelectableState = new Dictionary<int, BrushVisibilityState>();

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetVisibility(NodeID nodeID, bool visible)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var userID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;

            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(userID, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);

            brushSelectableState[userID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetPickingEnabled(NodeID nodeID, bool pickingEnabled)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return;

            var userID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;

            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(userID, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[userID] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushVisible(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var userID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;
            if (!brushSelectableState.TryGetValue(userID, out var result))
                return false;

            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushPickingEnabled(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var userID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;
            if (!brushSelectableState.TryGetValue(userID, out var result))
                return false;

            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushSelectable(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            var userID = GetHierarchy(compactNodeID).GetChildRef(compactNodeID).userID;
            if (!brushSelectableState.TryGetValue(userID, out var result))
                return false;

            return (result & BrushVisibilityState.Selectable) != BrushVisibilityState.None;
        }
#endif
        #endregion

        public static unsafe ref BrushOutline GetBrushOutline(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = GetCompactNodeID(nodeID);
            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return ref GetBrushOutline(ref hierarchy, compactNodeID);
        }

        public static ref BrushOutline GetBrushOutline(ref CompactHierarchy hierarchy, CompactNodeID compactNodeID)
        {
            if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            return ref hierarchy.GetOutline(compactNodeID);
        }

        #region CreateHierarchy
        public unsafe static ref CompactHierarchy CreateHierarchy(Int32 userID = 0)
        {
            var rootNodeID = CreateNodeID(out var rootNodeIndex);
            var hierarchyID = CreateHierarchyID(out var hierarchyIndex);
            var hierarchy = CompactHierarchy.CreateHierarchy(hierarchyID, rootNodeID, userID, Allocator.Persistent);
            if (hierarchies[hierarchyIndex].IsCreated)
                hierarchies[hierarchyIndex].Dispose();
            hierarchies[hierarchyIndex] = hierarchy;
            nodes[rootNodeIndex] = hierarchy.RootID;
            return ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[hierarchyIndex];
        }
        #endregion

        // TODO: make this work with ref *somehow*
        internal unsafe static ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[hierarchyIndex];
        }

        internal unsafe static ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID, out int hierarchyIndex)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[hierarchyIndex];
        }

        static ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid.", nameof(compactNodeID));

            return ref GetHierarchy(compactNodeID.hierarchyID);
        }

        static ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID, out int hierarchyIndex)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid.", nameof(compactNodeID));

            return ref GetHierarchy(compactNodeID.hierarchyID, out hierarchyIndex);
        }

        static ref CompactHierarchy GetHierarchy(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            return ref GetHierarchy(nodes[index].hierarchyID);
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
            while (index >= hierarchies.Length)
                hierarchies.Add(default);
            return new CompactHierarchyID(value: id, generation: generation);
        }

        internal static void FreeHierarchyID(CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                return;

            if (!hierarchyIDLookup.IsCreated)
                return;

            var index = hierarchyIDLookup.FreeID(hierarchyID.value, hierarchyID.generation);
            if (index < 0 || index >= hierarchies.Length)
                return;

            // This method is called from Dispose, so shouldn't call dispose (would cause circular loop)
            //hierarchies[index].Dispose();
            hierarchies[index] = default;
        }

        static CompactHierarchyID defaultHierarchyID = CompactHierarchyID.Invalid;

        static NodeID CreateNodeID(out int index)
        {
            index = nodeIDLookup.CreateID(out var id, out var generation);
            //Debug.Log($"CreateNodeID index:{index} id:{id} generation:{generation}");
            while (index >= nodes.Length)
                nodes.Add(CompactNodeID.Invalid);
            return new NodeID(value: id, generation: generation);
        }

        internal static void FreeNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var index = nodeIDLookup.FreeID(nodeID.value, nodeID.generation);
            if (index < 0 || index >= nodes.Length)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            nodes[index] = CompactNodeID.Invalid;
        }

        internal static void MoveNodeID(NodeID nodeID, CompactNodeID compactNodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            if (!nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            nodes[index] = compactNodeID;
        }
        
        internal static CompactNodeID GetCompactNodeID(NodeID nodeID, out int index)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            index = nodeIDLookup.GetIndex(nodeID.value, nodeID.generation);
            if (index < 0 || index >= nodes.Length)
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) points to an invalid index, are you using an old reference?", nameof(nodeID));

            return nodes[index];
        }

        public static CompactNodeID GetCompactNodeID(CSGTreeNode treeNode)
        {
            return GetCompactNodeID(treeNode.NodeID, out _);
        }

        public static CompactNodeID GetCompactNodeID(NodeID nodeID)
        {
            return GetCompactNodeID(nodeID, out _);
        }

        public static NodeID GetNodeID(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.GetNodeID(compactNodeID);
        }

        public static NodeID GetNodeIDNoErrors(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.GetNodeIDNoErrors(compactNodeID);
        }
        /*
        public static CompactHierarchyID GetHierarchyIDOfNode(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return CompactHierarchyID.Invalid;

            if (!nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                return CompactHierarchyID.Invalid;

            if (index < 0 || index >= nodes.Length)
                return CompactHierarchyID.Invalid;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return CompactHierarchyID.Invalid;

            return compactNodeID.hierarchyID;
        }
        */
        internal static bool IsValidHierarchyID(CompactHierarchyID hierarchyID, out int index)
        {
            index = -1;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;

            if (!hierarchyIDLookup.IsValidID(hierarchyID.value, hierarchyID.generation, out index))
                return false;

            if (index < 0 || index >= nodes.Length)
            {
                index = -1;
                return false;
            }

            return true;
        }

        public static bool IsValidHierarchyID(CompactHierarchyID hierarchyID)
        {
            return IsValidHierarchyID(hierarchyID, out _);
        }

        [BurstCompile]
        internal static bool IsValidNodeID(NodeID nodeID, out int index)
        {
            index = -1;
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out index))
                return false;

            if (index < 0 || index >= nodes.Length)
            {
                index = -1;
                return false;
            }

            return true;
        }

        [BurstCompile]
        public static bool IsValidNodeID(CSGTreeNode treeNode)
        {
            var nodeID = treeNode.NodeID;
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsCreated ||
                !nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                return false;

            if (index < 0 || index >= nodes.Length)
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            return true;
        }

        [BurstCompile]
        public static bool IsValidNodeID(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsCreated ||
                !nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                return false;

            if (index < 0 || index >= nodes.Length)
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateTree(Int32 userID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            ref var newHierarchy = ref CreateHierarchy(userID);
            return GetNodeID(newHierarchy.RootID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBranch(Int32 userID = 0) { return CreateBranch(CSGOperationType.Additive, userID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBranch(CSGOperationType operation, Int32 userID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            var generatedBranchNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBranch(generatedBranchNodeID, operation, userID);
            return generatedBranchNodeID;
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBrush(BrushMeshInstance brushMesh, Int32 userID = 0) { return CreateBrush(brushMesh, float4x4.identity, CSGOperationType.Additive, userID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBrush(BrushMeshInstance brushMesh, CSGOperationType operation, Int32 userID = 0) { return CreateBrush(brushMesh, float4x4.identity, operation, userID); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBrush(BrushMeshInstance brushMesh, float4x4 localTransformation, Int32 userID = 0) { return CreateBrush(brushMesh, localTransformation, CSGOperationType.Additive, userID); }
        */
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateBrush(BrushMeshInstance brushMesh, float4x4 localTransformation, CSGOperationType operation, Int32 userID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            var generatedBrushNodeID = CreateNodeID(out var index);
            nodes[index] = GetHierarchy(defaultHierarchyID).CreateBrush(generatedBrushNodeID, brushMesh.brushMeshHash, localTransformation, operation, userID);
            return generatedBrushNodeID;
        }


        #region Dirty
        internal static bool IsNodeDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
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

        internal static bool IsNodeDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return IsNodeDirty(nodes[index]);
        }

        internal unsafe static bool SetChildrenDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var node = ref hierarchy.GetChildRef(compactNodeID);
            if (node.brushMeshID != Int32.MaxValue)
                return false;

            var result = true;
            var count = hierarchy.ChildCount(compactNodeID);
            for (int i = 0; i < count; i++)
            {
                var childID = hierarchy.GetChildIDAtInternal(compactNodeID, i);
                result = SetDirty(childID) && result;
            }
            return result;
        }
        /*
        internal static bool SetChildrenDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return SetChildrenDirty(nodes[index]);
        }
        */
        internal static bool SetDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
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
                    //Debug.Assert(IsNodeDirty(compactNodeID));
                    return true; 
                }
                case CSGNodeType.Branch:
                {
                    node.flags |= NodeStatusFlags.BranchNeedsUpdate;
                    ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
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
        
        internal static bool SetDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return SetDirty(nodes[index]);
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ClearDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            GetHierarchy(compactNodeID).GetChildRef(compactNodeID).flags = NodeStatusFlags.None;
            return true;
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ClearDirty(CSGTreeNode treeNode)
        {
            if (!IsValidNodeID(treeNode.NodeID, out var index))
                return false;

            return ClearDirty(nodes[index]);
        }
        #endregion

        [BurstCompile]
        internal static CSGNodeType GetTypeOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return CSGNodeType.None;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return CSGNodeType.None;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            if (hierarchy.RootID == compactNodeID)
                return CSGNodeType.Tree;

            return (hierarchy.GetChildRef(compactNodeID).brushMeshID == Int32.MaxValue) ? CSGNodeType.Branch : CSGNodeType.Brush;
        }


        public unsafe static bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return false;

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                return false;
            
            if (!hierarchyIDLookup.IsValidID(compactNodeID.hierarchyID.value, compactNodeID.hierarchyID.generation, out var index))
                return false;

            if (index < 0 || index >= hierarchies.Length)
                return false;

            ref var hierarchy = ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[index];
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


        internal static NodeTransformations GetNodeTransformation(in CompactHierarchy hierarchy, CompactNodeID compactNodeID)
        {
            if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            var localTransformations = hierarchy.GetChildRef(compactNodeID).transformation;
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

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            nodeRef.transformation = result;
            nodeRef.bounds = BrushMeshManager.CalculateBounds(nodeRef.brushMeshID, in nodeRef.transformation);

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
        /*
        internal static Int32 GetBrushMeshID(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).brushMeshID;
        }
        */

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

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            nodeRef.brushMeshID = brushMeshID;
            nodeRef.bounds = BrushMeshManager.CalculateBounds(nodeRef.brushMeshID, in nodeRef.transformation);

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

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            if (nodeRef.operation == operation)
                return false;

            nodeRef.operation = operation;
            if (nodeRef.brushMeshID == Int32.MaxValue)
                nodeRef.flags |= NodeStatusFlags.BranchNeedsUpdate;
            else
                nodeRef.flags |= NodeStatusFlags.NeedFullUpdate;

            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }
        #endregion

        public static bool DestroyNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid");
                return false;
            }

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid");
                return false;
            }

            ref var currHierarchy = ref GetHierarchy(compactNodeID, out int hierarchyIndex);
            if (currHierarchy.RootID == compactNodeID)
            {
                if (defaultHierarchyID == CompactHierarchyID.Invalid)
                    Initialize();

                // move children to default hierarchy
                ref var defaultHierarchy = ref GetHierarchy(defaultHierarchyID, out int defaultHierarchyIndex);

                Debug.Assert(hierarchyIndex != defaultHierarchyIndex);
                for (int c = 0, childCount = currHierarchy.ChildCount(compactNodeID); c < childCount; c++)
                {
                    var child = currHierarchy.GetChildIDAtInternal(compactNodeID, c);
                    MoveChildNode(child, ref currHierarchy, ref defaultHierarchy, true);
                }

                hierarchies[hierarchyIndex] = default;
                currHierarchy.Dispose();
                FreeNodeID(nodeID);
                return true;
            }

            var oldParent = GetParentOfNode(nodeID);
            if (oldParent != NodeID.Invalid)
                SetDirty(oldParent);
            SetDirty(currHierarchy.RootID);

            currHierarchy.ClearOutline(compactNodeID);
            FreeNodeID(nodeID);

            return currHierarchy.Delete(compactNodeID);
        }

        public static NodeID GetParentOfNode(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            return GetNodeID(GetHierarchy(compactNodeID).ParentOf(compactNodeID));
        }
        /*
        internal static CompactNodeID GetRootOfCompactNode(NodeID nodeID)
        {
            return GetHierarchy(nodeID).RootID;
        }
        */
        public static NodeID GetRootOfNode(NodeID nodeID)
        {
            var rootID = GetNodeID(GetHierarchy(nodeID).RootID);
            if (nodeID == rootID)
                return NodeID.Invalid;
            var iterator = GetParentOfNode(nodeID);
            while (iterator != NodeID.Invalid)
            {
                if (iterator == rootID)
                    return rootID;
                iterator = GetParentOfNode(iterator);
            }
            return NodeID.Invalid;
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

        internal static MinMaxAABB GetBrushBounds(NodeID nodeID, float4x4 transformation)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var nodeRef = ref GetHierarchy(compactNodeID).GetChildRef(compactNodeID);
            if (nodeRef.brushMeshID == Int32.MaxValue)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));
            
            return BrushMeshManager.CalculateBounds(nodeRef.brushMeshID, in transformation);
        }
        
        static CompactNodeID DeepMove(int nodeIndex, CompactNodeID destinationParentCompactNodeID, ref CompactHierarchy destinationHierarchy, ref CompactHierarchy sourceHierarchy, ref CompactChildNode sourceNode)
        {
            Debug.Assert(destinationHierarchy.IsCreated, "Hierarchy has not been initialized");
            var srcCompactNodeID    = sourceNode.compactNodeID;
            var nodeID              = sourceNode.nodeID;
            var newCompactNodeID    = destinationHierarchy.CreateNode(sourceNode.nodeID, ref nodeIndex, in sourceNode.nodeInformation, destinationParentCompactNodeID);
            var childCount          = sourceHierarchy.ChildCount(srcCompactNodeID);
            if (childCount > 0)
            {
                var offset = destinationHierarchy.AllocateChildCount(nodeIndex, childCount);
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

            ref var newParentHierarchy = ref GetHierarchy(destinationParentID);
            ref var oldParentHierarchy = ref GetHierarchy(itemHierarchyID);

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
                Debug.LogError("Cannot add a child to a parent with an invalid node");
                return false;
            }
            if (childNode == NodeID.Invalid)
            {
                Debug.LogError("Cannot add invalid node as child");
                return false;
            }

            if (parent == childNode)
            {
                Debug.LogError("Cannot add self as child");
                return false;
            }

            if (!IsValidNodeID(parent, out var parentIndex))
            {
                Debug.LogError("!IsValidNodeID(parent, out var index)");
                return false;
            }

            var newParentCompactNodeID = nodes[parentIndex];
            if (!IsValidCompactNodeID(newParentCompactNodeID))
            {
                Debug.LogError("!IsValidCompactNodeID(newParentCompactNodeID)");
                return false;
            }

            if (!IsValidNodeID(childNode, out var childIndex))
            {
                Debug.LogError("!IsValidNodeID(childNode)");
                return false;
            }

            var childCompactNodeID = nodes[childIndex];
            if (!IsValidCompactNodeID(childCompactNodeID))
            {
                Debug.LogError("!IsValidCompactNodeID(childNode)");
                return false;
            }

            var newParentHierarchyID = newParentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("newParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }
             
            var currParentHierarchyID = childCompactNodeID.hierarchyID;
            if (currParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("currParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }

            ref var oldParentHierarchy = ref GetHierarchy(currParentHierarchyID);
            ref var newParentHierarchy = ref GetHierarchy(newParentHierarchyID);
            if (childCompactNodeID == oldParentHierarchy.RootID ||
                childCompactNodeID == newParentHierarchy.RootID)
            {
                Debug.LogError("Cannot add a tree as a child");
                return false;
            }

            var oldParentNodeID = GetParentOfNode(childNode);
            if (oldParentNodeID == parent)
            {
                if (SiblingIndexOf(childNode) == GetChildNodeCount(parent) - 1)
                    return false;
            }

            if (currParentHierarchyID != newParentHierarchyID)
            {
                // Create new copy of item in new hierarchy
                var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                nodes[childIndex] = newCompactNodeID;
                 
                // Delete item in old hierarchy
                oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                childCompactNodeID = newCompactNodeID;
                SetDirty(oldParentHierarchy.RootID);
            } else
            if (GetTypeOfNode(childNode) != CSGNodeType.Brush)
            {
                // We cannot add a child to its own descendant (would create a loop)
                if (IsDescendant(childNode, parent))
                    return false;
            }

            if (oldParentNodeID != NodeID.Invalid)
                SetDirty(oldParentNodeID);

            newParentHierarchy.AttachToParent(newParentCompactNodeID, childCompactNodeID);
            SetDirty(parent);
            SetDirty(childNode);
            SetDirty(newParentHierarchy.RootID);
            return true;
        }

        internal static bool IsDescendant(NodeID parent, NodeID child)
        {
            var iterator = child;
            while (iterator != parent)
            {
                if (iterator == NodeID.Invalid)
                    return false;
                iterator = GetParentOfNode(iterator);
            }
            return true;
        }

        internal static unsafe bool InsertChildNodeRange(NodeID parent, int index, CSGTreeNode* arrayPtr, int arrayLength)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var count = GetChildNodeCount(parent);
            if (index < 0 || index > count)
            {
                if (count == 0)
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' can only be 0 because since are no other children");
                else
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' must be between 0 .. {count}");
                return false;
            }

            var newParentHierarchyID = parentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("newParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }

            ref var newParentHierarchy = ref GetHierarchy(parent);
            for (int i = 0, lastNodex = arrayLength; i < lastNodex; i++)
            {
                var childNode = arrayPtr[i].NodeID;

                if (!IsValidNodeID(childNode, out var childIndex))
                {
                    Debug.LogError("Cannot add an invalid child to a parent");
                    return false;
                }

                var childCompactNodeID = nodes[childIndex];
                if (!IsValidCompactNodeID(childCompactNodeID))
                {
                    Debug.LogError("!IsValidCompactNodeID(childNode)");
                    return false;
                }

                var currParentHierarchyID = childCompactNodeID.hierarchyID;
                if (currParentHierarchyID == CompactHierarchyID.Invalid)
                {
                    Debug.LogError("currParentHierarchyID == CompactHierarchyID.Invalid");
                    return false;
                }

                ref var oldParentHierarchy = ref GetHierarchy(currParentHierarchyID);
                if (childCompactNodeID == oldParentHierarchy.RootID ||
                    childCompactNodeID == newParentHierarchy.RootID)
                {
                    Debug.LogError("Cannot add a tree as a child");
                    return false;
                }

                if (childNode == parent)
                {
                    Debug.LogError("A node cannot be its own child");
                    return false;
                }

                if (currParentHierarchyID == newParentHierarchyID &&
                    GetTypeOfNode(childNode) != CSGNodeType.Brush)
                {
                    // We cannot add a child to its own descendant (would create a loop)
                    if (IsDescendant(childNode, parent))
                    {
                        Debug.LogError("Cannot add a descendant as a child");
                        return false;
                    }
                }
            }
            
            for (int i = 0, lastNodex = arrayLength; i < lastNodex; i++)
            {
                var childNode = arrayPtr[i].NodeID;

                var oldParentNodeID     = GetParentOfNode(childNode);
                if (oldParentNodeID == parent)
                {
                    if (SiblingIndexOf(childNode) == GetChildNodeCount(parent) - 1)
                        continue;
                }

                var childCompactNodeID      = GetCompactNodeID(childNode, out var childIndex);
                var currParentHierarchyID   = childCompactNodeID.hierarchyID;
                ref var oldParentHierarchy  = ref GetHierarchy(currParentHierarchyID);

                if (currParentHierarchyID != newParentHierarchyID)
                {
                    // Create new copy of item in new hierarchy
                    var newCompactNodeID = MoveChildNode(childCompactNodeID, ref oldParentHierarchy, ref newParentHierarchy, true);

                    nodes[childIndex] = newCompactNodeID;

                    // Delete item in old hierarchy
                    oldParentHierarchy.DeleteRecursive(childCompactNodeID);
                    childCompactNodeID = newCompactNodeID;
                    SetDirty(oldParentHierarchy.RootID);
                }

                if (oldParentNodeID != NodeID.Invalid)
                    SetDirty(oldParentNodeID);

                newParentHierarchy.AttachToParentAt(parentCompactNodeID, index + i, childCompactNodeID);
                SetDirty(childCompactNodeID);
            }
            SetDirty(parent);
            return true;
        }

        internal unsafe static bool InsertChildNode(NodeID parent, int index, NodeID item)
        {
            CSGTreeNode treeNode = new CSGTreeNode { nodeID = item };
            return InsertChildNodeRange(parent, index, &treeNode, 1);
        }

        internal static unsafe bool SetChildNodes(NodeID parent, CSGTreeNode* arrayPtr, int arrayLength)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(parentCompactNodeID);

            if (arrayLength == 0)
                return true;

            for (int i = 0; i < arrayLength; i++)
            {
                var treeNode = arrayPtr[i];
                if (!IsValidNodeID(treeNode))
                    return false;
                for (int j = i + 1; j < arrayLength; j++)
                {
                    if (arrayPtr[j] == treeNode)
                    {
                        Debug.LogError("Have duplicate child");
                        return false;
                    }
                }
            }

            bool result = true;
            for (int i = 0; i < arrayLength; i++)
                result = AddChildNode(parent, arrayPtr[i].NodeID) && result;

            SetDirty(parent);
            return result;
        }

        public static bool RemoveChildNode(NodeID parent, NodeID item)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid");
                return false;
            }

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
            {
                Debug.LogError($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid");
                return false;
            }

            if (!IsValidNodeID(item, out var itemNodeIndex))
            {
                Debug.LogError($"The {nameof(NodeID)} {nameof(item)} (value: {item.value}, generation: {item.generation}) is invalid");
                return false;
            }

            var itemCompactNodeID = nodes[itemNodeIndex];
            if (!IsValidCompactNodeID(itemCompactNodeID))
            {
                Debug.LogError($"The {nameof(CompactNodeID)} {nameof(item)} (value: {itemCompactNodeID.value}, generation: {itemCompactNodeID.generation}) is invalid");
                return false;
            }

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var parentOfItem = hierarchy.ParentOf(itemCompactNodeID);
            if (parentOfItem != parentCompactNodeID)
                return false;

            if (parentOfItem == CompactNodeID.Invalid)
                return false;

            var result = hierarchy.Detach(itemCompactNodeID);
            if (result)
            {
                SetDirty(parent);
                SetDirty(itemCompactNodeID);
            }
            return result;
        }

        public static bool RemoveChildNodeAt(NodeID parent, int index)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var count = hierarchy.ChildCount(parentCompactNodeID);
            if (index < 0 || index >= count)
            {
                Debug.LogError($"{nameof(index)} is invalid, its value '{index}' must be between 0 .. {count}");
                return false;
            }

            var childCompactNodeID = GetChildCompactNodeAtIndex(parent, index);

            var result = hierarchy.DetachChildFromParentAt(parentCompactNodeID, index);
            if (result)
            {
                SetDirty(parent);
                SetDirty(childCompactNodeID);
            }
            return result;
        }

        internal static bool RemoveChildNodeRange(NodeID parent, int index, int range)
        {
            if (index < 0)
            {
                Debug.LogError($"{nameof(index)} must be positive");
                return false;
            }

            if (range < 0)
            {
                Debug.LogError($"{nameof(range)} must be positive");
                return false;
            }

            if (range == 0)
                return true;

            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var count = hierarchy.ChildCount(parentCompactNodeID);
            if (index + range > count)
            {
                Debug.LogError($"{nameof(index)} + {nameof(range)} must be below or equal to {count}");
                return false;
            }

            var list = new NativeList<CompactNodeID>(count, Allocator.Temp);
            try
            {
                for (int i = index; i < index + range; i++)
                {
                    var childID = hierarchy.GetChildIDAtInternal(parentCompactNodeID, i);
                    list.Add(childID);
                }

                var result = hierarchy.DetachChildrenFromParentAt(parentCompactNodeID, index, range);
                if (result)
                {
                    SetDirty(parent);
                    for (int i = 0; i < list.Length; i++)
                        SetDirty(list[i]);
                }
                return result;
            }
            finally
            {
                list.Dispose();
            }
        }

        internal static void ClearChildNodes(NodeID parent)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            if (hierarchy.ChildCount(parentCompactNodeID) == 0)
                return;

            SetChildrenDirty(parentCompactNodeID);
            hierarchy.DetachAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parentCompactNodeID);
        }

        internal static void DestroyChildNodes(CSGTreeNode treeNode)
        {
            var parent = treeNode.NodeID;
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            if (hierarchy.ChildCount(parentCompactNodeID) == 0)
                return;

            SetChildrenDirty(parentCompactNodeID);
            hierarchy.DestroyAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parentCompactNodeID);
        }

        internal static void DestroyChildNodes(NodeID parent)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            if (hierarchy.ChildCount(parentCompactNodeID) == 0)
                return;

            SetChildrenDirty(parentCompactNodeID);
            hierarchy.DestroyAllChildrenFromParent(parentCompactNodeID);
            SetDirty(parentCompactNodeID);
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

            ref var hierarchy = ref GetHierarchy(parentCompactNodeID);
            var childParent = hierarchy.ParentOf(childCompactNodeID);
            if (parentCompactNodeID != childParent)
            {
                Debug.LogError($"The parameter {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is not the parent of {nameof(NodeID)} {nameof(child)} (value: {child.value}, generation: {child.generation})");
                return -1;
            }

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

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.IsAnyStatusFlagSet(compactNodeID);
        }

        internal static bool IsStatusFlagSet(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
            {
                //throw new ArgumentException("NodeID is not valid", nameof(nodeID));
                Debug.LogError("NodeID is not valid");
                return false;
            }

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
            {
                //throw new ArgumentException("CompactNodeID is not valid", nameof(nodeID));
                Debug.LogError("CompactNodeID is not valid");
                return false;
            }

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.IsStatusFlagSet(compactNodeID, flag);
        }

        internal static void SetStatusFlag(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            hierarchy.SetStatusFlag(compactNodeID, flag);
        }

        internal static void ClearAllStatusFlags(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            hierarchy.ClearAllStatusFlags(compactNodeID);
        }

        internal static void ClearStatusFlag(NodeID nodeID, NodeStatusFlags flag)
        {
            if (!IsValidNodeID(nodeID, out var nodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[nodeIndex];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            hierarchy.ClearStatusFlag(compactNodeID, flag);
        }
    }
}