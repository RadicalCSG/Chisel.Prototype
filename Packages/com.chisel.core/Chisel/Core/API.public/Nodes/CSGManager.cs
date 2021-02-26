using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace Chisel.Core
{
    public delegate int FinishMeshUpdate(CSGTree tree, ref VertexBufferContents vertexBufferContents, 
                                         UnityEngine.Mesh.MeshDataArray meshDataArray, 
                                         NativeList<ChiselMeshUpdate>   colliderMeshUpdates,
                                         NativeList<ChiselMeshUpdate>   debugHelperMeshes,
                                         NativeList<ChiselMeshUpdate>   renderMeshes,
                                         JobHandle                      dependencies);

    /// <summary>This class is manager class for all <see cref="Chisel.Core.CSGTreeNode"/>s.</summary>	
    public static partial class CSGManager
    {
        /// <summary>Updates all pending changes to all <see cref="Chisel.Core.CSGTree"/>s.</summary>
        /// <returns>True if any <see cref="Chisel.Core.CSGTree"/>s have been updated, false if no changes have been found.</returns>
        public static bool Flush(FinishMeshUpdate finishMeshUpdates) { if (!UpdateAllTreeMeshes(finishMeshUpdates, out JobHandle handle)) return false; handle.Complete(); return true; }

        /// <summary>Destroys all <see cref="Chisel.Core.CSGTreeNode"/>s and all <see cref="Chisel.Core.BrushMesh"/>es.</summary>
        public static void	Clear	()	{ ClearAllNodes(); }


        /// <value>The number of <see cref="Chisel.Core.CSGTreeNode"/>s.</value>
        public static int	TreeNodeCount
        {
            get { return GetNodeCount(); } 
        }

        /// <value>The number of <see cref="Chisel.Core.CSGTreeBrush"/>es.</value>
        public static int	TreeBrushCount
        {
            get { return GetBrushCount(); } 
        }
        
        /// <value>The number of <see cref="Chisel.Core.CSGTreeBranch"/>es.</value>
        public static int	TreeBranchCount
        {
            get { return GetBranchCount(); } 
        }

        /// <value>The number of <see cref="Chisel.Core.CSGTree"/>s.</value>
        public static int	TreeCount
        {
            get { return GetTreeCount(); } 
        }

        /// <value>All the <see cref="Chisel.Core.CSGTreeNode"/>s.</value>
        public static CSGTreeNode[] AllTreeNodes
        {
            get { return GetAllTreeNodes(); }
        }

        /// <value>All the <see cref="Chisel.Core.CSGTree"/>s.</value>
        public static CSGTree[] AllTrees
        {
            get { return GetAllTrees(); }
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
        public static void SetVisibility(int brushNodeID, bool visible)
        {
            if (!IsValidNodeID(brushNodeID))
                return;

            var brushNodeIndex = brushNodeID - 1;
            var state = (visible ? BrushVisibilityState.Visible : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                state |= (result & BrushVisibilityState.PickingEnabled);
            brushSelectableState[brushNodeIndex] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void SetPickingEnabled(int brushNodeID, bool pickingEnabled)
        {
            if (!IsValidNodeID(brushNodeID))
                return;

            var brushNodeIndex = brushNodeID - 1;
            var state = (pickingEnabled ? BrushVisibilityState.PickingEnabled : BrushVisibilityState.None);
            if (brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                state |= (result & BrushVisibilityState.Visible);
            brushSelectableState[brushNodeIndex] = state;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushVisible(int brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.Visible) == BrushVisibilityState.Visible;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushPickingEnabled(int brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.PickingEnabled) != BrushVisibilityState.None;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool IsBrushSelectable(int brushNodeID)
        {
            if (!IsValidNodeID(brushNodeID))
                return false;
            var brushNodeIndex = brushNodeID - 1;
            if (!brushSelectableState.TryGetValue(brushNodeIndex, out var result))
                return false;
            return (result & BrushVisibilityState.Selectable) == BrushVisibilityState.Selectable;
        }
#endif
        #endregion

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void NotifyBrushMeshModified(int brushMeshID)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var treeNodeID = nodeHierarchies[i].treeNodeID;
                if (treeNodeID == CSGTreeNode.InvalidNodeID)
                    continue;

                var brushInfo = brushOutlineStates[i];
                if (brushInfo == null ||
                    brushInfo.brushMeshInstanceID != brushMeshID)
                    continue;

                if (CSGTreeNode.IsNodeIDValid(treeNodeID))
                    CSGTreeNode.SetDirty(treeNodeID);
            }
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void NotifyBrushMeshModified(HashSet<int> modifiedBrushMeshes)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var treeNodeID = nodeHierarchies[i].treeNodeID;
                if (treeNodeID == CSGTreeNode.InvalidNodeID)
                    continue;

                var brushInfo = brushOutlineStates[i];
                if (brushInfo == null ||
                    !modifiedBrushMeshes.Contains(brushInfo.brushMeshInstanceID))
                    continue;

                if (CSGTreeNode.IsNodeIDValid(treeNodeID))
                    CSGTreeNode.SetDirty(treeNodeID);
            }
        }

        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool ClearDirty(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID)) return false;
            var flags = nodeFlags[nodeID - 1];
            switch (flags.nodeType)
            {
                case CSGNodeType.Brush:		flags.UnSetNodeFlag(NodeStatusFlags.NeedFullUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Branch:	flags.UnSetNodeFlag(NodeStatusFlags.BranchNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Tree:		flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
            }
            return false;
        }
    }
}