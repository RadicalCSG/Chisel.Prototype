using System;
using System.Linq;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using static Chisel.Core.BrushMesh;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        public static BrushMesh CreateSphere(Vector3 diameterXYZ, bool generateFromCenter, int horzSegments, int vertSegments, SurfaceLayers layers = default, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            var lastVertSegment = vertSegments - 1;

            var triangleCount   = horzSegments + horzSegments;    // top & bottom
            var quadCount       = horzSegments * (vertSegments - 2);
            int polygonCount    = triangleCount + quadCount;
            int halfEdgeCount   = (triangleCount * 3) + (quadCount * 4);

            Vector3[] vertices = null;
            CreateSphereVertices(diameterXYZ, generateFromCenter, horzSegments, vertSegments, ref vertices);

            var polygons    = new Polygon[polygonCount];
            var halfEdges   = new HalfEdge[halfEdgeCount];

            var edgeIndex       = 0;
            var polygonIndex    = 0;
            var startVertex     = 2;
            for (int v = 0; v < vertSegments; v++)
            {
                var startEdge = edgeIndex;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p = h, h++)
                {
                    var n = (h + 1) % horzSegments;
                    int polygonEdgeCount;
                    if (v == 0) // top
                    {
                        //          0
                        //          *
                        //         ^ \
                        //     p1 /0 1\ n0
                        //       /  2  v
                        //		*<------*  
                        //     2    t    1
                        polygonEdgeCount = 3;
                        var p1 = (p * 3) + 1;
                        var n0 = (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge + (horzSegments * 3) + (h * 3) + 1) : (startEdge + (horzSegments * 3) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new HalfEdge { twinIndex = p1, vertexIndex = 0 };
                        halfEdges[edgeIndex + 1] = new HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - p };
                    }
                    else
                    if (v == lastVertSegment)
                    {
                        //     0    t    1
                        //		*------>*
                        //       ^  1  /  
                        //     p1 \0 2/ n0
                        //         \ v
                        //          *
                        //          2
                        polygonEdgeCount = 3;
                        var p2 = startEdge + (p * 3) + 2;
                        var n0 = startEdge + (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        halfEdges[edgeIndex + 0] = new HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new HalfEdge { twinIndex = n0, vertexIndex = 1 };
                    }
                    else
                    {
                        //     0    t3   1
                        //		*------>*
                        //      ^   1   |  
                        //   p1 |0     2| n0
                        //      |   3   v
                        //		*<------*
                        //     3    t1   2
                        polygonEdgeCount = 4;
                        var p1 = startEdge + (p * 4) + 2;
                        var n0 = startEdge + (n * 4) + 0;
                        var t3 = ((v == 1) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        var t1 = ((v == lastVertSegment - 1) ? (startEdge + (horzSegments * 4) + (h * 3) + 1) : (startEdge + (horzSegments * 4) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new HalfEdge { twinIndex = t3, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments };
                        halfEdges[edgeIndex + 3] = new HalfEdge { twinIndex = t1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments };
                    }

                    polygons[polygonIndex] = new Polygon
                    {
                        surfaceID = polygonIndex,
                        firstEdge = edgeIndex,
                        edgeCount = polygonEdgeCount,
                        description = new SurfaceDescription { UV0 = UVMatrix.centered, surfaceFlags = surfaceFlags, smoothingGroup = 0 },
                        layers = layers
                    };
                    
                    edgeIndex += polygonEdgeCount;
                    polygonIndex++;
                }
                if (v > 0)
                    startVertex += horzSegments;
            }

            return new BrushMesh
            {
                polygons = polygons,
                halfEdges = halfEdges,
                vertices = vertices
            };
        }

        public static void CreateSphereVertices(Vector3 diameterXYZ, bool generateFromCenter, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            //var lastVertSegment	= vertSegments - 1;
            int vertexCount = (horzSegments * (vertSegments - 1)) + 2;

            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            var radius = 0.5f * diameterXYZ;

            var offset = generateFromCenter ? 0.0f : radius.y;
            vertices[0] = Vector3.down * radius.y;
            vertices[1] = Vector3.up   * radius.y;

            vertices[0].y += offset;
            vertices[1].y += offset;

            // TODO: optimize

            var degreePerSegment = (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset = ((horzSegments & 1) == 1) ? 0.0f : ((360.0f / horzSegments) * 0.5f) * Mathf.Deg2Rad;
            for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
            {
                var segmentFactor   = ((v - (vertSegments / 2.0f)) / vertSegments); // [-0.5f ... 0.5f]
                var segmentDegree   = (segmentFactor * 180);                        // [-90 .. 90]
                var segmentHeight   = Mathf.Sin(segmentDegree * Mathf.Deg2Rad);
                var segmentRadius   = Mathf.Cos(segmentDegree * Mathf.Deg2Rad);     // [0 .. 0.707 .. 1 .. 0.707 .. 0]

                var yRingPos        = (segmentHeight * radius.y) + offset;
                var xRingRadius     = segmentRadius * radius.x;
                var zRingRadius     = segmentRadius * radius.z;

                if (radius.y < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(Mathf.Cos(hRad) * segmentRadius * radius.x,
                                                            yRingPos,
                                                            Mathf.Sin(hRad) * segmentRadius * radius.z);
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(Mathf.Cos(hRad) * segmentRadius * radius.x,
                                                            yRingPos,
                                                            Mathf.Sin(hRad) * segmentRadius * radius.z);
                    }
                }
            }
        }
    }
}