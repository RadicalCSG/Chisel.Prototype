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
    [BurstCompile(CompileSynchronously = true)]
    public struct FillBrushMeshBlobLookupJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>> brushMeshBlobs;        
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>          allBrushMeshInstanceIDs;

        // Write
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshLookup;
        [NoAlias, WriteOnly] public NativeReference<int> surfaceCountRef;

        public void Execute()
        {
            int surfaceCount = 0;
            for (int nodeOrder = 0; nodeOrder < allBrushMeshInstanceIDs.Length; nodeOrder++)
            {
                int brushMeshID = allBrushMeshInstanceIDs[nodeOrder];
                if (brushMeshID == 0)
                {
                    // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                    brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                } else
                if (brushMeshBlobs.TryGetValue(brushMeshID - 1, out var item))
                {
                    surfaceCount += item.Value.polygons.Length;
                    brushMeshLookup[nodeOrder] = item;
                } else
                {
                    // *should* never happen
                    
                    var indexOrder  = allTreeBrushIndexOrders[nodeOrder];
                    var nodeID      = indexOrder.nodeID;
                
                    Debug.LogError($"Brush with ID {nodeID} has its brushMeshID set to {brushMeshID}, which is not initialized.");
                    brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                }
            }
            surfaceCountRef.Value = surfaceCount;
        }
    }
}
