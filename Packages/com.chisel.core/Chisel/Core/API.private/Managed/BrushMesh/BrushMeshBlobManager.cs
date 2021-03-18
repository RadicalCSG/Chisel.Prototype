using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    static class BrushMeshGenerator
    {

        // TODO: batch & jobify this somehow
        public unsafe static BlobAssetReference<BrushMeshBlob> Build(BrushMesh brushMesh, Allocator allocator = Allocator.Persistent)
        {
            if (brushMesh == null ||
                brushMesh.vertices == null ||
                brushMesh.polygons == null ||
                brushMesh.halfEdges == null ||
                brushMesh.vertices.Length < 4 ||
                brushMesh.polygons.Length < 4 ||
                brushMesh.halfEdges.Length < 12)
                return BlobAssetReference<BrushMeshBlob>.Null;

            var srcVertices             = brushMesh.vertices;
            //var srcPlanes = brushMesh.planes;
            
            var totalPolygonSize        = 16 + (brushMesh.polygons.Length * UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>());
            var totalPlaneSize          = 16 + (brushMesh.planes.Length * UnsafeUtility.SizeOf<float4>());
            var totalPolygonIndicesSize = 16 + (brushMesh.halfEdgePolygonIndices.Length * UnsafeUtility.SizeOf<int>());
            var totalHalfEdgeSize       = 16 + (brushMesh.halfEdges.Length * UnsafeUtility.SizeOf<BrushMesh.HalfEdge>());
            var totalVertexSize         = 16 + (srcVertices.Length * UnsafeUtility.SizeOf<float3>());
            var totalSize               = totalPlaneSize + totalPolygonSize + totalPolygonIndicesSize + totalHalfEdgeSize + totalVertexSize;

            var min = srcVertices[0];
            var max = srcVertices[0];
            for (int i = 1; i < srcVertices.Length; i++)
            {
                min = math.min(min, srcVertices[i]);
                max = math.max(max, srcVertices[i]);
            }
            var localBounds = new MinMaxAABB { Min = min, Max = max };

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = localBounds;
            builder.Construct(ref root.localVertices, srcVertices);
            builder.Construct(ref root.halfEdges, brushMesh.halfEdges);
            builder.Construct(ref root.halfEdgePolygonIndices, brushMesh.halfEdgePolygonIndices);
            var polygonArray = builder.Allocate(ref root.polygons, brushMesh.polygons.Length);
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                ref var srcPolygon = ref brushMesh.polygons[p];
                ref var dstPolygon = ref polygonArray[p];
                dstPolygon.firstEdge        = srcPolygon.firstEdge;
                dstPolygon.edgeCount        = srcPolygon.edgeCount;
                dstPolygon.layerDefinition  = srcPolygon.surface?.brushMaterial?.LayerDefinition ?? SurfaceLayers.Empty;
                dstPolygon.UV0              = srcPolygon.surface?.surfaceDescription.UV0 ?? UVMatrix.identity;
            }

            builder.Construct(ref root.localPlanes, brushMesh.planes);
            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }
    }
}
