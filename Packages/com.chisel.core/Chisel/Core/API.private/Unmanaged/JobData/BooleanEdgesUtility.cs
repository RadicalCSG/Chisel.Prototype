using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    struct LoopSegment
    {
        public int edgeOffset;
        public int edgeLength;
        public int planesOffset;
        public int planesLength;
    }

    enum OperationResult : byte
    {
        Fail,
        Cut,
        Outside,
        Polygon1InsidePolygon2,
        Polygon2InsidePolygon1,
        Overlapping
    }

    static unsafe class BooleanEdgesUtility
    {
        const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOf([NoAlias] in NativeArray<Edge> edges, int edgesOffset, int edgesLength, Edge edge, out bool inverted)
        {
            for (int e = edgesOffset; e < edgesOffset + edgesLength; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOf([NoAlias] in NativeArray<Edge> edges, Edge edge, out bool inverted)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOf([NoAlias] in NativeListArray<Edge>.NativeList edges, Edge edge, out bool inverted)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int IndexOf([NoAlias] in UnsafeList<Edge> edges, Edge edge, out bool inverted)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe EdgeCategory IsOutsidePlanes([NoAlias] in NativeList<float4> planes, int planesOffset, int planesLength, float4 localVertex)
        {
            var planePtr = (float4*)planes.GetUnsafeReadOnlyPtr();
            for (int n = 0; n < planesLength; n++)
            {
                var distance = math.dot(planePtr[planesOffset + n], localVertex);

                // will be 'false' when distance is NaN or Infinity
                if (!(distance <= kFatPlaneWidthEpsilon))
                    return EdgeCategory.Outside;
            }
            return EdgeCategory.Inside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe bool IsOutsidePlanes([NoAlias] ref ChiselBlobArray<float4> planes, float4 localVertex)
        {
            for (int n = 0; n < planes.Length; n++)
            {
                var distance = math.dot(planes[n], localVertex);

                // will be 'false' when distance is NaN or Infinity
                if (!(distance <= kFatPlaneWidthEpsilon))
                    return true;
            }
            return false;
        }


        public unsafe static bool IsPointInPolygon(float3 right, float3 forward, [NoAlias] in NativeArray<Edge> edges, [NoAlias] in HashedVertices vertices, float3 point)
        {
            var px = math.dot(right, point);
            var py = math.dot(forward, point);

            bool result = false;
            for (int i = 0; i < edges.Length; i++)
            {
                var vert1 = vertices[edges[i].index1];
                var jx = math.dot(right, vert1);
                var jy = math.dot(forward, vert1);

                var vert2 = vertices[edges[i].index2];
                var ix = math.dot(right, vert2);
                var iy = math.dot(forward, vert2);

                if ((py >= iy && py < jy) ||
                    (py >= jy && py < iy))
                {
                    if (ix + (py - iy) / (jy - iy) * (jx - ix) < px)
                    {
                        result = !result;
                    }
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EdgeCategory CategorizeEdge(Edge edge, [NoAlias] in NativeList<float4> planes, [NoAlias] in NativeArray<Edge> edges, [NoAlias] in LoopSegment segment, [NoAlias] in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(in edges, segment.edgeOffset, segment.edgeLength, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            // TODO: shouldn't be testing against our own plane

            return IsOutsidePlanes(in planes, segment.planesOffset, segment.planesLength, new float4(midPoint, 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EdgeCategory CategorizeEdge(Edge edge, [NoAlias] ref ChiselBlobArray<float4> planes, [NoAlias] in NativeArray<Edge> edges, [NoAlias] in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(in edges, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            if (IsOutsidePlanes(ref planes, new float4(midPoint, 1)))
                return EdgeCategory.Outside;
            return EdgeCategory.Inside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EdgeCategory CategorizeEdge(Edge edge, [NoAlias] ref ChiselBlobArray<float4> planes, [NoAlias] in NativeListArray<Edge>.NativeList edges, [NoAlias] in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(in edges, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            if (IsOutsidePlanes(ref planes, new float4(midPoint, 1)))
                return EdgeCategory.Outside;
            return EdgeCategory.Inside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static EdgeCategory CategorizeEdge(Edge edge, [NoAlias] ref ChiselBlobArray<float4> planes, [NoAlias] in UnsafeList<Edge> edges, [NoAlias] in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(in edges, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            if (IsOutsidePlanes(ref planes, new float4(midPoint, 1)))
                return EdgeCategory.Outside;
            return EdgeCategory.Inside;
        }


        // Note: Assumes polygons are convex
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool AreLoopsOverlapping([NoAlias] in NativeListArray<Edge>.NativeList polygon1, [NoAlias] in NativeListArray<Edge>.NativeList polygon2)
        {
            if (polygon1.Length < 3 ||
                polygon2.Length < 3)
                return false;

            if (polygon1.Length != polygon2.Length)
                return false;

            for (int i = 0; i < polygon1.Length; i++)
            {
                if (IndexOf(in polygon2, polygon1[i], out bool _) == -1)
                    return false;
            }
            return true;
        }
        

        // Note: Assumes polygons are convex
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool AreLoopsOverlapping([NoAlias] in UnsafeList<Edge> polygon1, [NoAlias] in UnsafeList<Edge> polygon2)
        {
            if (polygon1.Length < 3 ||
                polygon2.Length < 3)
                return false;

            if (polygon1.Length != polygon2.Length)
                return false;

            for (int i = 0; i < polygon1.Length; i++)
            {
                if (IndexOf(in polygon2, polygon1[i], out bool _) == -1)
                    return false;
            }
            return true;
        }
    }
}
