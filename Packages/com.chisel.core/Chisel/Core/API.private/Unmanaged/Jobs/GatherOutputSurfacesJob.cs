using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct GatherOutputSurfacesJob : IJob
    {
        // Read / Write (Sort)
        [NoAlias] public NativeList<BrushIntersectionLoop>  outputSurfaces;

        // Write
        //[NoAlias, WriteOnly] public NativeReference<BlobAssetReference<BrushIntersectionLoops>> outputSurfaceLoops;
        [NoAlias, WriteOnly] public NativeArray<int2>       outputSurfacesRange;
        
        struct ListComparer : System.Collections.Generic.IComparer<BrushIntersectionLoop>
        {
            public int Compare(BrushIntersectionLoop x, BrushIntersectionLoop y)
            {
                var orderX = x.indexOrder0.nodeOrder;
                var orderY = y.indexOrder0.nodeOrder;
                var diff = orderX.CompareTo(orderY);
                if (diff != 0)
                    return diff;
                orderX = x.indexOrder1.nodeOrder;
                orderY = y.indexOrder1.nodeOrder;
                return orderX.CompareTo(orderY);
            }
        }

        static readonly ListComparer listComparer = new ListComparer();


        public void Execute()
        {
            if (outputSurfaces.Length == 0)
                return;

            outputSurfaces.Sort(listComparer);

            int previousOrder = outputSurfaces[0].indexOrder0.nodeOrder;
            int2 range = new int2(0, 1);
            for (int i = 1; i < outputSurfaces.Length; i++)
            {
                int currentOrder = outputSurfaces[i].indexOrder0.nodeOrder;
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
