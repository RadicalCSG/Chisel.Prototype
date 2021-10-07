using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{        
    [BurstCompile]
    unsafe struct FindModifiedBrushesJob : IJob
    {
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        // Read
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeArray<CompactNodeID>       brushes;
        [NoAlias, ReadOnly] public int                              brushCount;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>           allTreeBrushIndexOrders;

        // Read/Write
        [NoAlias] public NativeList<IndexOrder>                     rebuildTreeBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<NodeOrderNodeID>     transformTreeBrushIndicesList;
        [NoAlias, WriteOnly] public NativeList<NodeOrderNodeID>     brushBoundsUpdateList;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            transformTreeBrushIndicesList.Clear();
            rebuildTreeBrushIndexOrders.Clear();
            if (rebuildTreeBrushIndexOrders.Capacity < brushCount)
                rebuildTreeBrushIndexOrders.Capacity = brushCount;

            using (var usedBrushes = new NativeBitArray(brushes.Length, Allocator.Temp, NativeArrayOptions.ClearMemory))
            {
                for (int nodeOrder = 0; nodeOrder < brushes.Length; nodeOrder++)
                {
                    var brushCompactNodeID = brushes[nodeOrder];
                    if (compactHierarchy.IsAnyStatusFlagSet(brushCompactNodeID))
                    {
                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                        Debug.Assert(indexOrder.nodeOrder == nodeOrder);
                        if (!usedBrushes.IsSet(nodeOrder))
                        {
                            usedBrushes.Set(nodeOrder, true);
                            rebuildTreeBrushIndexOrders.AddNoResize(indexOrder);
                        }

                        // Fix up all flags

                        if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.ShapeModified) ||
                            compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.TransformationModified))
                        {
                            if (compactHierarchy.IsValidCompactNodeID(brushCompactNodeID))
                                brushBoundsUpdateList.Add(new NodeOrderNodeID { nodeOrder = indexOrder.nodeOrder, compactNodeID = brushCompactNodeID });
                        }

                        if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.ShapeModified))
                        {
                            // Need to update the basePolygons for this node
                            compactHierarchy.ClearStatusFlag(brushCompactNodeID, NodeStatusFlags.ShapeModified);
                            compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);
                        }

                        if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.HierarchyModified))
                            compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);

                        if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.TransformationModified))
                        {
                            if (compactHierarchy.IsValidCompactNodeID(brushCompactNodeID))
                                transformTreeBrushIndicesList.Add(new NodeOrderNodeID { nodeOrder = indexOrder.nodeOrder, compactNodeID = brushCompactNodeID });
                            compactHierarchy.ClearStatusFlag(brushCompactNodeID, NodeStatusFlags.TransformationModified);
                            compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);
                        }
                    }
                }
            }
        }
    }
}
