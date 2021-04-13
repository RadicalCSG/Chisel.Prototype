using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct NativeChiselSurface
    {
        public SurfaceLayers    layerDefinition;
        public UVMatrix         UV0;
    }
    

    public struct BrushMeshBlob
    {
        public struct Polygon
        {
            public Int32                firstEdge;
            public Int32                edgeCount;
            public Int32                descriptionIndex; // An ID that can be used to identify the material of a generator
            public NativeChiselSurface  surface;

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

        /// <summary>Defines a half edge of a <see cref="BrushMeshBlob"/>.</summary>
        /// <seealso cref="BrushMeshBlob"/>
        public struct HalfEdge
        {
            /// <value>The index to the vertex of this <seealso cref="HalfEdge"/>.</value>
            public Int32 vertexIndex;

            /// <value>The index to the twin <seealso cref="HalfEdge"/> of this <seealso cref="HalfEdge"/>.</value>
            public Int32 twinIndex;

            [EditorBrowsable(EditorBrowsableState.Never)]
            public override string ToString() { return $"{{ twinIndex = {twinIndex}, vertexIndex = {vertexIndex} }}"; }
        }


        public MinMaxAABB		    localBounds;

        public BlobArray<float3>	localVertices;
        public BlobArray<HalfEdge>	halfEdges;
        public BlobArray<int>       halfEdgePolygonIndices;
        public BlobArray<Polygon>	polygons;
        public BlobArray<float4>    localPlanes;

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
