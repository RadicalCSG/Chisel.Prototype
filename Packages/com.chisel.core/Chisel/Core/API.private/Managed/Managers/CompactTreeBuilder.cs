using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace Chisel.Core
{
    class CompactTreeBuilder
    {
        struct CompactTopDownBuilderNode
        {
            public CSGTreeNode  treeNode;
            public int          compactHierarchyindex;
        }


        [BurstCompile]
        public static BlobAssetReference<CompactTree> Create(ref NativeList<CompactNodeID> nodes, ref NativeList<CSGTreeBrush> brushes, NodeID treeNodeID)
        {
            if (brushes.Length == 0)
                return BlobAssetReference<CompactTree>.Null;

            var minNodeIDValue = int.MaxValue;
            var maxNodeIDValue = 0;
            for (int b = 0; b < nodes.Length; b++)
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
            for (int b = 0; b < brushes.Length; b++)
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

            var brushIDValueToAncestorLegend = new NativeArray<int>(desiredBrushIDValueToBottomUpLength, Allocator.Temp);
            var brushIDValueToOrder = new NativeArray<int>(desiredBrushIDValueToBottomUpLength, Allocator.Temp);

            using (var brushAncestorLegend = new NativeList<BrushAncestorLegend>(Allocator.Temp))
            using (var brushAncestorsIDValues = new NativeList<int>(Allocator.Temp))
            { 
                // Bottom-up -> per brush list of all ancestors to root
                for (int b = 0; b < brushes.Length; b++)
                {
                    var brush = brushes[b];
                    if (!brush.Valid)
                        continue;

                    var parentStart = brushAncestorsIDValues.Length;

                    var parent      = brush.Parent;
                    while (parent.Valid && parent.NodeID != treeNodeID)
                    {
                        var parentCompactNodeID = CompactHierarchyManager.GetCompactNodeID(parent.NodeID);
                        var parentCompactNodeIDValue = parentCompactNodeID.value;
                        brushAncestorsIDValues.Add(parentCompactNodeIDValue);
                        parent = parent.Parent;
                    }

                    var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush.NodeID);
                    var brushCompactNodeIDValue = brushCompactNodeID.value;
                    brushIDValueToAncestorLegend[brushCompactNodeIDValue - minBrushIDValue] = brushAncestorLegend.Length;
                    brushIDValueToOrder[brushCompactNodeIDValue - minBrushIDValue] = b;
                    brushAncestorLegend.Add(new BrushAncestorLegend()
                    {
                        ancestorEndIDValue   = brushAncestorsIDValues.Length,
                        ancestorStartIDValue = parentStart
                    });
                }

                var nodeQueue = new NativeQueue<CompactTopDownBuilderNode>(Allocator.Temp);
                var hierarchyNodes = new NativeList<CompactHierarchyNode>(Allocator.Temp);
                using (brushIDValueToAncestorLegend)
                using (brushIDValueToOrder)
                { 

                    if (brushAncestorLegend.Length == 0)
                        return BlobAssetReference<CompactTree>.Null;

                    // Top-down                    
                    nodeQueue.Enqueue(new CompactTopDownBuilderNode() { treeNode = new CSGTreeNode() { nodeID = treeNodeID }, compactHierarchyindex = 0 });
                    hierarchyNodes.Add(new CompactHierarchyNode()
                    {
                        Type        = CSGNodeType.Tree,
                        Operation   = CSGOperationType.Additive,
                        nodeID      = CompactHierarchyManager.GetCompactNodeID(treeNodeID)
                    });

                    while (nodeQueue.Count > 0)
                    {
                        var parent      = nodeQueue.Dequeue();
                        var nodeCount   = parent.treeNode.Count;
                        if (nodeCount == 0)
                        {
                            var item = hierarchyNodes[parent.compactHierarchyindex];
                            item.childOffset = -1;
                            item.childCount = 0;
                            hierarchyNodes[parent.compactHierarchyindex] = item;
                            continue;
                        }

                        int firstCompactTreeIndex = 0;
                        // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                        for (; firstCompactTreeIndex < nodeCount && parent.treeNode[firstCompactTreeIndex].Valid &&
                                            (parent.treeNode[firstCompactTreeIndex].Operation != CSGOperationType.Additive &&
                                             parent.treeNode[firstCompactTreeIndex].Operation != CSGOperationType.Copy); firstCompactTreeIndex++)
                            // NOP
                            ;

                        var firstChildIndex = hierarchyNodes.Length;
                        for (int i = firstCompactTreeIndex; i < nodeCount; i++)
                        {
                            var child = parent.treeNode[i];
                            // skip invalid nodes (they don't contribute to the mesh)
                            if (!child.Valid)
                                continue;

                            var childType = child.Type;
                            if (childType != CSGNodeType.Brush)
                                nodeQueue.Enqueue(new CompactTopDownBuilderNode()
                                {
                                    treeNode = child,
                                    compactHierarchyindex = hierarchyNodes.Length
                                });
                            var nodeID      = child.NodeID;
                            hierarchyNodes.Add(new CompactHierarchyNode()
                            {
                                Type        = childType,
                                Operation   = child.Operation,
                                nodeID      = CompactHierarchyManager.GetCompactNodeID(nodeID)
                            });
                        }

                        {
                            var item = hierarchyNodes[parent.compactHierarchyindex];
                            item.childOffset = firstChildIndex;
                            item.childCount = hierarchyNodes.Length - firstChildIndex;
                            hierarchyNodes[parent.compactHierarchyindex] = item;
                        }
                    }

                    using (hierarchyNodes)
                    using (nodeQueue)
                    { 
                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref builder.ConstructRoot<CompactTree>();
                        builder.Construct(ref root.compactHierarchy, hierarchyNodes);
                        builder.Construct(ref root.brushAncestorLegend, brushAncestorLegend);
                        builder.Construct(ref root.brushAncestors,      brushAncestorsIDValues);
                        root.minBrushIDValue = minBrushIDValue;
                        root.minNodeIDValue = minNodeIDValue;
                        root.maxNodeIDValue = maxNodeIDValue;
                        builder.Construct(ref root.brushIDValueToAncestorLegend, brushIDValueToAncestorLegend, desiredBrushIDValueToBottomUpLength);
                        var compactTree = builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
                        builder.Dispose();
                        return compactTree;
                    }
                }
            }
        }

    }
}
