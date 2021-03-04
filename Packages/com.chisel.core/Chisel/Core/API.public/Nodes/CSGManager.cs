using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
/*
namespace Chisel.Core.Old
{
    /// <summary>This class is manager class for all <see cref="Chisel.Core.CSGTreeNode"/>s.</summary>	
    public static partial class CSGManager
    {
        /// <summary>Updates all pending changes to all <see cref="Chisel.Core.CSGTree"/>s.</summary>
        /// <returns>True if any <see cref="Chisel.Core.CSGTree"/>s have been updated, false if no changes have been found.</returns>
        public static bool Flush(FinishMeshUpdate finishMeshUpdates) { if (!UpdateAllTreeMeshes(finishMeshUpdates, out JobHandle handle)) return false; handle.Complete(); return true; }

        /// <summary>Destroys all <see cref="Chisel.Core.CSGTreeNode"/>s and all <see cref="Chisel.Core.BrushMesh"/>es.</summary>
        public static void	Clear	()	{ ClearAllNodes(); }

        public static void GetAllTrees(List<CSGTree> allTrees)
        {
            allTrees.Clear();

            var nodeCount = trees.Count;
            if (nodeCount == 0)
                return;

            if (allTrees.Capacity < nodeCount)
                allTrees.Capacity = nodeCount;

            for (int i = 0; i < nodeCount; i++)
                allTrees.Add(new CSGTree { treeNodeID = trees[i] });
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
        public static void SetVisibility(CompactNodeID brushNodeID, bool visible)
        {
            if (!IsValidNodeID(brushNodeID))
                return;

            var brushNodeIndex = brushNodeID.ID - 1;
            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);
            brushSelectableState[brushNodeIndex] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetPickingEnabled(CompactNodeID brushNodeID, bool pickingEnabled)
        {
            if (!IsValidNodeID(brushNodeID))
                return;

            var brushNodeIndex = brushNodeID.ID - 1;
            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[brushNodeIndex] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushVisible(CompactNodeID brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID.ID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushPickingEnabled(CompactNodeID brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID.ID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushSelectable(CompactNodeID brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID.ID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.Selectable) == BrushVisibilityState.Selectable;
        }
#endif
        #endregion


        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void NotifyBrushMeshModified(HashSet<int> modifiedBrushMeshes)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var treeNodeID = nodeHierarchies[i].treeNodeID;
                if (treeNodeID == CompactNodeID.Invalid)
                    continue;

                var brushOutlineState = brushOutlineStates[i];
                if (brushOutlineState == null ||
                    !modifiedBrushMeshes.Contains(brushOutlineState.brushMeshInstanceID))
                    continue;

                if (IsValidNodeID(treeNodeID))
                    SetDirty(treeNodeID);
            }
        }
    }
}*/