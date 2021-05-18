using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile]
    unsafe struct UpdateBrushOutlineJob : IJob
    {
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                            compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                       allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeHashMap<int, RefCountedBrushMeshBlob>  brushMeshBlobs;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            for (int b = 0; b < allUpdateBrushIndexOrders.Length; b++)
            {
                var brushIndexOrder     = allUpdateBrushIndexOrders[b];
                var brushNodeID         = brushIndexOrder.compactNodeID;
                var brushMeshHash       = compactHierarchy.GetBrushMeshID(brushNodeID);
                if (brushMeshBlobs.TryGetValue(brushMeshHash, out var item) && item.brushMeshBlob.IsCreated)
                    compactHierarchy.FillOutline(brushNodeID, ref item.brushMeshBlob.Value);
            }
        }
    }
}
