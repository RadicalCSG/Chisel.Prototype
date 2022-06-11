using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace Chisel.Core
{
    class CompactTreeBuilder
    {
        struct CompactTopDownBuilderNode
        {
            public CompactNodeID  compactNodeID;
            public int            compactHierarchyindex;
        }


        public static ChiselBlobAssetReference<CompactTree> Create(ref CompactHierarchy       compactHierarchy, 
                                                             NativeArray<CompactNodeID> nodes, 
                                                             NativeArray<CompactNodeID> brushes, 
                                                             CompactNodeID              treeCompactNodeID)
        {
            if (brushes.Length == 0)
                return ChiselBlobAssetReference<CompactTree>.Null;

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
                var brushCompactNodeID = brushes[b];
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

            using (var brushAncestorLegend = new NativeList<BrushAncestorLegend>(brushes.Length, Allocator.Temp))
            using (var brushAncestorsIDValues = new NativeList<int>(brushes.Length, Allocator.Temp))
            { 
                // Bottom-up -> per brush list of all ancestors to root
                for (int b = 0; b < brushes.Length; b++)
                {
                    var brushCompactNodeID = brushes[b];
                    if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID))
                        continue;

                    var parentStart = brushAncestorsIDValues.Length;

                    var parentCompactNodeID = compactHierarchy.ParentOf(brushCompactNodeID);
                    while (compactHierarchy.IsValidCompactNodeID(parentCompactNodeID) && parentCompactNodeID != treeCompactNodeID)
                    {
                        var parentCompactNodeIDValue = parentCompactNodeID.value;
                        brushAncestorsIDValues.Add(parentCompactNodeIDValue);
                        parentCompactNodeID = compactHierarchy.ParentOf(parentCompactNodeID);
                    }

                    var brushCompactNodeIDValue = brushCompactNodeID.value;
                    brushIDValueToAncestorLegend[brushCompactNodeIDValue - minBrushIDValue] = brushAncestorLegend.Length;
                    brushIDValueToOrder[brushCompactNodeIDValue - minBrushIDValue] = b;
                    brushAncestorLegend.Add(new BrushAncestorLegend()
                    {
                        ancestorEndIDValue   = brushAncestorsIDValues.Length,
                        ancestorStartIDValue = parentStart
                    });
                }

                var nodeQueue = new NativeList<CompactTopDownBuilderNode>(brushes.Length, Allocator.Temp);
                var hierarchyNodes = new NativeList<CompactHierarchyNode>(brushes.Length, Allocator.Temp);
                using (brushIDValueToAncestorLegend)
                using (brushIDValueToOrder)
                { 
                    if (brushAncestorLegend.Length == 0)
                        return ChiselBlobAssetReference<CompactTree>.Null;

                    // Top-down                    
                    nodeQueue.Add(new CompactTopDownBuilderNode { compactNodeID = treeCompactNodeID, compactHierarchyindex = 0 });
                    hierarchyNodes.Add(new CompactHierarchyNode
                    {
                        Type            = CSGNodeType.Tree,
                        Operation       = CSGOperationType.Additive,
                        CompactNodeID   = treeCompactNodeID
                    });

                    while (nodeQueue.Length > 0)
                    {
                        var parentItem          = nodeQueue[0];
                        var parentCompactNodeID = parentItem.compactNodeID;
                        nodeQueue.RemoveAt(0);
                        var nodeCount   = compactHierarchy.ChildCount(parentCompactNodeID);
                        if (nodeCount == 0)
                        {
                            var item = hierarchyNodes[parentItem.compactHierarchyindex];
                            item.childOffset = -1;
                            item.childCount = 0;
                            hierarchyNodes[parentItem.compactHierarchyindex] = item;
                            continue;
                        }

                        int firstCompactTreeIndex = 0;
                        // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                        while (firstCompactTreeIndex < nodeCount)
                        {
                            var childCompactNodeID = compactHierarchy.GetChildCompactNodeIDAt(parentItem.compactNodeID, firstCompactTreeIndex);
                            if (!compactHierarchy.IsValidCompactNodeID(childCompactNodeID))
                                break;
                            var operation = compactHierarchy.GetOperation(childCompactNodeID);
                            if (operation == CSGOperationType.Additive ||
                                operation == CSGOperationType.Copy)
                                break;
                            firstCompactTreeIndex++;
                        } 

                        var firstChildIndex = hierarchyNodes.Length;
                        for (int i = firstCompactTreeIndex; i < nodeCount; i++)
                        {
                            var childCompactNodeID = compactHierarchy.GetChildCompactNodeIDAt(parentItem.compactNodeID, i);
                            // skip invalid nodes (they don't contribute to the mesh)
                            if (!compactHierarchy.IsValidCompactNodeID(childCompactNodeID))
                                continue;

                            var childType = compactHierarchy.GetTypeOfNode(childCompactNodeID);
                            if (childType != CSGNodeType.Brush)
                                nodeQueue.Add(new CompactTopDownBuilderNode
                                {
                                    compactNodeID = childCompactNodeID,
                                    compactHierarchyindex = hierarchyNodes.Length
                                });
                            hierarchyNodes.Add(new CompactHierarchyNode
                            {
                                Type            = childType,
                                Operation       = compactHierarchy.GetOperation(childCompactNodeID),
                                CompactNodeID   = childCompactNodeID
                            });
                        }

                        {
                            var item = hierarchyNodes[parentItem.compactHierarchyindex];
                            item.childOffset = firstChildIndex;
                            item.childCount = hierarchyNodes.Length - firstChildIndex;
                            hierarchyNodes[parentItem.compactHierarchyindex] = item;
                        }
                    }

                    using (hierarchyNodes)
                    using (nodeQueue)
                    { 
                        var builder = new ChiselBlobBuilder(Allocator.Temp);
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
