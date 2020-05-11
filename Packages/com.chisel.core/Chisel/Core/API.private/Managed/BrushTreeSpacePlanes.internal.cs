using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushTreeSpacePlanes
    {
        public BlobArray<float4> treeSpacePlanes;

        public static BlobAssetReference<BrushTreeSpacePlanes> Build(BlobAssetReference<BrushMeshBlob> brushMeshBlob, float4x4 nodeToTreeTransformation)
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
    }
}
