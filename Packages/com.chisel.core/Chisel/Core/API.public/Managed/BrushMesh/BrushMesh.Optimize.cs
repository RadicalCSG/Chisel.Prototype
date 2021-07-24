using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Mathematics;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public sealed partial class BrushMesh
    {
        const float kDistanceEpsilon = 0.00001f;
        const float kEqualityEpsilon = 0.0001f;

        public bool IsEmpty()
        {
            if (this.planes == null || this.planes.Length == 0)
                return true;
            if (this.polygons == null || this.polygons.Length == 0)
                return true;
            if (this.vertices == null || this.vertices.Length == 0)
                return true;
            if (this.halfEdges == null || this.halfEdges.Length == 0)
                return true;
            return false;
        }


        public void SnapPolygonVerticesToItsPlanes(int polygonIndex)
        {
            var firstEdge   = polygons[polygonIndex].firstEdge;
            var edgeCount   = polygons[polygonIndex].edgeCount;
            var lastEdge    = firstEdge + edgeCount;
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                vertices[vertexIndex] = GetVertexFromIntersectingPlanes(vertexIndex);
            }
        }

        static List<int> sSnapPlaneIndices = new List<int>();
        static List<float4> sSnapPlanes = new List<float4>();
        public float3 GetVertexFromIntersectingPlanes(int vertexIndex)
        {
            sSnapPlaneIndices.Clear();
            // TODO: precalculate this somehow
            for (int e = 0; e < halfEdges.Length; e++)
            {
                if (halfEdges[e].vertexIndex != vertexIndex)
                    continue;

                sSnapPlaneIndices.Add(halfEdgePolygonIndices[e]);
            }

            if (sSnapPlaneIndices.Count < 3)
            {
                return vertices[vertexIndex];
            }

            sSnapPlanes.Clear();
            sSnapPlaneIndices.Sort();
            for (int i = 0; i < sSnapPlaneIndices.Count; i++)
            {
                sSnapPlanes.Add(planes[sSnapPlaneIndices[i]]);
            }

            // most common case
            if (sSnapPlanes.Count == 3)
            {
                var vertex = (float3)PlaneExtensions.Intersection(sSnapPlanes[0], sSnapPlanes[1], sSnapPlanes[2]);
                if (double.IsNaN(vertex.x) || double.IsInfinity(vertex.x) ||
                    double.IsNaN(vertex.y) || double.IsInfinity(vertex.y) ||
                    double.IsNaN(vertex.z) || double.IsInfinity(vertex.z))
                {
                    Debug.LogWarning("NaN");
                    return vertices[vertexIndex];
                }
                return vertex;
            }

            double3 snappedVertex = double3.zero;
            int snappedVertexCount = 0;
            for (int a = 0; a < sSnapPlanes.Count - 2; a++)
            {
                for (int b = a + 1; b < sSnapPlanes.Count - 1; b++)
                {
                    for (int c = b + 1; c < sSnapPlanes.Count; c++)
                    {
                        // TODO: accumulate in a better way
                        var vertex = PlaneExtensions.Intersection(sSnapPlanes[a], sSnapPlanes[b], sSnapPlanes[c]);
                        if (double.IsNaN(vertex.x) || double.IsInfinity(vertex.x) ||
                            double.IsNaN(vertex.y) || double.IsInfinity(vertex.y) ||
                            double.IsNaN(vertex.z) || double.IsInfinity(vertex.z))
                            continue;

                        snappedVertex += vertex;
                        snappedVertexCount++;
                    }
                }
            }

            var finalVertex = (snappedVertex / snappedVertexCount);
            if (double.IsNaN(finalVertex.x) || double.IsInfinity(finalVertex.x) ||
                double.IsNaN(finalVertex.y) || double.IsInfinity(finalVertex.y) ||
                double.IsNaN(finalVertex.z) || double.IsInfinity(finalVertex.z))
            {
                Debug.LogWarning("NaN");
                return vertices[vertexIndex];
            }
            return (float3)finalVertex;
        }

        public Vector3 CenterAndSnapPlanes(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            Profiler.BeginSample("CenterAndSnapPlanes");
            /*
            for (int p = 0; p < polygons.Length; p++)
            {
                var plane       = planes[p];
                var edgeFirst   = polygons[p].firstEdge;
                var edgeLast    = edgeFirst + polygons[p].edgeCount;
                for (int e = edgeFirst; e < edgeLast; e++)
                {
                    var vertexIndex = halfEdges[e].vertexIndex;
                    vertices[vertexIndex] = (float3)MathExtensions.ProjectPointPlane(vertices[vertexIndex], plane);
                }
            }
            */

            var dmin = (double3)vertices[0];
            var dmax = dmin;
            for (int i = 1; i < vertices.Length; i++)
            {
                dmin = math.min(dmin, vertices[i]);
                dmax = math.max(dmax, vertices[i]);
            }

            var center = (float3)((dmin + dmax) * 0.5);

            var translate = float4x4.Translate(center);
            for (int i = 0; i < polygons.Length; i++)
            {
                ref var surface             = ref surfaceDefinition.surfaces[i];

                var localSpaceToPlaneSpace  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(planes[i]);
                var originalUVMatrix        = surface.surfaceDescription.UV0.ToFloat4x4();

                planes[i].w += math.dot(planes[i].xyz, center);
                
                var translatedPlaneSpaceToLocalSpace    = math.inverse(MathExtensions.GenerateLocalToPlaneSpaceMatrix(planes[i]));
                var newUVMatrix = math.mul(math.mul(math.mul(
                                            originalUVMatrix, 
                                            localSpaceToPlaneSpace), 
                                            translate),
                                            translatedPlaneSpaceToLocalSpace);
                
                surface.surfaceDescription.UV0 = new UVMatrix(newUVMatrix);
            }

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= center;

            Profiler.BeginSample("GetVertexFromIntersectingPlanes");
            for (int v = 0; v < vertices.Length; v++)
                vertices[v] = GetVertexFromIntersectingPlanes(v);
            Profiler.EndSample();

            Profiler.BeginSample("RemoveDegenerateTopology");
            RemoveDegenerateTopology(out _, out _);
            Profiler.EndSample();

            Profiler.BeginSample("CalculatePlanes");
            CalculatePlanes();
            Profiler.EndSample();

            //Profiler.BeginSample("SplitNonPlanarPolygons");
            //SplitNonPlanarPolygons();
            //Profiler.EndSample();

            Profiler.EndSample();
            return center;
        }

        public bool IsConcave()
        {
            bool hasConcaveEdges    = false;
            bool hasConvexEdges     = false;

            if (halfEdgePolygonIndices == null ||
                halfEdgePolygonIndices.Length != halfEdges.Length)
                UpdateHalfEdgePolygonIndices();

            // Detect if outline is concave
            for (int p = 0; p < polygons.Length; p++)
            {
                var localPlane = new Plane(planes[p].xyz, planes[p].w); 
                ref readonly var polygon = ref polygons[p];
                var firstEdge = polygon.firstEdge;
                var edgeCount = polygon.edgeCount;
                var lastEdge = firstEdge + edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    // Find the edge on the other side of our edge, so we can find it's polygon
                    var twin = halfEdges[e].twinIndex;
                    // Only need to check half the half-edges
                    if (twin < e)
                        continue;

                    if (twin >= halfEdgePolygonIndices.Length)
                        return true;

                    // Find polygon of our twin edge
                    var twinPolygonIndex = halfEdgePolygonIndices[twin]; 
                    ref readonly var twinPolygon = ref polygons[twinPolygonIndex];
                    var twinFirstEdge = twinPolygon.firstEdge;
                    var twinEdgeCount = twinPolygon.edgeCount;

                    var iterator = twin;
                    do
                    {
                        // Find next edge on twinPolygon 
                        iterator = (((iterator - twinFirstEdge) + 1) % twinEdgeCount) + twinFirstEdge;
                        var vertexIndex = halfEdges[iterator].vertexIndex;
                        var distance = localPlane.GetDistanceToPoint(vertices[vertexIndex]);
                        if (distance < -kDistanceEpsilon)
                        {
                            hasConvexEdges = true;
                            // the vertex is on the inside of the plane, so this edge is convex
                            break;
                        }

                        if (distance > kDistanceEpsilon)
                        {
                            hasConcaveEdges = true;
                            // the vertex is on the inside of the plane, so this edge is convex
                            break;
                        }

                        // This particular vertex was apparently *on* the plane, so we'll try the next vertex
                    } while (iterator != twin);
                }
            }
            return (hasConcaveEdges && hasConvexEdges);
        }

        public bool IsSelfIntersecting()
        {
            // TODO: determine if the brush is intersecting itself
            return false;
        }

        public bool HasVolume()
        {
            if (polygons == null || polygons.Length == 0)
                return false;
            if (vertices == null || vertices.Length == 0)
                return false;
            if (halfEdges == null || halfEdges.Length == 0)
                return false;
            // TODO: determine if the brush is a singularity (1D) or flat (2D), or has a volume (3D)
            return true;
        }

        public bool IsInsideOut()
        {
            if (polygons == null)
                return false;

            if (halfEdgePolygonIndices == null)
                UpdateHalfEdgePolygonIndices();

            if (planes == null)
                CalculatePlanes();

            // Detect if outline is inside-out
            for (int p = 0; p < polygons.Length; p++)
            {
                var localPlane = new Plane(planes[p].xyz, planes[p].w);
                ref readonly var polygon = ref polygons[p];
                var firstEdge = polygon.firstEdge;
                var edgeCount = polygon.edgeCount;
                var lastEdge = firstEdge + edgeCount;
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    // Find the edge on the other side of our edge, so we can find it's polygon
                    var twin = halfEdges[e].twinIndex;
                    // Only need to check half the half-edges
                    if (twin < e)
                        continue;

                    // Find polygon of our twin edge
                    var twinPolygonIndex = halfEdgePolygonIndices[twin];
                    ref readonly var twinPolygon = ref polygons[twinPolygonIndex];
                    var twinFirstEdge = twinPolygon.firstEdge;
                    var twinEdgeCount = twinPolygon.edgeCount;

                    var iterator = twin;
                    do
                    {
                        // Find next edge on twinPolygon 
                        iterator = (((iterator - twinFirstEdge) + 1) % twinEdgeCount) + twinFirstEdge;
                        var vertexIndex = halfEdges[iterator].vertexIndex;
                        var distance = localPlane.GetDistanceToPoint(vertices[vertexIndex]);
                        if (distance < -kDistanceEpsilon)
                            // the vertex is on the inside of the plane, so it's definitely not inverted (could still be concave/self intersecting)
                            return false;

                        if (distance > kDistanceEpsilon)
                            // If it's on the outside, it might still be inverted. So lets try the next edge
                            break;

                        // This particular vertex was apparently *on* the plane, so we'll try the next vertex
                    } while (iterator != twin);
                }
            }
            return true;
        }

        public void InvertWhenInsideOut()
        {
            if (IsInsideOut())
                // invert everything
                Invert();
        }

        public void Invert()
        {
            for (int e = 0; e < halfEdges.Length; e++)
            {
                var twin = halfEdges[e].twinIndex;
                // Only need to swizzle half the half-edges
                if (twin < e)
                    continue;

                var vertex1 = halfEdges[e].vertexIndex;
                var vertex2 = halfEdges[twin].vertexIndex;

                halfEdges[e].vertexIndex    = vertex2;
                halfEdges[twin].vertexIndex = vertex1;
            }
            for (int p = 0; p < polygons.Length; p++)
            {
                ref readonly var polygon = ref polygons[p];
                var firstEdge = polygon.firstEdge;
                var edgeCount = polygon.edgeCount;
                var lastEdge  = firstEdge + (edgeCount - 1);
                var half      = (edgeCount / 2);
                for (int n = 0; n < half; n++)
                {
                    var temp = halfEdges[firstEdge + n];
                    halfEdges[firstEdge + n] = halfEdges[lastEdge - n];
                    halfEdges[lastEdge - n] = temp;
                }

                for (int e = firstEdge; e <= lastEdge; e++)
                    halfEdges[halfEdges[e].twinIndex].twinIndex = e;
            }
            for (int s = 0; s < planes.Length; s++)
                planes[s] = -planes[s];
            Validate(logErrors: true);
        }

        public bool SplitNonPlanarPolygons()
        {
            if (this.polygons == null)
                return false;

            var orgPolygons               = this.polygons;
            var orgVertices               = this.vertices;
            var orgHalfEdges              = this.halfEdges;
            var orgHalfEdgePolygonIndices = this.halfEdgePolygonIndices;
            var orgPlanes                 = this.planes;

            var polygons                = orgPolygons;
            var vertices                = orgVertices;
            var halfEdges               = orgHalfEdges;
            var halfEdgePolygonIndices  = orgHalfEdgePolygonIndices;
            var planes                  = orgPlanes;

            bool haveSplitPolygons = false;
            for (int p = this.polygons.Length - 1; p >= 0; p--)
            {
                var edgeCount   = polygons[p].edgeCount;
                if (edgeCount <= 3)
                    continue;

                var firstEdge   = polygons[p].firstEdge;
                var lastEdge    = firstEdge + edgeCount;
                var plane       = new Plane(planes[p].xyz, planes[p].w);

                bool isPlanar = true;
                for (int e = firstEdge; e < lastEdge; e++)
                {
                    var vertexIndex = halfEdges[e].vertexIndex;
                    var vertex      = vertices[vertexIndex];

                    if (Mathf.Abs(plane.GetDistanceToPoint(vertex)) > kDistanceEpsilon)
                    {
                        isPlanar = false;
                        break;
                    }
                }

                if (isPlanar)
                    continue;
                
                var triangles = SplitNonPlanarPolygon(this, ref polygons[p]);
                if (triangles == null ||
                    triangles.Length <= 1)
                    continue;
                
                var newEdgeCount            = 3;
                var extraPolygons           = (triangles.Length - 1);
                var extraHalfEdges          = extraPolygons * 3;
                var edgeOffsetBeyondPolygon = edgeCount - newEdgeCount;

                var newPolygons = new Polygon[this.polygons.Length + extraPolygons];
                for (int pb = 0; pb < this.polygons.Length; pb++)
                    newPolygons[pb] = this.polygons[pb];
                for (int pb = p + 1; pb < this.polygons.Length; pb++)
                    newPolygons[pb].firstEdge -= edgeOffsetBeyondPolygon;

                var newHalfEdgeCount            = halfEdges.Length - edgeOffsetBeyondPolygon + extraHalfEdges;
                var newHalfEdgePolygonIndices   = new int[newHalfEdgeCount];
                var newHalfEdges                = new HalfEdge[newHalfEdgeCount];
                for (int e = 0; e < newHalfEdges.Length; e++)
                    newHalfEdges[e].twinIndex = -1;
                Array.ConstrainedCopy(halfEdges, 0, 
                                      newHalfEdges, 0, 
                                      firstEdge + newEdgeCount);
                Array.ConstrainedCopy(halfEdgePolygonIndices, 0, 
                                      newHalfEdgePolygonIndices, 0, 
                                      firstEdge + newEdgeCount);
                //var lastIndex = halfEdges.Length - lastEdge;
                if (lastEdge < halfEdges.Length)
                {
                    Array.ConstrainedCopy(halfEdges,    lastEdge, 
                                          newHalfEdges, lastEdge - edgeOffsetBeyondPolygon, 
                                          halfEdges.Length - lastEdge);
                    Array.ConstrainedCopy(halfEdgePolygonIndices,    lastEdge, 
                                          newHalfEdgePolygonIndices, lastEdge - edgeOffsetBeyondPolygon, 
                                          halfEdges.Length - lastEdge);
                }
                for (int e = 0; e < halfEdges.Length - edgeOffsetBeyondPolygon; e++)
                {
                    var twinIndex = newHalfEdges[e].twinIndex;
                    if (twinIndex >= lastEdge)
                        newHalfEdges[e].twinIndex = twinIndex - edgeOffsetBeyondPolygon;
                }
                //Debug.Log(p + " " + firstEdge + " " + lastEdge + " " + (firstEdge + newEdgeCount) + " " + triangles.Length + " " + newPolygons.Length + " " + polygons.Length);

                newPolygons[p].edgeCount = newEdgeCount;
                for (int e = 0; e < 3; e++)
                {
                    newHalfEdges[firstEdge + e].vertexIndex = triangles[0].vertexIndices[e];
                    newHalfEdges[firstEdge + e].twinIndex = -1;
                    newHalfEdgePolygonIndices[firstEdge + e] = p;
                }
                for (int pb = this.polygons.Length, t = 1, first = halfEdges.Length - edgeOffsetBeyondPolygon; t < triangles.Length; pb++, t++, first += 3)
                {
                    newPolygons[pb] = this.polygons[p];
                    newPolygons[pb].firstEdge = first;
                    newPolygons[pb].edgeCount = 3;
                    for (int e = 0; e < 3; e++)
                    {
                        newHalfEdges[first + e].vertexIndex = triangles[t].vertexIndices[e];
                        newHalfEdges[first + e].twinIndex = -1;
                        newHalfEdgePolygonIndices[first + e] = pb;
                    }
                }

                for (int e1 = 2, e2 = 0; e2 < 3; e1 = e2, e2++)
                {
                    var prevVertexIndex = triangles[0].vertexIndices[e1];
                    var currVertexIndex = triangles[0].vertexIndices[e2];

                    var twinIndex = FindTwinIndex(halfEdges, triangles, newPolygons, firstEdge, lastEdge, p, polygons.Length, edgeOffsetBeyondPolygon,
                                                  prevVertexIndex, currVertexIndex, p);
                    if (twinIndex == -1)
                    {
                        Debug.Log("FAIL " + p + "/0:" + e2 + " | " + (firstEdge + e1) + " " + (firstEdge + e2) + " | " + prevVertexIndex + " " + currVertexIndex);
                        continue;
                    }

                    newHalfEdges[firstEdge + e2].twinIndex = twinIndex;
                    newHalfEdges[twinIndex].twinIndex = firstEdge + e2;
                }

                for (int pb = this.polygons.Length, t = 1, first = halfEdges.Length - edgeOffsetBeyondPolygon; t < triangles.Length; pb++, t++, first += 3)
                {
                    for (int e1 = 2, e2 = 0; e2 < 3; e1 = e2, e2++)
                    {
                        var prevVertexIndex = triangles[t].vertexIndices[e1];
                        var currVertexIndex = triangles[t].vertexIndices[e2];

                        var twinIndex = FindTwinIndex(halfEdges, triangles, newPolygons, firstEdge, lastEdge, p, polygons.Length, edgeOffsetBeyondPolygon,
                                                      prevVertexIndex, currVertexIndex, pb);
                        if (twinIndex == -1)
                        {
                            Debug.Log("FAIL " + pb + "/" + t + ":" + e2 + " | " + (first + e1) + " " + (first + e2) + " | " + prevVertexIndex + " " + currVertexIndex);
                            continue;
                        }

                        newHalfEdges[first + e2].twinIndex = twinIndex;
                        newHalfEdges[twinIndex].twinIndex = first + e2; 
                    }
                }

                this.polygons               = newPolygons;
                this.halfEdgePolygonIndices = newHalfEdgePolygonIndices;
                this.halfEdges              = newHalfEdges;
                this.CalculatePlanes();
                if (!this.Validate())
                {                    
                    this.polygons               = orgPolygons;
                    this.vertices               = orgVertices;
                    this.halfEdges              = orgHalfEdges;
                    this.halfEdgePolygonIndices = orgHalfEdgePolygonIndices;
                    this.planes                 = orgPlanes;

                    this.CalculatePlanes();
                    continue;
                }
                polygons                = this.polygons;
                vertices                = this.vertices;
                halfEdges               = this.halfEdges;
                halfEdgePolygonIndices  = this.halfEdgePolygonIndices;
                planes                = this.planes;

                haveSplitPolygons = true;
            }

            return haveSplitPolygons;
        }
        
        public void RemoveDegenerateTopology(out int[] edgeRemap, out int[] polygonRemap)
        {
            edgeRemap = null;
            polygonRemap = null;
            const float kDistanceEpsilon = 0.0001f; // TODO: why??

            // TODO: optimize

            // FIXME: This piece of code might not work correctly when you have vertices very close to each other, 
            //        but in a row so that the last vertices are not close to the first, but each is close to it's neighbor
            //        VERIFY CORRECTNESS
            int[] vertexRemapping = null;
            for (int v0 = 0; v0 < vertices.Length - 1; v0++)
            {
                // If this vertex has already been remapped we skip it
                if (vertexRemapping != null &&
                    vertexRemapping[v0] != 0)
                    continue;

                var vertexV0 = vertices[v0];
                var newVertex = vertexV0;
                var overlapCount = 1;
                for (int v1 = v0 + 1; v1 < vertices.Length; v1++)
                {
                    // If this vertex has already been remapped we skip it
                    if (vertexRemapping != null &&
                        vertexRemapping[v1] != 0)
                        continue;

                    var vertexV1 = vertices[v1];
                    if (math.lengthsq(vertexV0 - vertexV1) >= kDistanceEpsilon)
                        continue;

                    if (vertexRemapping == null)
                        vertexRemapping = new int[vertices.Length];
                    vertexRemapping[v1] = (v0 + 1);
                    overlapCount++;
                    newVertex += vertexV1;
                }
                if (overlapCount > 1)
                {
                    newVertex /= overlapCount;
                    vertices[v0] = newVertex;
                }
            }
            if (vertexRemapping == null)
                return;

            for (int h = 0; h < halfEdges.Length; h++)
            {
                var originalVertexIndex = halfEdges[h].vertexIndex;
                var remap = vertexRemapping[originalVertexIndex];
                if (remap == 0)
                    continue;
                halfEdges[h].vertexIndex = remap - 1;
            }

            // TODO: remove unused vertices. Note that we might want to keep the original vertex indices so we can 
            //      detect newly generated "soft" edges when splitting non planar polygons

            // Make sure polygons are laid out sequentially
            Array.Sort(polygons, delegate (Polygon x, Polygon y) { return x.firstEdge - y.firstEdge; });

            edgeRemap       = new int[halfEdges.Length];
            var edgeCounts  = new int[polygons.Length];
            var offset = 0;
            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var edgeCount = polygons[p].edgeCount;
                var lastEdge = firstEdge + edgeCount;

                var prevOffset = offset;
                for (int e0 = lastEdge - 1, e1 = firstEdge; e1 < lastEdge; e0 = e1, e1++)
                {
                    if (halfEdges[e0].vertexIndex != halfEdges[e1].vertexIndex)
                    {
                        edgeRemap[e1] = e1;
                        continue;
                    }
                    edgeRemap[e1] = -1;
                    halfEdges[e1].vertexIndex = -1;
                    halfEdges[e1].twinIndex = -1;
                    offset++;
                }

                var newEdgeCount = edgeCount - (offset - prevOffset);
                if (newEdgeCount == 2)
                {
                    int edge1 = firstEdge;
                    while (edgeRemap[edge1] == -1) { edge1++; if (edge1 >= lastEdge) throw new InvalidOperationException("This should not be possible"); }

                    int edge2 = edge1;
                    do { edge2++; if (edge2 >= lastEdge) throw new InvalidOperationException("This should not be possible"); } while (edgeRemap[edge2] == -1);

                    var twin1 = halfEdges[edge1].twinIndex;
                    var twin2 = halfEdges[edge2].twinIndex;

                    halfEdges[twin1].twinIndex = twin2;
                    halfEdges[twin2].twinIndex = twin1;

                    edgeRemap[edge1] = -1; 
                    halfEdges[edge1].vertexIndex = -1;
                    halfEdges[edge1].twinIndex = -1;
                    offset++;

                    edgeRemap[edge2] = -1; 
                    halfEdges[edge2].vertexIndex = -1;
                    halfEdges[edge2].twinIndex = -1;
                    offset++;

                    newEdgeCount = 0;
                }
                edgeCounts[p] = newEdgeCount;
            }

            if (offset == 0)
                return;

            offset = 0;
            for (int h = 0; h < edgeRemap.Length; h++)
            {
                if (edgeRemap[h] == -1)
                {
                    Debug.Assert(halfEdges[h].vertexIndex == -1);
                    offset++;
                    continue;
                }

                Debug.Assert(halfEdges[h].vertexIndex != -1);
                edgeRemap[h] -= offset;
            }

            polygonRemap = new int[polygons.Length];

            var newPolygons = new List<Polygon>();
            for (int p = 0; p < polygons.Length; p++)
            {
                var edgeCount = edgeCounts[p];
                if (edgeCount <= 2)
                {
                    polygonRemap[p] = -1;
                    continue;
                }

                var polygon = polygons[p];

                var firstEdge = polygon.firstEdge;
                for (int i = 0; i < edgeCount; i++)
                {
                    var remap = edgeRemap[firstEdge + i];
                    if (remap == -1)
                        continue;
                    Debug.Assert(halfEdges[firstEdge + i].vertexIndex != -1);
                    polygon.firstEdge = remap;
                    break;
                }
                polygon.edgeCount = edgeCount;
                polygonRemap[p] = newPolygons.Count;
                newPolygons.Add(polygon);
            }
            polygons = newPolygons.ToArray();

            var newHalfEdges = new HalfEdge[halfEdges.Length - offset];
            for (int h = 0, n = 0; h < halfEdges.Length; h++)
            {
                if (edgeRemap[h] == -1)
                    continue;

                Debug.Assert(halfEdges[h].vertexIndex != -1);
                newHalfEdges[n] = halfEdges[h];
                n++;
            }
            for (int n = 0; n < newHalfEdges.Length; n++)
            {
                var remap = edgeRemap[newHalfEdges[n].twinIndex];
                Debug.Assert(newHalfEdges[remap].vertexIndex != -1, "halfEdges[" + remap + "].vertexIndex == -1");
                newHalfEdges[n].twinIndex = remap;
            }
            halfEdges = newHalfEdges;
        }

        public int[] RemoveUnusedVertices()
        {
            var usedVertices = new bool[vertices.Length];
            for (int e = 0; e < halfEdges.Length; e++)
                usedVertices[halfEdges[e].vertexIndex] = true;

            var vertexLookup = new int[vertices.Length];
            var newVertices = new List<float3>(vertices.Length);
            for (int v = 0; v < vertices.Length; v++)
            {
                if (usedVertices[v])
                {
                    vertexLookup[v] = newVertices.Count;
                    newVertices.Add(vertices[v]);
                } else
                    vertexLookup[v] = -1;
            }

            if (newVertices.Count == vertices.Length)
                return null;

            vertices = newVertices.ToArray();
            for (int e = 0; e < halfEdges.Length; e++)
                halfEdges[e].vertexIndex = vertexLookup[halfEdges[e].vertexIndex];

            // TODO: fixup vertexRemap table to include overlapping vertices

            return vertexLookup;
        }
    }
}
