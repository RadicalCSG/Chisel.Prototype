using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        // TODO: clean up
        public static bool GenerateSegmentedSubMesh(ref BrushMesh brushMesh, int horzSegments, int vertSegments, Vector3[] segmentVertices, bool topCap, bool bottomCap, int topVertex, int bottomVertex, in ChiselSurfaceDefinition surfaceDefinition)
        {
            // FIXME: hack, to fix math below .. 
            vertSegments++;

            //if (bottomCap || topCap)
            //	vertSegments++;

            int triangleCount, quadCount, capCount, extraVertices;

            capCount		= 0;
            triangleCount	= 0;
            extraVertices	= 0;
            if (topCap)
            {
                capCount			+= 1;
            } else
            {
                extraVertices		+= 1;
                triangleCount		+= horzSegments;
            }

            if (bottomCap)
            {
                capCount			+= 1;
            } else
            {
                extraVertices		+= 1;
                triangleCount		+= horzSegments;
            }

            quadCount = horzSegments * (vertSegments - 2);

            var vertexCount			= (horzSegments * (vertSegments - 1)) + extraVertices;
            var assetPolygonCount	= triangleCount + quadCount + capCount;
            var halfEdgeCount		= (triangleCount * 3) + (quadCount * 4) + (capCount * horzSegments);

            if (segmentVertices.Length != vertexCount)
            {
                Debug.LogError("segmentVertices.Length (" + segmentVertices.Length + ") != expectedVertexCount (" + vertexCount + ")");
                brushMesh.Clear();
                return false;
            }

            var vertices		= segmentVertices;
            var	polygons		= new BrushMesh.Polygon[assetPolygonCount];
            var halfEdges		= new BrushMesh.HalfEdge[halfEdgeCount];

            var twins			= new int[horzSegments];

            var edgeIndex		= 0;
            var polygonIndex	= 0;
            var startVertex		= extraVertices;
            var startSegment	= topCap ? 1 : 0;
            var lastVertSegment	= vertSegments - 1;
            var endSegment		= bottomCap ? lastVertSegment : vertSegments;
            
            if (topCap)
            {
                var polygonEdgeCount	= horzSegments;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p = h, h++)
                {
                    var currEdgeIndex = edgeIndex + (horzSegments - 1) - h;
                    halfEdges[currEdgeIndex] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p };
                    twins[h] = currEdgeIndex;
                }
                polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, surface = surfaceDefinition.surfaces[0] };
                edgeIndex += polygonEdgeCount;
                polygonIndex++;
            }

            for (int v = startSegment; v < endSegment; v++)
            {
                var startEdge   = edgeIndex;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p=h, h++)
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
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = topVertex };
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p};
                        twins[h] = edgeIndex + 2;
                    } else
                    if (v == lastVertSegment) // bottom
                    {
                        //     0    t    1
                        //		*------>*
                        //       ^  1  /  
                        //     p2 \0 2/ n0
                        //         \ v
                        //          *
                        //          2
                        polygonEdgeCount = 3;
                        var p2 = startEdge + (p * 3) + 2;
                        var n0 = startEdge + (n * 3) + 0;
                        var t  = twins[h];
                        halfEdges[twins[h]].twinIndex = edgeIndex + 1;
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = bottomVertex };
                    } else
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
                        var t  = twins[h];
                        halfEdges[twins[h]].twinIndex = edgeIndex + 1;
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments};
                        halfEdges[edgeIndex + 3] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments};
                        twins[h] = edgeIndex + 3;
                    }
                    polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, surface = surfaceDefinition.surfaces[0] };
                    edgeIndex += polygonEdgeCount;
                    polygonIndex++;
                }
                if (v > 0)
                    startVertex += horzSegments;
            }
            if (bottomCap)
            {
                var polygonEdgeCount	= horzSegments;
                for (int h = 0; h < horzSegments; h++)
                {
                    var currEdgeIndex = edgeIndex + h; 
                    halfEdges[twins[h]].twinIndex = currEdgeIndex;
                    halfEdges[currEdgeIndex] = new BrushMesh.HalfEdge { twinIndex = twins[h], vertexIndex = startVertex + (horzSegments - 1) - h };
                }
                polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, surface = surfaceDefinition.surfaces[0] };
            }
            
            brushMesh.polygons	= polygons;
            brushMesh.halfEdges	= halfEdges;
            brushMesh.vertices = new float3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                brushMesh.vertices[i] = vertices[i];
            return true;
        }

        enum SegmentTopology : sbyte
        {
            Quad = 0, 
            TrianglesNegative = -1,
            TrianglesPositive = 1,
            TriangleNegative = -2,
            TrianglePositive = 2,
            None = 3
        }

        static SegmentTopology[]    segmentTopology;
        static int[]                edgeIndices;


        public static bool CreateExtrudedSubMesh(ref BrushMesh brushMesh, int segments, int[] segmentDescriptionIndices, int segmentTopIndex, int segmentBottomIndex, Vector3[] vertices, in ChiselSurfaceDefinition surfaceDefinition)
        {
            if (vertices.Length < 3)
                return false;

            // TODO: vertex reverse winding when it's not clockwise
            // TODO: handle duplicate vertices, remove them or avoid them being created in the first place (maybe use indices?)

            if (segmentTopology == null ||
                segmentTopology.Length < segments)
                segmentTopology = new SegmentTopology[segments];

            if (edgeIndices == null ||
                edgeIndices.Length < segments * 2)
                edgeIndices = new int[segments * 2];

            var polygonCount = 2;
            var edgeOffset = segments + segments;
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //			 	  t0	 
                //	 ------>   ------->   -----> 
                //        v0 *          * v1
                //	 <-----    <-------   <-----
                //			^ |   e2   ^ |
                //          | |        | |
                //	   q1 p0| |e3 q2 e5| |n0 q3
                //          | |        | |
                //			| v   e4   | v
                //	 ------>   ------->   ----->
                //        v3 *          * v2
                //	 <-----    <-------   <-----  
                //			      b0    
                //
                var v0 = vertices[p];
                var v1 = vertices[e];
                var v2 = vertices[e + segments];
                var v3 = vertices[p + segments];

                var equals03 = math.lengthsq(v0 - v3) < 0.0001f;
                var equals12 = math.lengthsq(v1 - v2) < 0.0001f;
                if (equals03)
                {
                    if (equals12)
                        segmentTopology[n] = SegmentTopology.None;
                    else
                        segmentTopology[n] = SegmentTopology.TriangleNegative;
                } else
                if (equals12)
                {
                    segmentTopology[n] = SegmentTopology.TrianglePositive;
                } else
                {

                    var plane = new Plane(v0, v1, v3);
                    var dist = plane.GetDistanceToPoint(v2);
                    if (UnityEngine.Mathf.Abs(dist) < 0.001f)
                        segmentTopology[n] = SegmentTopology.Quad;
                    else
                        segmentTopology[n] = (SegmentTopology)UnityEngine.Mathf.Sign(dist);
                }
                
                switch (segmentTopology[n])
                {
                    case SegmentTopology.Quad:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 4;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount += 2;
                        edgeOffset += 6;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 3;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.None:
                        edgeIndices[(n * 2) + 0] = 0;
                        edgeIndices[(n * 2) + 1] = 0;
                        break;
                }
            }

            var polygons = new BrushMesh.Polygon[polygonCount];

            var surface0 = surfaceDefinition.surfaces[segmentTopIndex];
            var surface1 = surfaceDefinition.surfaces[segmentBottomIndex];
            
            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge = 0,        edgeCount = segments, surface = surface0 };
            polygons[1] = new BrushMesh.Polygon { surfaceID = 1, firstEdge = segments, edgeCount = segments, surface = surface1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s]);
                var firstEdge = edgeIndices[(s * 2) + 0] - 1;
                switch (segmentTopology[s])
                {
                    case SegmentTopology.Quad:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 4, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                            //			unless there's already a smoothing group for this edge; then use that

                        polygons[surfaceID + 0] = new BrushMesh.Polygon { surfaceID = surfaceID + 0, firstEdge = firstEdge,     edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        polygons[surfaceID + 1] = new BrushMesh.Polygon { surfaceID = surfaceID + 1, firstEdge = firstEdge + 3, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID += 2;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.None:
                    {
                        break;
                    }
                }
            }

            var halfEdges = new BrushMesh.HalfEdge[edgeOffset];
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    default:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                }
            }

            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                var nn = (n + 1) % segments;

                var p0 = edgeIndices[(e  * 2) + 1];
                var n0 = edgeIndices[(nn * 2) + 0];

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        continue;
                    }
                    case SegmentTopology.Quad:
                    {
                        //			 	  t0	 
                        //	 ------>   ------->   -----> 
                        //        v0 *          * v1
                        //	 <-----    <-------   <-----
                        //			^ |   e2   ^ |
                        //          | |        | |
                        //	   q1 p0| |e3 q2 e5| |n0 q3
                        //          | |        | |
                        //			| v   e4   | v
                        //	 ------>   ------->   ----->
                        //        v3 *          * v2
                        //	 <-----    <-------   <-----  
                        //			      b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |     e2   ^ ^ |
                        //          | |        / / | |
                        //          | |    e4/ /   | |
                        //	   q1 p0| |e3   / /  e7| |n0 q3
                        //          | |   / /e5    | |
                        //          | | / /        | |
                        //			| v/v   e6     | v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e6;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesPositive:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |^\   e5     ^ |
                        //          | | \ \        | |
                        //          | |   \ \ e6   | |
                        //	   q1 p0| |e3 e2\ \  e7| |n0 q3
                        //          | |       \ \  | |
                        //          | |        \ \ | |
                        //			| v   e4    \ v| v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e5;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    {
                        //			 	  t0	 
                        //	 --->                      -------> 
                        //       \                  ^ * v1
                        //	 <--- \                /  ^   <-----
                        //	     \ \              / / | |
                        //	      \ \            / /  | |
                        //	       \ \      t0  / /   | |
                        //	        \ \        / /e2  | | n0
                        //           \ \      / /     | |
                        //	          \ \    / / q2 e4| |   q3
                        //             \ \  / /       | |
                        //	            v \/ v   e3   | v
                        //	 ------------>   ------->   ----->
                        //              v3 *          * v2
                        //	 <-----------    <-------   <-----  
                        //			            b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e3;

                        // vi0 / vi3
                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };

                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        //			 	      	 
                        //	 ------->                            ---->
                        //        v0 * \                       ^ 
                        //	 <-----     \                     /  <---
                        //			^ |^ \                   / /
                        //          | | \ \                 / /
                        //          | |  \ \               / /
                        //          | |   \ \ t0          / /
                        //   q1     | | e2 \ \           / /
                        //          | |     \ \         / /
                        //	      p0| |e3    \ \       / /
                        //          | |   q2  \ \     / /
                        //          | |        \ \   / /
                        //			| v   e4    \ v / v 
                        //	 ------>   ----------->    ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------    <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        // vi1 / vi2
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        break;
                    }
                }
            }

            brushMesh.polygons = polygons;
            brushMesh.halfEdges = halfEdges;
            brushMesh.vertices = new float3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                brushMesh.vertices[i] = vertices[i];
            brushMesh.CalculatePlanes();
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
            return true;
        }



        public static bool CreateExtrudedSubMesh(ref BrushMesh brushMesh, int segments, int[] segmentDescriptionIndices, int segmentTopIndex, int segmentBottomIndex, float3[] vertices, in ChiselSurfaceDefinition surfaceDefinition)
        {
            if (vertices.Length < 3)
                return false;

            // TODO: vertex reverse winding when it's not clockwise
            // TODO: handle duplicate vertices, remove them or avoid them being created in the first place (maybe use indices?)
            
            if (segmentTopology == null ||
                segmentTopology.Length < segments)
                segmentTopology = new SegmentTopology[segments];

            if (edgeIndices == null ||
                edgeIndices.Length < segments * 2)
                edgeIndices = new int[segments * 2];

            var polygonCount = 2;
            var edgeOffset = segments + segments;
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //			 	  t0	 
                //	 ------>   ------->   -----> 
                //        v0 *          * v1
                //	 <-----    <-------   <-----
                //			^ |   e2   ^ |
                //          | |        | |
                //	   q1 p0| |e3 q2 e5| |n0 q3
                //          | |        | |
                //			| v   e4   | v
                //	 ------>   ------->   ----->
                //        v3 *          * v2
                //	 <-----    <-------   <-----  
                //			      b0    
                //
                var v0 = vertices[p];
                var v1 = vertices[e];
                var v2 = vertices[e + segments];
                var v3 = vertices[p + segments];

                var equals03 = math.lengthsq(v0 - v3) < 0.0001f;
                var equals12 = math.lengthsq(v1 - v2) < 0.0001f;
                if (equals03)
                {
                    if (equals12)
                        segmentTopology[n] = SegmentTopology.None;
                    else
                        segmentTopology[n] = SegmentTopology.TriangleNegative;
                } else
                if (equals12)
                {
                    segmentTopology[n] = SegmentTopology.TrianglePositive;
                } else
                {

                    var plane = new Plane(v0, v1, v3);
                    var dist = plane.GetDistanceToPoint(v2);
                    if (UnityEngine.Mathf.Abs(dist) < 0.001f)
                        segmentTopology[n] = SegmentTopology.Quad;
                    else
                        segmentTopology[n] = (SegmentTopology)UnityEngine.Mathf.Sign(dist);
                }
                
                switch (segmentTopology[n])
                {
                    case SegmentTopology.Quad:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 4;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount += 2;
                        edgeOffset += 6;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 3;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.None:
                        edgeIndices[(n * 2) + 0] = 0;
                        edgeIndices[(n * 2) + 1] = 0;
                        break;
                }
            }

            var polygons = brushMesh.polygons;
            if (polygons == null ||
                polygons.Length != polygonCount)
                polygons = new BrushMesh.Polygon[polygonCount];

            var surface0 = surfaceDefinition.surfaces[segmentTopIndex];
            var surface1 = surfaceDefinition.surfaces[segmentBottomIndex];
            
            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge = 0,        edgeCount = segments, surface = surface0 };
            polygons[1] = new BrushMesh.Polygon { surfaceID = 1, firstEdge = segments, edgeCount = segments, surface = surface1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s] + 2);
                var firstEdge = edgeIndices[(s * 2) + 0] - 1;
                switch (segmentTopology[s])
                {
                    case SegmentTopology.Quad:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 4, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                            //			unless there's already a smoothing group for this edge; then use that

                        polygons[surfaceID + 0] = new BrushMesh.Polygon { surfaceID = surfaceID + 0, firstEdge = firstEdge,     edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        polygons[surfaceID + 1] = new BrushMesh.Polygon { surfaceID = surfaceID + 1, firstEdge = firstEdge + 3, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID += 2;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.None:
                    {
                        break;
                    }
                }
            }
            
            var halfEdges = brushMesh.halfEdges;
            if (halfEdges == null ||
                halfEdges.Length != edgeOffset)
                halfEdges = new BrushMesh.HalfEdge[edgeOffset];
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    default:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                }
            }

            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                var nn = (n + 1) % segments;

                var p0 = edgeIndices[(e  * 2) + 1];
                var n0 = edgeIndices[(nn * 2) + 0];

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        continue;
                    }
                    case SegmentTopology.Quad:
                    {
                        //			 	  t0	 
                        //	 ------>   ------->   -----> 
                        //        v0 *          * v1
                        //	 <-----    <-------   <-----
                        //			^ |   e2   ^ |
                        //          | |        | |
                        //	   q1 p0| |e3 q2 e5| |n0 q3
                        //          | |        | |
                        //			| v   e4   | v
                        //	 ------>   ------->   ----->
                        //        v3 *          * v2
                        //	 <-----    <-------   <-----  
                        //			      b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |     e2   ^ ^ |
                        //          | |        / / | |
                        //          | |    e4/ /   | |
                        //	   q1 p0| |e3   / /  e7| |n0 q3
                        //          | |   / /e5    | |
                        //          | | / /        | |
                        //			| v/v   e6     | v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e6;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesPositive:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |^\   e5     ^ |
                        //          | | \ \        | |
                        //          | |   \ \ e6   | |
                        //	   q1 p0| |e3 e2\ \  e7| |n0 q3
                        //          | |       \ \  | |
                        //          | |        \ \ | |
                        //			| v   e4    \ v| v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e5;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    {
                        //			 	  t0	 
                        //	 --->                      -------> 
                        //       \                  ^ * v1
                        //	 <--- \                /  ^   <-----
                        //	     \ \              / / | |
                        //	      \ \            / /  | |
                        //	       \ \      t0  / /   | |
                        //	        \ \        / /e2  | | n0
                        //           \ \      / /     | |
                        //	          \ \    / / q2 e4| |   q3
                        //             \ \  / /       | |
                        //	            v \/ v   e3   | v
                        //	 ------------>   ------->   ----->
                        //              v3 *          * v2
                        //	 <-----------    <-------   <-----  
                        //			            b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e3;

                        // vi0 / vi3
                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };

                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        //			 	      	 
                        //	 ------->                            ---->
                        //        v0 * \                       ^ 
                        //	 <-----     \                     /  <---
                        //			^ |^ \                   / /
                        //          | | \ \                 / /
                        //          | |  \ \               / /
                        //          | |   \ \ t0          / /
                        //   q1     | | e2 \ \           / /
                        //          | |     \ \         / /
                        //	      p0| |e3    \ \       / /
                        //          | |   q2  \ \     / /
                        //          | |        \ \   / /
                        //			| v   e4    \ v / v 
                        //	 ------>   ----------->    ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------    <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        // vi1 / vi2
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        break;
                    }
                }
            }

            brushMesh.polygons  = polygons;
            brushMesh.halfEdges = halfEdges;
            brushMesh.vertices  = vertices;
            brushMesh.CalculatePlanes();
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
            return true;
        }

        public static void CreateExtrudedSubMesh(ref BrushMesh brushMesh, Vector3[] sideVertices, Vector3 extrusion, int[] segmentDescriptionIndices, in ChiselSurfaceDefinition surfaceDefinition)
        {
            const float distanceEpsilon = 0.0000001f;
            for (int i = sideVertices.Length - 1; i >= 0; i--)
            {
                var j = (i - 1 + sideVertices.Length) % sideVertices.Length;
                var magnitude = math.lengthsq(sideVertices[j] - sideVertices[i]);
                if (magnitude < distanceEpsilon)
                {
                    // TODO: improve on this
                    var tmp = sideVertices.ToList();
                    tmp.RemoveAt(i);
                    sideVertices = tmp.ToArray();
                }
            }

            var segments		= sideVertices.Length;
            var isSegmentConvex = new sbyte[segments];
            var edgeIndices		= new int[segments * 2];
            var vertices		= new Vector3[segments * 2];

            var polygonCount	= 2;
            var edgeOffset		= segments + segments;
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //			 	  t0	 
                //	 ------>   ------->   -----> 
                //        v0 *          * v1
                //	 <-----    <-------   <-----
                //			^ |   e2   ^ |
                //          | |        | |
                //	   q1 p0| |e3 q2 e5| |n0 q3
                //          | |        | |
                //			| v   e4   | v
                //	 ------>   ------->   ----->
                //        v3 *          * v2
                //	 <-----    <-------   <-----  
                //			      b0    
                //

                var v0 = sideVertices[p];
                var v1 = sideVertices[e];
                var v2 = sideVertices[e] + extrusion;
                var v3 = sideVertices[p] + extrusion;

                vertices[p] = v0;
                vertices[e] = v1;
                vertices[e + segments] = v2;
                vertices[p + segments] = v3;

                var plane = new Plane(v0, v1, v3);
                var dist = plane.GetDistanceToPoint(v2);

                if (UnityEngine.Mathf.Abs(dist) < 0.001f)
                    isSegmentConvex[n] = 0;
                else
                    isSegmentConvex[n] = (sbyte)UnityEngine.Mathf.Sign(dist);

                edgeIndices[(n * 2) + 0] = edgeOffset + 1;

                if (isSegmentConvex[n] == 0)
                {
                    polygonCount++;
                    edgeOffset += 4;
                } else
                {
                    polygonCount += 2;
                    edgeOffset += 6;
                }
                edgeIndices[(n * 2) + 1] = edgeOffset - 1;
            }

            var polygons = new BrushMesh.Polygon[polygonCount];
            
            var surfaceIndex0 = (segmentDescriptionIndices == null) ? 0 : (segmentDescriptionIndices[0]);
            var surfaceIndex1 = (segmentDescriptionIndices == null) ? 1 : (segmentDescriptionIndices[1]);
            var surface0 = surfaceDefinition.surfaces[surfaceIndex0];
            var surface1 = surfaceDefinition.surfaces[surfaceIndex1];

            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge =        0, edgeCount = segments, surface = surface0 };
            polygons[1] = new BrushMesh.Polygon { surfaceID = 1, firstEdge = segments, edgeCount = segments, surface = surface1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s + 2]);
                var firstEdge		 = edgeIndices[(s * 2) + 0] - 1;
                if (isSegmentConvex[s] == 0)
                {
                    polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 4, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    surfaceID++;
                } else
                {
                    var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                        //			unless there's already a smoothing group for this edge; then use that

                    polygons[surfaceID + 0] = new BrushMesh.Polygon { surfaceID = surfaceID + 0, firstEdge = firstEdge, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    polygons[surfaceID + 1] = new BrushMesh.Polygon { surfaceID = surfaceID + 1, firstEdge = firstEdge + 3, edgeCount = 3, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    surfaceID += 2;
                }
            }

            var halfEdges = new BrushMesh.HalfEdge[edgeOffset];
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi1 = e; 
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
            }

            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                var p0 = edgeIndices[(p * 2) + 1];
                var n0 = edgeIndices[(n * 2) + 0];

                if (isSegmentConvex[e] == 0)
                {
                    //			 	  t0	 
                    //	 ------>   ------->   -----> 
                    //        v0 *          * v1
                    //	 <-----    <-------   <-----
                    //			^ |   e2   ^ |
                    //          | |        | |
                    //	   q1 p0| |e3 q2 e5| |n0 q3
                    //          | |        | |
                    //			| v   e4   | v
                    //	 ------>   ------->   ----->
                    //        v3 *          * v2
                    //	 <-----    <-------   <-----  
                    //			      b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;

                    halfEdges[t0].twinIndex = e2;
                    halfEdges[b0].twinIndex = e4;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                } else
                if (isSegmentConvex[e] == -1)
                {
                    //			 	    t0	 
                    //	 ------>   ----------->   -----> 
                    //        v0 *              * v1
                    //	 <-----    <-----------   <-----
                    //			^ |     e2   ^ ^ |
                    //          | |        / / | |
                    //          | |    e4/ /   | |
                    //	   q1 p0| |e3   / /  e7| |n0 q3
                    //          | |   / /e5    | |
                    //          | | / /        | |
                    //			| v/v   e6     | v
                    //	 ------>   ----------->   ----->
                    //        v3 *              * v2
                    //	 <-----    <-----------   <-----  
                    //			        b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;
                    var e6 = q2 + 3;
                    var e7 = q2 + 4;

                    halfEdges[t0].twinIndex = e2;
                    halfEdges[b0].twinIndex = e6;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                    halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                } else
                if (isSegmentConvex[e] == 1)
                {
                    //			 	    t0	 
                    //	 ------>   ----------->   -----> 
                    //        v0 *              * v1
                    //	 <-----    <-----------   <-----
                    //			^ |^\   e5     ^ |
                    //          | | \ \        | |
                    //          | |   \ \ e6   | |
                    //	   q1 p0| |e3 e2\ \  e7| |n0 q3
                    //          | |       \ \  | |
                    //          | |        \ \ | |
                    //			| v   e4    \ v| v
                    //	 ------>   ----------->   ----->
                    //        v3 *              * v2
                    //	 <-----    <-----------   <-----  
                    //			        b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;
                    var e6 = q2 + 3;
                    var e7 = q2 + 4;

                    halfEdges[t0].twinIndex = e5;
                    halfEdges[b0].twinIndex = e4;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                    halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                }
            }

            brushMesh.polygons	= polygons;
            brushMesh.halfEdges	= halfEdges;
            brushMesh.vertices = new float3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                brushMesh.vertices[i] = vertices[i];
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
        }
    }
}