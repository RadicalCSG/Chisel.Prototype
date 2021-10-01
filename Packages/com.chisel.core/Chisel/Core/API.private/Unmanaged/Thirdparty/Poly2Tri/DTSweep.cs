/* Poly2Tri
 * Copyright (c) 2009-2010, Poly2Tri Contributors
 * http://code.google.com/p/poly2tri/
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification,
 * are permitted provided that the following conditions are met:
 *
 * * Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 * * Redistributions in binary form must reproduce the above copyright notice,
 *   this list of conditions and the following disclaimer in the documentation
 *   and/or other materials provided with the distribution.
 * * Neither the name of Poly2Tri nor the names of its contributors may be
 *   used to endorse or promote products derived from this software without specific
 *   prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/*
 * Sweep-line, Constrained Delauney Triangulation (CDT) See: Domiter, V. and
 * Zalik, B.(2008)'Sweep-line algorithm for constrained Delaunay triangulation',
 * International Journal of Geographical Information Science
 * 
 * "FlipScan" Constrained Edge Algorithm invented by author of this code.
 * 
 * Author: Thomas Åhlén, thahlen@gmail.com 
 */

/// Changes from the Java version
///   Turned DTSweep into a static class
///   Lots of deindentation via early bailout
/// Future possibilities
///   Comments!

using Chisel.Core;
using System;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Poly2Tri
{
    unsafe struct DTSweep
    {
        const double PI_div2     = (Math.PI / 2);
        const double PI_3div4    = (3 * Math.PI / 4);

        public unsafe struct DelaunayTriangle
        {
            public enum EdgeFlags : int
            {
                None = 0,
                Constrained = 1,
                Delaunay = 2
            }

            public int3 indices;
            public int3 neighbors;
            int3  edgeFlags;

            public DelaunayTriangle(int3 p)
            {
                indices   = p;
                neighbors = new int3(int.MaxValue, int.MaxValue, int.MaxValue);
                edgeFlags = int3.zero;
            }

            public DelaunayTriangle(int p1, int p2, int p3) : this(new int3(p1, p2, p3))
            {
            }

            public void ClearDelauney()
            {
                edgeFlags[0] &= (int)~EdgeFlags.Delaunay;
                edgeFlags[1] &= (int)~EdgeFlags.Delaunay;
                edgeFlags[2] &= (int)~EdgeFlags.Delaunay;
            }
            public void SetDelauneyEdge(int index, bool value)
            {
                if (value)
                    edgeFlags[index] |= (int)EdgeFlags.Delaunay;
                else
                    edgeFlags[index] &= (int)~EdgeFlags.Delaunay;
            }

            public bool GetDelaunayEdge(int idx) { return (edgeFlags[idx] & (byte)EdgeFlags.Delaunay) != 0; }

            public void SetConstrainedEdge(int index, bool value)
            {
                if (value)
                    edgeFlags[index] |= (int)EdgeFlags.Constrained;
                else
                    edgeFlags[index] &= (int)~EdgeFlags.Constrained;
            }

            public bool GetConstrainedEdge(int idx) { return (edgeFlags[idx] & (byte)EdgeFlags.Constrained) != 0; }

            public int IndexOf(int p)
            {
                if (indices[0] == p) return (int)0; else if (indices[1] == p) return (int)1; else if (indices[2] == p) return (int)2;
                return int.MaxValue;
            }

            public bool Contains(int p)
            {
                if (indices[0] == p || indices[1] == p || indices[2] == p) 
                    return true;
                return false;
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            public static void MarkNeighborException()
            {
                throw new Exception("Error marking neighbors -- t doesn't contain edge p1-p2!");
            }


            public void MarkNeighbor(int p1, int p2, int triangleIndex)
            {
                int i = EdgeIndex(p1, p2);
                if (i == int.MaxValue)
                    MarkNeighborException();
                neighbors[i] = triangleIndex;
            }


            public int NeighborCWFrom(int point)
            {
                return neighbors[(IndexOf(point) + 1) % 3];
            }

            public int NeighborCCWFrom(int point)
            {
                return neighbors[(IndexOf(point) + 2) % 3];
            }

            public int NeighborAcrossFrom(int point)
            {
                return neighbors[IndexOf(point)];
            }

            public int PointCCWFrom(int point)
            {
                return indices[(IndexOf(point) + 1) % 3];
            }

            public int PointCWFrom(int point)
            {
                return indices[(IndexOf(point) + 2) % 3];
            }

            public void Legalize(int oPoint, int nPoint)
            {
                //RotateCW();
                {
                    var index0 = indices[0];
                    var index1 = indices[1];
                    var index2 = indices[2];

                    indices[2] = index1;
                    indices[1] = index0;
                    indices[0] = index2;
                }

                indices[(IndexOf(oPoint) + 1) % 3] = nPoint;
            }


            public void MarkConstrainedEdge(int index) { SetConstrainedEdge(index, true); }



            /// <summary>
            /// Mark edge as constrained
            /// </summary>
            public void MarkConstrainedEdge(int p, int q)
            {
                int i = EdgeIndex(p, q);
                if (i != int.MaxValue)
                    SetConstrainedEdge(i, true);
            }



            /// <summary>
            /// Get the index of the neighbor that shares this edge (or int.MaxValue if it isn't shared)
            /// </summary>
            /// <returns>index of the shared edge or int.MaxValue if edge isn't shared</returns>
            public int EdgeIndex(int p1, int p2)
            {
                int i1 = IndexOf(p1);
                int i2 = IndexOf(p2);

                // Points of this triangle in the edge p1-p2
                bool a = (i1 == 0 || i2 == 0);
                bool b = (i1 == 1 || i2 == 1);
                bool c = (i1 == 2 || i2 == 2);

                if (b && c)
                {
                    return 0;
                }
                if (a && c)
                {
                    return 1;
                }
                if (a && b)
                {
                    return 2;
                }

                return int.MaxValue;
            }

            public bool GetConstrainedEdgeCCW(int p) { return GetConstrainedEdge((IndexOf(p) + 2) % 3); }
            public bool GetConstrainedEdgeCW(int p) { return GetConstrainedEdge((IndexOf(p) + 1) % 3); }

            public void SetConstrainedEdgeCCW(int p, bool ce)
            {
                int idx = (IndexOf(p) + 2) % 3;
                SetConstrainedEdge(idx, ce);
            }
            public void SetConstrainedEdgeCW(int p, bool ce)
            {
                int idx = (IndexOf(p) + 1) % 3;
                SetConstrainedEdge(idx, ce);
            }
            public void SetConstrainedEdgeAcross(int p, bool ce)
            {
                int idx = IndexOf(p);
                SetConstrainedEdge(idx, ce);
            }

            public bool GetDelaunayEdgeCCW(int p) { return GetDelaunayEdge((IndexOf(p) + 2) % 3); }

            public bool GetDelaunayEdgeCW(int p) { return GetDelaunayEdge((IndexOf(p) + 1) % 3); }

            public void SetDelaunayEdgeCCW(int p, bool ce) { SetDelauneyEdge((IndexOf(p) + 2) % 3, ce); }

            public void SetDelaunayEdgeCW(int p, bool ce) { SetDelauneyEdge((IndexOf(p) + 1) % 3, ce); }
        
        }

        enum Orientation : byte
        {
            CW,
            CCW,
            Collinear
        }

        struct DTSweepConstraint
        {
            public int P;
            public int Q;
        }

        public struct AdvancingFrontNode
        {
            public int prevNodeIndex;
            public int nextNodeIndex;
            public int triangleIndex;
            public int pointIndex;
            public float2 nodePoint;
        }

        public struct DirectedEdge
        {
            public int index2;
            public int next;
        }

        //
        // SweepContext
        //

        [NoAlias, ReadOnly] public quaternion                        rotation;
        [NoAlias, ReadOnly] public float3                           normal;
        [NoAlias, ReadOnly] public HashedVertices                   vertices;
        [NoAlias, ReadOnly] public NativeArray<float2>              points;
        [NoAlias, ReadOnly] public int                              edgeLength;
        [NoAlias, ReadOnly] public NativeArray<int>                 edges;
        [NoAlias, ReadOnly] public NativeList<DirectedEdge>         allEdges;
        [NoAlias, ReadOnly] public NativeList<DelaunayTriangle>     triangles;
        [NoAlias, ReadOnly] public NativeList<bool>                 triangleInterior;
        [NoAlias, ReadOnly] public NativeList<int>                  sortedPoints;
        [NoAlias, ReadOnly] public NativeList<AdvancingFrontNode>   advancingFrontNodes;
        [NoAlias, ReadOnly] public NativeList<UnsafeList<Edge>>     edgeLookupEdges;
        [NoAlias, ReadOnly] public NativeHashMap<int, int>          edgeLookups;
        [NoAlias, ReadOnly] public NativeList<UnsafeList<Edge>>     foundLoops;
        [NoAlias, ReadOnly] public NativeList<UnsafeList<int>>      children;
        [NoAlias, ReadOnly] public NativeList<Edge>                 inputEdgesCopy;
        [NoAlias, ReadOnly] public UnsafeList<Edge>                 inputEdges;
        [NoAlias]           public NativeList<int>                  surfaceIndicesArray;

        void Clear()
        {
            for (int i = 0; i < edgeLength; i++)
                edges[i] = int.MaxValue;

            allEdges.Clear();
            triangles.Clear();
            triangleInterior.Clear();
            advancingFrontNodes.Clear();
            sortedPoints.Clear();
        }

        // Inital triangle factor, seed triangle will extend 30% of 
        // PointSet width to both left and right.
        const float ALPHA = 0.3f;

        int headNodeIndex;
        int tailNodeIndex;
        int searchNodeIndex;
        int headPointIndex;
        int tailPointIndex;

        //Basin
        int leftNodeIndex;
        int bottomNodeIndex;
        int rightNodeIndex;
        float basinWidth;
        bool basinLeftHighest;

        DTSweepConstraint edgeEventConstrainedEdge;
        bool edgeEventRight;

        
        internal unsafe static bool IsPointInPolygon(float3 right, float3 forward, UnsafeList<Edge> indices1, UnsafeList<Edge> indices2, HashedVertices vertices)
        {
            int index = 0;
            while (index < indices2.Length &&
                indices1.Contains(indices2[index]))
                index++;

            if (index >= indices2.Length)
                return false;

            var point = vertices[indices2[index].index1];

            var px = math.dot(right, point);
            var py = math.dot(forward, point);

            float ix, iy, jx, jy;

            var vert = vertices[indices1[indices1.Length - 1].index1];
            ix = math.dot(right, vert);
            iy = math.dot(forward, vert);

            bool result = false;
            for (int i = 0; i < indices1.Length; i++)
            {
                jx = ix;
                jy = iy;

                vert = vertices[indices1[i].index1];
                ix = math.dot(right, vert);
                iy = math.dot(forward, vert);

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



        public void Execute()
        {
            surfaceIndicesArray.Clear();
            if (inputEdges.Length < 4)
            {
                Triangulate(ref inputEdges, surfaceIndicesArray);
                return;
            }

            // This is a hack around bugs in the triangulation code

            surfaceIndicesArray.Clear();
            edgeLookupEdges.Clear();
            edgeLookups.Clear();
            foundLoops.Clear();


            inputEdgesCopy.ResizeUninitialized(inputEdges.Length);
            for (int i = 0; i < inputEdges.Length; i++)
                inputEdgesCopy[i] = inputEdges[i];
            
            for (int i = 0; i < inputEdgesCopy.Length; i++)
            {
                if (!edgeLookups.TryGetValue(inputEdgesCopy[i].index1, out int edgeLookupIndex))
                {
                    edgeLookupIndex = edgeLookupEdges.Length;
                    edgeLookups[inputEdgesCopy[i].index1] = edgeLookupIndex;
                    var edges = new UnsafeList<Edge>(inputEdgesCopy.Length, Allocator.Temp);
                    edges.AddNoResize(inputEdgesCopy[i]);
                    edgeLookupEdges.Add(edges);
                } else
                {
                    var edges = edgeLookupEdges[edgeLookupIndex];
                    edges.AddNoResize(inputEdgesCopy[i]);
                    edgeLookupEdges[edgeLookupIndex] = edges;
                }
            }

            var edgeCount = edgeLookups.Count();

            while (inputEdgesCopy.Length > 0)
            {
                var lastIndex   = inputEdgesCopy.Length - 1;
                var edge        = inputEdgesCopy[lastIndex];
                var newLoops    = new UnsafeList<Edge>(1 + (2 * edgeCount), Allocator.Temp); // TODO: figure out a more sensible max size
                newLoops.AddNoResize(edge);

                var edgesStartingAtVertex = edgeLookupEdges[edgeLookups[edge.index1]];
                if (edgesStartingAtVertex.Length > 1)
                {
                    edgesStartingAtVertex.Remove(edge);
                    edgeLookupEdges[edgeLookups[edge.index1]] = edgesStartingAtVertex;
                } else
                    edgeLookups.Remove(edge.index1);

                inputEdgesCopy.RemoveAt(lastIndex);

                var firstIndex = edge.index1;
                while (edgeLookups.ContainsKey(edge.index2))
                { 
                    var nextEdges = edgeLookupEdges[edgeLookups[edge.index2]];
                    var nextEdge = nextEdges[0];
                    if (nextEdges.Length > 1)
                    {
                        var vertex1 = vertices[edge.index1];
                        var vertex2 = vertices[edge.index2];
                        var vertex3 = vertices[nextEdge.index2];
                        var prevAngle = math.dot((vertex2 - vertex1), (vertex1 - vertex3));
                        for (int i = 1; i < nextEdges.Length; i++)
                        {
                            vertex3 = vertices[nextEdge.index2];
                            var currAngle = math.dot((vertex2 - vertex1), (vertex3 - vertex1));
                            if (currAngle > prevAngle)
                            {
                                nextEdge = nextEdges[i];
                            }
                        }
                        nextEdges.Remove(nextEdge);
                        edgeLookupEdges[edgeLookups[edge.index2]] = nextEdges;
                    } else
                        edgeLookups.Remove(edge.index2);
                    newLoops.AddNoResize(nextEdge);
                    inputEdgesCopy.Remove(nextEdge);
                    edge = nextEdge;
                    if (edge.index2 == firstIndex)
                        break;
                }
                foundLoops.Add(newLoops);
            }

            if (foundLoops.Length == 0)
                return;

            if (foundLoops.Length == 1)
            {
                var foundLoop = foundLoops[0];
                if (foundLoop.Length == 0)
                    return;
                Triangulate(ref foundLoop, surfaceIndicesArray);
                foundLoops[0] = foundLoop;
                return;
            }

            children.Clear();
            children.Resize(foundLoops.Length, NativeArrayOptions.ClearMemory);

            for (int l1 = 0; l1 < children.Length; l1++)
            {
                children[l1] = new UnsafeList<int>(1 + (children.Length * 2), Allocator.Temp);
            }

            MathExtensions.CalculateTangents(normal, out float3 right, out float3 forward);
            for (int l1 = foundLoops.Length - 1; l1 >= 0; l1--)
            {
                if (foundLoops[l1].Length == 0)
                    continue;
                for (int l2 = l1 - 1; l2 >= 0; l2--)
                {
                    if (foundLoops[l2].Length == 0)
                        continue;
                    if (IsPointInPolygon(right, forward, foundLoops[l1], foundLoops[l2], vertices))
                    {
                        var child = children[l1];
                        child.AddNoResize(l2);
                        children[l1] = child;
                    } else
                    if (IsPointInPolygon(right, forward, foundLoops[l2], foundLoops[l1], vertices))
                    {
                        var child = children[l1];
                        child.AddNoResize(l1);
                        children[l2] = child;
                        break;
                    }
                }
            }

            for (int l1 = children.Length - 1; l1 >= 0; l1--)
            {
                var child = children[l1];
                if (child.Length > 0) child.Remove(l1); // just in case
                if (child.Length > 0)
                {
                    int startOffset = 0;
                    while (startOffset < child.Length)
                    {
                        var nextOffset = child.Length;
                        for (int l2 = nextOffset - 1; l2 >= startOffset; l2--)
                        {
                            var index = child[l2];
                            if (children[index].Length > 0)
                            {
                                child.AddRangeNoResize(children[index]);
                                child.Remove(l1); // just in case
                                children[index].Clear();
                            }
                        }
                        startOffset = nextOffset;
                    }

                    for (int l2 = 0; l2 < child.Length; l2++)
                    {
                        var index = child[l2];
                        var edges = foundLoops[l1];
                        edges.AddRangeNoResize(foundLoops[index]);
                        foundLoops[l1] = edges;

                        var edges2 = foundLoops[index];
                        edges2.Clear();
                        foundLoops[index] = edges2;
                    }
                }
                children[l1] = child;
            }


            for (int l1 = foundLoops.Length - 1; l1 >= 0; l1--)
            {
                var subLoop = foundLoops[l1];
                if (subLoop.Length == 0)
                    continue;
                Triangulate(ref subLoop, surfaceIndicesArray);
            }
        }

        /// <summary>
        /// Triangulate simple polygon with holes
        /// </summary>
        public void Triangulate(ref UnsafeList<Edge> inputEdgesList, NativeList<int> triangleIndices)
        {
            var startIndex = triangleIndices.Length;
            int prevEdgeCount = inputEdgesList.Length;
            AddMoreTriangles:
            Clear();
            PrepareTriangulation(inputEdgesList);
            CreateAdvancingFront(0);
            bool success = Sweep(inputEdgesList.Length * 10);
            FixupConstrainedEdges();
            FinalizationPolygon(triangleIndices);
            if (!success)
            {
                UnityEngine.Debug.LogError("StackOverflow in triangulation");
            } else
            {
                //TODO: Optimize
                for (int i = startIndex; i < triangleIndices.Length; i += 3)
                {
                    var index0 = triangleIndices[i + 0];
                    var index1 = triangleIndices[i + 1];
                    var index2 = triangleIndices[i + 2];
                    for (int e = inputEdgesList.Length - 1; e >= 0; e--)
                    {
                        var edge = inputEdgesList[e];
                        if (index0 == edge.index1)
                        {
                            if (index1 == edge.index2 ||
                                index2 == edge.index2)
                            {
                                inputEdgesList.RemoveAtSwapBack(e);
                            }
                        } else
                        if (index1 == edge.index1)
                        {
                            if (index0 == edge.index2 ||
                                index2 == edge.index2)
                            {
                                inputEdgesList.RemoveAtSwapBack(e);
                            }
                        } else
                        if (index2 == edge.index1)
                        {
                            if (index1 == edge.index2 ||
                                index0 == edge.index2)
                            {
                                inputEdgesList.RemoveAtSwapBack(e);
                            }
                        }
                    }
                }
                if (inputEdgesList.Length > 3 && prevEdgeCount != inputEdgesList.Length)
                {
                    prevEdgeCount = inputEdgesList.Length;
                    goto AddMoreTriangles;
                }
            }
        }





        void AddTriangle(DelaunayTriangle triangle)
        {
            triangles.Add(triangle);
            triangleInterior.Add(false);
        }


        bool HasNext(int nodeIndex) { if (nodeIndex == int.MaxValue) return false; return advancingFrontNodes[nodeIndex].nextNodeIndex != int.MaxValue; }
        bool HasPrev(int nodeIndex) { if (nodeIndex == int.MaxValue) return false; return advancingFrontNodes[nodeIndex].prevNodeIndex != int.MaxValue; }

        int LocateNode(float x)
        {
            var nodeIndex = searchNodeIndex;
            if (nodeIndex >= advancingFrontNodes.Length)
                return int.MaxValue;
            if (x < advancingFrontNodes[nodeIndex].nodePoint.x)
            {
                nodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                while (nodeIndex != int.MaxValue)
                {
                    if (x >= advancingFrontNodes[nodeIndex].nodePoint.x)
                    {
                        searchNodeIndex = nodeIndex;
                        return nodeIndex;
                    }
                    nodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                }
            } else
            {
                nodeIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
                while (nodeIndex != int.MaxValue)
                {
                    if (x < advancingFrontNodes[nodeIndex].nodePoint.x)
                    {
                        searchNodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                        return advancingFrontNodes[nodeIndex].prevNodeIndex;
                    }
                    nodeIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
                }
            }

            return int.MaxValue;
        }

        internal int CreateAdvancingFrontNode(float2 point, int pointIndex, int prevIndex, int nextIndex)
        {
            var newIndex = (int)advancingFrontNodes.Length;
            advancingFrontNodes.Add(new AdvancingFrontNode() { nodePoint = point, pointIndex = pointIndex, nextNodeIndex = nextIndex, prevNodeIndex = prevIndex, triangleIndex = int.MaxValue });
            return newIndex;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void FailedToFindNodeForGivenAfrontPointException()
        {
            throw new Exception("Failed to find Node for given afront point");
        }

        /// <summary>
        /// This implementation will use simple node traversal algorithm to find a point on the front
        /// </summary>
        int LocatePoint(int index)
        {
            var px          = points[index].x;
            var nodeIndex   = searchNodeIndex;
            var nx          = advancingFrontNodes[nodeIndex].nodePoint.x;

            if (px == nx)
            {
                if (index != advancingFrontNodes[nodeIndex].pointIndex)
                {
                    //CheckValidIndex(advancingFrontNodes[nodeIndex].prevNodeIndex);
                    // We might have two nodes with same x value for a short time
                    if (advancingFrontNodes[nodeIndex].prevNodeIndex != int.MaxValue &&
                        index == advancingFrontNodes[advancingFrontNodes[nodeIndex].prevNodeIndex].pointIndex)
                    {
                        nodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                    }
                    else
                    {
                        CheckValidIndex(advancingFrontNodes[nodeIndex].nextNodeIndex);
                        if (advancingFrontNodes[nodeIndex].nextNodeIndex != int.MaxValue &&
                        index == advancingFrontNodes[advancingFrontNodes[nodeIndex].nextNodeIndex].pointIndex)
                        {
                            nodeIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
                        }
                        else
                        {
                            FailedToFindNodeForGivenAfrontPointException();
                        }
                    }
                }
            } else 
            if (px < nx)
            {
                nodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                while (nodeIndex != int.MaxValue)
                {
                    if (index == advancingFrontNodes[nodeIndex].pointIndex)
                        break;
                    nodeIndex = advancingFrontNodes[nodeIndex].prevNodeIndex;
                }
            } else
            {
                nodeIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
                while (nodeIndex != int.MaxValue)
                {
                    if (index == advancingFrontNodes[nodeIndex].pointIndex)
                        break;
                    nodeIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
                }
            }
            searchNodeIndex = nodeIndex;

            return nodeIndex;
        }


        void MeshClean(NativeList<int> triangleIndices, int triangleIndex)
        {
            if (triangleIndex == int.MaxValue || triangleInterior[triangleIndex])
                return;
            
            triangleInterior[triangleIndex] = true;

            var triangle = triangles[triangleIndex];

            var index0 = triangle.indices[0];
            var index1 = triangle.indices[1];
            var index2 = triangle.indices[2];
            if (index0 < vertices.Length &&
                index1 < vertices.Length &&
                index2 < vertices.Length)
            {
                triangleIndices.Add(index0);
                triangleIndices.Add(index1);
                triangleIndices.Add(index2);
            }

            if (!triangle.GetConstrainedEdge(0)) MeshClean(triangleIndices, triangle.neighbors[0]);
            if (!triangle.GetConstrainedEdge(1)) MeshClean(triangleIndices, triangle.neighbors[1]);
            if (!triangle.GetConstrainedEdge(2)) MeshClean(triangleIndices, triangle.neighbors[2]);
        }



        void CreateAdvancingFront(int index)
        {
            // Initial triangle
            var triangleIndex = (int)triangles.Length;
            AddTriangle(new DelaunayTriangle(new int3(sortedPoints[index], tailPointIndex, headPointIndex)));

            headNodeIndex = (int)advancingFrontNodes.Length;
            var middleNodeIndex = (int)(headNodeIndex + 1);
            tailNodeIndex = (int)(middleNodeIndex + 1);

            var triangle = triangles[triangleIndex];
            var index0 = triangle.indices[0];
            var index1 = triangle.indices[1];
            var index2 = triangle.indices[2];

            advancingFrontNodes.Add(new AdvancingFrontNode() { nodePoint = points[index1], pointIndex = index1, triangleIndex = triangleIndex, prevNodeIndex = int.MaxValue, nextNodeIndex = middleNodeIndex });
            advancingFrontNodes.Add(new AdvancingFrontNode() { nodePoint = points[index0], pointIndex = index0, triangleIndex = triangleIndex, prevNodeIndex = headNodeIndex, nextNodeIndex = tailNodeIndex });
            advancingFrontNodes.Add(new AdvancingFrontNode() { nodePoint = points[index2], pointIndex = index2, triangleIndex = int.MaxValue, prevNodeIndex = middleNodeIndex, nextNodeIndex = int.MaxValue });

            searchNodeIndex = headNodeIndex;
        }


        /// <summary>
        /// Try to map a node to all sides of this triangle that don't have 
        /// a neighbor.
        /// </summary>
        void MapTriangleToNodes(int triangleIndex)
        {
            var triangle = triangles[triangleIndex];
            if (triangle.neighbors[0] == int.MaxValue)
            {
                var index = triangle.indices[0];
                var nodeIndex = LocatePoint(triangle.PointCWFrom(index));
                if (nodeIndex != int.MaxValue)
                {
                    var node = advancingFrontNodes[nodeIndex];
                    node.triangleIndex = triangleIndex;
                    advancingFrontNodes[nodeIndex] = node;
                }
            }

            if (triangle.neighbors[1] == int.MaxValue)
            {
                var index = triangle.indices[1];
                var nodeIndex = LocatePoint(triangle.PointCWFrom(index));
                if (nodeIndex != int.MaxValue)
                {
                    var node = advancingFrontNodes[nodeIndex];
                    node.triangleIndex = triangleIndex;
                    advancingFrontNodes[nodeIndex] = node;
                }
            }

            if (triangle.neighbors[2] == int.MaxValue)
            {
                var index = triangle.indices[2];
                var nodeIndex = LocatePoint(triangle.PointCWFrom(index));
                if (nodeIndex != int.MaxValue)
                {
                    var node = advancingFrontNodes[nodeIndex];
                    node.triangleIndex = triangleIndex;
                    advancingFrontNodes[nodeIndex] = node;
                }
            }
        }

        struct PointComparer : System.Collections.Generic.IComparer<int>
        {
            public NativeArray<float2> points;
            public int Compare(int i1, int i2)
            {
                var pt1 = points[i1];
                var pt2 = points[i2];
                if (pt1.y < pt2.y) return -1; if (pt1.y > pt2.y) return 1;
                if (pt1.x < pt2.x) return -1; if (pt1.x > pt2.x) return 1;
                return 0;
            }
        }


        void PrepareTriangulation(UnsafeList<Edge> inputEdgesArray)
        {
            var min = new float2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new float2(float.NegativeInfinity, float.NegativeInfinity);

            var s_KnownVertices = stackalloc bool[vertices.Length];
            for (int e = 0; e < inputEdgesArray.Length; e++)
            {
                var edge = inputEdgesArray[e];
                var index1 = edge.index1;
                var index2 = edge.index2;

                if (index1 == index2)
                    continue;

                if (!s_KnownVertices[index1])
                {
                    s_KnownVertices[index1] = true;
                    sortedPoints.Add(index1);
                    var pt = math.mul(rotation, vertices[index1]).xy;
                    points[index1] = pt;

                    // Calculate bounds
                    max = math.max(max, pt);
                    min = math.min(min, pt);
                }
                if (!s_KnownVertices[index2])
                {
                    s_KnownVertices[index2] = true;
                    sortedPoints.Add(index2);
                    var pt = math.mul(rotation, vertices[index2]).xy;
                    points[index2] = pt;

                    // Calculate bounds
                    max = math.max(max, pt);
                    min = math.min(min, pt);
                }

                // Add constraints
                var p1 = points[index1];
                var p2 = points[index2];

                int P, Q;
                if (p1.y > p2.y || (p1.y == p2.y && p1.x > p2.x)) { Q = index1; P = index2; }
                else { P = index1; Q = index2; }

                allEdges.Add(new DirectedEdge() { index2 = P, next = this.edges[Q] });
                this.edges[Q] = (int)(allEdges.Length - 1);
            }


            var pointComparer = new PointComparer();
            pointComparer.points = points;

            // Sort the points along y-axis
            NativeSortExtension.Sort<int, PointComparer>((int*)sortedPoints.GetUnsafePtr(), sortedPoints.Length, pointComparer);

            headPointIndex = (int)(vertices.Length);
            tailPointIndex = (int)(vertices.Length + 1);

            var delta = ALPHA * (max - min);
            points[headPointIndex] = new float2(max.x + delta.x, min.y - delta.y);
            points[tailPointIndex] = new float2(min.x - delta.x, min.y - delta.y);
        }




        //
        // Sweep
        //

        
        /// <summary>
        /// Start sweeping the Y-sorted point set from bottom to top
        /// </summary>
        bool Sweep(int maxStackDepth)
        {
            var sortedPoints = this.sortedPoints;
            for (int i = 1; i < sortedPoints.Length; i++)
            {
                var pointIndex      = sortedPoints[i];
                var point           = points[pointIndex];
                var frontNodeIndex  = LocateNode(point.x);

                if (frontNodeIndex == int.MaxValue)
                    continue;

                var triangleIndex       = (int)triangles.Length;
                var frontNodeNextIndex  = advancingFrontNodes[frontNodeIndex].nextNodeIndex;
                AddTriangle(new DelaunayTriangle(pointIndex, advancingFrontNodes[frontNodeIndex].pointIndex, advancingFrontNodes[frontNodeNextIndex].pointIndex));

                MarkNeighbor(advancingFrontNodes[frontNodeIndex].triangleIndex, triangleIndex);

                var nodeIndex       = CreateAdvancingFrontNode(point, pointIndex, frontNodeIndex, frontNodeNextIndex);

                {
                    var frontNodeNext = advancingFrontNodes[frontNodeNextIndex];
                    if (nodeIndex == frontNodeIndex)
                    {
                        UnityEngine.Debug.Assert(nodeIndex != frontNodeIndex);
                    } else
                    {
                        frontNodeNext.prevNodeIndex = nodeIndex;
                        advancingFrontNodes[frontNodeNextIndex] = frontNodeNext;
                    }
                }
                {
                    var frontNode = advancingFrontNodes[frontNodeIndex];
                    if (nodeIndex == frontNodeIndex)
                    {
                        UnityEngine.Debug.Assert(nodeIndex != frontNodeIndex);
                    } else
                    {
                        frontNode.nextNodeIndex = nodeIndex;
                        advancingFrontNodes[frontNodeIndex] = frontNode;
                    }
                }

                if (!Legalize(triangleIndex))
                {
                    MapTriangleToNodes(triangleIndex); 
                }

                // Only need to check +epsilon since point never have smaller 
                // x value than node due to how we fetch nodes from the front
                if ((point.x - advancingFrontNodes[frontNodeIndex].nodePoint.x) <= kEpsilon)
                {
                    Fill(frontNodeIndex);
                }

                {
                    // Fill right holes
                    { 
                        var iterator = advancingFrontNodes[nodeIndex].nextNodeIndex;
                        while (HasNext(iterator))
                        {
                            var angle = HoleAngle(iterator);
                            if (angle > PI_div2 || angle < -PI_div2)
                            {
                                break;
                            }
                            Fill(iterator);
                            iterator = advancingFrontNodes[iterator].nextNodeIndex;
                        }
                    }

                    // Fill left holes
                    {
                        var iterator = advancingFrontNodes[nodeIndex].prevNodeIndex;
                        while (HasPrev(iterator))
                        {
                            var angle = HoleAngle(iterator);
                            if (angle > PI_div2 || angle < -PI_div2)
                            {
                                break;
                            }
                            Fill(iterator);
                            iterator = advancingFrontNodes[iterator].prevNodeIndex;
                        }
                    }

                    // Fill right basins
                    if (HasNext(nodeIndex) && HasNext(advancingFrontNodes[nodeIndex].nextNodeIndex))
                    {
                        var angle = BasinAngle(nodeIndex);
                        if (angle < PI_3div4)
                        {
                            FillBasin(nodeIndex);
                        }
                    }
                }

                int stackDepth = maxStackDepth;
                var edgeIndex = this.edges[pointIndex];
                while (edgeIndex != int.MaxValue)
                {
                    stackDepth--;
                    if (stackDepth <= 0)
                        return false;

                    var pIndex  = allEdges[edgeIndex].index2;
                    edgeIndex = allEdges[edgeIndex].next;
                    var qIndex  = pointIndex;
                    var edge    = new DTSweepConstraint() { P = pIndex, Q = qIndex };
                    
                    edgeEventConstrainedEdge = edge;

                    var P       = points[pIndex];
                    var Q       = points[qIndex];
                    edgeEventRight = P.x > Q.x;

                    if (IsEdgeSideOfTriangle(advancingFrontNodes[nodeIndex].triangleIndex, pIndex, qIndex))
                        continue;

                    // For now we will do all needed filling
                    // TODO: integrate with flip process might give some better performance 
                    //       but for now this avoid the issue with cases that needs both flips and fills
                    if (edgeEventRight)
                    {
                        if (!FillRightAboveEdgeEvent(edge, nodeIndex, maxStackDepth))
                            return false;
                    } else
                    {
                        if (!FillLeftAboveEdgeEvent(edge, nodeIndex, maxStackDepth))
                            return false;
                    }


                    if (!PerformEdgeEvent(pIndex, qIndex, advancingFrontNodes[nodeIndex].triangleIndex, qIndex, maxStackDepth))
                        return false;
                }
            }
            return true;
        }


        void FixupConstrainedEdges()
        {
            for (int t = 0; t < triangles.Length; t++)
            {
                var triangle = triangles[t];
                var index0  = triangle.indices[0];
                var index1  = triangle.indices[1];
                var index2  = triangle.indices[2];
                if (!triangle.GetConstrainedEdgeCCW(index0) && HasEdgeCCW(t, index0))
                {
                    triangle.MarkConstrainedEdge(2);
                    triangles[t] = triangle;
                }

                if (!triangle.GetConstrainedEdgeCCW(index1) && HasEdgeCCW(t, index1))
                {
                    triangle.MarkConstrainedEdge(0);
                    triangles[t] = triangle;
                }

                if (!triangle.GetConstrainedEdgeCCW(index2) && HasEdgeCCW(t, index2))
                {
                    triangle.MarkConstrainedEdge(1);
                    triangles[t] = triangle;
                }
            }
        }


        void FinalizationPolygon(NativeList<int> triangleIndices)
        {
            // Get an Internal triangle to start with
            var headNextNode    = advancingFrontNodes[advancingFrontNodes[headNodeIndex].nextNodeIndex];
            var pointIndex      = headNextNode.pointIndex;
            var triangleIndex   = headNextNode.triangleIndex;

            while (!triangles[triangleIndex].GetConstrainedEdgeCW(pointIndex))
            {
                var ccwNeighborIndex = triangles[triangleIndex].NeighborCCWFrom(pointIndex);
                if (ccwNeighborIndex == int.MaxValue)
                    break;
                triangleIndex = ccwNeighborIndex;
            }

            // Collect interior triangles constrained by edges
            MeshClean(triangleIndices, triangleIndex);
        }



        // returns false on failure
        bool FillRightConcaveEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var nodeNextIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
            Fill(nodeNextIndex); 
            nodeNextIndex = advancingFrontNodes[nodeIndex].nextNodeIndex;
            var nodeNext = advancingFrontNodes[nodeNextIndex];
            if (nodeNext.pointIndex != edge.P)
            {
                // Next above or below edge?
                if (Orient2d(points[edge.Q], nodeNext.nodePoint, points[edge.P]) == Orientation.CCW)
                {
                    var node         = advancingFrontNodes[nodeIndex];
                    var nodeNextNext = advancingFrontNodes[nodeNext.nextNodeIndex];
                    // Below
                    if (Orient2d(node.nodePoint, nodeNext.nodePoint, nodeNextNext.nodePoint) == Orientation.CCW)
                    {
                        // Next is concave
                        return FillRightConcaveEdgeEvent(edge, nodeIndex, stackDepth);
                    }
                    else
                    {
                        // Next is convex
                    }
                }
            }
            return true;
        }


        // returns false on failure
        bool FillRightConvexEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var node                = advancingFrontNodes[nodeIndex];
            var nodeNext            = advancingFrontNodes[node.nextNodeIndex];
            var nodeNextNext        = advancingFrontNodes[nodeNext.nextNodeIndex];
            var nodeNextNextNext    = advancingFrontNodes[nodeNextNext.nextNodeIndex];
            // Next concave or convex?
            if (Orient2d(nodeNext.nodePoint, nodeNextNext.nodePoint, nodeNextNextNext.nodePoint) == Orientation.CCW)
            {
                // Concave
                return FillRightConcaveEdgeEvent(edge, node.nextNodeIndex, stackDepth);
            } else
            {
                // Convex
                // Next above or below edge?
                if (Orient2d(points[edge.Q], nodeNextNext.nodePoint, points[edge.P]) == Orientation.CCW)
                {
                    // Below
                    return FillRightConvexEdgeEvent(edge, node.nextNodeIndex, stackDepth);
                } else
                {
                    // Above
                }
            }
            return true;
        }

        // returns false on failure
        bool FillRightBelowEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var node = advancingFrontNodes[nodeIndex];
            if (node.nodePoint.x < points[edge.P].x)
            {
                var nodeNext     = advancingFrontNodes[node.nextNodeIndex];
                var nodeNextNext = advancingFrontNodes[nodeNext.nextNodeIndex];
                // needed?
                if (Orient2d(node.nodePoint, nodeNext.nodePoint, nodeNextNext.nodePoint) == Orientation.CCW)
                {
                    // Concave 
                    return FillRightConcaveEdgeEvent(edge, nodeIndex, stackDepth);
                } else
                {
                    // Convex
                    if (!FillRightConvexEdgeEvent(edge, nodeIndex, stackDepth))
                        return false;
                    // Retry this one
                    return FillRightBelowEdgeEvent(edge, nodeIndex, stackDepth);
                }
            }
            return true;
        }


        // returns false on failure
        bool FillRightAboveEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            var node = advancingFrontNodes[nodeIndex];
            var edgeP = points[edge.P];
            while (advancingFrontNodes[node.nextNodeIndex].nodePoint.x < edgeP.x)
            {
                stackDepth--;
                if (stackDepth <= 0)
                    return false;

                // Check if next node is below the edge
                var o1 = Orient2d(points[edge.Q], advancingFrontNodes[node.nextNodeIndex].nodePoint, edgeP);
                if (o1 == Orientation.CCW)
                {
                    if (!FillRightBelowEdgeEvent(edge, nodeIndex, stackDepth))
                        return false;
                } else
                    nodeIndex = node.nextNodeIndex;
                node = advancingFrontNodes[nodeIndex];
                edgeP = points[edge.P];
            }
            return true;
        }

        // returns false on failure
        bool FillLeftConvexEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var node             = advancingFrontNodes[nodeIndex];
            var nodePrev         = advancingFrontNodes[node.prevNodeIndex];
            var nodePrevPrev     = advancingFrontNodes[nodePrev.prevNodeIndex];
            var nodePrevPrevPrev = advancingFrontNodes[nodePrevPrev.prevNodeIndex];
            // Next concave or convex?
            if (Orient2d(nodePrev.nodePoint, nodePrevPrev.nodePoint, nodePrevPrevPrev.nodePoint) == Orientation.CW)
            {
                // Concave
                return FillLeftConcaveEdgeEvent(edge, node.prevNodeIndex, stackDepth);
            } else
            {
                // Convex
                // Next above or below edge?
                if (Orient2d(points[edge.Q], nodePrevPrev.nodePoint, points[edge.P]) == Orientation.CW)
                {
                    // Below
                    return FillLeftConvexEdgeEvent(edge, node.prevNodeIndex, stackDepth);
                }
                else
                {
                    // Above
                }
            }
            return true;
        }


        // returns false on failure
        bool FillLeftConcaveEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var node = advancingFrontNodes[nodeIndex];
            Fill(node.prevNodeIndex); 
            node = advancingFrontNodes[nodeIndex];
            var nodePrev = advancingFrontNodes[node.prevNodeIndex];
            if (nodePrev.pointIndex != edge.P)
            {
                // Next above or below edge?
                if (Orient2d(points[edge.Q], nodePrev.nodePoint, points[edge.P]) == Orientation.CW)
                {
                    var nodePrevPrev = advancingFrontNodes[nodePrev.prevNodeIndex];
                    // Below
                    if (Orient2d(node.nodePoint, nodePrev.nodePoint, nodePrevPrev.nodePoint) == Orientation.CW)
                    {
                        // Next is concave
                        if (!FillLeftConcaveEdgeEvent(edge, nodeIndex, stackDepth))
                            return false;
                    }
                    else
                    {
                        // Next is convex
                    }
                }
            }
            return true;
        }


        // returns false on failure
        bool FillLeftBelowEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var node = advancingFrontNodes[nodeIndex];
            if (node.nodePoint.x > points[edge.P].x)
            {
                var nodePrev     = advancingFrontNodes[node.prevNodeIndex];
                var nodePrevPrev = advancingFrontNodes[nodePrev.prevNodeIndex];
                if (Orient2d(node.nodePoint, nodePrev.nodePoint, nodePrevPrev.nodePoint) == Orientation.CW)
                {
                    // Concave 
                    return FillLeftConcaveEdgeEvent(edge, nodeIndex, stackDepth);
                }
                else
                {
                    // Convex
                    if (!FillLeftConvexEdgeEvent(edge, nodeIndex, stackDepth))
                        return false;
                    // Retry this one
                    return FillLeftBelowEdgeEvent(edge, nodeIndex, stackDepth);
                }

            }
            return true;
        }


        // returns false on failure
        bool FillLeftAboveEdgeEvent(DTSweepConstraint edge, int nodeIndex, int stackDepth)
        {
            var node = advancingFrontNodes[nodeIndex];
            var edgeP = points[edge.P];
            while (advancingFrontNodes[node.prevNodeIndex].nodePoint.x > edgeP.x)
            {
                stackDepth--;
                if (stackDepth <= 0)
                    return false;

                // Check if next node is below the edge
                var o1 = Orient2d(points[edge.Q], advancingFrontNodes[node.prevNodeIndex].nodePoint, edgeP);
                if (o1 == Orientation.CW)
                {
                    if (!FillLeftBelowEdgeEvent(edge, nodeIndex, stackDepth))
                        return false;
                } else
                        nodeIndex = node.prevNodeIndex;
                node = advancingFrontNodes[nodeIndex];
                edgeP = points[edge.P];
            }
            return true;
        }


        bool IsEdgeSideOfTriangle(int triangleIndex, int epIndex, int eqIndex)
        {
            if (triangleIndex == int.MaxValue)
                return false;

            int index = triangles[triangleIndex].EdgeIndex(epIndex, eqIndex);
            if (index == int.MaxValue)
                return false;

            var triangle = triangles[triangleIndex];
            triangle.MarkConstrainedEdge(index);
            triangles[triangleIndex] = triangle;

            triangleIndex = triangle.neighbors[index];
            if (triangleIndex != int.MaxValue)
            {
                triangle = triangles[triangleIndex];
                triangle.MarkConstrainedEdge(epIndex, eqIndex);
                triangles[triangleIndex] = triangle;
            }
            return true;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckValidIndex(int index)
        {
            if (index == int.MaxValue)
                throw new Exception("invalid index (== int.MaxValue)");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void PointOnConstrainedEdgeNotSupportedException(int epIndex, int eqIndex, int p1Index)
        {
            throw new Exception($"PerformEdgeEvent - Point on constrained edge not supported yet {epIndex} {eqIndex} {p1Index}");
        }

        bool PerformEdgeEvent(int epIndex, int eqIndex, int triangleIndex, int pointIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            //CheckValidIndex(triangleIndex);
            if (triangleIndex == int.MaxValue)
                return false;

            if (IsEdgeSideOfTriangle(triangleIndex, epIndex, eqIndex))
                return true;

            var eqPoint = points[eqIndex];
            var epPoint = points[epIndex];

            var triangle = triangles[triangleIndex];
            var p1Index = triangle.PointCCWFrom(pointIndex);
            var o1 = Orient2d(eqPoint, points[p1Index], epPoint);
            if (o1 == Orientation.Collinear)
            {
                if (triangle.Contains(eqIndex) && triangle.Contains(p1Index))
                {
                    triangle.MarkConstrainedEdge(eqIndex, p1Index);
                    // We are modifying the constraint maybe it would be better to
                    // not change the given constraint and just keep a variable for the new constraint
                    edgeEventConstrainedEdge.Q = p1Index;
                    triangles[triangleIndex] = triangle;
                    triangleIndex = triangle.NeighborAcrossFrom(pointIndex);
                    return PerformEdgeEvent(epIndex, p1Index, triangleIndex, p1Index, stackDepth);
                } else
                {
                    PointOnConstrainedEdgeNotSupportedException(epIndex, eqIndex, p1Index);
                    return false;
                }
            }

            var p2Index = triangle.PointCWFrom(pointIndex);
            var o2 = Orient2d(eqPoint, points[p2Index], epPoint);
            if (o2 == Orientation.Collinear)
            {
                if (triangle.Contains(eqIndex) && triangle.Contains(p2Index))
                {
                    triangle.MarkConstrainedEdge(eqIndex, p2Index);
                    // We are modifying the constraint maybe it would be better to
                    // not change the given constraint and just keep a variable for the new constraint
                    edgeEventConstrainedEdge.Q = p2Index;
                    triangles[triangleIndex] = triangle;
                    triangleIndex = triangle.NeighborAcrossFrom(pointIndex);
                    if (triangleIndex != int.MaxValue)
                    {
                        return PerformEdgeEvent(epIndex, p2Index, triangleIndex, p2Index, stackDepth);
                    }
                    return true;
                } else
                {
                    PointOnConstrainedEdgeNotSupportedException(epIndex, eqIndex, p2Index);
                    return false;
                }
            }

            if (o1 == o2)
            {
                // Need to decide if we are rotating CW or CCW to get to a triangle
                // that will cross edge
                if (o1 == Orientation.CW)
                    triangleIndex = triangle.NeighborCCWFrom(pointIndex);
                else
                    triangleIndex = triangle.NeighborCWFrom(pointIndex);
                return PerformEdgeEvent(epIndex, eqIndex, triangleIndex, pointIndex, stackDepth);
            } else
            {
                // This triangle crosses constraint so lets flippin start!
                return FlipEdgeEvent(epIndex, eqIndex, triangleIndex, pointIndex, stackDepth);
            }
        }

        int OppositePoint(int triangleIndex1, int triangleIndex2, int p)
        {
            return triangles[triangleIndex1].PointCWFrom(triangles[triangleIndex2].PointCWFrom(p));
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void FLIPFailedDueToMissingTriangleException()
        {
            throw new Exception("[BUG:FIXME] FLIP failed due to missing triangle");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void CheckSelfPointer(int triangleIndex, int otIndex)
        {
            if (triangleIndex == otIndex)
                throw new Exception("[BUG:FIXME] self-pointer error");
            //UnityEngine.Debug.Assert(triangleIndex != otIndex, "self-pointer error");
        }

        bool FlipEdgeEvent(int epIndex, int eqIndex, int triangleIndex, int pIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var otIndex = triangles[triangleIndex].NeighborAcrossFrom(pIndex);            
            if (otIndex == int.MaxValue)
            {
                // If we want to integrate the fillEdgeEvent do it here
                // With current implementation we should never get here
                FLIPFailedDueToMissingTriangleException();
                return false;
            }

            CheckSelfPointer(triangleIndex, otIndex);
            var opIndex = OppositePoint(otIndex, triangleIndex, pIndex);

            var opPoint = points[opIndex];
            bool inScanArea = InScanArea(points[pIndex],
                                            points[triangles[triangleIndex].PointCCWFrom(pIndex)],
                                            points[triangles[triangleIndex].PointCWFrom(pIndex)],
                                            opPoint);
            if (inScanArea)
            {
                // Lets rotate shared edge one vertex CW
                RotateTrianglePair(triangleIndex, pIndex, otIndex, opIndex);
                MapTriangleToNodes(triangleIndex); 
                MapTriangleToNodes(otIndex); 

                if (pIndex == eqIndex && opIndex == epIndex)
                {
                    if (eqIndex == edgeEventConstrainedEdge.Q &&
                        epIndex == edgeEventConstrainedEdge.P)
                    {
                        var triangle      = triangles[triangleIndex];
                        var otherTriangle = triangles[otIndex];
                        triangle     .MarkConstrainedEdge(epIndex, eqIndex);
                        otherTriangle.MarkConstrainedEdge(epIndex, eqIndex);
                        triangles[triangleIndex] = triangle;
                        triangles[otIndex] = otherTriangle;
                        Legalize(triangleIndex);
                        Legalize(otIndex);
                    }
                    else
                    {
                        // XXX: I think one of the triangles should be legalized here?
                    }
                }
                else
                {
                    var o = Orient2d(points[eqIndex], opPoint, points[epIndex]);
                    triangleIndex = NextFlipTriangle(o, triangleIndex, otIndex, pIndex, opIndex);
                    return FlipEdgeEvent(epIndex, eqIndex, triangleIndex, pIndex, stackDepth);
                }
            }
            else
            {
                if (!NextFlipPoint(epIndex, eqIndex, otIndex, opIndex, out int newP))
                    return false;
                
                if (!FlipScanEdgeEvent(epIndex, eqIndex, triangleIndex, otIndex, newP, stackDepth))
                    return false;
                return PerformEdgeEvent(epIndex, eqIndex, triangleIndex, pIndex, stackDepth);
            }
            return true;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void OrientationNotHandledException()
        {
            throw new NotImplementedException("Orientation not handled");
        }

        bool NextFlipPoint(int epIndex, int eqIndex, int otherTriangleIndex, int opIndex, out int newP)
        {
            newP = int.MaxValue;
            var o2d = Orient2d(points[eqIndex], points[opIndex], points[epIndex]);
            switch (o2d)
            {
                case Orientation.CW:
                    newP = triangles[otherTriangleIndex].PointCCWFrom(opIndex);
                    return true;
                case Orientation.CCW:
                    newP = triangles[otherTriangleIndex].PointCWFrom(opIndex);
                    return true;
                case Orientation.Collinear:
                    // TODO: implement support for point on constraint edge
                    PointOnConstrainedEdgeNotSupportedException(eqIndex, opIndex, epIndex);
                    return false;
                default:
                    OrientationNotHandledException();
                    return false;
            }
        }

        int NextFlipTriangle(Orientation o, int triangleIndex, int otherTriangleIndex, int pIndex, int opIndex)
        {
            int edgeIndex;
            if (o == Orientation.CCW)
            {
                // ot is not crossing edge after flip
                var otherTriangle = triangles[otherTriangleIndex];
                edgeIndex = otherTriangle.EdgeIndex(pIndex, opIndex);
                otherTriangle.SetDelauneyEdge(edgeIndex, true);
                triangles[otherTriangleIndex] = otherTriangle;
                Legalize(otherTriangleIndex);
                otherTriangle = triangles[otherTriangleIndex];
                otherTriangle.ClearDelauney();
                triangles[otherTriangleIndex] = otherTriangle;
                return triangleIndex;
            }
            // t is not crossing edge after flip
            var triangle = triangles[triangleIndex];
            edgeIndex = triangle.EdgeIndex(pIndex, opIndex);
            triangle.SetDelauneyEdge(edgeIndex, true);
            triangles[triangleIndex] = triangle;
            Legalize(triangleIndex);
            triangle = triangles[triangleIndex];
            triangle.ClearDelauney();
            triangles[triangleIndex] = triangle;
            return otherTriangleIndex;
        }

        bool FlipScanEdgeEvent(int epIndex, int eqIndex, int flipTriangle, int triangleIndex, int pIndex, int stackDepth)
        {
            stackDepth--;
            if (stackDepth <= 0)
                return false;

            var otIndex = triangles[triangleIndex].NeighborAcrossFrom(pIndex);
            if (otIndex == int.MaxValue)
            {
                // If we want to integrate the fillEdgeEvent do it here
                // With current implementation we should never get here
                FLIPFailedDueToMissingTriangleException();
                return false;
            }

            CheckSelfPointer(triangleIndex, otIndex);
            var opIndex = OppositePoint(otIndex, triangleIndex, pIndex);

            var inScanArea = InScanArea(points[eqIndex],
                                        points[triangles[flipTriangle].PointCCWFrom(eqIndex)],
                                        points[triangles[flipTriangle].PointCWFrom(eqIndex)],
                                        points[opIndex]);
            if (inScanArea)
            {
                // flip with new edge op->eq
                return FlipEdgeEvent(eqIndex, opIndex, otIndex, opIndex, stackDepth);
                // TODO: Actually I just figured out that it should be possible to 
                //       improve this by getting the next ot and op before the the above 
                //       flip and continue the flipScanEdgeEvent here
                // set new ot and op here and loop back to inScanArea test
                // also need to set a new flipTriangle first
                // Turns out at first glance that this is somewhat complicated
                // so it will have to wait.
            }
            else
            {
                if (!NextFlipPoint(epIndex, eqIndex, otIndex, opIndex, out int newP))
                    return false;
                
                var triangle = triangles[otIndex];
                var index0 = triangle.indices[0];
                var index1 = triangle.indices[1];
                var index2 = triangle.indices[2];
                if (index0 != index1 && index0 != index2 && index1 != index2)
                    return FlipScanEdgeEvent(epIndex, eqIndex, flipTriangle, otIndex, newP, stackDepth);
                //newP = NextFlipPoint(ep, eq, ot, op);
            }
            return true;
        }

        void FillBasin(int nodeIndex)
        {
            var node         = advancingFrontNodes[nodeIndex];
            var nodeNext     = advancingFrontNodes[node.nextNodeIndex];
            var nodeNextNext = advancingFrontNodes[nodeNext.nextNodeIndex];
            if (Orient2d(node.nodePoint, nodeNext.nodePoint, nodeNextNext.nodePoint) == Orientation.CCW)
            {
                leftNodeIndex = nodeIndex;
            }
            else
            {
                leftNodeIndex = node.nextNodeIndex;
            }

            // Find the bottom and right node
            bottomNodeIndex = leftNodeIndex;
            var bottomNode      = advancingFrontNodes[bottomNodeIndex];
            while (HasNext(bottomNodeIndex) && bottomNode.nodePoint.y >= advancingFrontNodes[bottomNode.nextNodeIndex].nodePoint.y)
            {
                bottomNodeIndex = bottomNode.nextNodeIndex;
                bottomNode = advancingFrontNodes[bottomNodeIndex];
            }

            if (bottomNodeIndex == leftNodeIndex)
            {
                return; // No valid basin
            }

            rightNodeIndex  = bottomNodeIndex;
            var rightNode       = advancingFrontNodes[rightNodeIndex];
            while (HasNext(rightNodeIndex) && rightNode.nodePoint.y < advancingFrontNodes[rightNode.nextNodeIndex].nodePoint.y)
            {
                rightNodeIndex = rightNode.nextNodeIndex;
                rightNode = advancingFrontNodes[rightNodeIndex];
            }

            if (rightNodeIndex == bottomNodeIndex)
            {
                return; // No valid basins
            }

            var leftNode    = advancingFrontNodes[leftNodeIndex];
            basinWidth  = rightNode.nodePoint.x - leftNode.nodePoint.x;
            basinLeftHighest = leftNode.nodePoint.y > rightNode.nodePoint.y;

            FillBasinReq(bottomNodeIndex);
        }


        /// <summary>
        /// Recursive algorithm to fill a Basin with triangles
        /// </summary>
        void FillBasinReq(int nodeIndex)
        {
            if (IsShallow(nodeIndex))
            {
                return; // if shallow stop filling
            }

            Fill(nodeIndex); 

            var node = advancingFrontNodes[nodeIndex];
            if (node.prevNodeIndex == leftNodeIndex && node.nextNodeIndex == rightNodeIndex)
            {
                return;
            }
            else if (node.prevNodeIndex == leftNodeIndex)
            {
                var nodeNext     = advancingFrontNodes[node.nextNodeIndex];
                var nodeNextNext = advancingFrontNodes[nodeNext.nextNodeIndex];
                var o = Orient2d(node.nodePoint, nodeNext.nodePoint, nodeNextNext.nodePoint);
                if (o == Orientation.CW)
                {
                    return;
                }
                nodeIndex = node.nextNodeIndex;
            }
            else if (node.nextNodeIndex == rightNodeIndex)
            {
                var nodePrev = advancingFrontNodes[node.prevNodeIndex];
                var nodePrevPrev = advancingFrontNodes[nodePrev.prevNodeIndex];
                var o = Orient2d(node.nodePoint, nodePrev.nodePoint, nodePrevPrev.nodePoint);
                if (o == Orientation.CCW)
                {
                    return;
                }
                nodeIndex = node.prevNodeIndex;
            }
            else
            {
                var nodePrev = advancingFrontNodes[node.prevNodeIndex];
                var nodeNext = advancingFrontNodes[node.nextNodeIndex];
                // Continue with the neighbor node with lowest Y value
                if (nodePrev.nodePoint.y < nodeNext.nodePoint.y)
                {
                    nodeIndex = node.prevNodeIndex;
                }
                else
                {
                    nodeIndex = node.nextNodeIndex;
                }
            }
            FillBasinReq(nodeIndex);
        }


        bool IsShallow(int nodeIndex)
        {
            var node    = advancingFrontNodes[nodeIndex];
            var height  = basinLeftHighest ? (advancingFrontNodes[leftNodeIndex].nodePoint.y  - node.nodePoint.y)
                                            : (advancingFrontNodes[rightNodeIndex].nodePoint.y - node.nodePoint.y);
            return basinWidth > height;
        }

        double HoleAngle(int nodeIndex)
        {
            var node     = advancingFrontNodes[nodeIndex];
            var nodePrev = advancingFrontNodes[node.prevNodeIndex];
            var nodeNext = advancingFrontNodes[node.nextNodeIndex];
            // XXX: do we really need a signed angle for holeAngle?
            //      could possible save some cycles here
            /* Complex plane
                * ab = cosA +i*sinA
                * ab = (ax + ay*i)(bx + by*i) = (ax*bx + ay*by) + i(ax*by-ay*bx)
                * atan2(y,x) computes the principal value of the argument function
                * applied to the complex number x+iy
                * Where x = ax*bx + ay*by
                *       y = ax*by - ay*bx
                */
            var px = (double)(node.nodePoint.x);
            var py = (double)(node.nodePoint.y);
            var ax = (double)(nodeNext.nodePoint.x - px);
            var ay = (double)(nodeNext.nodePoint.y - py);
            var bx = (double)(nodePrev.nodePoint.x - px);
            var by = (double)(nodePrev.nodePoint.y - py);
            return math.atan2((ax * by) - (ay * bx), (ax * bx) + (ay * by));
        }


        /// <summary>
        /// The basin angle is decided against the horizontal line [1,0]
        /// </summary>
        double BasinAngle(int nodeIndex)
        {
            var node = advancingFrontNodes[nodeIndex];
            var nodeNext = advancingFrontNodes[node.nextNodeIndex];
            var nodeNextNext = advancingFrontNodes[nodeNext.nextNodeIndex];
            var ax = (double)(node.nodePoint.x - nodeNextNext.nodePoint.x);
            var ay = (double)(node.nodePoint.y - nodeNextNext.nodePoint.y);
            return math.atan2(ay, ax);
        }

        void Fill(int nodeIndex)
        {
            var node     = advancingFrontNodes[nodeIndex];
            var nodePrevIndex = node.prevNodeIndex;
            var nodeNextIndex = node.nextNodeIndex;
            var triangleIndex = (int)triangles.Length;

            if (nodePrevIndex == int.MaxValue ||
                nodeNextIndex == int.MaxValue ||
                node.pointIndex == int.MaxValue)
            {
                //CheckValidIndex(nodePrevIndex);
                //CheckValidIndex(nodeNextIndex);
                //CheckValidIndex(node.pointIndex);
                return;
            }

            AddTriangle(new DelaunayTriangle(advancingFrontNodes[nodePrevIndex].pointIndex, node.pointIndex, advancingFrontNodes[nodeNextIndex].pointIndex));
            // TODO: should copy the cEdge value from neighbor triangles
            //       for now cEdge values are copied during the legalize 
            MarkNeighbor(advancingFrontNodes[nodePrevIndex].triangleIndex, triangleIndex);
            MarkNeighbor(node.triangleIndex, triangleIndex);

            // Update the advancing front
            {
                var nodePrev = advancingFrontNodes[nodePrevIndex];
                if (nodeNextIndex == nodePrevIndex)
                {
                    UnityEngine.Debug.Assert(nodeNextIndex != nodePrevIndex);
                } else
                {
                    nodePrev.nextNodeIndex = nodeNextIndex;
                    advancingFrontNodes[nodePrevIndex] = nodePrev;
                }
            }
            {
                var nodeNext = advancingFrontNodes[nodeNextIndex];
                if (nodeNextIndex == nodePrevIndex)
                {
                    UnityEngine.Debug.Assert(nodeNextIndex != nodePrevIndex);
                } else
                {
                    nodeNext.prevNodeIndex = nodePrevIndex;
                    advancingFrontNodes[nodeNextIndex] = nodeNext;
                }
            }
            
            // If it was legalized the triangle has already been mapped
            if (!Legalize(triangleIndex))
            {
                MapTriangleToNodes(triangleIndex); 
            }
        }


        /// <summary>
        /// Returns true if triangle was legalized
        /// </summary>
        bool Legalize(int triangleIndex)
        {
            var inputTriangle = triangles[triangleIndex];
            
            // To legalize a triangle we start by finding if any of the three edges
            // violate the Delaunay condition
            for (int i = 0; i < 3; i++)
            {
                // TODO: fix so that cEdge is always valid when creating new triangles then we can check it here
                //       instead of below with ot
                if (inputTriangle.GetDelaunayEdge(i))
                {
                    continue;
                }

                var pIndex = inputTriangle.indices[i];
                var otIndex = inputTriangle.neighbors[i];
                if (otIndex == int.MaxValue)
                {
                    continue;
                }

                var otTriangle = triangles[otIndex];

                int opIndex = OppositePoint(otIndex, triangleIndex, pIndex);
                int oi = otTriangle.IndexOf(opIndex);
                // If this is a Constrained Edge or a Delaunay Edge(only during recursive legalization)
                // then we should not try to legalize
                if (otTriangle.GetConstrainedEdge(oi) ||
                    otTriangle.GetDelaunayEdge(oi))
                {
                    inputTriangle.SetConstrainedEdgeAcross(pIndex, otTriangle.GetConstrainedEdge(oi)); // XXX: have no good way of setting this property when creating new triangles so lets set it here
                    triangles[triangleIndex] = inputTriangle;
                    continue;
                }

                var pt0 = points[pIndex];
                var pt1 = points[inputTriangle.PointCCWFrom(pIndex)];
                var pt2 = points[inputTriangle.PointCWFrom(pIndex)];
                var pt3 = points[opIndex];
                if (!SmartIncircle(pt0, pt1, pt2, pt3))
                {
                    continue;
                }

                // Lets mark this shared edge as Delaunay 
                {
                    inputTriangle.SetDelauneyEdge(i, true);
                    triangles[triangleIndex] = inputTriangle;
                }
                {
                    otTriangle.SetDelauneyEdge(oi, true);
                    triangles[otIndex] = otTriangle;
                }

                // Lets rotate shared edge one vertex CW to legalize it
                RotateTrianglePair(triangleIndex, pIndex, otIndex, opIndex);

                // We now got one valid Delaunay Edge shared by two triangles
                // This gives us 4 new edges to check for Delaunay

                // Make sure that triangle to node mapping is done only one time for a specific triangle
                if (!Legalize(triangleIndex))
                {
                    MapTriangleToNodes(triangleIndex);
                }
                if (!Legalize(otIndex))
                {
                    MapTriangleToNodes(otIndex);
                }
                inputTriangle   = triangles[triangleIndex];
                otTriangle      = triangles[otIndex];

                // Reset the Delaunay edges, since they only are valid Delaunay edges
                // until we add a new triangle or point.
                // XXX: need to think about ctx Can these edges be tried after we 
                //      return to previous recursive level?
                {
                    inputTriangle.SetDelauneyEdge(i, false);
                    triangles[triangleIndex] = inputTriangle;
                }
                {
                    otTriangle.SetDelauneyEdge(oi, false);
                    triangles[otIndex] = otTriangle;
                }

                // If triangle have been legalized no need to check the other edges since
                // the recursive legalization will handles those so we can end here.
                return true;
            }
            return false;
        }


        /// <summary>
        /// Rotates a triangle pair one vertex CW
        ///       n2                    n2
        ///  P +-----+             P +-----+
        ///    | t  /|               |\  t |  
        ///    |   / |               | \   |
        ///  n1|  /  |n3           n1|  \  |n3
        ///    | /   |    after CW   |   \ |
        ///    |/ oT |               | oT \|
        ///    +-----+ oP            +-----+
        ///       n4                    n4
        /// </summary>
        void RotateTrianglePair(int triangleIndex, int pIndex, int otherTriangleIndex, int opIndex)
        {
            // TODO: optimize
            var otherTriangle   = triangles[otherTriangleIndex];
            var triangle        = triangles[triangleIndex];

            var n1 = triangle.NeighborCCWFrom(pIndex);
            var n2 = triangle.NeighborCWFrom(pIndex);
            var n3 = otherTriangle.NeighborCCWFrom(opIndex);
            var n4 = otherTriangle.NeighborCWFrom(opIndex);

            var ce1 = triangle.GetConstrainedEdgeCCW(pIndex);
            var ce2 = triangle.GetConstrainedEdgeCW(pIndex);
            var ce3 = otherTriangle.GetConstrainedEdgeCCW(opIndex);
            var ce4 = otherTriangle.GetConstrainedEdgeCW(opIndex);

            var de1 = triangle.GetDelaunayEdgeCCW(pIndex);
            var de2 = triangle.GetDelaunayEdgeCW(pIndex);
            var de3 = otherTriangle.GetDelaunayEdgeCCW(opIndex);
            var de4 = otherTriangle.GetDelaunayEdgeCW(opIndex);
            
            triangle.Legalize(pIndex, opIndex);
            otherTriangle.Legalize(opIndex, pIndex);

            // Remap dEdge
            otherTriangle.SetDelaunayEdgeCCW(pIndex, de1);
            triangle.SetDelaunayEdgeCW(pIndex, de2);
            triangle.SetDelaunayEdgeCCW(opIndex, de3);
            otherTriangle.SetDelaunayEdgeCW(opIndex, de4);

            // Remap cEdge
            otherTriangle.SetConstrainedEdgeCCW(pIndex, ce1);
            triangle.SetConstrainedEdgeCW(pIndex, ce2);
            triangle.SetConstrainedEdgeCCW(opIndex, ce3);
            otherTriangle.SetConstrainedEdgeCW(opIndex, ce4);

            // Remap neighbors
            // XXX: might optimize the markNeighbor by keeping track of
            //      what side should be assigned to what neighbor after the 
            //      rotation. Now mark neighbor does lots of testing to find 
            //      the right side.
            triangle.neighbors[0] = int.MaxValue;
            triangle.neighbors[1] = int.MaxValue;
            triangle.neighbors[2] = int.MaxValue;
            otherTriangle.neighbors[0] = int.MaxValue;
            otherTriangle.neighbors[1] = int.MaxValue;
            otherTriangle.neighbors[2] = int.MaxValue;
            triangles[otherTriangleIndex] = otherTriangle;
            triangles[triangleIndex] = triangle;
            if (n1 != int.MaxValue) MarkNeighbor(n1, otherTriangleIndex);
            if (n2 != int.MaxValue) MarkNeighbor(n2, triangleIndex);
            if (n3 != int.MaxValue) MarkNeighbor(n3, triangleIndex);
            if (n4 != int.MaxValue) MarkNeighbor(n4, otherTriangleIndex);
            MarkNeighbor(otherTriangleIndex, triangleIndex);
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void FailedToMarkNeighborException()
        {
            throw new Exception("Failed to mark neighbor, doesn't share an edge!");
        }

        /// <summary>
        /// Exhaustive search to update neighbor pointers
        /// </summary>
        void MarkNeighbor(int triangleIndex1, int triangleIndex2)
        {
            var triangle2 = triangles[triangleIndex2];

            var index0 = triangle2.indices[0];
            var index1 = triangle2.indices[1];
            var index2 = triangle2.indices[2];

            var triangle1 = triangles[triangleIndex1];

            // Points of this triangle also belonging to t
            bool a = triangle1.Contains(index0);
            bool b = triangle1.Contains(index1);
            bool c = triangle1.Contains(index2);

            if (b && c)
            {
                triangle2.neighbors[0] = triangleIndex1;
                triangle1.MarkNeighbor(index1, index2, triangleIndex2);
            }
            else if (a && c)
            {
                triangle2.neighbors[1] = triangleIndex1;
                triangle1.MarkNeighbor(index0, index2, triangleIndex2);
            }
            else if (a && b)
            {
                triangle2.neighbors[2] = triangleIndex1;
                triangle1.MarkNeighbor(index0, index1, triangleIndex2);
            }
            else
            {
                FailedToMarkNeighborException();
            }

            triangles[triangleIndex1] = triangle1;
            triangles[triangleIndex2] = triangle2;
        }



        bool HasEdgeByPoints(int p1, int p2)
        {
            if (p2 == int.MaxValue ||
                p2 == p1)
                return false;

            var edgeIndex = this.edges[p1];
            while (edgeIndex != int.MaxValue)
            {
                var sc = allEdges[edgeIndex].index2;
                edgeIndex = allEdges[edgeIndex].next;
                if (sc == p2)
                    return true;
            }
            return false;
        }


        bool HasEdgeCCW(int triangleIndex, int p)
        {
            var triangle = triangles[triangleIndex];
            int pointIndex = triangle.IndexOf(p);
            int idx = (pointIndex + 2) % 3;

            if (idx < 0 || idx > 2)
                return false;

            var p1 = triangle.indices[(idx + 1) % 3];
            var p2 = triangle.indices[(idx + 2) % 3];
            return HasEdgeByPoints(p1, p2) || HasEdgeByPoints(p2, p1);
        }

        static bool SmartIncircle(double2 pa, double2 pb, double2 pc, double2 pd)
        {
            var pdx = pd.x;
            var pdy = pd.y;
            var adx = pa.x - pdx;
            var ady = pa.y - pdy;
            var bdx = pb.x - pdx;
            var bdy = pb.y - pdy;

            var adxbdy = adx * bdy;
            var bdxady = bdx * ady;
            var oabd = adxbdy - bdxady;
            if (oabd <= 0)
            {
                return false;
            }

            var cdx = pc.x - pdx;
            var cdy = pc.y - pdy;

            var cdxady = cdx * ady;
            var adxcdy = adx * cdy;
            var ocad = cdxady - adxcdy;
            if (ocad <= 0)
            {
                return false;
            }

            var bdxcdy = bdx * cdy;
            var cdxbdy = cdx * bdy;

            var alift = adx * adx + ady * ady;
            var blift = bdx * bdx + bdy * bdy;
            var clift = cdx * cdx + cdy * cdy;

            var det = alift * (bdxcdy - cdxbdy) + blift * ocad + clift * oabd;

            return det > 0;
        }


        static bool InScanArea(double2 pa, double2 pb, double2 pc, double2 pd)
        {
            var pdx = pd.x;
            var pdy = pd.y;
            var adx = pa.x - pdx;
            var ady = pa.y - pdy;
            var bdx = pb.x - pdx;
            var bdy = pb.y - pdy;

            var adxbdy = adx * bdy;
            var bdxady = bdx * ady;
            var oabd = adxbdy - bdxady;
            if (oabd <= 0)
            {
                return false;
            }

            var cdx = pc.x - pdx;
            var cdy = pc.y - pdy;

            var cdxady = cdx * ady;
            var adxcdy = adx * cdy;
            var ocad = cdxady - adxcdy;
            if (ocad <= 0)
            {
                return false;
            }
            return true;
        }

        const double kEpsilon = 1e-8f;//12f;

        /// Forumla to calculate signed area
        /// Positive if CCW
        /// Negative if CW
        /// 0 if collinear
        /// A[P1,P2,P3]  =  (x1*y2 - y1*x2) + (x2*y3 - y2*x3) + (x3*y1 - y3*x1)
        ///              =  (x1-x3)*(y2-y3) - (y1-y3)*(x2-x3)
        static Orientation Orient2d(double2 pa, double2 pb, double2 pc)
        {
            var detleft     = (pa.x - pc.x) * (pb.y - pc.y);
            var detright    = (pa.y - pc.y) * (pb.x - pc.x);
            var val = detleft - detright;
            if (val > -kEpsilon && 
                val <  kEpsilon)
            {
                return Orientation.Collinear;
            }
            else if (val > 0)
            {
                return Orientation.CCW;
            }
            return Orientation.CW;
        }
    }
}
