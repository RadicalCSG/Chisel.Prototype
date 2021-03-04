using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
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
                var nodeID = allTreeBrushIndexOrders[i].nodeID;
                brushTreeSpaceBoundLookup[nodeID] = brushTreeSpaceBoundCache[i];
                brushRenderBufferLookup[nodeID] = brushRenderBufferCache[i];
            }
        }
    }
}
