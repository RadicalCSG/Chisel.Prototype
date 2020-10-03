using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct FindBrushPairsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int maxOrder;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushes;

        // Read (Re-allocate) / Write
        [NativeDisableUnsafePtrRestriction]
        [NoAlias, WriteOnly] public UnsafeList* uniqueBrushPairs;


        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeBitArray usedLookup;

        public void Execute()
        {
            var maxPairs = (maxOrder * maxOrder);

            if (!usedLookup.IsCreated || usedLookup.Length < maxPairs)
            {
                if (usedLookup.IsCreated) usedLookup.Dispose();
                usedLookup = new NativeBitArray(maxPairs, Allocator.Temp);
            } else
                usedLookup.Clear();

            for (int b0 = 0; b0 < allUpdateBrushIndexOrders.Length; b0++)
            {
                var brushIndexOrder0        = allUpdateBrushIndexOrders[b0];
                int brushNodeOrder0         = brushIndexOrder0.nodeOrder;

                var brushesTouchedByBrush   = brushesTouchedByBrushes[brushNodeOrder0];
                if (brushesTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                    continue;
                    
                ref var intersections = ref brushesTouchedByBrush.Value.brushIntersections;
                if (intersections.Length == 0)
                    continue;

                if (uniqueBrushPairs->Capacity < intersections.Length)
                    uniqueBrushPairs->Capacity = intersections.Length;

                // Find all intersections between brushes
                for (int i = 0; i < intersections.Length; i++)
                {
                    var intersection        = intersections[i];
                    var brushIndexOrder1    = intersection.nodeIndexOrder;
                    int brushNodeOrder1     = brushIndexOrder1.nodeOrder;

                    var brushPair       = new BrushPair2
                    {
                        type             = intersection.type,
                        brushIndexOrder0 = brushIndexOrder0,
                        brushIndexOrder1 = brushIndexOrder1
                    };

                    if (brushNodeOrder0 > brushNodeOrder1) // ensures we do calculations exactly the same for each brush pair
                        brushPair.Flip();

                    int testIndex = (brushPair.brushIndexOrder0.nodeOrder * maxOrder) + brushPair.brushIndexOrder1.nodeOrder;

                    if (!usedLookup.IsSet(testIndex))
                    {
                        usedLookup.Set(testIndex, true);
                        uniqueBrushPairs->AddNoResize(brushPair);
                    }
                }
            }
        }
    }
}
