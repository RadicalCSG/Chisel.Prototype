using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct FillBrushMeshBlobLookupJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>  brushMeshBlobs;        
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                       allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>                             allBrushMeshIDs;

        // Write
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshLookup;
        [NoAlias, WriteOnly] public NativeReference<int> surfaceCountRef;

        public void Execute()
        {
            int surfaceCount = 0;
            for (int nodeOrder = 0; nodeOrder < allBrushMeshIDs.Length; nodeOrder++)
            {
                int brushMeshHash = allBrushMeshIDs[nodeOrder];
                if (brushMeshHash != 0 &&
                    brushMeshBlobs.TryGetValue(brushMeshHash, out var item))
                {
                    surfaceCount += item.brushMeshBlob.Value.polygons.Length;
                    brushMeshLookup[nodeOrder] = item.brushMeshBlob;
                } else
                {
                    if (brushMeshHash == 0)
                    {
                        Debug.LogError($"The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly.");
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                    } else
                    {
                        // *should* never happen

                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                        var nodeID = indexOrder.compactNodeID;

                        Debug.LogError($"Brush with ID {nodeID} has its brushMeshID set to {brushMeshHash}, which is not initialized.");
                        brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                    }
                }
            }
            surfaceCountRef.Value = surfaceCount * 2; // TODO: figure out why we need to allocate more surfaces than we have?
        }
    }
}
