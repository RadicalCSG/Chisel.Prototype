using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushMeshBlob
    {
        public struct Polygon
        {
            public Int32            firstEdge;
            public Int32            edgeCount;
            public SurfaceLayers    layerDefinition;
            public UVMatrix         UV0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override unsafe int GetHashCode() { unchecked { return (int)GetHashCode(ref this); } }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe uint GetHashCode(ref Polygon polygon)
            {
                fixed (Polygon* polygonPtr = &polygon)
                {
                    return math.hash(polygonPtr, sizeof(Polygon));
                }
            }
        }

        public MinMaxAABB		                localBounds;

        public BlobArray<float3>	            localVertices;
        public BlobArray<BrushMesh.HalfEdge>	halfEdges;
        public BlobArray<int>                   halfEdgePolygonIndices;
        public BlobArray<Polygon>	            polygons;
        public BlobArray<float4>                localPlanes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override unsafe int GetHashCode()
        {
            unchecked
            {
                return (int)math.hash(
                            new uint3(math.hash(polygons     .GetUnsafePtr(), sizeof(Polygon) * polygons.Length),
                                      math.hash(localVertices.GetUnsafePtr(), sizeof(float3) * localVertices.Length),
                                      math.hash(halfEdges    .GetUnsafePtr(), sizeof(BrushMesh.HalfEdge) * halfEdges.Length)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty()
        {
            return (localPlanes.Length == 0 || polygons.Length == 0 || localVertices.Length == 0 || halfEdges.Length == 0);
        }
    }
}
