using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Entities;

namespace Chisel.Core
{
    public struct LoopSegment
    {
        public int edgeOffset;
        public int edgeLength;
        public int planesOffset;
        public int planesLength;
    }

    public enum OperationResult : byte
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


        public static int IndexOf(NativeArray<Edge> edges, int edgesOffset, int edgesLength, Edge edge, out bool inverted)
        {
            for (int e = edgesOffset; e < edgesOffset + edgesLength; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }
        public static int IndexOf(NativeListArray<Edge>.NativeList edges, int edgesOffset, int edgesLength, Edge edge, out bool inverted)
        {
            for (int e = edgesOffset; e < edgesOffset + edgesLength; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        public static int IndexOf(NativeArray<Edge> edges, Edge edge, out bool inverted)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        public static int IndexOf(NativeListArray<Edge>.NativeList edges, Edge edge, out bool inverted)
        {
            for (int e = 0; e < edges.Length; e++)
            {
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true; return e; }
            }
            inverted = false;
            return -1;
        }

        public static unsafe EdgeCategory IsOutsidePlanes(NativeList<float4> planes, int planesOffset, int planesLength, float4 localVertex)
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

        public static unsafe bool IsOutsidePlanes(ref BlobArray<float4> planes, float4 localVertex)
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



        public unsafe static bool IsPointInPolygon(float3 right, float3 forward, NativeArray<Edge> edges, in HashedVertices vertices, float3 point)
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

        public static EdgeCategory CategorizeEdge(Edge edge, in NativeList<float4> planes, in NativeArray<Edge> edges, in LoopSegment segment, in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(edges, segment.edgeOffset, segment.edgeLength, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            // TODO: shouldn't be testing against our own plane

            return IsOutsidePlanes(planes, segment.planesOffset, segment.planesLength, new float4(midPoint, 1));
        }

        internal static EdgeCategory CategorizeEdge(Edge edge, ref BlobArray<float4> planes, in NativeArray<Edge> edges, in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(edges, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            if (IsOutsidePlanes(ref planes, new float4(midPoint, 1)))
                return EdgeCategory.Outside;
            return EdgeCategory.Inside;
        }

        internal static EdgeCategory CategorizeEdge(Edge edge, ref BlobArray<float4> planes, in NativeListArray<Edge>.NativeList edges, in HashedVertices vertices)
        {
            // TODO: use something more clever than looping through all edges
            if (IndexOf(edges, edge, out bool inverted) != -1)
                return (inverted) ? EdgeCategory.ReverseAligned : EdgeCategory.Aligned;
            var midPoint = (vertices[edge.index1] + vertices[edge.index2]) * 0.5f;

            if (IsOutsidePlanes(ref planes, new float4(midPoint, 1)))
                return EdgeCategory.Outside;
            return EdgeCategory.Inside;
        }


        // Note: Assumes polygons are convex
        public unsafe static bool AreLoopsOverlapping(NativeListArray<Edge>.NativeList polygon1, NativeListArray<Edge>.NativeList polygon2)
        {
            if (polygon1.Length < 3 ||
                polygon2.Length < 3)
                return false;

            if (polygon1.Length != polygon2.Length)
                return false;

            for (int i = 0; i < polygon1.Length; i++)
            {
                if (IndexOf(polygon2, polygon1[i], out bool _) == -1)
                    return false;
            }
            return true;
        }
    }
}
