using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

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

        static float3 ProjectPointPlane(float3 point, float4 plane)
        {
            float px = point.x;
            float py = point.y;
            float pz = point.z;

            float nx = plane.x;
            float ny = plane.y;
            float nz = plane.z;

            float ax = (px + (nx * plane.w)) * nx;
            float ay = (py + (ny * plane.w)) * ny;
            float az = (pz + (nz * plane.w)) * nz;
            float dot = ax + ay + az;

            float rx = px - (dot * nx);
            float ry = py - (dot * ny);
            float rz = pz - (dot * nz);

            return new float3(rx, ry, rz);
        }

        public Vector3 CenterAndSnapPlanes()
        {
            for (int p = 0; p < polygons.Length; p++)
            {
                var plane       = planes[p];
                var edgeFirst   = polygons[p].firstEdge;
                var edgeLast    = edgeFirst + polygons[p].edgeCount;
                for (int e = edgeFirst; e < edgeLast; e++)
                {
                    var vertexIndex = halfEdges[e].vertexIndex;
                    vertices[vertexIndex] = ProjectPointPlane(vertices[vertexIndex], plane);
                }
            }

            double3 dmin = (double3)vertices[0];
            double3 dmax = (double3)vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                dmin = math.min(dmin, vertices[i]);
                dmax = math.max(dmax, vertices[i]);
            }
            var center = (float3)((dmin + dmax) * 0.5);
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] -= center;
            return center;
        }

        public bool IsConcave()
        {
            bool hasConcaveEdges    = false;
            bool hasConvexEdges     = false;
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


        private sealed class Triangle
        {
            public int[]        vertexIndices;
            public Plane        localPlane;
        }




        private sealed class TriangulationPath
        {
            public TriangulationPath[]  subPaths;
            public int                  vertexIndex;
            public int[]                triangles;
            public float                heuristic;
        }

        private sealed class TrianglePathCache
        {
            public readonly Dictionary<UInt64, int>	HardEdges		= new Dictionary<UInt64, int>();
            public readonly Dictionary<UInt64, int>	TriangleIndices	= new Dictionary<UInt64, int>();
            public readonly List<Triangle>			AllTriangles	= new List<Triangle>();
            public TriangulationPath			    Path            = new TriangulationPath();
        }

        private static class VertPair
        {
            public static ulong Create(int a, int b)
            {
                var a32 = (ulong)a;
                var b32 = (ulong)b;
                return (a < b) ?
                        (a32 | (b32 << 32)) :
                        (b32 | (a32 << 32));
            }
        }

        private static class TriangleIndex
        {
            public static ulong Create(int a, int b, int c)
            {
                // FIXME: assumes vertex indices are 16-bit
                var a32 = (ulong)a;
                var b32 = (ulong)b;
                var c32 = (ulong)c;
                if (a < b)
                {
                    if (c > b) return a32 | (b32 << 16) | (c32 << 32);
                    if (c > a) return a32 | (c32 << 16) | (b32 << 32);
                    return c32 | (a32 << 16) | (b32 << 32);
                } else
                {
                    if (c > a) return b32 | (a32 << 16) | (c32 << 32);
                    if (c > b) return b32 | (c32 << 16) | (a32 << 32);
                    return c32 | (b32 << 16) | (a32 << 32);
                }
            }
        }
        
        private static Triangle FindTriangleWithEdge(int[] triangles, TrianglePathCache cache, int vertexIndex0, int vertexIndex1)
        {
            if (triangles == null)
                return null;

            for (var i = 0; i < triangles.Length; i++)
            {
                var triangle            = cache.AllTriangles[triangles[i]];
                var vertexIndices       = triangle.vertexIndices;
                var edge0VertexIndex    = vertexIndices[0];
                var edge1VertexIndex    = vertexIndices[1];
                var edge2VertexIndex    = vertexIndices[2];

                if ((edge0VertexIndex != vertexIndex0 || edge1VertexIndex != vertexIndex1) &&
                    (edge0VertexIndex != vertexIndex0 || edge2VertexIndex != vertexIndex1) &&
                    (edge1VertexIndex != vertexIndex0 || edge0VertexIndex != vertexIndex1) &&
                    (edge1VertexIndex != vertexIndex0 || edge2VertexIndex != vertexIndex1) &&
                    (edge2VertexIndex != vertexIndex0 || edge0VertexIndex != vertexIndex1) &&
                    (edge2VertexIndex != vertexIndex0 || edge1VertexIndex != vertexIndex1))
                    continue;

                return triangle;
            }
            return null;
        }

        private static float GetEdgeHeuristic(TrianglePathCache cache, float4[] planes, float3[] vertices, int vi0, int vi1, int vi2)
        {
            int polygonIndex;
            if (!cache.HardEdges.TryGetValue(VertPair.Create(vi0, vi1), out polygonIndex))
                return 0;
            
            if (polygonIndex < 0 || polygonIndex >= planes.Length)
                return float.PositiveInfinity;
            var error = math.dot(planes[polygonIndex].xyz, vertices[vi2] - vertices[vi1]) - 1;
            return (error * error);
        }
        
        private static float GetTriangleHeuristic(TrianglePathCache cache, float4[] planes, float3[] vertices, int vi0, int vi1, int vi2)
        {
            var pair0 = VertPair.Create(vi0, vi1);
            var pair1 = VertPair.Create(vi1, vi2);
            var pair2 = VertPair.Create(vi2, vi0);
                
            return GetEdgeHeuristic(cache, planes, vertices, vi0, vi1, vi2) +
                   GetEdgeHeuristic(cache, planes, vertices, vi1, vi2, vi0) +
                   GetEdgeHeuristic(cache, planes, vertices, vi2, vi0, vi1);
        }

        private static float SplitAtEdge(out int[] triangles, TrianglePathCache cache, int[] vertexIndices, int vertexIndicesLength, float3[] vertices, float4[] planes)
        {
            if (vertexIndicesLength == 3)
            {
                var vi0 = vertexIndices[0];
                var vi1 = vertexIndices[1];
                var vi2 = vertexIndices[2];

                if (vi0 < 0 || vi0 >= vertices.Length ||
                    vi1 < 0 || vi1 >= vertices.Length ||
                    vi2 < 0 || vi2 >= vertices.Length)
                {
                    triangles = null;
                    return float.PositiveInfinity;
                }

                var triangleIndex = TriangleIndex.Create(vi0, vi1, vi2);
                Triangle triangle;
                if (!cache.TriangleIndices.TryGetValue(triangleIndex, out int index))
                {
                    triangle = new Triangle
                    {
                        vertexIndices   = new[] { vi0, vi1, vi2 },
                        localPlane      = new Plane(vertices[vi0], vertices[vi1], vertices[vi2])
                    };
                    index = cache.AllTriangles.Count;
                    cache.TriangleIndices[triangleIndex] = index;
                    cache.AllTriangles.Add(triangle);
                } else
                    triangle = cache.AllTriangles[index];

                triangles = new [] { index };
                return GetTriangleHeuristic(cache, planes, vertices, vi0, vi1, vi2);
            }

            TriangulationPath curLeftPath   = null;
            TriangulationPath curRightPath  = null;

            float curHeuristic  = float.PositiveInfinity;
            int[] tempEdges     = null;
            for (var startPoint = 0; startPoint < vertexIndicesLength - 2; startPoint++)
            {
                for (var offset = 2; offset < vertexIndicesLength - 1; offset++)
                {
                    var endPoint = (startPoint + offset) % vertexIndicesLength;
                    int t0, t1;
                    if (endPoint < startPoint) { t0 = endPoint;   t1 = startPoint; }
                    else                       { t0 = startPoint; t1 = endPoint; }
                    var vertexIndex0	= vertexIndices[t0];
                    var vertexIndex1	= vertexIndices[t1];

                    var leftPath = cache.Path;
                    var startIndex = -1;
                    // try to find the triangulation in the cache
                    for (var i = t0; i <= t1; i++)
                    {
                        if (leftPath.subPaths == null) { startIndex = i; break; }
                        var	index = vertexIndices[i];
                        var found = false;
                        for (var j = 0; j < leftPath.subPaths.Length; j++)
                        {
                            if (leftPath.subPaths[j].vertexIndex != index)
                                continue;

                            found = true;
                            leftPath = leftPath.subPaths[j];
                            break;
                        }
                        if (found)
                            continue;

                        startIndex = i;
                        break;
                    }
                    
                    float leftHeuristic;
                    #region Left Path
                    int[] leftTriangles;
                    if (startIndex != -1 || leftPath.triangles == null)
                    {
                        var length0 = (t1 - t0) + 1;
                        if (tempEdges == null || tempEdges.Length < length0) tempEdges = new int[length0];

                        Array.Copy(vertexIndices, t0, tempEdges, 0, (t1 - t0) + 1);

                        // triangulate for the given vertices
                        leftHeuristic = SplitAtEdge(out leftTriangles, cache, tempEdges, length0, vertices, planes);

                        // store the found triangulation in the cache
                        if (startIndex != -1)
                        {
                            for (var i = startIndex; i <= t1; i++)
                            {
                                var newSubPath = new TriangulationPath {vertexIndex = vertexIndices[i]}; // FIXME: shouldn't this be tempEdges?
                                if (leftPath.subPaths == null) { leftPath.subPaths = new[] { newSubPath }; }
                                else
                                {
                                    System.Array.Resize(ref leftPath.subPaths, leftPath.subPaths.Length + 1);
                                    leftPath.subPaths[leftPath.subPaths.Length - 1] = newSubPath;
                                }
                                leftPath = newSubPath;
                            }
                        }

                        leftPath.triangles	= leftTriangles;
                        leftPath.heuristic	= leftHeuristic;
                    } else
                    {
                        leftHeuristic		= leftPath.heuristic;
                        leftTriangles		= leftPath.triangles;
                    }
                    #endregion

                    var newHeuristic = leftHeuristic;
                    if (newHeuristic >= curHeuristic + kEqualityEpsilon)
                        continue;
                    
                    
                    var offsetB = (vertexIndicesLength - t1);
                    var length1 = (t0 + 1) + offsetB;
                    if (tempEdges == null || tempEdges.Length < length1) tempEdges = new int[length1];

                    Array.Copy(vertexIndices, t1, tempEdges, 0, offsetB);
                    Array.Copy(vertexIndices, 0, tempEdges, offsetB, (t0 + 1));
                    
                    var	rightPath = cache.Path;
                    startIndex = -1;
                    // try to find the triangulation in the cache
                    for (int i = 0; i < length1; i++)
                    {
                        if (rightPath.subPaths == null) { startIndex = i; break; }
                        var		index = tempEdges[i];
                        bool	found = false;
                        for (int j = 0; j < rightPath.subPaths.Length; j++)
                        {
                            if (rightPath.subPaths[j].vertexIndex == index) { found = true; rightPath = rightPath.subPaths[j]; break; }
                        }
                        if (!found) { startIndex = i; break; }
                    }
                    
                    float rightHeuristic;
                    #region Right Path
                    int[] rightTriangles;
                    if (startIndex != -1 || rightPath.triangles == null)
                    {
                        // triangulate for the given vertices
                        rightHeuristic = SplitAtEdge(out rightTriangles, cache, tempEdges, length1, vertices, planes);
                        
                        // store the found triangulation in the cache
                        if (startIndex != -1)
                        { 
                            for (var i = startIndex; i < tempEdges.Length; i++)
                            {
                                var newSubPath = new TriangulationPath { vertexIndex = tempEdges[i] };
                                if (rightPath.subPaths == null)	{ rightPath.subPaths = new[] { newSubPath }; }
                                else
                                {
                                    System.Array.Resize(ref rightPath.subPaths, rightPath.subPaths.Length + 1);
                                    rightPath.subPaths[rightPath.subPaths.Length - 1] = newSubPath;
                                }
                                rightPath = newSubPath;
                            }
                        }

                        rightPath.triangles	= rightTriangles;
                        rightPath.heuristic	= rightHeuristic;
                    } else
                    {
                        rightHeuristic		= rightPath.heuristic;
                        rightTriangles		= rightPath.triangles;
                    }
                    #endregion

                    newHeuristic += rightHeuristic;
                    if (newHeuristic >= curHeuristic + kEqualityEpsilon)
                        continue;
                    
                    var leftTriangle	= FindTriangleWithEdge(leftTriangles,  cache, vertexIndex0, vertexIndex1);
                    var rightTriangle	= FindTriangleWithEdge(rightTriangles, cache, vertexIndex0, vertexIndex1);

                    if (leftTriangle == null ||
                        rightTriangle == null)
                        continue;

                    var leftPlane	= leftTriangle .localPlane;
                    var rightPlane	= rightTriangle.localPlane;
                    var error		= Vector3.Dot(leftPlane.normal, rightPlane.normal) - 1;
                    newHeuristic += (error * error);

                    if (!(newHeuristic < curHeuristic - kEqualityEpsilon))
                        continue;

                    curLeftPath	    = leftPath;
                    curRightPath	= rightPath;
                    curHeuristic	= newHeuristic;
                }
            }
            if (curLeftPath != null &&
                curRightPath != null)
            {
                triangles = new int[curLeftPath.triangles.Length + curRightPath.triangles.Length];
                Array.Copy(curLeftPath.triangles,     triangles, curLeftPath.triangles.Length);
                Array.Copy(curRightPath.triangles, 0, triangles, curLeftPath.triangles.Length, curRightPath.triangles.Length);
            } else
            {
                curHeuristic = float.PositiveInfinity;
                triangles = null;
            }
            return curHeuristic;
        }

        private static Triangle[] SplitNonPlanarPolygon(BrushMesh subMesh, ref Polygon polygon)
        {
            var sTrianglePathCache      = new TrianglePathCache();

            var halfEdges               = subMesh.halfEdges;
            var vertices                = subMesh.vertices;
            var surfaces                = subMesh.planes;
            var halfEdgePolygonIndices  = subMesh.halfEdgePolygonIndices;

            var firstEdge               = polygon.firstEdge;
            var edgeCount               = polygon.edgeCount;
            var lastEdge                = firstEdge + edgeCount;

            for (var e = firstEdge; e < lastEdge; e++)
            {
                var twinIndex           = halfEdges[e].twinIndex;
                var curVertexIndex      = halfEdges[e].vertexIndex;
                var polygonIndex        = halfEdgePolygonIndices[e];
                var twinVertexIndex     = halfEdges[twinIndex].vertexIndex;
                var pair                = VertPair.Create(curVertexIndex, twinVertexIndex);
                sTrianglePathCache.HardEdges[pair] = polygonIndex;
            }

            var sPolyVerts = new int[edgeCount];

            for (var i = 0; i < edgeCount; i++)
                sPolyVerts[i] = halfEdges[i + firstEdge].vertexIndex;

            SplitAtEdge(out int[] foundTriangleIndices, sTrianglePathCache, sPolyVerts, edgeCount, vertices, surfaces);

            var sNewTriangles = new List<Triangle>();
            while (foundTriangleIndices != null && foundTriangleIndices.Length > 0)
            {
                if (foundTriangleIndices.Length == 1)
                {
                    sNewTriangles.Add(sTrianglePathCache.AllTriangles[foundTriangleIndices[0]]);
                    break;
                }

                var found = -1;
                var foundEdgeCount = 0;
                for (var t = 0; t < foundTriangleIndices.Length; t++)
                {
                    var triangle = sTrianglePathCache.AllTriangles[foundTriangleIndices[t]];
                    var vertPair1 = VertPair.Create(triangle.vertexIndices[0], triangle.vertexIndices[1]);
                    var vertPair2 = VertPair.Create(triangle.vertexIndices[1], triangle.vertexIndices[2]);
                    var vertPair3 = VertPair.Create(triangle.vertexIndices[2], triangle.vertexIndices[0]);

                    var hardEdgeCount = 0;
                    if (sTrianglePathCache.HardEdges.ContainsKey(vertPair1)) hardEdgeCount++;
                    if (sTrianglePathCache.HardEdges.ContainsKey(vertPair2)) hardEdgeCount++;
                    if (sTrianglePathCache.HardEdges.ContainsKey(vertPair3)) hardEdgeCount++;

                    if (hardEdgeCount <= foundEdgeCount)
                        continue;

                    foundEdgeCount = hardEdgeCount;
                    found = t;
                }
                if (found == -1)
                {
                    Debug.LogWarning("Failed to find appropriate triangle to clip during triangulation");
                    return null;
                }

                {
                    var triangle = sTrianglePathCache.AllTriangles[foundTriangleIndices[found]];
                    var vertPair1 = VertPair.Create(triangle.vertexIndices[0], triangle.vertexIndices[1]);
                    var vertPair2 = VertPair.Create(triangle.vertexIndices[1], triangle.vertexIndices[2]);
                    var vertPair3 = VertPair.Create(triangle.vertexIndices[2], triangle.vertexIndices[0]);

                    sNewTriangles.Add(triangle);

                    // TODO: optimize
                    var list = new List<int>(foundTriangleIndices);
                    list.RemoveAt(found);
                    foundTriangleIndices = list.ToArray();

                    //found_triangles.RemoveAt(found);
                    if (!sTrianglePathCache.HardEdges.ContainsKey(vertPair1)) sTrianglePathCache.HardEdges.Add(vertPair1, -1);
                    if (!sTrianglePathCache.HardEdges.ContainsKey(vertPair2)) sTrianglePathCache.HardEdges.Add(vertPair2, -1);
                    if (!sTrianglePathCache.HardEdges.ContainsKey(vertPair3)) sTrianglePathCache.HardEdges.Add(vertPair3, -1);
                }
            }

            return sNewTriangles.ToArray();
        }

        static int FindTwinIndex(BrushMesh.HalfEdge[] orgHalfEdges, Triangle[] triangles, Polygon[] newPolygons, int firstEdge, int lastEdge, int triangulatedPolygonIndex, int lastPolygonIndex, int edgeOffsetBeyondPolygon, int prevVertexIndex, int currVertexIndex, int currPolygonIndex)
        {
            for (int e3 = lastEdge - 1, e4 = firstEdge; e4 < lastEdge; e3 = e4, e4++)
            {
                var prevOrgVertexIndex = orgHalfEdges[e3].vertexIndex;
                if (prevOrgVertexIndex != prevVertexIndex)
                    continue;

                var currOrgVertexIndex = orgHalfEdges[e4].vertexIndex;
                if (currOrgVertexIndex != currVertexIndex)
                    continue;

                var orgTwinIndex = orgHalfEdges[e4].twinIndex;
                if (orgTwinIndex >= lastEdge)
                    orgTwinIndex -= edgeOffsetBeyondPolygon;

                return orgTwinIndex;
            }

            if (currPolygonIndex != triangulatedPolygonIndex)
            {
                var triangle        = triangles[0];
                var triangleOffset  = newPolygons[triangulatedPolygonIndex].firstEdge;
                for (int n1 = 2, n2 = 0; n2 < 3; n1 = n2, n2++)
                {
                    var prevTriVertexIndex = triangle.vertexIndices[n1];
                    if (prevTriVertexIndex != currVertexIndex)
                        continue;

                    var currTriVertexIndex = triangle.vertexIndices[n2];
                    if (currTriVertexIndex != prevVertexIndex)
                        continue;

                    return triangleOffset + n2;
                }
            }

            for (var t2 = 1; t2 < triangles.Length; t2++)
            {
                var polygonIndex = lastPolygonIndex + (t2 - 1);
                if (currPolygonIndex == polygonIndex)
                    continue;

                var triangle        = triangles[t2];
                var triangleOffset  = newPolygons[polygonIndex].firstEdge;
                for (int n1 = 2, n2 = 0; n2 < 3; n1 = n2, n2++)
                {
                    var prevTriVertexIndex = triangle.vertexIndices[n1];
                    if (prevTriVertexIndex != currVertexIndex)
                        continue;

                    var currTriVertexIndex = triangle.vertexIndices[n2];
                    if (currTriVertexIndex != prevVertexIndex)
                        continue;

                    return triangleOffset + n2;
                }
            }
            return -1;
        }

        public void SplitNonPlanarPolygons()
        {
            if (this.polygons == null)
                return;

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
                var newHalfEdges                = new BrushMesh.HalfEdge[newHalfEdgeCount];
                for (int e = 0; e < newHalfEdges.Length; e++)
                    newHalfEdges[e].twinIndex = -1;
                Array.ConstrainedCopy(halfEdges, 0, 
                                      newHalfEdges, 0, 
                                      firstEdge + newEdgeCount);
                Array.ConstrainedCopy(halfEdgePolygonIndices, 0, 
                                      newHalfEdgePolygonIndices, 0, 
                                      firstEdge + newEdgeCount);
                var lastIndex = halfEdges.Length - lastEdge;
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
                    this.planes               = orgPlanes;

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

            if (!haveSplitPolygons)
                return;
        }
        
        public void RemoveDegenerateTopology(out int[] edgeRemap, out int[] polygonRemap)
        {
            edgeRemap = null;
            polygonRemap = null;
            const float kDistanceEpsilon = 0.0001f;

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
