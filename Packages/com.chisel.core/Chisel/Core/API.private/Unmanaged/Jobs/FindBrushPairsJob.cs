using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushPairsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int maxOrder;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushes;

        // Read (Re-allocate) / Write
        [NoAlias] public NativeList<BrushPair2> uniqueBrushPairs;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeBitArray usedLookup;

        public void Execute()
        {
            var maxPairs = (maxOrder * maxOrder);

            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref usedLookup, maxPairs);

            uniqueBrushPairs.Clear();

            int requiredCapacity = 0;
            for (int b0 = 0; b0 < allUpdateBrushIndexOrders.Length; b0++)
            {
                var brushIndexOrder0 = allUpdateBrushIndexOrders[b0];
                int brushNodeOrder0 = brushIndexOrder0.nodeOrder;

                var brushesTouchedByBrush = brushesTouchedByBrushes[brushNodeOrder0];
                if (brushesTouchedByBrush == ChiselBlobAssetReference<BrushesTouchedByBrush>.Null)
                    continue;

                ref var intersections = ref brushesTouchedByBrush.Value.brushIntersections;
                if (intersections.Length == 0)
                    continue;

                requiredCapacity += intersections.Length + 1;
            }

            if (uniqueBrushPairs.Capacity < requiredCapacity + 1)
                uniqueBrushPairs.Capacity = requiredCapacity + 1;
            // Workaround for the incredibly dumb "can't create a stream that is zero sized" when the value is determined at runtime. Yeah, thanks
            uniqueBrushPairs.AddNoResize(new BrushPair2 { type = IntersectionType.InvalidValue });

            for (int b0 = 0; b0 < allUpdateBrushIndexOrders.Length; b0++)
            {
                var brushIndexOrder0        = allUpdateBrushIndexOrders[b0];
                int brushNodeOrder0         = brushIndexOrder0.nodeOrder;

                var brushesTouchedByBrush   = brushesTouchedByBrushes[brushNodeOrder0];
                if (brushesTouchedByBrush == ChiselBlobAssetReference<BrushesTouchedByBrush>.Null)
                    continue;
                    
                ref var intersections = ref brushesTouchedByBrush.Value.brushIntersections;
                if (intersections.Length == 0)
                    continue;

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
                        uniqueBrushPairs.AddNoResize(brushPair);
                    }
                }
            }
        }
    }
}
