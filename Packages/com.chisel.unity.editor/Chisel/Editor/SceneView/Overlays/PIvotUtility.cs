using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public static class PIvotUtility
    {
        public static Vector3 FindSelectionWorldSpaceCenter(ChiselNode selectedNode)
        {
            var hierarchyItem = selectedNode.hierarchyItem;
            var bounds = hierarchyItem.Bounds; // Note: bounds in tree space, not world space
            var modelTransform = hierarchyItem.Model.hierarchyItem.LocalToWorldMatrix;
            var center = modelTransform.MultiplyPoint((bounds.min + bounds.max) * 0.5f);
            return center;
        }

        public static Vector3 FindSelectionWorldSpaceCenter(IReadOnlyList<ChiselNode> selectedNodes)
        {
            if (selectedNodes == null || selectedNodes.Count == 0)
                return Vector3.zero;

            Vector3 center;
            var hierarchyItem = selectedNodes[0].hierarchyItem;
            var bounds = hierarchyItem.Bounds;
            var modelTransform = hierarchyItem.Model.hierarchyItem.LocalToWorldMatrix;
            var boundsCenter = modelTransform.MultiplyPoint((bounds.min + bounds.max) * 0.5f);
            if (selectedNodes.Count == 1)
                return boundsCenter;

            var min = boundsCenter;
            var max = boundsCenter;
            for (int i = 1; i < selectedNodes.Count; i++)
            {
                hierarchyItem = selectedNodes[i].hierarchyItem;
                bounds = hierarchyItem.Bounds; // Note: bounds in tree space, not world space
                modelTransform = hierarchyItem.Model.hierarchyItem.LocalToWorldMatrix;
                boundsCenter = modelTransform.MultiplyPoint((bounds.min + bounds.max) * 0.5f);

                min.x = Mathf.Min(min.x, boundsCenter.x);
                min.y = Mathf.Min(min.y, boundsCenter.y);
                min.z = Mathf.Min(min.z, boundsCenter.z);

                max.x = Mathf.Max(max.x, boundsCenter.x);
                max.y = Mathf.Max(max.y, boundsCenter.y);
                max.z = Mathf.Max(max.z, boundsCenter.z);
            }
            center = (min + max) * 0.5f;
            return center;
        }

        public static void MovePivotTo(IReadOnlyList<ChiselNode> nodes, Vector3 newPosition)
        {
            // TODO: optimize
            var nodesWithChildObjects = new HashSet<UnityEngine.Object>();
            var nodesWithChildren = new HashSet<ChiselNode>();
            foreach (var node in nodes)
            {
                var children = node.GetComponentsInChildren<ChiselNode>(includeInactive: true);
                foreach (var child in children)
                {
                    nodesWithChildren.Add(child);
                    nodesWithChildObjects.Add(child);
                    nodesWithChildObjects.Add(child.hierarchyItem.Transform);
                }
            }

            Undo.RecordObjects(nodesWithChildObjects.ToArray(), "Move Pivot");
            foreach (var node in nodesWithChildren)
                node.SetPivot(newPosition);
        }

        public static void MovePivotToCenter(IReadOnlyList<ChiselNode> nodes)
        {
            // TODO: optimize
            var nodesWithChildObjects = new HashSet<UnityEngine.Object>();
            var nodesWithChildren = new HashSet<ChiselNode>();
            foreach (var node in nodes)
            {
                var children = node.GetComponentsInChildren<ChiselNode>(includeInactive: true);
                foreach (var child in children)
                {
                    nodesWithChildren.Add(child);
                    nodesWithChildObjects.Add(child);
                    nodesWithChildObjects.Add(child.hierarchyItem.Transform);
                }
            }

            Undo.RecordObjects(nodesWithChildObjects.ToArray(), "Move Pivot");
            foreach (var node in nodesWithChildren)
            {
                var newPosition = PIvotUtility.FindSelectionWorldSpaceCenter(node);
                node.SetPivot(newPosition);
            }
        }

        public static void CenterPivotOnSelection()
        {
            var selectedNodes = ChiselSelectionManager.SelectedNodes;
            if (selectedNodes != null && selectedNodes.Count != 0)
            {
                var center = PIvotUtility.FindSelectionWorldSpaceCenter(selectedNodes);
                PIvotUtility.MovePivotTo(selectedNodes, center);
            }
        }

        public static void CenterPivotOnEachNodeInSelection()
        {
            var selectedNodes = ChiselSelectionManager.SelectedNodes;
            if (selectedNodes != null && selectedNodes.Count != 0)
            {
                PIvotUtility.MovePivotToCenter(selectedNodes);
            }
        }
    }
}
