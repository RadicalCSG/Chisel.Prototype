using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct InvalidateBrushesJob : IJob
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                                            compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeReference<bool>                                        needRemappingRef;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                       rebuildTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>  brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>                                    brushes;
        [NoAlias, ReadOnly] public int                                                          brushCount;
        [NoAlias, ReadOnly] public NativeList<int>                                              nodeIDValueToNodeOrder;
        [NoAlias, ReadOnly] public NativeReference<int>                                         nodeIDValueToNodeOrderOffsetRef;

        // Write
        [NoAlias, WriteOnly] public NativeParallelHashSet<IndexOrder>   brushesThatNeedIndirectUpdateHashMap;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            if (rebuildTreeBrushIndexOrders.Length == 0 &&
                rebuildTreeBrushIndexOrders.Length == brushCount && !needRemappingRef.Value)
                return;

            var nodeIDValueToNodeOrderOffset = nodeIDValueToNodeOrderOffsetRef.Value;
            for (int b = 0; b < rebuildTreeBrushIndexOrders.Length; b++)
            {
                var indexOrder          = rebuildTreeBrushIndexOrders[b];
                var brushCompactNodeID  = indexOrder.compactNodeID;
                int nodeOrder           = indexOrder.nodeOrder;

                if (!compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated))
                    continue;

                var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                if (!brushTouchedByBrush.IsCreated ||
                    brushTouchedByBrush == ChiselBlobAssetReference<BrushesTouchedByBrush>.Null)
                    continue;

                ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                for (int i = 0; i < brushIntersections.Length; i++)
                {
                    var otherBrushID = brushIntersections[i].nodeIndexOrder.compactNodeID;
                                
                    if (!compactHierarchy.IsValidCompactNodeID(otherBrushID))
                        continue;

                    // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                    if (!brushes.Contains(otherBrushID))
                        continue;

                    var otherBrushIDValue   = otherBrushID.value;
                    var otherBrushOrder     = nodeIDValueToNodeOrder[otherBrushIDValue - nodeIDValueToNodeOrderOffset];
                    var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                    brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                }
            }
        }
    }
}
