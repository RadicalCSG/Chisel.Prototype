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
            for (int index = 0; index < allUpdateBrushIndexOrders.Length; index++)
            {
                var brushIndexOrder     = allUpdateBrushIndexOrders[index];
                var brushNodeID         = brushIndexOrder.compactNodeID;
                var brushMeshHash       = compactHierarchy.GetBrushMeshID(brushNodeID);
                if (brushMeshBlobs.TryGetValue(brushMeshHash, out var item) && item.brushMeshBlob.IsCreated)
                    compactHierarchy.FillOutline(brushNodeID, ref item.brushMeshBlob.Value);
            }
        }
    }
}
