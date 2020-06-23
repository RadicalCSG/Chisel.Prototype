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
    struct CreateTreeSpaceVerticesAndBoundsJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                      treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                             transformations;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>               brushMeshLookup;

        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<MinMaxAABB>                                     brushTreeSpaceBounds;
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>> treeSpaceVerticesArray;

        public void Execute(int b)
        {
            var brushIndexOrder = treeBrushIndexOrders[b];
            int brushNodeIndex  = brushIndexOrder.nodeIndex;
            int brushNodeOrder  = brushIndexOrder.nodeOrder;
            var transform       = transformations[brushNodeOrder];

            var mesh                    = brushMeshLookup[brushNodeOrder];
            ref var vertices            = ref mesh.Value.localVertices;
            var nodeToTreeSpaceMatrix   = transform.nodeToTree;

            var brushTreeSpaceVerticesBlob  = BrushTreeSpaceVerticesBlob.Build(ref vertices, nodeToTreeSpaceMatrix);
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
            treeSpaceVerticesArray[brushIndexOrder.nodeOrder] = brushTreeSpaceVerticesBlob;
        }
    }
}
