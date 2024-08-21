using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    sealed partial class BrushMesh
    {
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
            if (!cache.HardEdges.TryGetValue(VertPair.Create(vi0, vi1), out int polygonIndex))
                return 0;
            
            if (polygonIndex < 0 || polygonIndex >= planes.Length)
                return float.PositiveInfinity;
            var error = math.dot(planes[polygonIndex].xyz, vertices[vi2] - vertices[vi1]) - 1;
            return (error * error);
        }
        
        private static float GetTriangleHeuristic(TrianglePathCache cache, float4[] planes, float3[] vertices, int vi0, int vi1, int vi2)
        {
            //var pair0 = VertPair.Create(vi0, vi1);
            //var pair1 = VertPair.Create(vi1, vi2);
            //var pair2 = VertPair.Create(vi2, vi0);
                
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
                } //else
                    //triangle = cache.AllTriangles[index];

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
    }
}
