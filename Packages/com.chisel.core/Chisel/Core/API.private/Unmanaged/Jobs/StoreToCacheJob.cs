using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    // TODO: move somewhere else
    [BurstCompile(CompileSynchronously = true)]
    struct StoreToCacheJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder> allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB> brushTreeSpaceBoundCache;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

        // Read, Write
        [NoAlias] public NativeHashMap<CompactNodeID, MinMaxAABB> brushTreeSpaceBoundLookup;
        [NoAlias] public NativeHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferLookup;

        public void Execute()
        {
            brushTreeSpaceBoundLookup.Clear();
            brushRenderBufferLookup.Clear();
            for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
            {
                var nodeID = allTreeBrushIndexOrders[i].compactNodeID;
                brushTreeSpaceBoundLookup[nodeID] = brushTreeSpaceBoundCache[i];
                brushRenderBufferLookup[nodeID] = brushRenderBufferCache[i];
            }
        }
    }
}
