using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateBlobPolygonsBlobs : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                      allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>       brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly      brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>  treeSpaceVerticesCache;
        
        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BasePolygonsBlob>>           basePolygonCache;


        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices            hashedTreeSpaceVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         edges;
        [NativeDisableContainerSafetyRestriction] NativeArray<ValidPolygon> validPolygons;
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         tempEdges;


        static bool IsDegenerate(HashedVertices hashedTreeSpaceVertices, NativeArray<Edge> edges, int edgeCount)
        {
            if (edgeCount < 3)
                return true;

            for (int i = 0; i < edgeCount; i++)
            {
                var vertexIndex1 = edges[i].index1;
                var vertex1 = hashedTreeSpaceVertices[vertexIndex1];
                for (int j = 0; j < edgeCount; j++)
                {
                    if (i == j)
                        continue;

                    var vertexIndexA = edges[j].index1;
                    var vertexIndexB = edges[j].index2;

                    // Loop loops back on same vertex
                    if (vertexIndex1 == vertexIndexA ||
                        vertexIndex1 == vertexIndexB ||
                        vertexIndexA == vertexIndexB)
                        continue;

                    var vertexA = hashedTreeSpaceVertices[vertexIndexA];
                    var vertexB = hashedTreeSpaceVertices[vertexIndexB];

                    var distance = GeometryMath.SqrDistanceFromPointToLineSegment(vertex1, vertexA, vertexB);
                    if (distance <= CSGConstants.kSqrEdgeDistanceEpsilon)
                        return true;
                }
            }
            return false;
        }
        
        static void RemoveDuplicates(NativeArray<Edge> edges, ref int edgeCount)
        {
            if (edgeCount < 3)
            {
                edgeCount = 0;
                return;
            }

            for (int e = edgeCount - 1; e >= 0; e--)
            {
                if (edges[e].index1 != edges[e].index2)
                    continue;
                edges[e] = edges[edgeCount - 1];
                edgeCount--;
            }
        }

        bool CopyPolygonToIndices(BlobAssetReference<BrushMeshBlob> mesh, ref BlobArray<float3> treeSpaceVertices, int polygonIndex, HashedVertices hashedTreeSpaceVertices, NativeArray<Edge> edges, ref int edgeCount)
        {
            ref var halfEdges   = ref mesh.Value.halfEdges;
            ref var polygon     = ref mesh.Value.polygons[polygonIndex];

            var firstEdge   = polygon.firstEdge;
            var lastEdge    = firstEdge + polygon.edgeCount;

            
            // TODO: put in job so we can burstify this, maybe join with RemoveIdenticalIndicesJob & IsDegenerate?
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex     = halfEdges[e].vertexIndex;
                var treeSpaceVertex = treeSpaceVertices[vertexIndex];

                var newIndex = hashedTreeSpaceVertices.AddNoResize(treeSpaceVertex);
                if (e > firstEdge)
                {
                    var edge = edges[edgeCount - 1];
                    edge.index2 = newIndex;
                    edges[edgeCount - 1] = edge;
                }
                edges[edgeCount] = new Edge { index1 = newIndex };
                //edges.AddNoResize(new Edge { index1 = newIndex });
                edgeCount++;
            }
            {
                var edge = edges[edgeCount - 1];
                edge.index2 = edges[0].index1;
                edges[edgeCount - 1] = edge;
            }

            RemoveDuplicates(edges, ref edgeCount);

            if ((edgeCount == 0) || IsDegenerate(hashedTreeSpaceVertices, edges, edgeCount))
            {
                edgeCount = 0;
                return false;
            }

            return true;
        }

        struct ValidPolygon
        {
            public ushort basePlaneIndex;
            public ushort startEdgeIndex;
            public ushort endEdgeIndex;
        }

        public void Execute(int b)
        {
            var indexOrder = allUpdateBrushIndexOrders[b];
            int nodeOrder  = indexOrder.nodeOrder;
            int nodeIndex = indexOrder.nodeIndex;

            if (treeSpaceVerticesCache[nodeOrder] == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                return;

            var mesh                    = brushMeshLookup[nodeOrder];
            ref var treeSpaceVertices   = ref treeSpaceVerticesCache[nodeOrder].Value.treeSpaceVertices;
            ref var halfEdges           = ref mesh.Value.halfEdges;
            ref var localPlanes         = ref mesh.Value.localPlanes;
            ref var polygons            = ref mesh.Value.polygons;

            if (!hashedTreeSpaceVertices.IsCreated)
            {
                hashedTreeSpaceVertices = new HashedVertices(math.max(treeSpaceVertices.Length, 1000), Allocator.Temp);
            } else
            {
                if (hashedTreeSpaceVertices.Capacity < treeSpaceVertices.Length)
                {
                    hashedTreeSpaceVertices.Dispose();
                    hashedTreeSpaceVertices = new HashedVertices(treeSpaceVertices.Length, Allocator.Temp);
                } else
                    hashedTreeSpaceVertices.Clear();
            }


            var totalEdgeCount      = 0;
            var totalSurfaceCount   = 0;

            if (!edges.IsCreated || edges.Length < halfEdges.Length)
            {
                if (edges.IsCreated) edges.Dispose();
                edges = new NativeArray<Edge>(halfEdges.Length, Allocator.Temp);
            }
            if (!validPolygons.IsCreated || validPolygons.Length < polygons.Length)
            {
                if (validPolygons.IsCreated) validPolygons.Dispose();
                validPolygons = new NativeArray<ValidPolygon>(polygons.Length, Allocator.Temp);
            }

            //var edges           = new NativeArray<Edge>(halfEdges.Length, Allocator.Temp);
            //var validPolygons   = new NativeArray<ValidPolygon>(polygons.Length, Allocator.Temp);
            for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
            {
                var polygon = polygons[polygonIndex];
                if (polygon.edgeCount < 3 || polygonIndex >= localPlanes.Length)
                    continue;

                // Note: can end up with duplicate vertices when close enough vertices are snapped together

                int edgeCount = 0;
                int startEdgeIndex = totalEdgeCount;

                if (!tempEdges.IsCreated || tempEdges.Length < polygons.Length)
                {
                    if (tempEdges.IsCreated) tempEdges.Dispose();
                    tempEdges = new NativeArray<Edge>(polygons.Length, Allocator.Temp);
                }

                //var tempEdges = new NativeArray<Edge>(polygon.edgeCount, Allocator.Temp);
                CopyPolygonToIndices(mesh, ref treeSpaceVertices, polygonIndex, hashedTreeSpaceVertices, tempEdges, ref edgeCount);
                if (edgeCount == 0) // Can happen when multiple vertices are collapsed on eachother / degenerate polygon
                    continue;

                for (int e = 0; e < edgeCount; e++)
                {
                    edges[totalEdgeCount] = tempEdges[e];
                    totalEdgeCount++;
                }

                var endEdgeIndex = totalEdgeCount;
                validPolygons[totalSurfaceCount] = new ValidPolygon
                {
                    basePlaneIndex  = (ushort)polygonIndex,
                    startEdgeIndex  = (ushort)startEdgeIndex,
                    endEdgeIndex    = (ushort)endEdgeIndex
                };
                totalSurfaceCount++;
            }

            // TODO: do this section as a separate pass where we first calculate worldspace vertices, 
            //       then snap them all, then do this job

            // NOTE: assumes brushIntersections is in the same order as the brushes are in the tree
            ref var brushIntersections = ref brushesTouchedByBrushCache[nodeOrder].Value.brushIntersections;
            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var intersectingNodeOrder = brushIntersections[i].nodeIndexOrder.nodeOrder;
                if (intersectingNodeOrder < nodeOrder)
                    continue;

                if (treeSpaceVerticesCache[intersectingNodeOrder] == BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null)
                    continue;

                // In order, goes through the previous brushes in the tree, 
                // and snaps any vertex that is almost the same in the next brush, with that vertex

                // TODO: figure out a better way to do this that merges vertices to an average position instead, 
                //       this will break down if too many vertices are close to each other
                ref var intersectingTreeSpaceVertices = ref treeSpaceVerticesCache[intersectingNodeOrder].Value.treeSpaceVertices;
                hashedTreeSpaceVertices.ReplaceIfExists(ref intersectingTreeSpaceVertices);
            }


            // TODO: the topology information could possibly just be used from the original mesh? (just with worldspace vertices?)
            // TODO: preallocate some structure to store data in?

            var totalEdgeSize       = 16 + (totalEdgeCount    * UnsafeUtility.SizeOf<Edge>());
            var totalPolygonSize    = 16 + (totalSurfaceCount * UnsafeUtility.SizeOf<BasePolygon>());
            var totalSurfaceSize    = 16 + (totalSurfaceCount * UnsafeUtility.SizeOf<BaseSurface>());
            var totalVertexSize     = 16 + (hashedTreeSpaceVertices.Length * UnsafeUtility.SizeOf<float3>());
            var totalSize           = totalEdgeSize + totalPolygonSize + totalSurfaceSize + totalVertexSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BasePolygonsBlob>();
            var polygonArray = builder.Allocate(ref root.polygons, totalSurfaceCount);
            root.nodeIndex = nodeIndex;
            builder.Construct(ref root.edges,    edges   , totalEdgeCount);
            builder.Construct(ref root.vertices, hashedTreeSpaceVertices);
            var surfaceArray = builder.Allocate(ref root.surfaces, totalSurfaceCount);
            for (int i = 0; i < totalSurfaceCount; i++)
            {
                var polygon = polygons[validPolygons[i].basePlaneIndex];
                polygonArray[i] = new BasePolygon()
                {
                    nodeIndexOrder      = indexOrder,
                    surfaceInfo     = new SurfaceInfo
                    {
                        basePlaneIndex      = (ushort)validPolygons[i].basePlaneIndex,
                        interiorCategory    = (CategoryGroupIndex)(int)CategoryIndex.ValidAligned,
                        //nodeIndex           = nodeIndex,
                    },
                    startEdgeIndex  = validPolygons[i].startEdgeIndex,
                    endEdgeIndex    = validPolygons[i].endEdgeIndex
                };
                surfaceArray[i] = new BaseSurface
                {
                    layers      = polygon.layerDefinition,
                    localPlane  = localPlanes[validPolygons[i].basePlaneIndex],
                    UV0         = polygon.UV0
                };
            }
            var basePolygonsBlob = builder.CreateBlobAssetReference<BasePolygonsBlob>(Allocator.Persistent);
            basePolygonCache[nodeOrder] = basePolygonsBlob;
            //builder.Dispose();

            //hashedTreeSpaceVertices.Dispose();
            //edges.Dispose();
        }
    }
}
