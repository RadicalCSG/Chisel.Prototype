using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

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
            public override int GetHashCode() { unchecked { return (int)GetHashCode(ref this); } }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static uint GetHashCode([ReadOnly] ref Polygon polygon) { return HashExtensions.GetHashCode(ref polygon); }
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


        public AABB                 localBounds;

        public BlobArray<float3>    localVertices;
        public BlobArray<HalfEdge>	halfEdges;
        public BlobArray<int>       halfEdgePolygonIndices;
        public BlobArray<Polygon>   polygons;
        public BlobArray<float4>    localPlanes;        // surface planes + edge planes (to reject vertices at sharp plane intersections)
        public int                  localPlaneCount;    // number of surface planes

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateHashCode([NoAlias, ReadOnly] ref BrushMeshBlob blob)
        {
            unchecked
            {
                return (int)math.hash(
                            new uint3(blob.polygons.Length      == 0 ? 0 : HashExtensions.GetHashCode(ref blob.polygons),
                                      blob.localVertices.Length == 0 ? 0 : HashExtensions.GetHashCode(ref blob.localVertices),
                                      blob.halfEdges.Length     == 0 ? 0 : HashExtensions.GetHashCode(ref blob.halfEdges)));
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
