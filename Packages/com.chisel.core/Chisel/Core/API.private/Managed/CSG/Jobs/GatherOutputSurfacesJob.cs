using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct GatherOutputSurfacesJob : IJob
    {
        // Read / Write (Sort)
        [NoAlias] public NativeArray<BlobAssetReference<BrushIntersectionLoop>> outputSurfaces;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int2>       outputSurfacesRange;
        
        struct ListComparer : IComparer<BlobAssetReference<BrushIntersectionLoop>>
        {
            public int Compare(BlobAssetReference<BrushIntersectionLoop> x, BlobAssetReference<BrushIntersectionLoop> y)
            {
                var orderX = x.Value.indexOrder0.nodeOrder;
                var orderY = y.Value.indexOrder0.nodeOrder;
                var diff = orderX.CompareTo(orderY);
                if (diff != 0)
                    return diff;
                orderX = x.Value.indexOrder1.nodeOrder;
                orderY = y.Value.indexOrder1.nodeOrder;
                return orderX.CompareTo(orderY);
            }
        }


        public void Execute()
        {
            if (outputSurfaces.Length == 0)
                return;

            outputSurfaces.Sort(new ListComparer());

            int previousOrder = outputSurfaces[0].Value.indexOrder0.nodeOrder;
            int2 range = new int2(0, 1);
            for (int i = 1; i < outputSurfaces.Length; i++)
            {
                int currentOrder = outputSurfaces[i].Value.indexOrder0.nodeOrder;
                if (currentOrder != previousOrder)
                {
                    //Debug.Log($"{previousOrder} {range}");
                    outputSurfacesRange[previousOrder] = range;
                    previousOrder = currentOrder;
                    range.x = i;
                    range.y = 1;
                } else
                    range.y++;
            }
            //throw new Exception($"{previousOrder} {range.x} {range.y} / {brushBrushIntersections.Length}");
            outputSurfacesRange[previousOrder] = range;
        }
    }
}
