using System;

using Unity.Entities;
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
        public IndexOrder  nodeIndexOrder;
        public SurfaceInfo surfaceInfo;
        public int         startEdgeIndex;
        public int         endEdgeIndex;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BaseSurface // TODO: what is the difference with InternalChiselSurface?
	{
		public SurfaceDestinationFlags      destinationFlags;
		public SurfaceDestinationParameters destinationParameters;
        public UVMatrix UV0;
        public float4   localPlane;
    }

    struct BasePolygonsBlob
    {
        public BlobArray<BasePolygon> polygons;
        public BlobArray<Edge>        edges;
        public BlobArray<float3>      vertices;
        public BlobArray<BaseSurface> surfaces;
    }
}
