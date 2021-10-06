using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct NativeChiselSurface
    {
        public static readonly NativeChiselSurface Default = new NativeChiselSurface
        {
            layerDefinition     = SurfaceLayers.Empty,
            surfaceDescription  = SurfaceDescription.Default
        };

        public SurfaceLayers        layerDefinition;
        public SurfaceDescription   surfaceDescription;
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


        public ChiselAABB		            localBounds;

        public ChiselBlobArray<float3>	    localVertices;
        public ChiselBlobArray<HalfEdge>	halfEdges;
        public ChiselBlobArray<int>         halfEdgePolygonIndices;
        public ChiselBlobArray<Polygon>	    polygons;
        public ChiselBlobArray<float4>      localPlanes;        // surface planes + edge planes (to reject vertices at sharp plane intersections)
        public int                          localPlaneCount;    // number of surface planes

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstCompile(CompileSynchronously = true, DisableDirectCall = false)]
        public static unsafe int CalculateHashCode(ref BrushMeshBlob blob)
        {
            unchecked
            {
                return (int)math.hash(
                            new uint3(blob.polygons.Length      == 0 ? 0 : math.hash(blob.polygons.GetUnsafePtr(), UnsafeUtility.SizeOf<Polygon>() * blob.polygons.Length),
                                      blob.localVertices.Length == 0 ? 0 : math.hash(blob.localVertices.GetUnsafePtr(), UnsafeUtility.SizeOf<float3>() * blob.localVertices.Length),
                                      blob.halfEdges.Length     == 0 ? 0 : math.hash(blob.halfEdges.GetUnsafePtr(), UnsafeUtility.SizeOf<HalfEdge>() * blob.halfEdges.Length)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return CalculateHashCode(ref this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty()
        {
            return (localPlanes.Length == 0 || polygons.Length == 0 || localVertices.Length == 0 || halfEdges.Length == 0);
        }
    }
}
