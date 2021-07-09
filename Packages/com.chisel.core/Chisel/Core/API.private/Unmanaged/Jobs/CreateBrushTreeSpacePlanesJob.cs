using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateBrushTreeSpacePlanesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly  brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                         transformationCache;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        BlobAssetReference<BrushTreeSpacePlanes> Build(ref BrushMeshBlob brushMeshBlob, float4x4 nodeToTreeTransformation)
        {
            var nodeToTreeInversed = math.inverse(nodeToTreeTransformation);
            var nodeToTreeInverseTransposed = math.transpose(nodeToTreeInversed);

            ref var localPlanes = ref brushMeshBlob.localPlanes;

            var totalSize = 16 + (localPlanes.Length * UnsafeUtility.SizeOf<float4>());

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushTreeSpacePlanes>();
            var treeSpacePlaneArray = builder.Allocate(ref root.treeSpacePlanes, localPlanes.Length);
            for (int i = 0; i < localPlanes.Length; i++)
            {
                var localPlane = localPlanes[i];
                var treePlane = math.mul(nodeToTreeInverseTransposed, localPlane);
                treePlane /= math.length(treePlane.xyz);
                treeSpacePlaneArray[i] = treePlane;
            }
            var result = builder.CreateBlobAssetReference<BrushTreeSpacePlanes>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var brushIndexOrder = allUpdateBrushIndexOrders[index];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;
            var brushMeshBlob   = brushMeshLookup[brushNodeOrder];
            if (!brushMeshBlob.IsCreated)
            {
                Debug.LogError($"BrushMeshBlob invalid for brush with index {brushIndexOrder.compactNodeID}");
                brushTreeSpacePlanes[brushNodeOrder] = BlobAssetReference<BrushTreeSpacePlanes>.Null;
                return;
            }
            var worldPlanes     = Build(ref brushMeshLookup[brushNodeOrder].Value, transformationCache[brushNodeOrder].nodeToTree);
            brushTreeSpacePlanes[brushNodeOrder] = worldPlanes;
        }
    }
}
