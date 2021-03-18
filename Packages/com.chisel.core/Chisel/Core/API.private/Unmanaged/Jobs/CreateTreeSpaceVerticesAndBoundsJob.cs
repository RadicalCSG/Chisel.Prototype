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
    [BurstCompile(CompileSynchronously = true)]
    struct CreateTreeSpaceVerticesAndBoundsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  rebuildTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                         transformationCache;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly  brushMeshLookup;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<MinMaxAABB>                                     brushTreeSpaceBounds;
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesCache;
        
        unsafe static BlobAssetReference<BrushTreeSpaceVerticesBlob> Build(ref BlobArray<float3> localVertices, float4x4 nodeToTreeSpaceMatrix)
        {
            var totalSize   = localVertices.Length * sizeof(float3);
            var builder     = new BlobBuilder(Allocator.Temp, math.max(4, totalSize));
            ref var root    = ref builder.ConstructRoot<BrushTreeSpaceVerticesBlob>();
            var treeSpaceVertices = builder.Allocate(ref root.treeSpaceVertices, localVertices.Length);
            for (int i = 0; i < localVertices.Length; i++)
                treeSpaceVertices[i] = math.mul(nodeToTreeSpaceMatrix, new float4(localVertices[i], 1)).xyz;
            var result = builder.CreateBlobAssetReference<BrushTreeSpaceVerticesBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        public void Execute(int b)
        {
            var brushIndexOrder = rebuildTreeBrushIndexOrders[b];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;
            var transform       = transformationCache[brushNodeOrder];

            var mesh            = brushMeshLookup[brushNodeOrder];
            if (mesh == BlobAssetReference<BrushMeshBlob>.Null ||
                !mesh.IsCreated)
                return;
            ref var vertices            = ref mesh.Value.localVertices;
            var nodeToTreeSpaceMatrix   = transform.nodeToTree;

            var brushTreeSpaceVerticesBlob  = Build(ref vertices, nodeToTreeSpaceMatrix);
            ref var brushTreeSpaceVertices  = ref brushTreeSpaceVerticesBlob.Value.treeSpaceVertices;

            var treeSpaceVertex = brushTreeSpaceVertices[0];
            var min = treeSpaceVertex;
            var max = treeSpaceVertex;
            for (int vertexIndex = 1; vertexIndex < brushTreeSpaceVertices.Length; vertexIndex++)
            {
                treeSpaceVertex = brushTreeSpaceVertices[vertexIndex];
                min = math.min(min, treeSpaceVertex); max = math.max(max, treeSpaceVertex);
            }

            var bounds = new MinMaxAABB() { Min = min, Max = max };
            brushTreeSpaceBounds[brushNodeOrder] = bounds;
            treeSpaceVerticesCache[brushIndexOrder.nodeOrder] = brushTreeSpaceVerticesBlob;
        }
    }
}
