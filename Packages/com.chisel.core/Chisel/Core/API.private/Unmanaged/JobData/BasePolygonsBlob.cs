using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct Edge : IEquatable<Edge>
    {
        public ushort index1;
        public ushort index2;

        public bool Equals(Edge other) => (index1 == other.index1 && index2 == other.index2);
        public override int GetHashCode() => (int)math.hash(new int2(index1, index2));
        public override string ToString() => $"({index1}, {index2})";
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BasePolygon
    {
        public IndexOrder       nodeIndexOrder;
        public SurfaceInfo      surfaceInfo;
        public int              startEdgeIndex;
        public int              endEdgeIndex;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BaseSurface
    {
        public SurfaceLayers    layers;
        public float4           localPlane;
        public UVMatrix         UV0;
    }

    struct BasePolygonsBlob
    {
        public ChiselBlobArray<BasePolygon>   polygons;
        public ChiselBlobArray<Edge>          edges;
        public ChiselBlobArray<float3>        vertices;
        public ChiselBlobArray<BaseSurface>   surfaces;
    }
}
