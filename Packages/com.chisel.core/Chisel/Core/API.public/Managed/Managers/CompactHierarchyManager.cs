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

    // TODO: unify/clean up error messages
    // TODO: use struct, make this non static somehow?
    // TODO: rename

    // TODO: create "ReadOnlyCompact...Manager" wrapper can be used in jobs
    public partial class CompactHierarchyManager
    {
        static NativeList<CompactHierarchy>     hierarchies;
        static IDManager                        hierarchyIDLookup;

        // Burst forces us to write unmaintable code
        internal static NativeList<CompactHierarchy> HierarchyList { get { return hierarchies; } }
        internal static ref IDManager HierarchyIDLookup { get { return ref hierarchyIDLookup; } }

        static NativeList<CompactNodeID>        nodes;
        static IDManager                        nodeIDLookup;

        // Burst forces us to write unmaintable code
        internal static NativeList<CompactNodeID> Nodes { get { return nodes; } }
        internal static ref IDManager NodeIDLookup { get { return ref nodeIDLookup; } }


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

        [return: MarshalAs(UnmanagedType.U1)]
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
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool Flush(FinishMeshUpdate finishMeshUpdates) 
        { 
            if (!UpdateAllTreeMeshes(finishMeshUpdates, out JobHandle handle)) 
                return false; 
            handle.Complete(); 
            return true; 
        }
        

        public static void GetAllTreeNodes(NativeList<CSGTreeNode> allNodes)
        {
            allNodes.Clear();

            var hierarchyCount = hierarchies.Length;
            if (hierarchyCount == 0)
                return;

            if (allNodes.Capacity < hierarchyCount)
                allNodes.Capacity = hierarchyCount;

            using (var tempNodes = new NativeList<CSGTreeNode>(Allocator.Temp))
            {
                for (int i = 0; i < hierarchyCount; i++)
                {
                    if (!hierarchies[i].IsCreated)
                        continue;
                    tempNodes.Clear();
                    hierarchies[i].GetAllNodes(tempNodes);
                    allNodes.AddRange(tempNodes);
                }
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
                var tree = CSGTree.Find(hierarchies[i].RootID);
                if (!tree.Valid)
                    continue;
                allTrees.Add(tree);
            }
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
        public static void SetVisibility(NodeID nodeID, [MarshalAs(UnmanagedType.U1)] bool visible)
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
        public static void SetPickingEnabled(NodeID nodeID, [MarshalAs(UnmanagedType.U1)] bool pickingEnabled)
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
        [return: MarshalAs(UnmanagedType.U1)]
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
        [return: MarshalAs(UnmanagedType.U1)]
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
        [return: MarshalAs(UnmanagedType.U1)]
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

        #region GetHierarchy
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID)
        {
            return ref GetHierarchy(ref hierarchyIDLookup, hierarchies, hierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static ref CompactHierarchy GetHierarchy(ref IDManager hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, CompactHierarchyID hierarchyID)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[hierarchyIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ref CompactHierarchy GetHierarchy(CSGTree tree)
        {
            return ref GetHierarchy(tree.nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static ref CompactHierarchy GetHierarchy(CompactHierarchyID hierarchyID, out int hierarchyIndex)
        {
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return ref ((CompactHierarchy*)hierarchies.GetUnsafePtr())[hierarchyIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid.", nameof(compactNodeID));

            return ref GetHierarchy(compactNodeID.hierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ref CompactHierarchy GetHierarchy(CompactNodeID compactNodeID, out int hierarchyIndex)
        {
            if (compactNodeID == default)
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid.", nameof(compactNodeID));

            return ref GetHierarchy(compactNodeID.hierarchyID, out hierarchyIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ref CompactHierarchy GetHierarchy(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            return ref GetHierarchy(nodes[index].hierarchyID);
        }
        #endregion

        internal static int GetHierarchyIndex(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"{nameof(NodeID)} is invalid.", nameof(nodeID));

            if (!IsValidNodeID(nodeID, out int index))
                throw new ArgumentException($"{nameof(NodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid, are you using an old reference?", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid, are you using an old reference?", nameof(compactNodeID));

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return hierarchyIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetHierarchyIndexUnsafe(ref IDManager hierarchyIDLookup, CompactNodeID compactNodeID)
        {
            var hierarchyID = compactNodeID.hierarchyID;
            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            return hierarchyIndex;
        }

        internal static int GetHierarchyIndex(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"{nameof(CompactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid, are you using an old reference?", nameof(compactNodeID));

            var hierarchyID = compactNodeID.hierarchyID;
            if (hierarchyID == CompactHierarchyID.Invalid)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) is invalid.", nameof(hierarchyID));

            var hierarchyIndex = hierarchyIDLookup.GetIndex(hierarchyID.value, hierarchyID.generation);
            if (hierarchyIndex < 0 || hierarchyIndex >= hierarchies.Length)
                throw new ArgumentException($"{nameof(CompactHierarchyID)} (value: {hierarchyID.value}, generation: {hierarchyID.generation}) with index {hierarchyIndex} has an invalid hierarchy (out of bounds [0...{hierarchies.Length}]), are you using an old reference?", nameof(hierarchyID));

            return hierarchyIndex;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeID(NodeID nodeID, CompactNodeID compactNodeID)
        {
            MoveNodeID(ref nodeIDLookup, nodes, nodeID, compactNodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveNodeID(ref IDManager nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID, CompactNodeID compactNodeID)
        {
            if (nodeID == NodeID.Invalid)
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            if (!nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            nodes[index] = compactNodeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactNodeID GetCompactNodeIDNoError(ref IDManager nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID, out int index)
        {
            index = -1;
            if (nodeID == NodeID.Invalid)
                return CompactNodeID.Invalid;

            index = nodeIDLookup.GetIndex(nodeID.value, nodeID.generation);
            if (index < 0 || index >= nodes.Length)
                return CompactNodeID.Invalid;

            return nodes[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactNodeID GetCompactNodeIDNoError(ref IDManager nodeIDLookup, NativeList<CompactNodeID> nodes, NodeID nodeID)
        {
            return GetCompactNodeIDNoError(ref nodeIDLookup, nodes, nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID GetCompactNodeIDNoError(NodeID nodeID)
        {
            return GetCompactNodeIDNoError(ref nodeIDLookup, nodes, nodeID, out _);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID GetCompactNodeID(NodeID nodeID)
        {
            return GetCompactNodeID(nodeID, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactNodeID GetCompactNodeID(CSGTreeNode treeNode)
        {
            return GetCompactNodeID(treeNode.nodeID, out _);
        }


        [return: MarshalAs(UnmanagedType.U1)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool IsValidHierarchyID(CompactHierarchyID hierarchyID)
        {
            return IsValidHierarchyID(hierarchyID, out _);
        }

        [BurstCompile]
        [return: MarshalAs(UnmanagedType.U1)]
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
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool IsValidNodeID(CSGTreeNode treeNode)
        {
            var nodeID = treeNode.nodeID;
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
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool IsValidNodeID(NodeID nodeID)
        {
            return IsValidNodeID(ref hierarchyIDLookup, hierarchies, nodes, nodeID);
        }

        [BurstCompile]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool IsValidNodeID(ref IDManager hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, NativeList<CompactNodeID> nodes, NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return false;

            if (!nodeIDLookup.IsCreated ||
                !nodeIDLookup.IsValidID(nodeID.value, nodeID.generation, out var index))
                return false;

            if (index < 0 || index >= nodes.Length)
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(ref hierarchyIDLookup, hierarchies, compactNodeID))
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NodeID CreateTree(Int32 userID = 0)
        {
            if (defaultHierarchyID == CompactHierarchyID.Invalid)
                Initialize();
            ref var newHierarchy = ref CreateHierarchy(userID);
            return newHierarchy.GetNodeID(newHierarchy.RootID);
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
        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool IsNodeDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.IsNodeDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool IsNodeDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return IsNodeDirty(nodes[index]);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal unsafe static bool SetChildrenDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.SetChildrenDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool SetDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.SetDirty(compactNodeID);
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool SetDirty(NodeID nodeID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            return SetDirty(nodes[index]);
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool ClearDirty(CompactNodeID compactNodeID)
        {
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            return hierarchy.ClearDirty(compactNodeID);
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static bool ClearDirty(CSGTreeNode treeNode)
        {
            if (!IsValidNodeID(treeNode.nodeID, out var index))
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
            return hierarchy.GetTypeOfNode(compactNodeID);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: MarshalAs(UnmanagedType.U1)]
        public unsafe static bool IsValidCompactNodeID(CompactNodeID compactNodeID)
        {
            return IsValidCompactNodeID(ref hierarchyIDLookup, hierarchies, compactNodeID);
        }


        [return: MarshalAs(UnmanagedType.U1)]
        internal unsafe static bool IsValidCompactNodeID(ref IDManager hierarchyIDLookup, NativeList<CompactHierarchy> hierarchies, CompactNodeID compactNodeID)
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

            return GetHierarchy(compactNodeID).GetUserIDOfNode(compactNodeID);
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

        static readonly NodeTransformations kIdentityNodeTransformation = new NodeTransformations
        {
            nodeToTree = float4x4.identity,
            treeToNode = float4x4.identity
        };


        // TODO: Optimize this, doing A LOT of redundant work here
        internal static NodeTransformations GetNodeTransformation(in CompactHierarchy hierarchy, CompactNodeID compactNodeID)
        {
            if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(compactNodeID)} (value: {compactNodeID.value}, generation: {compactNodeID.generation}) is invalid", nameof(compactNodeID));

            if (compactNodeID == hierarchy.RootID)
                return kIdentityNodeTransformation;

            ref var childNode = ref hierarchy.GetChildRef(compactNodeID);

            var localNodeToTree = childNode.transformation;
            var localTreeToNode = math.inverse(localNodeToTree);

            var parentCompactNodeID = hierarchy.ParentOf(compactNodeID);
            if (parentCompactNodeID == CompactNodeID.Invalid)
            {
                return new NodeTransformations
                {
                    nodeToTree = localNodeToTree,
                    treeToNode = localTreeToNode
                };
            }

            var parentTransformation = GetNodeTransformation(in hierarchy, parentCompactNodeID);
            return new NodeTransformations
            {
                nodeToTree = math.mul(parentTransformation.nodeToTree, localNodeToTree),
                treeToNode = math.mul(localTreeToNode, parentTransformation.treeToNode)
            };
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool SetNodeLocalTransformation(NodeID nodeID, in float4x4 result)
        {   
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            ref var nodeRef = ref hierarchy.GetChildRef(compactNodeID);
            if (math.any(nodeRef.transformation.c0 != result.c0) ||
                math.any(nodeRef.transformation.c1 != result.c1) ||
                math.any(nodeRef.transformation.c2 != result.c2) ||
                math.any(nodeRef.transformation.c3 != result.c3))
            {
                nodeRef.transformation = result;
                //nodeRef.bounds = BrushMeshManager.CalculateBounds(nodeRef.brushMeshHash, in nodeRef.transformation);

                nodeRef.flags |= NodeStatusFlags.TransformationModified;
                ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
                rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            }
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool GetTreeToNodeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: Optimize
            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var nodeTransformation = GetNodeTransformation(in hierarchy, compactNodeID);
            result = nodeTransformation.treeToNode;
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool GetNodeToTreeSpaceMatrix(NodeID nodeID, out float4x4 result)
        {
            result = float4x4.identity;
            if (!IsValidNodeID(nodeID, out var index))
                return false;

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                return false;

            // TODO: Optimize
            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var nodeTransformation = GetNodeTransformation(in hierarchy, compactNodeID);
            result = nodeTransformation.nodeToTree;
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

            return GetHierarchy(compactNodeID).GetChildRef(compactNodeID).brushMeshHash;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static bool SetBrushMeshID(NodeID nodeID, Int32 brushMeshID)
        {
            if (!IsValidNodeID(nodeID, out var index))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            var compactNodeID = nodes[index];
            if (!IsValidCompactNodeID(compactNodeID))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(nodeID)} (value: {nodeID.value}, generation: {nodeID.generation}) is invalid", nameof(nodeID));

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var result = hierarchy.SetBrushMeshID(compactNodeID, brushMeshID);
            if (result)
                hierarchy.SetTreeDirty();
            return result;
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

        [return: MarshalAs(UnmanagedType.U1)]
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
            if (nodeRef.brushMeshHash == Int32.MaxValue)
                nodeRef.flags |= NodeStatusFlags.BranchNeedsUpdate;
            else
                nodeRef.flags |= NodeStatusFlags.NeedFullUpdate;

            ref var rootNode = ref hierarchy.GetChildRef(hierarchy.RootID);
            rootNode.flags |= NodeStatusFlags.TreeNeedsUpdate;
            return true;
        }
        #endregion

        [return: MarshalAs(UnmanagedType.U1)]
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
                    var child = currHierarchy.GetChildCompactNodeIDAtInternal(compactNodeID, c);
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

            ref var hierarchy = ref GetHierarchy(compactNodeID);
            var parentCompactNodeID = hierarchy.ParentOf(compactNodeID);
            if (parentCompactNodeID == CompactNodeID.Invalid)
                return NodeID.Invalid;
            return hierarchy.GetNodeID(parentCompactNodeID);
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
                    var srcChildID = sourceHierarchy.GetChildCompactNodeIDAt(srcCompactNodeID, i);
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
        static CompactNodeID MoveChildNode(CompactNodeID compactNodeID, ref CompactHierarchy sourceHierarchy, ref CompactHierarchy destinationHierarchy, [MarshalAs(UnmanagedType.U1)] bool recursive = true)
        {
            Debug.Assert(sourceHierarchy.IsCreated, "Source hierarchy has not been initialized");
            Debug.Assert(destinationHierarchy.IsCreated, "Destination hierarchy has not been initialized");

            ref var sourceNode = ref sourceHierarchy.GetNodeRef(compactNodeID);
            if (recursive)
                return DeepMove(Int32.MaxValue, CompactNodeID.Invalid, ref destinationHierarchy, ref sourceHierarchy, ref sourceNode);

            return destinationHierarchy.CreateNode(sourceNode.nodeID, sourceNode.nodeInformation);
        }

        // Move nodes from one hierarchy to another
        public static CompactNodeID MoveChildNode(NodeID nodeID, CompactHierarchyID destinationParentID, [MarshalAs(UnmanagedType.U1)] bool recursive = true)
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

        [return: MarshalAs(UnmanagedType.U1)]
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

            var oldParentNodeID = oldParentHierarchy.ParentOf(childCompactNodeID);
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
            {
                if (oldParentNodeID == newParentCompactNodeID)
                {
                    if (oldParentHierarchy.SiblingIndexOf(childCompactNodeID) == oldParentHierarchy.ChildCount(newParentCompactNodeID) - 1)
                        return false;
                }
                
                if (oldParentHierarchy.GetTypeOfNode(childCompactNodeID) != CSGNodeType.Brush)
                {
                    // We cannot add a child to its own descendant (would create a loop)
                    if (oldParentHierarchy.IsDescendant(childCompactNodeID, newParentCompactNodeID))
                        return false;
                }
            }

            if (oldParentNodeID != CompactNodeID.Invalid)
                oldParentHierarchy.SetDirty(oldParentNodeID);

            newParentHierarchy.AttachToParent(newParentCompactNodeID, childCompactNodeID);
            SetDirty(parent);
            SetDirty(childNode);
            SetDirty(newParentHierarchy.RootID);
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal static unsafe bool InsertChildNodeRange(NodeID parent, int index, CSGTreeNode* arrayPtr, int arrayLength)
        {
            if (!IsValidNodeID(parent, out var parentNodeIndex))
                throw new ArgumentException($"The {nameof(NodeID)} {nameof(parent)} (value: {parent.value}, generation: {parent.generation}) is invalid", nameof(parent));

            var parentCompactNodeID = nodes[parentNodeIndex];
            if (!IsValidCompactNodeID(parentCompactNodeID))
                throw new ArgumentException($"The {nameof(CompactNodeID)} {nameof(parent)} (value: {parentCompactNodeID.value}, generation: {parentCompactNodeID.generation}) is invalid", nameof(parent));

            var newParentHierarchyID = parentCompactNodeID.hierarchyID;
            if (newParentHierarchyID == CompactHierarchyID.Invalid)
            {
                Debug.LogError("newParentHierarchyID == CompactHierarchyID.Invalid");
                return false;
            }

            ref var newParentHierarchy = ref GetHierarchy(newParentHierarchyID);
            var count = newParentHierarchy.ChildCount(parentCompactNodeID);
            if (index < 0 || index > count)
            {
                if (count == 0)
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' can only be 0 because since are no other children");
                else
                    Debug.LogError($"{nameof(index)} is invalid, its value '{index}' must be between 0 .. {count}");
                return false;
            }
            for (int i = 0, lastNodex = arrayLength; i < lastNodex; i++)
            {
                var childNode = arrayPtr[i].nodeID;

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

                if (childCompactNodeID == parentCompactNodeID)
                {
                    Debug.LogError("A node cannot be its own child");
                    return false;
                }

                if (currParentHierarchyID == newParentHierarchyID &&
                    oldParentHierarchy.GetTypeOfNode(childCompactNodeID) != CSGNodeType.Brush)
                {
                    // We cannot add a child to its own descendant (would create a loop)
                    if (oldParentHierarchy.IsDescendant(childCompactNodeID, parentCompactNodeID))
                    {
                        Debug.LogError("Cannot add a descendant as a child");
                        return false;
                    }
                }
            }
            
            for (int i = 0, lastNodex = arrayLength; i < lastNodex; i++)
            {
                var childNode               = arrayPtr[i].nodeID;
                var childCompactNodeID      = GetCompactNodeID(childNode, out var childIndex);
                var currParentHierarchyID   = childCompactNodeID.hierarchyID;
                ref var oldParentHierarchy  = ref GetHierarchy(currParentHierarchyID);

                var oldParentNodeID = GetParentOfNode(childNode);
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
                {
                    var oldParentCompactNodeID = GetCompactNodeIDNoError(oldParentNodeID);
                    if (oldParentCompactNodeID == parentCompactNodeID)
                    {
                        if (oldParentHierarchy.SiblingIndexOf(childCompactNodeID) == oldParentHierarchy.ChildCount(parentCompactNodeID) - 1)
                            continue;
                    }
                }


                if (oldParentNodeID != NodeID.Invalid)
                    SetDirty(oldParentNodeID);

                newParentHierarchy.AttachToParentAt(parentCompactNodeID, index + i, childCompactNodeID);
                SetDirty(childCompactNodeID);
            }
            SetDirty(parent);
            return true;
        }

        [return: MarshalAs(UnmanagedType.U1)]
        internal unsafe static bool InsertChildNode(NodeID parent, int index, NodeID item)
        {
            var treeNode = CSGTreeNode.Find(item);
            return InsertChildNodeRange(parent, index, &treeNode, 1);
        }

        [return: MarshalAs(UnmanagedType.U1)]
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
                result = AddChildNode(parent, arrayPtr[i].nodeID) && result;

            SetDirty(parent);
            return result;
        }

        [return: MarshalAs(UnmanagedType.U1)]
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

        [return: MarshalAs(UnmanagedType.U1)]
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

            var childCompactNodeID  = hierarchy.GetChildCompactNodeIDAt(parentCompactNodeID, index);
            var result              = hierarchy.DetachChildFromParentAt(parentCompactNodeID, index);
            if (result)
            {
                SetDirty(parent);
                SetDirty(childCompactNodeID);
            }
            return result;
        }

        [return: MarshalAs(UnmanagedType.U1)]
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
                    var childID = hierarchy.GetChildCompactNodeIDAtInternal(parentCompactNodeID, i);
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
            var parent = treeNode.nodeID;
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

    }
}