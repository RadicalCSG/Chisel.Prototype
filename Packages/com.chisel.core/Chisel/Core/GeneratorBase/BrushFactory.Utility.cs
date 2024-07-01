using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 CalculatePlane(in BrushMeshBlob.Polygon polygon, in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in ChiselBlobBuilderArray<float3> vertices)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var lastEdge	= polygon.firstEdge + polygon.edgeCount;
            var normal		= double3.zero;
            var prevVertex	= (double3)vertices[halfEdges[lastEdge - 1].vertexIndex];
            for (int n = polygon.firstEdge; n < lastEdge; n++)
            {
                var currVertex = (double3)vertices[halfEdges[n].vertexIndex];
                normal.x += ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                normal.y += ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                normal.z += ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                prevVertex = currVertex;
            }
            normal = math.normalize(normal);

            var d = 0.0;
            for (int n = polygon.firstEdge; n < lastEdge; n++)
                d -= math.dot(normal, vertices[halfEdges[n].vertexIndex]);
            d /= polygon.edgeCount;

            return new float4((float3)normal, (float)d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateHalfEdgePolygonIndices(ref ChiselBlobBuilderArray<int> halfEdgePolygonIndices, in ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons)
        {
            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var edgeCount = polygons[p].edgeCount;
                var lastEdge  = firstEdge + edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                    halfEdgePolygonIndices[e] = p;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselAABB CalculateBounds(in ChiselBlobBuilderArray<float3> vertices)
        {
            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < vertices.Length; i++)
            {
                min = math.min(min, vertices[i]);
                max = math.max(max, vertices[i]);
            }
            return new ChiselAABB { Min = min, Max = max };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FitXZ(ref ChiselBlobBuilderArray<float3> vertices, int firstVertex, int vertexCount, float2 expectedSize)
        {
            if (math.any(expectedSize == float2.zero))
                return;
            
            var min     = vertices[firstVertex].xz;
            var max     = vertices[firstVertex].xz;
            var last    = firstVertex + vertexCount;
            for (int v = firstVertex + 1; v < last; v++)
            {
                min = math.min(min, vertices[v].xz);
                max = math.max(max, vertices[v].xz);
            }

            var size = math.abs(max - min);
            if (math.any(size == float2.zero))
                return;
            
            var translation = (max + min) * 0.5f;
            var resize = expectedSize / size;
            for (int v = firstVertex; v < last; v++)
            {
                vertices[v].xz -= translation;
                vertices[v].xz = vertices[v].xz * resize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculatePlanes(ref ChiselBlobBuilderArray<float4> planes, in ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons, in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in ChiselBlobBuilderArray<float3> vertices)
        {
            for (int p = 0; p < polygons.Length; p++)
                planes[p] = CalculatePlane(in polygons[p], in halfEdges, in vertices);
        }

        public unsafe static bool GenerateSegmentedSubMesh(int horzSegments, int vertSegments, bool topCap, bool bottomCap, int topVertex, int bottomVertex, 
                                                           in ChiselBlobBuilderArray<float3> segmentVertices, 
                                                           ref NativeChiselSurfaceDefinition surfaceDefinition,
                                                           in ChiselBlobBuilder builder,
                                                           ref BrushMeshBlob root,
                                                           out ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons,
                                                           out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges)
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

            polygons = default;
            halfEdges = default;
            if (segmentVertices.Length != vertexCount)
            {
                Debug.LogError($"segmentVertices.Length ({segmentVertices.Length}) != expectedVertexCount ({vertexCount})");
                return false;
            }

            polygons            = builder.Allocate(ref root.polygons, assetPolygonCount);
            halfEdges		    = builder.Allocate(ref root.halfEdges, halfEdgeCount);

            // TODO: use NativeArray (safer)
            var twins			= stackalloc int[horzSegments];

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
                    halfEdges[currEdgeIndex] = new BrushMeshBlob.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p };
                    twins[h] = currEdgeIndex;
                }
                polygons[polygonIndex] = new BrushMeshBlob.Polygon { firstEdge = edgeIndex, edgeCount = polygonEdgeCount, descriptionIndex = 0, surface = surfaceDefinition.surfaces[0] };
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
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = topVertex };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p};
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
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = bottomVertex };
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
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments};
                        halfEdges[edgeIndex + 3] = new BrushMeshBlob.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments};
                        twins[h] = edgeIndex + 3;
                    }
                    polygons[polygonIndex] = new BrushMeshBlob.Polygon { firstEdge = edgeIndex, edgeCount = polygonEdgeCount, descriptionIndex = 0, surface = surfaceDefinition.surfaces[0] };
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
                    halfEdges[currEdgeIndex] = new BrushMeshBlob.HalfEdge { twinIndex = twins[h], vertexIndex = startVertex + (horzSegments - 1) - h };
                }
                polygons[polygonIndex] = new BrushMeshBlob.Polygon { firstEdge = edgeIndex, edgeCount = polygonEdgeCount, descriptionIndex = 0, surface = surfaceDefinition.surfaces[0] };
            }
            return true;
        }


        public static unsafe void CreateExtrudedSubMesh(int segments, int segmentTopIndex, int segmentBottomIndex,
                                                        in ChiselBlobBuilderArray<float3> localVertices,
                                                        in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                        in ChiselBlobBuilder builder, ref BrushMeshBlob root,
                                                        out ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons,
                                                        out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges)
        {
            CreateExtrudedSubMesh(segments, null, 0, segmentTopIndex, segmentBottomIndex, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
        }

        public static unsafe void CreateExtrudedSubMesh(int segments, [ReadOnly] NativeArray<int> segmentDescriptionIndices, int segmentDescriptionLength, int segmentTopIndex, int segmentBottomIndex, 
                                                in ChiselBlobBuilderArray<float3>                          localVertices,
                                                in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                in ChiselBlobBuilder builder, ref BrushMeshBlob root,
                                                out ChiselBlobBuilderArray<BrushMeshBlob.Polygon>    polygons,
                                                out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge>   halfEdges)
        {
            CreateExtrudedSubMesh(segments, (int*)segmentDescriptionIndices.GetUnsafePtr(), segmentDescriptionLength, segmentTopIndex, segmentBottomIndex,
                                                        in localVertices, in surfaceDefinitionBlob, in builder, ref root, out polygons, out halfEdges);
        }

        static unsafe void CreateExtrudedSubMesh(int segments, int* segmentDescriptionIndices, int segmentDescriptionLength, int segmentTopIndex, int segmentBottomIndex, 
                                                 in ChiselBlobBuilderArray<float3>                          localVertices,
                                                 in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                 in ChiselBlobBuilder builder, ref BrushMeshBlob root,
                                                 out ChiselBlobBuilderArray<BrushMeshBlob.Polygon>    polygons,
                                                 out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge>   halfEdges)
        {
            ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;

            // TODO: vertex reverse winding when it's not clockwise
            // TODO: handle duplicate vertices, remove them or avoid them being created in the first place (maybe use indices?)

            var segmentTopology = stackalloc SegmentTopology[segments];
            var edgeIndices     = stackalloc int[segments * 2];
            var polygonCount    = 2;
            var edgeOffset      = segments + segments;
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
                var v0 = localVertices[p];
                var v1 = localVertices[e];
                var v2 = localVertices[e + segments];
                var v3 = localVertices[p + segments];

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
                    if (math.abs(dist) < 0.001f)
                        segmentTopology[n] = SegmentTopology.Quad;
                    else
                        segmentTopology[n] = (SegmentTopology)math.sign(dist);
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

            polygons = builder.Allocate(ref root.polygons, polygonCount);

            var surfaceTop      = surfaceDefinition.surfaces[segmentTopIndex];
            var surfaceBottom   = surfaceDefinition.surfaces[segmentBottomIndex];
            
            polygons[0] = new BrushMeshBlob.Polygon { firstEdge = 0,        edgeCount = segments, descriptionIndex = segmentTopIndex, surface = surfaceTop };
            polygons[1] = new BrushMeshBlob.Polygon { firstEdge = segments, edgeCount = segments, descriptionIndex = segmentBottomIndex, surface = surfaceBottom };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null || s >= segmentDescriptionLength) ? s + 2 : (segmentDescriptionIndices[s]);
                var firstEdge = edgeIndices[(s * 2) + 0] - 1;
                switch (segmentTopology[s])
                {
                    case SegmentTopology.Quad:
                    {
                        polygons[surfaceID] = new BrushMeshBlob.Polygon { firstEdge = firstEdge, edgeCount = 4, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                            //			unless there's already a smoothing group for this edge; then use that

                        polygons[surfaceID + 0] = new BrushMeshBlob.Polygon { firstEdge = firstEdge,     edgeCount = 3, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        polygons[surfaceID + 1] = new BrushMeshBlob.Polygon { firstEdge = firstEdge + 3, edgeCount = 3, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID += 2;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        Debug.Assert(surfaceID < polygons.Length);
                        Debug.Assert(descriptionIndex < surfaceDefinition.surfaces.Length);
                        polygons[surfaceID] = new BrushMeshBlob.Polygon { firstEdge = firstEdge, edgeCount = 3, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.None:
                    {
                        break;
                    }
                }
            }

            halfEdges = builder.Allocate(ref root.halfEdges, edgeOffset);
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
                        halfEdges[t0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[b0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = t0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        halfEdges[t0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = -1 };
                        halfEdges[b0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    default:
                    {
                        halfEdges[t0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                        halfEdges[b0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
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

                        halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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

                        halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                        halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                        halfEdges[e6] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e7] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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

                        halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                        halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                        halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e6] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                        halfEdges[e7] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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
                        halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = t0 };

                        halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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

                        halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        // vi1 / vi2
                        halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        break;
                    }
                }
            }
        }

        public static unsafe bool CreateExtrudedSubMesh([ReadOnly] NativeArray<float3> sideVertices, float3 extrusion,
                                                        [ReadOnly] NativeArray<int> segmentDescriptionIndices, 
                                                        ref NativeChiselSurfaceDefinition surfaceDefinition,
                                                        in ChiselBlobBuilder builder, ref BrushMeshBlob root,
                                                        out ChiselBlobBuilderArray<BrushMeshBlob.Polygon>    polygons,
                                                        out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge>   halfEdges,
                                                        out ChiselBlobBuilderArray<float3>                   localVertices)
        {
            int sideVerticesLength = sideVertices.Length;
            
            // TODO: fix this
            /*
            const float kDistanceEpsilon = 0.0000001f;
            for (int i = sideVerticesLength - 1; i >= 0; i--)
            {
                var j = (i - 1 + sideVerticesLength) % sideVerticesLength;
                var magnitude = math.lengthsq(sideVertices[j] - sideVertices[i]);
                if (magnitude < kDistanceEpsilon)
                {
                    // TODO: improve on this
                    NativeListExtensions.MemMove(sideVertices, sideVerticesLength, i, i + 1, sideVerticesLength - (i + 1));
                    sideVerticesLength--;
                }
            }*/

            polygons = default;
            halfEdges = default;
            localVertices = default;

            if (sideVerticesLength < 3)
                return false;

            var segments		= sideVerticesLength;
            var isSegmentConvex = stackalloc sbyte[segments]; // TODO: get rid of stackalloc
            var edgeIndices		= stackalloc int[segments * 2];// TODO: get rid of stackalloc

            localVertices = builder.Allocate(ref root.localVertices, segments * 2);

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

                localVertices[p] = v0;
                localVertices[e] = v1;
                localVertices[e + segments] = v2;
                localVertices[p + segments] = v3;

                var plane = new Plane(v0, v1, v3);
                var dist = plane.GetDistanceToPoint(v2);

                if (math.abs(dist) < 0.001f)
                    isSegmentConvex[n] = 0;
                else
                    isSegmentConvex[n] = (sbyte)math.sign(dist);

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

            polygons = builder.Allocate(ref root.polygons, polygonCount);
            
            var surfaceIndex0 = (segmentDescriptionIndices == null || 0 >= segmentDescriptionIndices.Length) ? 0 : (segmentDescriptionIndices[0]);
            var surfaceIndex1 = (segmentDescriptionIndices == null || 1 >= segmentDescriptionIndices.Length) ? 1 : (segmentDescriptionIndices[1]);
            var surface0 = surfaceDefinition.surfaces[surfaceIndex0];
            var surface1 = surfaceDefinition.surfaces[surfaceIndex1];

            polygons[0] = new BrushMeshBlob.Polygon { firstEdge =        0, edgeCount = segments, descriptionIndex = surfaceIndex0, surface = surface0 };
            polygons[1] = new BrushMeshBlob.Polygon { firstEdge = segments, edgeCount = segments, descriptionIndex = surfaceIndex1, surface = surface1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null || (s + 2) >= segmentDescriptionIndices.Length) ? s + 2 : (segmentDescriptionIndices[s + 2]);
                var firstEdge		 = edgeIndices[(s * 2) + 0] - 1;
                if (isSegmentConvex[s] == 0)
                {
                    polygons[surfaceID] = new BrushMeshBlob.Polygon { firstEdge = firstEdge, edgeCount = 4, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    surfaceID++;
                } else
                {
                    var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                        //			unless there's already a smoothing group for this edge; then use that

                    polygons[surfaceID + 0] = new BrushMeshBlob.Polygon { firstEdge = firstEdge, edgeCount = 3, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    polygons[surfaceID + 1] = new BrushMeshBlob.Polygon { firstEdge = firstEdge + 3, edgeCount = 3, descriptionIndex = descriptionIndex, surface = surfaceDefinition.surfaces[descriptionIndex] };
                    surfaceID += 2;
                }
            }

            halfEdges = builder.Allocate(ref root.halfEdges, edgeOffset);
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi1 = e; 
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                halfEdges[t0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                halfEdges[b0] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
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

                    halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                    halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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

                    halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                    halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                    halfEdges[e6] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e7] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
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

                    halfEdges[e2] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                    halfEdges[e3] = new BrushMeshBlob.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                    halfEdges[e5] = new BrushMeshBlob.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e6] = new BrushMeshBlob.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                    halfEdges[e7] = new BrushMeshBlob.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                }
            }

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


        public static bool Validate(in ChiselBlobBuilderArray<float3> vertices, in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons, bool logErrors = false)
        {
            if (vertices.Length == 0)
            {
                if (logErrors) Debug.LogError("BrushMesh has no vertices set");
                return false;
            }

            if (halfEdges.Length == 0)
            {
                if (logErrors) Debug.LogError("BrushMesh has no halfEdges set");
                return false;
            }

            if (polygons.Length == 0)
            {
                if (logErrors) Debug.LogError("BrushMesh has no polygons set");
                return false;
            }

            bool fail = false;

            for (int h = 0; h < halfEdges.Length; h++)
            {
                if (halfEdges[h].vertexIndex < 0)
                {
                    if (logErrors) Debug.LogError($"halfEdges[{h}].vertexIndex is {halfEdges[h].vertexIndex}");
                    fail = true;
                } else
                if (halfEdges[h].vertexIndex >= vertices.Length)
                {
                    if (logErrors) Debug.LogError($"halfEdges[{h}].vertexIndex is {halfEdges[h].vertexIndex}, but there are {vertices.Length} vertices.");
                    fail = true;
                }

                if (halfEdges[h].twinIndex < 0)
                {
                    if (logErrors) Debug.LogError($"halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}");
                    fail = true;
                    continue;
                } else
                if (halfEdges[h].twinIndex >= halfEdges.Length)
                {
                    if (logErrors) Debug.LogError($"halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}, but there are {halfEdges.Length} edges.");
                    fail = true;
                    continue;
                }

                var twinIndex	= halfEdges[h].twinIndex;
                var twin		= halfEdges[twinIndex];
                if (twin.twinIndex != h)
                {
                    if (logErrors) Debug.LogError($"halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}, but the twinIndex of its twin is {twin.twinIndex} instead of {h}.");
                    fail = true;
                }
            }

            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var count     = polygons[p].edgeCount;
                var polygonFail = false;
                if (firstEdge < 0)
                {
                    if (logErrors) Debug.LogError($"polygons[{p}].firstEdge is {firstEdge}");
                    polygonFail = true;
                } else
                if (firstEdge >= halfEdges.Length)
                {
                    if (logErrors) Debug.LogError($"polygons[{p}].firstEdge is {firstEdge}, but there are {halfEdges.Length} edges.");
                    polygonFail = true;
                }
                if (count <= 2)
                {
                    if (logErrors) Debug.LogError($"polygons[{p}].edgeCount is {count}");
                    polygonFail = true;
                } else
                if (firstEdge + count - 1 >= halfEdges.Length)
                {
                    if (logErrors) Debug.LogError($"polygons[{p}].firstEdge + polygons[{p}].edgeCount is {(firstEdge + count)}, but there are {halfEdges.Length} edges.");
                    polygonFail = true;
                } else
                if (p < polygons.Length - 1 &&
                    polygons[p + 1].firstEdge != firstEdge + count)
                {
                    if (logErrors) Debug.LogError($"polygons[{(p + 1)}].firstEdge does not equal polygons[{p}].firstEdge + polygons[{p}].edgeCount.");
                    polygonFail = true;
                }

                fail = fail || polygonFail;
                if (polygonFail)
                    continue;
                
                for (int i0 = count - 1, i1 = 0; i1 < count; i0 = i1, i1++)
                {
                    var h0 = halfEdges[i0 + firstEdge];	// curr
                    var h1 = halfEdges[i1 + firstEdge]; // curr.next
                    if (h1.twinIndex < 0 || h1.twinIndex >= halfEdges.Length)
                    {
                        fail = true;
                        continue;
                    }
                    var t1 = halfEdges[h1.twinIndex];   // curr.next.twin

                    if (h0.vertexIndex != t1.vertexIndex)
                    {
                        if (logErrors)
                        {
                            Debug.LogError($"halfEdges[{(i0 + firstEdge)}].vertexIndex ({h0.vertexIndex}) is not equal to halfEdges[halfEdges[{(i1 + firstEdge)}].twinIndex({h1.twinIndex})].vertexIndex ({t1.vertexIndex}).");
                        }
                        fail = true;
                    }
                }
            }
            if (fail)
                return false;

            if (IsSelfIntersecting(in vertices, in halfEdges, in polygons))
            {
                if (logErrors)
                {
                    Debug.LogError("Brush is self intersecting");
                }
                return false;
            }

            if (!HasVolume(in vertices, in halfEdges, in polygons))
            {
                if (logErrors)
                {
                    Debug.LogError("Brush has no volume");
                }
                return false;
            }

            return true;
        }
        
        public static bool IsSelfIntersecting(in ChiselBlobBuilderArray<float3> vertices, in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons)
        {
            // TODO: determine if the brush is intersecting itself
            return false;
        }

        public static bool HasVolume(in ChiselBlobBuilderArray<float3> vertices, in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges, in ChiselBlobBuilderArray<BrushMeshBlob.Polygon> polygons)
        {
            if (polygons.Length == 0)
                return false;
            if (vertices.Length == 0)
                return false;
            if (halfEdges.Length == 0)
                return false;

            // TODO: determine if the brush is a singularity (1D) or flat (2D), or has a volume (3D)
            return true;
        }


        /// <summary>
        /// Creates a brush out of a set of planes. A brush consists of several faces, each of which can be defined by a plane (of which there must be at least four).
        /// Each plane defines a half space, that is, an infinite set of points that is bounded by a plane. The intersection of these half spaces forms a convex polyhedron.
        /// The brush geometry is not centered unless your planes are, use <seealso cref="BrushMesh.CenterAndSnapPlanes"/> to center and position the game object to match.
        /// <para>Note: The current algorithm only works within a limited working area, specified by the <paramref name="bounds"/> parameter.
        /// If you do not know the size of the resulting brush beforehand, this can also be oversized. For example +-4096 world units from the world center.
        /// If possible, perform your operations near the center of the world for optimal accuracy.</para>
        /// </summary>
        public static void CreateFromPlanes(float4[] planes, Bounds bounds, ref ChiselSurfaceDefinition surfaceDefinition, ref BrushMesh brushMesh)
        {
            Debug.Assert(planes != null && planes.Length >= 4);

            // create a box brush with the size of the specified bounds.
            surfaceDefinition.EnsureSize(planes.Length);
            CreateBox(bounds.min, bounds.max, out brushMesh);

            // cut the brush using the given planes.
            brushMesh.Cut(planes);
        }

        /// <summary>
        /// Creates a brush out of a set of points. A convex hull is generated that encompasses all points (of which there must be at least four).
        /// The brush geometry is not centered unless your points are, use <seealso cref="BrushMesh.CenterAndSnapPlanes"/> to center and position the game object to match.
        /// <para>If possible, place your points near the center of the world for optimal accuracy.</para>
        /// </summary>
        public static void CreateFromPoints(Vector3[] points, ref ChiselSurfaceDefinition surfaceDefinition, ref BrushMesh brushMesh)
        {
            Debug.Assert(points != null && points.Length >= 4);

            // guess a capacity that's large enough to hold the planes, division by 6 due to triangulation.
            // the convex hull has duplicate polygons, degenerate triangles and T-Junctions.
            // as such this should be more than we will need to store a handful of unique planes.
            var planes = new List<float4>(points.Length / 6);

            // calculate a convex hull out of the points.
            ConvexHullCalculator convexHullCalculator = new ConvexHullCalculator();
            convexHullCalculator.GenerateHull(points, ref planes, out Bounds bounds);

            // create a brush out of the convex hull.
            CreateFromPlanes(planes.ToArray(), bounds, ref surfaceDefinition, ref brushMesh);
        }
    }
}