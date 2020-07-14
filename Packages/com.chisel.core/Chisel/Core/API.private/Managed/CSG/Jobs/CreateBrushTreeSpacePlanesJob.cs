using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateBrushTreeSpacePlanesJob : IJobParallelFor   
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                           rebuildTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>    brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                  transformations;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes;


        BlobAssetReference<BrushTreeSpacePlanes> Build(BlobAssetReference<BrushMeshBlob> brushMeshBlob, float4x4 nodeToTreeTransformation)
        {
            if (!brushMeshBlob.IsCreated)
                return BlobAssetReference<BrushTreeSpacePlanes>.Null;

            var nodeToTreeInverseTransposed = math.transpose(math.inverse(nodeToTreeTransformation));

            var totalSize = 16 + (brushMeshBlob.Value.localPlanes.Length * UnsafeUtility.SizeOf<float4>());

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
            var treeSpacePlaneArray = builder.Allocate(ref root.treeSpacePlanes, brushMeshBlob.Value.localPlanes.Length);
            for (int i = 0; i < brushMeshBlob.Value.localPlanes.Length; i++)
            {
                var localPlane = brushMeshBlob.Value.localPlanes[i];
                treeSpacePlaneArray[i] = math.mul(nodeToTreeInverseTransposed, localPlane);
            }
            var result = builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;
            var brushMeshBlob   = brushMeshLookup[brushNodeOrder];
            if (!brushMeshBlob.IsCreated)
            {
                Debug.LogError($"BrushMeshBlob invalid for brush with index {brushIndexOrder.nodeIndex}");
                brushTreeSpacePlanes[brushNodeOrder] = BlobAssetReference<BrushTreeSpacePlanes>.Null;
                return;
            }
            var worldPlanes     = Build(brushMeshLookup[brushNodeOrder], transformations[brushNodeOrder].nodeToTree);
            brushTreeSpacePlanes[brushNodeOrder] = worldPlanes;
        }
    }
}
