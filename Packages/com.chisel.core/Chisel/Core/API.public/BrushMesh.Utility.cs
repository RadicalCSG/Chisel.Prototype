using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class BrushMesh
    {
        [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct VertexSide
        {
            public float    Distance;
            public int      Halfspace;
        }

        [Serializable, StructLayout(LayoutKind.Sequential)]
        private struct TraversalSide
        {
            public VertexSide   Side;
            public int          EdgeIndex;
            public int          VertexIndex;
        };

        [Serializable, StructLayout(LayoutKind.Sequential)]
        private struct PlaneTraversals
        {
            public TraversalSide Traversal0;
            public TraversalSide Traversal1;
        };

        Plane CalculatePlane(in Polygon polygon)
        {        
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var lastEdge	= polygon.firstEdge + polygon.edgeCount;
            var normal		= Vector3.zero;
            var prevVertex	= vertices[halfEdges[lastEdge - 1].vertexIndex];
            for (int n = polygon.firstEdge; n < lastEdge; n++)
            {
                var currVertex = vertices[halfEdges[n].vertexIndex];
                normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                prevVertex = currVertex;
            }
            normal = normal.normalized;

            var d = 0.0f;
            for (int n = polygon.firstEdge; n < lastEdge; n++)
                d -= Vector3.Dot(normal, vertices[halfEdges[n].vertexIndex]);
            d /= polygon.edgeCount;

            return new Plane(normal, d);
        }


        public void CalculatePlanes()
        {
            if (polygons == null)
                return;

            if (surfaces == null ||
                surfaces.Length != polygons.Length)
                surfaces = new Surface[polygons.Length];

            for (int p = 0; p < polygons.Length; p++)
            {
                var plane       = CalculatePlane(in polygons[p]);
                var planeVector = (Vector4)plane.normal;
                planeVector.w = plane.distance;
                surfaces[p].localPlane = planeVector;
            }
        }

        public void UpdateHalfEdgePolygonIndices()
        {
            if (polygons == null ||
                halfEdges == null)
                return;

            if (halfEdgePolygonIndices == null ||
                halfEdgePolygonIndices.Length != halfEdges.Length)
                halfEdgePolygonIndices = new int[halfEdges.Length];

            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var edgeCount = polygons[p].edgeCount;
                var lastEdge  = firstEdge + edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                    halfEdgePolygonIndices[e] = p;
            }
        }

        public void RemoveRedundantVertices()
        {
            if (halfEdges == null ||
                vertices == null ||
                vertices.Length == 0)
                return;

            var newVertexPosition = new int[vertices.Length];
            for (int v = 0; v < newVertexPosition.Length; v++)
                newVertexPosition[v] = -1;

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                newVertexPosition[vertexIndex] = vertexIndex;
            }

            for (int v = 0; v < vertices.Length; v++)
            {
                if (newVertexPosition[v] != -1)
                    continue;
                vertices[v].x = float.NaN;
                vertices[v].y = float.NaN;
                vertices[v].z = float.NaN;
            }

            int from    = -1;
            int to      = 0;
            int count   = 0;            
            for (int v = 0; v < newVertexPosition.Length; v++)
            {
                if (newVertexPosition[v] != -1)
                {
                    if (from == -1)
                        from = v;
                    count++;
                    continue;
                }

                if (from == -1)
                    continue;

                if (from != to)
                {
                    Array.Copy(vertices, from, vertices, to, count);
                    for (int n = 0; n < count; n++)
                        newVertexPosition[from + n] = to + n;
                }
                to += count;
                count = 0;
                from = -1;
            }
            if (from != -1 &&
                from != to)
            {
                Array.Copy(vertices, from, vertices, to, count);
                for (int n = 0; n < count; n++)
                    newVertexPosition[from + n] = to + n;
            }
            to += count;
            if (to != vertices.Length)
                Array.Resize(ref vertices, to);

            for (int e = 0; e < halfEdges.Length; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                vertexIndex = newVertexPosition[vertexIndex];
                halfEdges[e].vertexIndex = vertexIndex;
            }
        }

        public void CompactHalfEdges()
        {
            if (halfEdges == null ||
                polygons  == null ||
                polygons.Length == 0)
                return;

            // Remove empty polygons
            for (int p = polygons.Length - 1; p >= 0; p--)
            {
                if (polygons[p].edgeCount == 0)
                {
                    var newLength = polygons.Length - 1;
                    if (p < newLength)
                    {
                        for (int p2 = p + 1; p2 < polygons.Length; p2++)
                            polygons[p2 - 1] = polygons[p2];
                        //Array.Copy(polygons, p + 1, polygons, p, polygons.Length - p);
                    }
                    Array.Resize(ref polygons, newLength);
                    continue;
                }
            }

            bool needCompaction = false;
            for (int p = polygons.Length - 1; p >= 1; p--)
            {
                var currFirstEdge   = polygons[p].firstEdge;                
                var prevLastEdge    = polygons[p].firstEdge + polygons[p].edgeCount;
                if (prevLastEdge != currFirstEdge)
                {
                    needCompaction = true;
                    break;
                }
            }

            // Don't bother trying to compact if we don't need to
            if (!needCompaction)
            {
                UpdateHalfEdgePolygonIndices();
                return;
            }

            var lookup = new int[halfEdges.Length];
            for (int e = 0; e < halfEdges.Length; e++)
                lookup[e] = e;

            var firstEdge0  = 0;// polygons[0].firstEdge;
            var edgeCount0  = 0;// polygons[0].edgeCount;
            var lastEdge0   = firstEdge0 + edgeCount0;
            int lastEdge1, firstEdge1, edgeCount1;

            // Remove any spaces between polygons
            for (int p1 = 0; p1 < polygons.Length; p1++, firstEdge0 = firstEdge1, edgeCount0 = edgeCount1, lastEdge0 = lastEdge1)
            {
                firstEdge1  = polygons[p1].firstEdge;
                edgeCount1  = polygons[p1].edgeCount;
                lastEdge1   = firstEdge1 + edgeCount1;

                var offset  = firstEdge1 - lastEdge0;
                if (offset <= 0)
                {
                    if (offset < 0)
                        Debug.LogError("Polygon has overlapping halfEdges");
                    continue;
                }

                // Make sure we can lookup the new location of existing edges so we can fix up twins later on
                for (int e = firstEdge1; e < lastEdge1; e++)
                    lookup[e] = e - offset;
                
                // Move the halfEdges of this polygon to the new location
                Array.Copy(halfEdges, firstEdge1, halfEdges, lastEdge0, edgeCount1);
                polygons[p1].firstEdge = lastEdge0;
                lastEdge1 = lastEdge0 + edgeCount1;
            }
            
            // Ensure all the twinIndices point to the correct halfEdges
            var lastEdge = polygons[polygons.Length - 1].firstEdge + polygons[polygons.Length - 1].edgeCount;
            for (int e = 0; e < lastEdge; e++)
                halfEdges[e].twinIndex = lookup[halfEdges[e].twinIndex];

            Array.Resize(ref halfEdges, lastEdge);

            UpdateHalfEdgePolygonIndices();
        }


        static List<PlaneTraversals> s_Intersections = new List<PlaneTraversals>(); // avoids allocations at runtime

        // TODO: return inside and outside polygon index
        bool SplitPolygon(Plane cuttingPlane, int polygonIndex, List<VertexSide> vertexDistances)
        {
            ref var polygon     = ref polygons[polygonIndex];
            var firstEdge       = polygon.firstEdge;
            var edgeCount       = polygon.edgeCount;
            var lastEdge        = firstEdge + edgeCount;


            // Find all the edges where one vertex is in a different half-space than the other
            PlaneTraversals intersection;
            { 
                var edgeIndex0		= lastEdge - 1;
                var vertexIndex0	= halfEdges[edgeIndex0].vertexIndex;

                intersection.Traversal1.Side		= vertexDistances[vertexIndex0];
                intersection.Traversal1.EdgeIndex	= edgeIndex0;
                intersection.Traversal1.VertexIndex = vertexIndex0;
            }

            s_Intersections.Clear();
            for (var edgeIndex1 = firstEdge; edgeIndex1 < lastEdge; edgeIndex1++)
            {
                intersection.Traversal0 = intersection.Traversal1;

                var vertexIndex1	= halfEdges[edgeIndex1].vertexIndex;

                intersection.Traversal1.Side		= vertexDistances[vertexIndex1];
                intersection.Traversal1.EdgeIndex	= edgeIndex1;
                intersection.Traversal1.VertexIndex = vertexIndex1;

                if (intersection.Traversal0.Side.Halfspace != intersection.Traversal1.Side.Halfspace)
                    s_Intersections.Add(intersection);
            }


            // If we don't have any intersections then the polygon is either completely inside or outside
            if (s_Intersections.Count == 0)
            {
                //if (intersection.Traversal1.Side.Halfspace == 1)
                //    polygon.edgeCount = 0;
                return true;
            }

            Debug.Assert(s_Intersections.Count != 1, "Number of edges crossing the plane boundary is 1, which should not be possible!");

            // Remove any intersections that only touch the plane but never cut
            for (int i0 = s_Intersections.Count - 1, i1 = 0; i1 < s_Intersections.Count; i0 = i1, i1++)
            {
                if (s_Intersections[i0].Traversal1.Side.Halfspace != s_Intersections[i1].Traversal0.Side.Halfspace ||
                    s_Intersections[i0].Traversal1.Side.Halfspace != 0)
                    continue;

                // Note: we know traversal0.side.halfspace and traversal1.side.halfspace are always different from each other.

                if (s_Intersections[i0].Traversal0.Side.Halfspace != s_Intersections[i1].Traversal1.Side.Halfspace)
                    continue;

                // possibilities: (can have multiple vertices on the plane between intersections)
                //
                //       outside				      outside
                //								       0      1
                //       1  0					        \    /
                //  .....*..*....... intersect	 ........*..*.... intersect
                //      /    \					         1  0
                //     0      1					
                //        inside				      inside

                bool outside = s_Intersections[i0].Traversal0.Side.Halfspace == 1 ||
                               s_Intersections[i1].Traversal0.Side.Halfspace == 1;

                if (i0 > i1)
                {
                    s_Intersections.RemoveAt(i0);
                    s_Intersections.RemoveAt(i1);
                } else
                {
                    s_Intersections.RemoveAt(i1);
                    s_Intersections.RemoveAt(i0);
                }

                // Check if we have any intersections left
                if (s_Intersections.Count == 0)
                {
                    //if (outside)
                    //    polygon.edgeCount = 0;
                    return true;
                }
            }

            Debug.Assert(s_Intersections.Count >= 2 && s_Intersections.Count <= 4, "Number of edges crossing the plane boundary should be ≥2 and ≤4!");
            
            // Find all traversals that go straight from one side to the other side. 
            //	Create a new intersection point there, split traversals into two traversals.
            for (var i0 = 0; i0 < s_Intersections.Count; i0++)
            {
                // Note: we know traversal0.side.halfspace and traversal1.side.halfspace are always different from each other.

                var planeTraversal0 = s_Intersections[i0];

                // Skip all traversals that already have a vertex on the plane
                if (planeTraversal0.Traversal0.Side.Halfspace == 0 ||
                    planeTraversal0.Traversal1.Side.Halfspace == 0)
                    continue;

                // possibilities:
                //    
                //       outside                      outside
                //       0                                 1       
                //        \                               /      
                //  .......\......... intersect  ......../....... intersect
                //          \                           /    
                //           1                         0
                //        inside                      inside

                // Calculate intersection of edge with plane split the edge into two, inserting the new vertex


                var edgeIndex0		= planeTraversal0.Traversal0.EdgeIndex;
                var edgeIndex1		= planeTraversal0.Traversal1.EdgeIndex;

                float distance0, distance1;
                int vertexIndex0, vertexIndex1;
                
                // Ensure we always cut edges in the same direction, to ensure floating point inaccuracies are consistent.
                if (planeTraversal0.Traversal0.Side.Halfspace < 0)
                {
                    vertexIndex0 = planeTraversal0.Traversal0.VertexIndex;
                    vertexIndex1 = planeTraversal0.Traversal1.VertexIndex;
                    distance0 = planeTraversal0.Traversal0.Side.Distance;
                    distance1 = planeTraversal0.Traversal1.Side.Distance;
                } else
                {
                    vertexIndex1 = planeTraversal0.Traversal0.VertexIndex;
                    vertexIndex0 = planeTraversal0.Traversal1.VertexIndex;
                    distance1 = planeTraversal0.Traversal0.Side.Distance;
                    distance0 = planeTraversal0.Traversal1.Side.Distance;
                }


                // Calculate the intersection
                var vertex0		= vertices[vertexIndex0];
                var vertex1		= vertices[vertexIndex1];
                var vector	    = vertex0 - vertex1;
                var length	    = distance0 - distance1;
                var delta	    = distance0 / length;
                var newVertex	= vertex0 - (vector * delta);

                // Create a new vertex
                var newVertexIndex  = vertices.Length;
                // TODO: would be nice if we could do allocations just once in CutPolygon
                var newVertices     = new Vector3[vertices.Length + 1];
                Array.Copy(vertices, newVertices, newVertexIndex);
                newVertices[newVertexIndex] = newVertex;
                vertices = newVertices;

                // Update the vertex indices for the new vertex
                var newVertexDistance = new VertexSide() { Distance = 0, Halfspace = 0 };
                vertexDistances.Add(newVertexDistance);

                //Debug.Log("< " + halfEdges[edgeIndex0].vertexIndex + " " + halfEdges[edgeIndex1].vertexIndex + " : " + newVertexIndex);

                // Split the halfEdge (on both sides) and insert the vertex index in between
                SplitHalfEdge(edgeIndex1, newVertexIndex, s_Intersections, out int newEdgeIndex);
                //Debug.Log(newVertexIndex + " " + newVertex + " | " + vertex0 + " " + vertex1 + " " + length + " " + distance0 + " " + distance1 + "|"+
                //    planeTraversal0.Traversal0.Side.Halfspace + " " + planeTraversal0.Traversal1.Side.Halfspace);
                
                polygon     = ref polygons[polygonIndex];

                // Fix up the intersections since edge-indices might have changed
                //var replacedEdgeIndex	= ((edgeIndex1 + polygon.edgeCount - polygon.firstEdge - 1) % polygon.edgeCount) + polygon.firstEdge;
                //newEdgeIndex		= ((edgeIndex0 + polygon.edgeCount - polygon.firstEdge - 1) % polygon.edgeCount) + polygon.firstEdge;

                //Debug.Log("> " + halfEdges[edgeIndex0].vertexIndex + " " + halfEdges[edgeIndex1].vertexIndex + " " + halfEdges[replacedEdgeIndex].vertexIndex + " " + halfEdges[newEdgeIndex].vertexIndex);
                /*
                for (var i1 = 0; i1 < s_Intersections.Count; i1++)
                {
                    if (i1 == i0)
                        continue;

                    var planeTraversal1 = s_Intersections[i1];
                    if (planeTraversal1.Traversal0.EdgeIndex == edgeIndex1)
                    {
                        planeTraversal1.Traversal0.EdgeIndex = replacedEdgeIndex;
                        s_Intersections[i1] = planeTraversal1;
                    } else
                    if (planeTraversal1.Traversal1.EdgeIndex == edgeIndex1)
                    {
                        planeTraversal1.Traversal1.EdgeIndex = replacedEdgeIndex;
                        s_Intersections[i1] = planeTraversal1;
                    }
                }
                */

                Debug.Assert(halfEdges[newEdgeIndex].vertexIndex == newVertexIndex);

                // Create a new intersection for the part that crossed the plane and ends up at the intersection point
                var newIntersection = new PlaneTraversals
                {
                    Traversal0 = planeTraversal0.Traversal0,
                    Traversal1 = { EdgeIndex = newEdgeIndex, VertexIndex = newVertexIndex, Side = newVertexDistance }
                };
                s_Intersections.Insert(i0, newIntersection);

                // skip the intersection we just added so we don't process it again, 
                // also the position of s_Intersections[i0] moved forward because of the insert.
                i0++;

                // ... finally make the existing plane traversal start at the new intersection point
                planeTraversal0.Traversal0 = newIntersection.Traversal1;
                s_Intersections[i0] = planeTraversal0;
                //if (s_Intersections.Count == 2)
                //    return true;
            }

            // NOTE: from this point on Traversal𝑛.EdgeIndex may no longer be valid!


            Debug.Assert((s_Intersections.Count & 1) != 1, "Found an uneven number of edge-plane traversals??");
            Debug.Assert(s_Intersections.Count == 4, "Expected 4 intersected edge pieces at this point");


            if (s_Intersections.Count != 4)
                return true;

            int first = 0;
            if (s_Intersections[0].Traversal0.Side.Halfspace == 0)
                first++;


            int indexOut, indexIn;
            if (s_Intersections[first].Traversal0.Side.Halfspace < 0)
            {
                indexOut	= s_Intersections[first + 1].Traversal0.EdgeIndex;
                indexIn	    = s_Intersections[first + 2].Traversal1.EdgeIndex;
            } else
            {
                indexOut	= s_Intersections[first + 2].Traversal1.EdgeIndex;
                indexIn	    = s_Intersections[first + 1].Traversal0.EdgeIndex;
            }

            UpdateHalfEdgePolygonIndices();

            SplitPolygon(polygonIndex, indexOut, indexIn);
            return true;
        }
        
        public void SplitPolygon(int polygonIndex, int indexOut, int indexIn)
        {
            //Debug.Log("SplitPolygon");
            var firstEdge = polygons[polygonIndex].firstEdge;
            var edgeCount = polygons[polygonIndex].edgeCount;
            var lastEdge  = firstEdge + edgeCount;

            //Debug.Log("SplitPolygon " + firstEdge + " " + indexIn + " " + indexOut + "|"+ polygonIndex + " " + halfEdgePolygonIndices[indexIn] + " " + halfEdgePolygonIndices[indexOut]);
            if (halfEdgePolygonIndices[indexIn ] != polygonIndex ||
                halfEdgePolygonIndices[indexOut] != polygonIndex)
            {
                Debug.Assert(false);/*
                var builder = new System.Text.StringBuilder();
                builder.AppendLine("polygons:");
                for (int i = 0; i < polygons.Length; i++)
                    builder.AppendLine("  " + i + ":" + polygons[i].ToString());
                Debug.Log(builder.ToString()); */
                return;
            }

            if (indexIn < indexOut)
            {
                lastEdge--;

                var segment1     = (indexIn  - firstEdge) + 1;
                var segment2     = (indexOut - indexIn  ) + 1;
                var segment3     = (lastEdge - indexOut ) + 1;
                var newEdgeCount = segment1 + segment3;
                //Debug.Log(firstEdge + " " + indexIn + " " + indexOut + " " + lastEdge + " " + newEdgeCount + " " + segment1 + " " + segment2 + " " + segment3);

                if (segment2 <= 2 ||
                    (segment3 + segment1) <= 2)
                {
                    Debug.Assert(false);
                    //Debug.Log(segment1 + " " + segment2 + " " + segment3);
                    return;
                }

                var newFirstIndex   = halfEdges.Length;
                var newPolygonIndex = polygons.Length;

                var newPolygons = new Polygon[polygons.Length + 1];
                Array.Copy(polygons, newPolygons, polygons.Length);

                var newFirstEdge = newPolygons[newPolygonIndex - 1].firstEdge + newPolygons[newPolygonIndex - 1].edgeCount;

                newPolygons[newPolygonIndex].firstEdge      = newFirstEdge;
                newPolygons[newPolygonIndex].edgeCount      = newEdgeCount;
                newPolygons[newPolygonIndex].surfaceID      = polygons[polygons.Length - 1].surfaceID + 1;
                newPolygons[newPolygonIndex].surface        = polygons[polygonIndex].surface;

                newPolygons[polygonIndex].firstEdge = indexIn;
                newPolygons[polygonIndex].edgeCount = segment2;

                var newHalfEdges = new HalfEdge[halfEdges.Length + newEdgeCount];
                Array.Copy(halfEdges, newHalfEdges, halfEdges.Length);

                var offset = newFirstIndex;
                Array.Copy(halfEdges, indexOut,  newHalfEdges, offset, segment3); offset += segment3;
                Array.Copy(halfEdges, firstEdge, newHalfEdges, offset, segment1); offset += segment1;

                Array.Copy(halfEdges, indexIn,   newHalfEdges, indexIn, segment2);

                //Debug.Log(firstEdge + " <-> " + (firstEdge + segment2 - 1) + " " + segment2 + " | " + newFirstIndex + " <-> " + (newFirstIndex + newEdgeCount - 1) + " " + newEdgeCount);

                var index1 = newPolygons[polygonIndex   ].firstEdge;// + (newPolygons[polygonIndex   ].edgeCount - 1);
                var index2 = newPolygons[newPolygonIndex].firstEdge;// + (newPolygons[newPolygonIndex].edgeCount - 1);
                
                newHalfEdges[index1].twinIndex = index2;
                newHalfEdges[index2].twinIndex = index1;

                {
                    ref var polygon = ref newPolygons[polygonIndex];
                    for (int e = polygon.firstEdge; 
                             e < polygon.firstEdge + polygon.edgeCount; 
                             e++)
                        newHalfEdges[newHalfEdges[e].twinIndex].twinIndex = e;
                }

                {
                    ref var polygon = ref newPolygons[polygons.Length];
                    for (int e = polygon.firstEdge; 
                             e < polygon.firstEdge + polygon.edgeCount; 
                             e++)
                        newHalfEdges[newHalfEdges[e].twinIndex].twinIndex = e;
                }

                this.halfEdges = newHalfEdges;
                this.polygons  = newPolygons;
            } else
            {
                lastEdge--;

                // f**o***i**l
                
                var segment1     = (indexOut - firstEdge) + 1;
                var segment2     = (indexIn  - indexOut) + 1;
                var segment3     = (lastEdge - indexIn ) + 1;
                var newEdgeCount = segment1 + segment3;
                //Debug.Log(firstEdge + " " + indexIn + " " + indexOut + " " + lastEdge + " " + newEdgeCount + " " + segment1 + " " + segment2 + " " + segment3);

                if (segment2 <= 2 ||
                    (segment3 + segment1) <= 2)
                {
                    Debug.Assert(false);
                    //Debug.Log(segment1 + " " + segment2 + " " + segment3);
                    return;
                }

                var newFirstIndex   = halfEdges.Length;
                var newPolygonIndex = polygons.Length;

                var newPolygons = new Polygon[polygons.Length + 1];
                Array.Copy(polygons, newPolygons, polygons.Length);

                var newFirstEdge = newPolygons[newPolygonIndex - 1].firstEdge + newPolygons[newPolygonIndex - 1].edgeCount;

                newPolygons[newPolygonIndex].firstEdge      = newFirstEdge;
                newPolygons[newPolygonIndex].edgeCount      = newEdgeCount;
                newPolygons[newPolygonIndex].surfaceID      = polygons[polygons.Length - 1].surfaceID + 1;
                newPolygons[newPolygonIndex].surface        = polygons[polygonIndex].surface;

                newPolygons[polygonIndex].firstEdge = indexOut;
                newPolygons[polygonIndex].edgeCount = segment2;

                var newHalfEdges = new HalfEdge[halfEdges.Length + newEdgeCount];
                Array.Copy(halfEdges, newHalfEdges, halfEdges.Length);

                var offset = newFirstIndex;
                Array.Copy(halfEdges, indexIn,   newHalfEdges, offset, segment3); offset += segment3;
                Array.Copy(halfEdges, firstEdge, newHalfEdges, offset, segment1); offset += segment1;

                Array.Copy(halfEdges, indexOut,  newHalfEdges, indexOut, segment2);

                //Debug.Log(firstEdge + " <-> " + (firstEdge + segment2 - 1) + " " + segment2 + " | " + newFirstIndex + " <-> " + (newFirstIndex + newEdgeCount - 1) + " " + newEdgeCount);

                var index1 = newPolygons[polygonIndex   ].firstEdge;// + (newPolygons[polygonIndex   ].edgeCount - 1);
                var index2 = newPolygons[newPolygonIndex].firstEdge;// + (newPolygons[newPolygonIndex].edgeCount - 1);
                
                newHalfEdges[index1].twinIndex = index2;
                newHalfEdges[index2].twinIndex = index1;

                {
                    ref var polygon = ref newPolygons[polygonIndex];
                    for (int e = polygon.firstEdge; 
                             e < polygon.firstEdge + polygon.edgeCount; 
                             e++)
                        newHalfEdges[newHalfEdges[e].twinIndex].twinIndex = e;
                }

                {
                    ref var polygon = ref newPolygons[polygons.Length];
                    for (int e = polygon.firstEdge; 
                             e < polygon.firstEdge + polygon.edgeCount; 
                             e++)
                        newHalfEdges[newHalfEdges[e].twinIndex].twinIndex = e;
                }

                this.halfEdges = newHalfEdges;
                this.polygons  = newPolygons;
            }
            // TODO: implement


            //Debug.Log("compact");
            CompactHalfEdges();
            CalculatePlanes();
            //Debug.Log("validate");
            Validate(logErrors: true);
            //Debug.Log("after");
        }


        void Dump(int polygonIndex, int indexIn, int indexOut, Polygon[] newPolygons, HalfEdge[] newHalfEdges)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append("original[" + polygonIndex + "]: ");
            {
                ref var polygon = ref polygons[polygonIndex];
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    if (e == indexIn) builder.Append("(i)");
                    if (e == indexOut) builder.Append("(o)");
                    builder.Append(halfEdges[e].vertexIndex);
                }
                builder.Append(" | ");
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    if (e == indexIn) builder.Append("(i)");
                    if (e == indexOut) builder.Append("(o)");
                    builder.Append(halfEdges[e].twinIndex);
                }
            }
            builder.AppendLine();
            builder.Append("original-after[" + polygonIndex + "]: ");
            {
                ref var polygon = ref newPolygons[polygonIndex];
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    builder.Append(newHalfEdges[e].vertexIndex);
                }
                builder.Append(" | ");
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    builder.Append(newHalfEdges[e].twinIndex);
                }
            }
            builder.AppendLine();

            builder.Append("new[" + polygonIndex + "]: ");
            {
                ref var polygon = ref newPolygons[polygons.Length];
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    builder.Append(newHalfEdges[e].vertexIndex);
                }
                builder.Append(" | ");
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (e > polygon.firstEdge) builder.Append(',');
                    builder.Append(newHalfEdges[e].twinIndex);
                }
            }
            builder.AppendLine();
            Debug.Log(builder.ToString());
        }

        public void RemoveEdge(int removeEdgeIndex)
        {
            if (removeEdgeIndex < 0 ||
                removeEdgeIndex >= halfEdges.Length)
                return;

            var twin            = halfEdges[removeEdgeIndex].twinIndex;
            var polygonIndex1   = halfEdgePolygonIndices[removeEdgeIndex];
            var polygonIndex2   = halfEdgePolygonIndices[twin];
            
            var newPolygons = new Polygon[polygons.Length + 1];
            Array.Copy(polygons, newPolygons, polygons.Length);
            newPolygons[polygonIndex1].edgeCount = 0;
            newPolygons[polygonIndex2].edgeCount = 0;

            var newCount        = (polygons[polygonIndex1].edgeCount - 1) + (polygons[polygonIndex2].edgeCount - 1);
            var newFirstEdge    = halfEdges.Length;

            var newHalfEdges    = new HalfEdge[halfEdges.Length + newCount];
            Array.Copy(halfEdges, newHalfEdges, halfEdges.Length);

            var newPolygonIndex = polygons.Length;
            newPolygons[newPolygonIndex].edgeCount      = newCount;
            newPolygons[newPolygonIndex].firstEdge      = newFirstEdge;
            newPolygons[newPolygonIndex].surfaceID      = polygons[polygons.Length - 1].surfaceID + 1;
            newPolygons[newPolygonIndex].surface        = polygons[polygonIndex1].surface;

            var edgeCount1 = polygons[polygonIndex1].edgeCount;
            var firstEdge1 = polygons[polygonIndex1].firstEdge;
            int newLastEdge = newFirstEdge;
            for (int e = 1; e < edgeCount1; e++, newLastEdge++)
            {
                var edgeIndex = ((removeEdgeIndex - firstEdge1 + e) % edgeCount1) + firstEdge1;
                newHalfEdges[newLastEdge] = halfEdges[edgeIndex];
            }
            var edgeCount2 = polygons[polygonIndex2].edgeCount;
            var firstEdge2 = polygons[polygonIndex2].firstEdge;
            for (int e = 1; e < edgeCount2; e++, newLastEdge++)
            {
                var edgeIndex = ((twin - firstEdge2 + e) % edgeCount2) + firstEdge2;
                newHalfEdges[newLastEdge] = halfEdges[edgeIndex];
            }
            Debug.Assert(newLastEdge == (newFirstEdge + newCount));

            for (int n = newFirstEdge; n < newLastEdge; n++)
                newHalfEdges[newHalfEdges[n].twinIndex].twinIndex = n;

            var oldPolygonLength = polygons.Length;

            polygons    = newPolygons;
            halfEdges   = newHalfEdges;

            CompactHalfEdges();
            CalculatePlanes();
            UpdateHalfEdgePolygonIndices();

            Validate(logErrors: true);
        }

        void Dump()
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < polygons.Length; i++)
            {
                builder.Append("polygons[" + i + ":"+ polygons[i].edgeCount +"]: ");
                {
                    ref var polygon = ref polygons[i];
                    for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                    {
                        if (e > polygon.firstEdge) builder.Append(',');
                        builder.Append(halfEdges[e].vertexIndex);
                    }
                    builder.Append(" | ");
                    for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                    {
                        if (e > polygon.firstEdge) builder.Append(',');
                        builder.Append(halfEdges[e].twinIndex);
                    }
                    builder.Append(" | ");
                    for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                    {
                        if (e > polygon.firstEdge) builder.Append(',');
                        builder.Append(vertices[halfEdges[e].vertexIndex]);
                    }
                }
                builder.AppendLine();
            }
            for (int i = 0; i < vertices.Length; i++)
                builder.AppendLine("vertices[" + i + "]:" + vertices[i]);
            Debug.Log(builder.ToString());
        }
        
        public void SplitHalfEdge(int halfEdgeIndex, int newVertexIndex, out int newEdgeIndex)
        {
            SplitHalfEdge(halfEdgeIndex, newVertexIndex, null, out newEdgeIndex);
        }

        void SplitHalfEdge(int halfEdgeIndex, int newVertexIndex, List<PlaneTraversals> intersections, out int newEdgeIndex)
        {
            newEdgeIndex = 0;
            var selfEdgeIndex   = halfEdgeIndex;
            var selfEdge	    = halfEdges[selfEdgeIndex];
            var twinEdgeIndex   = selfEdge.twinIndex;
            var twinEdge	    = halfEdges[twinEdgeIndex];

            // To simplify everything, we want to ensure that selfEdgeIndex < twinEdgeIndex in the halfEdges array
            bool swapped = false;
            if (selfEdgeIndex > twinEdgeIndex)
            {
                swapped = true;
                { var temp = selfEdgeIndex;    selfEdgeIndex    = twinEdgeIndex;    twinEdgeIndex    = temp; }
                { var temp = selfEdge;         selfEdge         = twinEdge;         twinEdge         = temp; }
            }

            // Note: halfEdges are assumed to be in order of the polygons, so if selfEdgeIndex < twinEdgeIndex, 
            //       the same is true about the polygons.
            var selfPolygonIndex	= halfEdgePolygonIndices[selfEdgeIndex];
            var twinPolygonIndex    = halfEdgePolygonIndices[twinEdgeIndex];

            var oldVertexIndex = twinEdge.vertexIndex;

            //
            //        self
            //	o<--------------
            //   --------------->
            //        twin
            //

            //
            //   n-self    self    
            //	o<----- x<------		x = new vertex
            //   ------>x ------>		o = old vertex
            //   n-twin     twin
            //

            var oldLength       = halfEdges.Length;
            
            // TODO: would be nice if we could do allocations just once in CutPolygon
            var newHalfEdges    = new HalfEdge[oldLength + 2];


            var srcNewSelfIndex = selfEdgeIndex;
            var srcNewTwinIndex = twinEdgeIndex;
            var dstNewSelfIndex = srcNewSelfIndex;
            var dstNewTwinIndex = srcNewTwinIndex;

            //halfEdges[selfEdgeIndex] = new BrushMesh.HalfEdge { vertexIndex = -1, twinIndex = -1 };
            //halfEdges[twinEdgeIndex] = new BrushMesh.HalfEdge { vertexIndex = -1, twinIndex = -1 };

            // copy all edges until selfEdgeIndex (excluding)
            // |****
            var firstStart     = 0;
            var firstEnd       = srcNewSelfIndex - 1;
            var firstEdgeCount = (firstEnd - firstStart) + 1;
            if (firstEdgeCount > 0) Array.Copy(halfEdges, newHalfEdges, firstEdgeCount);

            // set the new self halfEdge
            // |****n
            var dstOffset = 1;
            dstNewTwinIndex += dstOffset;
            newHalfEdges[dstNewSelfIndex] = new BrushMesh.HalfEdge { vertexIndex = newVertexIndex, twinIndex = dstNewTwinIndex };
            //newHalfEdges[dstNewSelfIndex] = new BrushMesh.HalfEdge { vertexIndex = -2, twinIndex = -2 };

            // copy all halfEdges, from (including) selfEdgeIndex to twinEdgeIndex (including), to after the new halfEdge
            // |****ns****t 
            var midStart        = firstEnd + 1;
            var midEnd          = srcNewTwinIndex - 1;
            var midEdgeCount    = (midEnd - midStart) + 1;
            if (midEdgeCount > 0) Array.Copy(halfEdges, midStart, newHalfEdges, dstNewSelfIndex + 1, midEdgeCount);

            // set the new twin halfEdge
            // |****ns****tn
            newHalfEdges[dstNewTwinIndex] = new BrushMesh.HalfEdge { vertexIndex = newVertexIndex, twinIndex = dstNewSelfIndex };
            //newHalfEdges[dstNewTwinIndex] = new BrushMesh.HalfEdge { vertexIndex = -2, twinIndex = -2 };
            dstOffset++;
            
            // copy everything that's left behind twinEdgeIndex (excluding) 
            // |****ns****tn*****|
            var lastStart       = midEnd + 1;
            var lastEnd         = oldLength - 1;
            var lastEdgeCount   = (lastEnd - lastStart) + 1;
            if (lastEdgeCount > 0) Array.Copy(halfEdges, lastStart, newHalfEdges, dstNewTwinIndex + 1, lastEdgeCount);
            
            /*
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("polygons (before):");
            for (int i = 0; i < polygons.Length; i++)
                builder.AppendLine("  " + i + ":" + polygons[i].ToString());
            builder.AppendLine("halfEdges:");
            for (int i = 0; i < halfEdges.Length; i++)
                builder.AppendLine("  "+i + ":" + halfEdges[i].ToString());
            builder.AppendLine("newHalfEdges (before twin fix):");
            for (int i = 0; i < newHalfEdges.Length; i++)
                builder.AppendLine("  " + i + ":" + newHalfEdges[i].ToString());
            */
            // Fix up all the twinIndices since the position of all half-edges beyond selfEdgeIndex have moved
            for (int e = 0; e < dstNewSelfIndex; e++)
            {
                var twinIndex = newHalfEdges[e].twinIndex;
                if (twinIndex < 0 || twinIndex < dstNewSelfIndex) continue;
                newHalfEdges[e].twinIndex = twinIndex + ((twinIndex >= srcNewTwinIndex) ? 2 : 1);
            }
            for (int e = dstNewSelfIndex + 1; e < dstNewTwinIndex; e++)
            {
                var twinIndex = newHalfEdges[e].twinIndex;
                if (twinIndex < 0 || twinIndex < dstNewSelfIndex) continue;
                newHalfEdges[e].twinIndex = twinIndex + ((twinIndex >= srcNewTwinIndex) ? 2 : 1);
            }
            for (int e = dstNewTwinIndex + 1; e < newHalfEdges.Length; e++)
            {
                var twinIndex = newHalfEdges[e].twinIndex;
                if (twinIndex < 0 || twinIndex < dstNewSelfIndex) continue;
                newHalfEdges[e].twinIndex = twinIndex + ((twinIndex >= srcNewTwinIndex) ? 2 : 1);
            }

            if (intersections != null)
            {
                for (int i = 0; i < intersections.Count; i++)
                {
                    var intersection = intersections[i];
                    {
                        var edgeIndex = intersection.Traversal0.EdgeIndex;
                        if (edgeIndex >= dstNewSelfIndex)
                        {
                            intersection.Traversal0.EdgeIndex = edgeIndex + ((edgeIndex >= srcNewTwinIndex) ? 2 : 1);
                        }
                    }
                    {
                        var edgeIndex = intersection.Traversal1.EdgeIndex;
                        if (edgeIndex >= dstNewSelfIndex)
                        {
                            intersection.Traversal1.EdgeIndex = edgeIndex + ((edgeIndex >= srcNewTwinIndex) ? 2 : 1);
                        }
                    }
                    intersections[i] = intersection;
                }
            }


            { var temp = newHalfEdges[dstNewSelfIndex].twinIndex; newHalfEdges[dstNewSelfIndex].twinIndex = newHalfEdges[dstNewSelfIndex + 1].twinIndex; newHalfEdges[dstNewSelfIndex + 1].twinIndex = temp; }
            { var temp = newHalfEdges[dstNewTwinIndex].twinIndex; newHalfEdges[dstNewTwinIndex].twinIndex = newHalfEdges[dstNewTwinIndex + 1].twinIndex; newHalfEdges[dstNewTwinIndex + 1].twinIndex = temp; }

            /*
            builder.AppendLine("newHalfEdges (after twin fix):");
            for (int i = 0; i < newHalfEdges.Length; i++)
                builder.AppendLine("  " + i + ":" + newHalfEdges[i].ToString());
            */

            // Fix up the polygons that we added an edge to
            polygons[selfPolygonIndex].edgeCount++;
            polygons[twinPolygonIndex].edgeCount++;

            // Fix up the firstEdge of all polygons since the position of all half-edges beyond selfEdgeIndex have moved
            polygons[0].firstEdge = 0;
            for (int p = 1; p < polygons.Length; p++)
                polygons[p].firstEdge = polygons[p - 1].firstEdge + polygons[p - 1].edgeCount;
            /*
            builder.AppendLine("polygons (after):");
            for (int i = 0; i < polygons.Length; i++)
                builder.AppendLine("  " + i + ":" + polygons[i].ToString());
            Debug.Log(builder.ToString());
            //*/

            halfEdges = newHalfEdges;

            UpdateHalfEdgePolygonIndices();
            CalculatePlanes();
            Validate(logErrors: true);
            //Debug.Log("validated");

            if (swapped)
            {
                var edgeCount = polygons[twinPolygonIndex].edgeCount;
                var firstEdge = polygons[twinPolygonIndex].firstEdge;
                newEdgeIndex = dstNewTwinIndex;// (((dstNewTwinIndex + edgeCount - firstEdge) + 1) % edgeCount) + firstEdge;
            } else
            {
                var edgeCount = polygons[selfPolygonIndex].edgeCount;
                var firstEdge = polygons[selfPolygonIndex].firstEdge;
                newEdgeIndex = dstNewSelfIndex;// (((dstNewSelfIndex + edgeCount - firstEdge) + 1) % edgeCount) + firstEdge;
            }
        }

        public void Clear()
        {
            surfaces = null;
            vertices = null;
            halfEdges = null;
            halfEdgePolygonIndices = null;
            polygons = null;
        }

        static List<VertexSide> s_VertexDistances   = new List<VertexSide>();   // avoids allocations at runtime
        static List<int>        s_IntersectedEdges  = new List<int>();          // avoids allocations at runtime

        public bool Cut(Plane cuttingPlane, in ChiselSurface chiselSurface)
        {
            if (s_IntersectedEdges != null)
                s_IntersectedEdges.Clear();

            if (polygons == null)
                return false;

            CompactHalfEdges();

            if (surfaces == null)
                CalculatePlanes();

            s_VertexDistances.Clear();
            var oldVertexCount = vertices.Length;
            for (var p = 0; p < vertices.Length; p++)
            {
                var distance	= cuttingPlane.GetDistanceToPoint(vertices[p]);
                var	halfspace	= (short)((distance < -kDistanceEpsilon) ? -1 : (distance > kDistanceEpsilon) ? 1 : 0);
                s_VertexDistances.Add(new VertexSide() { Distance  = distance, Halfspace = halfspace });
            }

            // We split all the polygons by the cutting plane (which creates new polygons at the end, so we start at the end going backwards)
            for (var p = polygons.Length - 1; p >= 0; p--)
                SplitPolygon(cuttingPlane, p, s_VertexDistances);

            for (var p = oldVertexCount; p < vertices.Length; p++)
            {
                var distance = cuttingPlane.GetDistanceToPoint(vertices[p]);
                var halfspace = (short)((distance < -kDistanceEpsilon) ? -1 : (distance > kDistanceEpsilon) ? 1 : 0);
                Debug.Assert(halfspace == 0);
                s_VertexDistances.Add(new VertexSide() { Distance = distance, Halfspace = halfspace });
            }

            // We look for all the half-edges that lie on the cutting plane, 
            // since these form the polygons on the cutting plane.
            for (int currEdgeIndex = 0; currEdgeIndex < halfEdges.Length; currEdgeIndex++)
            {
                var vertexIndex     = halfEdges[currEdgeIndex].vertexIndex;
                if (s_VertexDistances[vertexIndex].Halfspace != 0)
                    continue;

                var polygonIndex    = halfEdgePolygonIndices[currEdgeIndex];
                var firstEdge       = polygons[polygonIndex].firstEdge;
                var edgeCount       = polygons[polygonIndex].edgeCount;
                var prevEdgeIndex   = ((currEdgeIndex + edgeCount - firstEdge - 1) % edgeCount) + firstEdge;

                if (s_VertexDistances[halfEdges[prevEdgeIndex].vertexIndex].Halfspace != 0)
                    continue;

                s_IntersectedEdges.Add(currEdgeIndex);
            }

            if (s_IntersectedEdges.Count == 0)
            {
                if (s_VertexDistances[0].Halfspace < 0)
                {
                    Clear();
                    return false;
                }
                return true;
            }

            // We should have an even number of intersections
            Debug.Assert((s_IntersectedEdges.Count % 2) == 0);

            var prevIndex = s_IntersectedEdges.Count - 1;
            while (prevIndex > 0)
            {
                var currEdgeIndex1   = s_IntersectedEdges[prevIndex];

                var polygonIndex1    = halfEdgePolygonIndices[currEdgeIndex1];
                var firstEdge1       = polygons[polygonIndex1].firstEdge;
                var edgeCount1       = polygons[polygonIndex1].edgeCount;

                var prevEdgeIndex1   = ((currEdgeIndex1 + edgeCount1 - firstEdge1 - 1) % edgeCount1) + firstEdge1;
                var currVertexIndex1 = halfEdges[currEdgeIndex1].vertexIndex;
                var prevVertexIndex1 = halfEdges[prevEdgeIndex1].vertexIndex;
                var lastIndex        = prevIndex - 1;

                // We look for the next edge that starts from the current vertex, but doesn't lead to it's previous vertex
                //  (that would be an edge on a reversed polygon)
                bool found = false;
                for (int currIndex = lastIndex; currIndex >= 0; currIndex--)
                {
                    var currEdgeIndex2  = s_IntersectedEdges[currIndex];

                    var polygonIndex2    = halfEdgePolygonIndices[currEdgeIndex2];
                    var firstEdge2       = polygons[polygonIndex2].firstEdge;
                    var edgeCount2       = polygons[polygonIndex2].edgeCount;
                    

                    var prevEdgeIndex2   = ((currEdgeIndex2 + edgeCount2 - firstEdge2 - 1) % edgeCount2) + firstEdge2;
                    var currVertexIndex2 = halfEdges[currEdgeIndex2].vertexIndex;
                    var prevVertexIndex2 = halfEdges[prevEdgeIndex2].vertexIndex;

                    if (prevVertexIndex2 != currVertexIndex1 ||
                        currVertexIndex2 == prevVertexIndex1)
                        continue;

                    if (currIndex != lastIndex)
                    {
                        var t = s_IntersectedEdges[currIndex];
                        s_IntersectedEdges[currIndex] = s_IntersectedEdges[lastIndex];
                        s_IntersectedEdges[lastIndex] = t;
                    }
                    found = true;
                    break;
                }
                if (!found)
                {
                    // We have reached the half-way mark and we don't need to check the other half of 
                    // the half-edges since those are the twins of the half-edges we already found.
                    Debug.Assert(prevIndex == (s_IntersectedEdges.Count / 2));
                    
                    // So we remove the rest, leaving us with the half-edges that form the 
                    // polygon on the cutting plane.
                    s_IntersectedEdges.RemoveRange(0, prevIndex);
                    break;
                }
                prevIndex--;
            }

            var newEdgeCount    = s_IntersectedEdges.Count;
            var polygonStart1   = halfEdges.Length;
            var polygonStart2   = polygonStart1 + newEdgeCount;
            var newHalfEdges    = new HalfEdge[polygonStart2 + newEdgeCount];
            Array.Copy(halfEdges, newHalfEdges, halfEdges.Length);
            for (int i = 0, j = newEdgeCount - 1; i < newEdgeCount; i++, j--)
            {
                var edgeIndex1      = polygonStart1 + j;
                var edgeIndex2      = polygonStart2 + i;
                var srcIndex1       = s_IntersectedEdges[i];
                var srcIndex2       = halfEdges[srcIndex1].twinIndex;
                var vertexIndex1    = halfEdges[srcIndex1].vertexIndex;
                var vertexIndex2    = halfEdges[srcIndex2].vertexIndex;
                newHalfEdges[edgeIndex1] = new HalfEdge() { vertexIndex = vertexIndex1, twinIndex = srcIndex2 };
                newHalfEdges[edgeIndex2] = new HalfEdge() { vertexIndex = vertexIndex2, twinIndex = srcIndex1 };

                newHalfEdges[srcIndex2].twinIndex = edgeIndex1;
                newHalfEdges[srcIndex1].twinIndex = edgeIndex2;
            }

            var testPolygons    = new List<int>();
            {
                var polygonIndex1 = polygons.Length;
                var polygonIndex2 = polygonIndex1 + 1;
                var newPolygons = new Polygon[polygons.Length + 2];
                Array.Copy(polygons, newPolygons, polygons.Length);
                newPolygons[polygonIndex1].firstEdge        = polygonStart1;
                newPolygons[polygonIndex1].edgeCount        = newEdgeCount;
                newPolygons[polygonIndex1].surface          = chiselSurface;
                newPolygons[polygonIndex1].surfaceID        = polygonIndex1;

                newPolygons[polygonIndex2].firstEdge        = polygonStart2;
                newPolygons[polygonIndex2].edgeCount        = newEdgeCount;
                newPolygons[polygonIndex2].surface          = chiselSurface;
                newPolygons[polygonIndex2].surfaceID        = polygonIndex2;

                polygons  = newPolygons;
                halfEdges = newHalfEdges;

                UpdateHalfEdgePolygonIndices();
                CalculatePlanes();

                testPolygons.Add(polygonIndex1);
            }

            var testedPolygons  = new bool[polygons.Length];
            testedPolygons[testPolygons[0]] = true;
            while (testPolygons.Count > 0)
            {
                var polygonIndex = testPolygons[testPolygons.Count - 1];
                testPolygons.RemoveAt(testPolygons.Count - 1);

                var firstEdge = polygons[polygonIndex].firstEdge;
                var edgeCount = polygons[polygonIndex].edgeCount;
                var lastEdge  = firstEdge + edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var twinIndex = halfEdges[e].twinIndex;
                    polygonIndex = halfEdgePolygonIndices[twinIndex];
                    if (testedPolygons[polygonIndex])
                        continue;
                    testedPolygons[polygonIndex] = true;
                    testPolygons.Add(polygonIndex);
                }
            }


            var side = false;
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                if (s_VertexDistances[vertexIndex].Halfspace == 0)
                    continue;

                var polygonIndex = halfEdgePolygonIndices[e];
                side = (s_VertexDistances[vertexIndex].Halfspace > 0) == testedPolygons[polygonIndex];
                break;
            }

            for (int p = 0; p < polygons.Length; p++)
            {
                if (testedPolygons[p] == side)
                    continue;
                polygons[p].edgeCount = 0;
            }

            CompactHalfEdges();
            RemoveRedundantVertices();
            CalculatePlanes();
            UpdateHalfEdgePolygonIndices();

            Validate(logErrors: true);
            return true;
        }

        public Vector3 GetPolygonCenter(int polygonIndex)
        {
            if (polygonIndex < 0 || polygonIndex >= polygons.Length)
                throw new IndexOutOfRangeException();

            var edgeCount = polygons[polygonIndex].edgeCount;
            var firstEdge = polygons[polygonIndex].firstEdge;
            var lastEdge = firstEdge + edgeCount;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            var center = Vector3.zero;
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                var vertex = vertices[vertexIndex];
                min.x = Mathf.Min(min.x, vertex.x);
                min.y = Mathf.Min(min.y, vertex.y);
                min.z = Mathf.Min(min.z, vertex.z);
                
                max.x = Mathf.Max(max.x, vertex.x);
                max.y = Mathf.Max(max.y, vertex.y);
                max.z = Mathf.Max(max.z, vertex.z);
            }

            return (min + max) * 0.5f;
        }
        
        public Vector3 GetPolygonCentroid(int polygonIndex)
        {
            const float kMinimumArea = 1E-7f;

            if (polygonIndex < 0 || polygonIndex >= polygons.Length)
                throw new IndexOutOfRangeException();

            var edgeCount = polygons[polygonIndex].edgeCount;
            if (edgeCount < 3)
                return Vector3.zero;

            var firstEdge = polygons[polygonIndex].firstEdge;
            var lastEdge  = firstEdge + edgeCount;
            
            var vectorA = vertices[halfEdges[firstEdge    ].vertexIndex];
            var vectorB = vertices[halfEdges[firstEdge + 1].vertexIndex];

            var centroid = Vector3.zero;
            float accumulatedArea = 0.0f;
            
            for (int e = firstEdge + 2; e < lastEdge; e++)
            {
                var vertexIndex     = halfEdges[e].vertexIndex;
                var vectorC         = vertices[vertexIndex];

                var edgeCA = vectorC - vectorA;
                var edgeCB = vectorC - vectorB;
                
                var area = Vector3.Cross(edgeCA, edgeCB).magnitude;

                centroid.x += area * (vectorA.x + vectorB.x + vectorC.x);
                centroid.y += area * (vectorA.y + vectorB.y + vectorC.y);
                centroid.z += area * (vectorA.z + vectorB.z + vectorC.z);

                accumulatedArea += area;
                vectorB = vectorC;
            }

            if (Math.Abs(accumulatedArea) < kMinimumArea)
                return Vector3.zero;
            
            return centroid * (1.0f / (accumulatedArea * 3.0f));
        }


        public bool IsInside(Vector3 localPoint)
        {
            if (surfaces == null)
                return false;

            for (int s = 0; s < surfaces.Length; s++)
            {
                var localPlane = new Plane((Vector3)surfaces[s].localPlane, surfaces[s].localPlane.w);
                if (localPlane.GetDistanceToPoint(localPoint) > -kDistanceEpsilon)
                    return false;
            }
            return true;
        }

        public bool IsInsideOrOn(Vector3 localPoint)
        {
            if (surfaces == null)
                return false;

            for (int s = 0; s < surfaces.Length; s++)
            {
                var localPlane = new Plane((Vector3)surfaces[s].localPlane, surfaces[s].localPlane.w);
                if (localPlane.GetDistanceToPoint(localPoint) > kDistanceEpsilon)
                    return false;
            }
            return true;
        }

        public int FindVertexIndexOfVertex(Vector3 vertex)
        {
            for (int v = 0; v < vertices.Length; v++)
            {
                if (vertices[v].x != vertex.x ||
                    vertices[v].y != vertex.y ||
                    vertices[v].z != vertex.z)
                    continue;
                return v;
            }
            return -1;
        }

        public int FindEdgeByVertexIndices(int vertexIndex1, int vertexIndex2)
        {
            for (int e = 0; e < halfEdges.Length; e++)
            {
                if (halfEdges[e].vertexIndex != vertexIndex2)
                    continue;
                var twin = halfEdges[e].twinIndex;
                if (halfEdges[twin].vertexIndex == vertexIndex1)
                    return e;
            }
            return -1;
        }
        
        public int FindPolygonEdgeByVertexIndex(int polygonIndex, int vertexIndex)
        {
            var edgeCount = polygons[polygonIndex].edgeCount;
            var firstEdge = polygons[polygonIndex].firstEdge;
            var lastEdge = firstEdge + edgeCount;
            for (int e = firstEdge; e < lastEdge; e++)
            {
                if (halfEdges[e].vertexIndex != vertexIndex)
                    continue;
                return e;
            }
            return -1;
        }

        public int FindAnyHalfEdgeWithVertexIndex(int vertexIndex)
        {
            for (int e = 0; e < halfEdges.Length; e++)
            {
                if (halfEdges[e].vertexIndex != vertexIndex)
                    continue;
                return e;
            }
            return -1;
        }

        public bool IsVertexIndexPartOfPolygon(int polygonIndex, int vertexIndex)
        {
            ref var polygon = ref polygons[polygonIndex];

            var firstEdge = polygon.firstEdge;
            var edgeCount = polygon.edgeCount;
            var lastEdge  = firstEdge + edgeCount;
            for (int e = firstEdge; e < lastEdge; e++)
            {
                if (halfEdges[e].vertexIndex == vertexIndex)
                    return true;
            }
            return false;
        }

        public bool IsEdgeIndexPartOfPolygon(int polygonIndex, int edgeIndex)
        {
            ref var polygon = ref polygons[polygonIndex];

            var firstEdge = polygon.firstEdge;
            var edgeCount = polygon.edgeCount;
            var lastEdge = firstEdge + edgeCount;
            return (edgeIndex >= firstEdge &&
                    edgeIndex < lastEdge);
        }
    }
}