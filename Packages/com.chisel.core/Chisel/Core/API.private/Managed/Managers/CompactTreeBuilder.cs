using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    class CompactTreeBuilder
    {
        struct CompactTopDownBuilderNode
        {
            public CSGTreeNode  treeNode;
            public int          compactHierarchyindex;
        }

        static readonly List<BrushAncestorLegend>        s_BrushAncestorLegend    = new List<BrushAncestorLegend>();
        static readonly List<int>                        s_BrushAncestorsIDValues = new List<int>();
        static readonly Queue<CompactTopDownBuilderNode> s_NodeQueue              = new Queue<CompactTopDownBuilderNode>();
        static readonly List<CompactHierarchyNode>       s_HierarchyNodes         = new List<CompactHierarchyNode>();
        static int[]    s_BrushIDValueToAncestorLegend;
        static int[]    s_BrushIDValueToOrder;

        public static BlobAssetReference<CompactTree> Create(List<CompactNodeID> nodes, List<CSGTreeBrush> brushes, NodeID treeNodeID)
        {
            if (brushes.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            s_BrushAncestorLegend.Clear();
            s_BrushAncestorsIDValues.Clear();

            var minNodeIDValue = int.MaxValue;
            var maxNodeIDValue = 0;
            for (int b = 0; b < nodes.Count; b++)
            {
                var nodeID = nodes[b];
                if (nodeID == CompactNodeID.Invalid)
                    continue;

                var nodeIDValue = nodeID.value;
                minNodeIDValue = math.min(nodeIDValue, minNodeIDValue);
                maxNodeIDValue = math.max(nodeIDValue, maxNodeIDValue);
            }

            if (minNodeIDValue == int.MaxValue)
                minNodeIDValue = 0;

            var minBrushIDValue = int.MaxValue;
            var maxBrushIDValue = 0;
            for (int b = 0; b < brushes.Count; b++)
            {
                var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brushes[b].NodeID);
                if (brushCompactNodeID == CompactNodeID.Invalid)
                    continue;

                var brushCompactNodeIDValue = brushCompactNodeID.value;
                minBrushIDValue = math.min(brushCompactNodeIDValue, minBrushIDValue);
                maxBrushIDValue = math.max(brushCompactNodeIDValue, maxBrushIDValue);
            }

            if (minBrushIDValue == int.MaxValue)
                minBrushIDValue = 0;

            var desiredBrushIDValueToBottomUpLength = (maxBrushIDValue + 1) - minBrushIDValue;
            if (s_BrushIDValueToAncestorLegend == null ||
                s_BrushIDValueToAncestorLegend.Length < desiredBrushIDValueToBottomUpLength)
            {
                s_BrushIDValueToAncestorLegend = new int[desiredBrushIDValueToBottomUpLength];
                s_BrushIDValueToOrder = new int[desiredBrushIDValueToBottomUpLength];
            }

            // Bottom-up -> per brush list of all ancestors to root
            for (int b = 0; b < brushes.Count; b++)
            {
                var brush = brushes[b];
                if (!brush.Valid)
                    continue;

                var parentStart = s_BrushAncestorsIDValues.Count;

                var parent      = brush.Parent;
                while (parent.Valid && parent.NodeID != treeNodeID)
                {
                    var parentCompactNodeID = CompactHierarchyManager.GetCompactNodeID(parent.NodeID);
                    var parentCompactNodeIDValue = parentCompactNodeID.value;
                    s_BrushAncestorsIDValues.Add(parentCompactNodeIDValue);
                    parent = parent.Parent;
                }

                var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush.NodeID);
                var brushCompactNodeIDValue = brushCompactNodeID.value;
                s_BrushIDValueToAncestorLegend[brushCompactNodeIDValue - minBrushIDValue] = s_BrushAncestorLegend.Count;
                s_BrushIDValueToOrder[brushCompactNodeIDValue - minBrushIDValue] = b;
                s_BrushAncestorLegend.Add(new BrushAncestorLegend()
                {
                    ancestorEndIDValue   = s_BrushAncestorsIDValues.Count,
                    ancestorStartIDValue = parentStart
                });
            }

            if (s_BrushAncestorLegend.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            // Top-down
            s_NodeQueue.Clear();
            s_HierarchyNodes.Clear(); // TODO: set capacity to number of nodes in tree

            s_NodeQueue.Enqueue(new CompactTopDownBuilderNode() { treeNode = new CSGTreeNode() { nodeID = treeNodeID }, compactHierarchyindex = 0 });
            s_HierarchyNodes.Add(new CompactHierarchyNode()
            {
                Type        = CSGNodeType.Tree,
                Operation   = CSGOperationType.Additive,
                nodeID      = CompactHierarchyManager.GetCompactNodeID(treeNodeID)
            });

            while (s_NodeQueue.Count > 0)
            {
                var parent      = s_NodeQueue.Dequeue();
                var nodeCount   = parent.treeNode.Count;
                if (nodeCount == 0)
                {
                    var item = s_HierarchyNodes[parent.compactHierarchyindex];
                    item.childOffset = -1;
                    item.childCount = 0;
                    s_HierarchyNodes[parent.compactHierarchyindex] = item;
                    continue;
                }

                int firstCompactTreeIndex = 0;
                // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                for (; firstCompactTreeIndex < nodeCount && parent.treeNode[firstCompactTreeIndex].Valid &&
                                    (parent.treeNode[firstCompactTreeIndex].Operation != CSGOperationType.Additive &&
                                     parent.treeNode[firstCompactTreeIndex].Operation != CSGOperationType.Copy); firstCompactTreeIndex++)
                    // NOP
                    ;

                var firstChildIndex = s_HierarchyNodes.Count;
                for (int i = firstCompactTreeIndex; i < nodeCount; i++)
                {
                    var child = parent.treeNode[i];
                    // skip invalid nodes (they don't contribute to the mesh)
                    if (!child.Valid)
                        continue;

                    var childType = child.Type;
                    if (childType != CSGNodeType.Brush)
                        s_NodeQueue.Enqueue(new CompactTopDownBuilderNode()
                        {
                            treeNode = child,
                            compactHierarchyindex = s_HierarchyNodes.Count
                        });
                    var nodeID      = child.NodeID;
                    s_HierarchyNodes.Add(new CompactHierarchyNode()
                    {
                        Type        = childType,
                        Operation   = child.Operation,
                        nodeID      = CompactHierarchyManager.GetCompactNodeID(nodeID)
                    });
                }

                {
                    var item = s_HierarchyNodes[parent.compactHierarchyindex];
                    item.childOffset = firstChildIndex;
                    item.childCount = s_HierarchyNodes.Count - firstChildIndex;
                    s_HierarchyNodes[parent.compactHierarchyindex] = item;
                }
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CompactTree>();
            builder.Construct(ref root.compactHierarchy, s_HierarchyNodes);
            builder.Construct(ref root.brushAncestorLegend, s_BrushAncestorLegend);
            builder.Construct(ref root.brushAncestors,      s_BrushAncestorsIDValues);
            root.minBrushIDValue = minBrushIDValue;
            root.minNodeIDValue = minNodeIDValue;
            root.maxNodeIDValue = maxNodeIDValue;
            builder.Construct(ref root.brushIDValueToAncestorLegend, s_BrushIDValueToAncestorLegend, desiredBrushIDValueToBottomUpLength);
            var compactTree = builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
            builder.Dispose();

            return compactTree;
        }

    }
}
