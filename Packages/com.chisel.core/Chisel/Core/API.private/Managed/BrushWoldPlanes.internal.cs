using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushWorldPlanes
    {
        public BlobArray<float4> worldPlanes;

        public static BlobAssetReference<BrushWorldPlanes> Build(BlobAssetReference<BrushMeshBlob> brushMeshBlob, float4x4 nodeToTreeTransformation)
        {
            if (!brushMeshBlob.IsCreated)
                return BlobAssetReference<BrushWorldPlanes>.Null;

            var nodeToTreeInverseTransposed = math.transpose(math.inverse(nodeToTreeTransformation));
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushWorldPlanes>();
            var worldPlaneArray = builder.Allocate(ref root.worldPlanes, brushMeshBlob.Value.localPlanes.Length);
            for (int i = 0; i < brushMeshBlob.Value.localPlanes.Length; i++)
            {
                var localPlane = brushMeshBlob.Value.localPlanes[i];
                worldPlaneArray[i] = math.mul(nodeToTreeInverseTransposed, localPlane);
            }
            var result = builder.CreateBlobAssetReference<BrushWorldPlanes>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
