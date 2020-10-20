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
        [NoAlias] public NativeArray<BrushIntersectionLoop> outputSurfaces;

        // Write
        //[NoAlias, WriteOnly] public NativeReference<BlobAssetReference<BrushIntersectionLoops>> outputSurfaceLoops;
        [NoAlias, WriteOnly] public NativeArray<int2>       outputSurfacesRange;
        
        struct ListComparer : IComparer<BrushIntersectionLoop>
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
            /*
            var blobBuilder = new BlobBuilder(Allocator.Temp);
            ref var root = ref blobBuilder.ConstructRoot<BrushIntersectionLoops>();

            var outputLoops = blobBuilder.Allocate(ref root.loops, outputSurfaces.Length);
            for (int i = 0; i < outputSurfaces.Length; i++)
            {
                ref var outputSurface = ref outputSurfaces[i].Value;
                outputLoops[i].indexOrder0 = outputSurface.indexOrder0;
                outputLoops[i].indexOrder1 = outputSurface.indexOrder1;
                outputLoops[i].surfaceInfo = outputSurface.surfaceInfo;
                blobBuilder.Construct<float3>(ref outputLoops[i].loopVertices, ref outputSurface.loopVertices);
            }
            blobBuilder.Construct(ref root.ranges, outputSurfacesRange);


            outputSurfaceLoops.Value = blobBuilder.CreateBlobAssetReference<BrushIntersectionLoops>(Allocator.TempJob);
            */
        }
    }
}
