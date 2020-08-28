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
    struct GatherBrushIntersectionPairsJob : IJob
    {
        // Read / Write (Sort)
        [NoAlias] public NativeArray<BrushPair>         brushBrushIntersections;

        // Write
        [NoAlias, WriteOnly] public NativeArray<int2>   brushBrushIntersectionRange;
        
        struct ListComparer : IComparer<BrushPair>
        {
            public int Compare(BrushPair x, BrushPair y)
            {
                var orderX = x.brushIndexOrder0.nodeOrder;
                var orderY = y.brushIndexOrder0.nodeOrder;
                var diff = orderX.CompareTo(orderY);
                if (diff != 0)
                    return diff;

                orderX = x.brushIndexOrder1.nodeOrder;
                orderY = y.brushIndexOrder1.nodeOrder;
                return orderX.CompareTo(orderY);
            }
        }


        public void Execute()
        {
            if (brushBrushIntersections.Length == 0)
                return;

            brushBrushIntersections.Sort(new ListComparer());

            int previousOrder = brushBrushIntersections[0].brushIndexOrder0.nodeOrder;
            int2 range = new int2(0, 1);
            for (int i = 1; i < brushBrushIntersections.Length; i++)
            {
                int currentOrder = brushBrushIntersections[i].brushIndexOrder0.nodeOrder;
                if (currentOrder != previousOrder)
                {
                    //Debug.Log($"{previousOrder} {range}");
                    brushBrushIntersectionRange[previousOrder] = range;
                    previousOrder = currentOrder;
                    range.x = i;
                    range.y = 1;
                } else
                    range.y++;
            }
            //throw new Exception($"{previousOrder} {range.x} {range.y} / {brushBrushIntersections.Length}");
            brushBrushIntersectionRange[previousOrder] = range;
        }
    }
}
