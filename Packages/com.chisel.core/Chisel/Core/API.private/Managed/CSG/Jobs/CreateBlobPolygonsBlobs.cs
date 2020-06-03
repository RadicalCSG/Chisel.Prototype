using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct CreateBlobPolygonsBlobs : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                      treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<NodeTransformations>>  transformations;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>        brushMeshLookup;

        [NoAlias, WriteOnly] public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>.ParallelWriter basePolygons;
        [NoAlias, WriteOnly] public NativeHashMap<int, MinMaxAABB>.ParallelWriter               brushTreeSpaceBounds;


        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices            hashedVertices;
        

        static bool IsDegenerate(HashedVertices hashedVertices, NativeArray<Edge> edges, int edgeCount)
        {
            if (edgeCount < 3)
                return true;

            for (int i = 0; i < edgeCount; i++)
            {
                var vertexIndex1 = edges[i].index1;
                var vertex1 = hashedVertices[vertexIndex1];
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

                    var vertexA = hashedVertices[vertexIndexA];
                    var vertexB = hashedVertices[vertexIndexB];

                    var distance = GeometryMath.SqrDistanceFromPointToLineSegment(vertex1, vertexA, vertexB);
                    if (distance <= CSGConstants.kSqrDistanceEpsilon)
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

        MinMaxAABB CopyPolygonToIndices(BlobAssetReference<BrushMeshBlob> mesh, int polygonIndex, float4x4 nodeToTreeSpaceMatrix, HashedVertices hashedVertices, NativeArray<Edge> edges, ref int edgeCount)
        {
            ref var halfEdges   = ref mesh.Value.halfEdges;
            ref var vertices    = ref mesh.Value.vertices;
            ref var polygon     = ref mesh.Value.polygons[polygonIndex];

            var firstEdge   = polygon.firstEdge;
            var lastEdge    = firstEdge + polygon.edgeCount;

            var min = float3.zero;
            var max = float3.zero;

            // TODO: put in job so we can burstify this, maybe join with RemoveIdenticalIndicesJob & IsDegenerate?
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                var localVertex = new float4(vertices[vertexIndex], 1);
                var worldVertex = math.mul(nodeToTreeSpaceMatrix, localVertex);

                // TODO: could do this in separate loop on vertices
                if (e == firstEdge)
                {
                    min.x = worldVertex.x; max.x = worldVertex.x;
                    min.y = worldVertex.y; max.y = worldVertex.y;
                    min.z = worldVertex.z; max.z = worldVertex.z;
                } else
                {
                    min.x = math.min(min.x, worldVertex.x); max.x = math.max(max.x, worldVertex.x);
                    min.y = math.min(min.y, worldVertex.y); max.y = math.max(max.y, worldVertex.y);
                    min.z = math.min(min.z, worldVertex.z); max.z = math.max(max.z, worldVertex.z);
                }

                var newIndex = hashedVertices.AddNoResize(worldVertex.xyz);
                if (e > firstEdge)
                {
                    var edge = edges[edgeCount - 1];
                    edge.index2 = newIndex;
                    edges[edgeCount - 1] = edge;
                }
                edges[edgeCount] = new Edge() { index1 = newIndex };
                //edges.AddNoResize(new Edge() { index1 = newIndex });
                edgeCount++;
            }
            {
                var edge = edges[edgeCount - 1];
                edge.index2 = edges[0].index1;
                edges[edgeCount - 1] = edge;
            }

            RemoveDuplicates(edges, ref edgeCount);

            if ((edgeCount == 0) || IsDegenerate(hashedVertices, edges, edgeCount))
            {
                edgeCount = 0;
                return new MinMaxAABB();
            }

            return new MinMaxAABB { Min = min, Max = max };
        }

        struct ValidPolygon
        {
            public ushort basePlaneIndex;
            public ushort startEdgeIndex;
            public ushort endEdgeIndex;
        }

        public void Execute(int b)
        {
            var brushOrderIndex = treeBrushIndexOrders[b];
            var brushNodeIndex  = brushOrderIndex.NodeIndex;
            var transform       = transformations[brushNodeIndex];

            var mesh                    = brushMeshLookup[brushNodeIndex];
            ref var vertices            = ref mesh.Value.vertices;
            ref var halfEdges           = ref mesh.Value.halfEdges;
            ref var localPlanes         = ref mesh.Value.localPlanes;
            ref var polygons            = ref mesh.Value.polygons;
            var nodeToTreeSpaceMatrix   = transform.Value.nodeToTree;

            if (!hashedVertices.IsCreated)
            {
                hashedVertices          = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
            } else
            {
                if (hashedVertices.Capacity < vertices.Length)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(vertices.Length, Allocator.Temp);
                } else
                    hashedVertices.Clear();
            }
            
            var min                 = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max                 = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var totalEdgeCount      = 0;
            var totalSurfaceCount   = 0;

            var edges           = new NativeArray<Edge>(halfEdges.Length, Allocator.Temp);
            var validPolygons   = new NativeArray<ValidPolygon>(polygons.Length, Allocator.Temp);
            for (int p = 0; p < polygons.Length; p++)
            {
                var polygon = polygons[p];
                if (polygon.edgeCount < 3 || p >= localPlanes.Length)
                    continue;

                // Note: can end up with duplicate vertices when close enough vertices are snapped together

                int edgeCount = 0;
                int startEdgeIndex = totalEdgeCount;
                var tempEdges = new NativeArray<Edge>(polygon.edgeCount, Allocator.Temp);
                var aabb = CopyPolygonToIndices(mesh, p, nodeToTreeSpaceMatrix, hashedVertices, tempEdges, ref edgeCount);
                if (edgeCount == 0) // Can happen when multiple vertices are collapsed on eachother / degenerate polygon
                    continue;

                for (int e = 0; e < edgeCount; e++)
                {
                    edges[totalEdgeCount] = tempEdges[e];
                    totalEdgeCount++;
                }

                min = math.min(min, aabb.Min);
                max = math.max(max, aabb.Max);

                var endEdgeIndex = totalEdgeCount;
                validPolygons[totalSurfaceCount] = new ValidPolygon
                {
                    basePlaneIndex  = (ushort)p,
                    startEdgeIndex  = (ushort)startEdgeIndex,
                    endEdgeIndex    = (ushort)endEdgeIndex
                };
                totalSurfaceCount++;
            }

            // TODO: the topology information could possibly just be used from the original mesh? (just with worldspace vertices?)
            // TODO: preallocate some structure to store data in?

            var totalEdgeSize       = 16 + (totalEdgeCount    * UnsafeUtility.SizeOf<Edge>());
            var totalPolygonSize    = 16 + (totalSurfaceCount * UnsafeUtility.SizeOf<BasePolygon>());
            var totalSurfaceSize    = 16 + (totalSurfaceCount * UnsafeUtility.SizeOf<BaseSurface>());
            var totalVertexSize     = 16 + (hashedVertices.Length * UnsafeUtility.SizeOf<float3>());
            var totalSize           = totalEdgeSize + totalPolygonSize + totalSurfaceSize + totalVertexSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BasePolygonsBlob>();
            var polygonArray = builder.Allocate(ref root.polygons, totalSurfaceCount);
            builder.Construct(ref root.edges,    edges   , totalEdgeCount);
            builder.Construct(ref root.vertices, hashedVertices);
            var surfaceArray = builder.Allocate(ref root.surfaces, totalSurfaceCount);
            for (int i = 0; i < totalSurfaceCount; i++)
            {
                var polygon = polygons[validPolygons[i].basePlaneIndex];
                polygonArray[i] = new BasePolygon()
                {
                    surfaceInfo = new SurfaceInfo()
                    {
                        basePlaneIndex      = (ushort)validPolygons[i].basePlaneIndex,
                        brushIndexOrder     = brushOrderIndex,
                        interiorCategory    = (CategoryGroupIndex)(int)CategoryIndex.ValidAligned,
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
            basePolygons.TryAdd(brushNodeIndex, basePolygonsBlob);
            //builder.Dispose();

            //hashedVertices.Dispose();
            //edges.Dispose();


            var bounds = new MinMaxAABB() { Min = min, Max = max };
            brushTreeSpaceBounds.TryAdd(brushNodeIndex, bounds);
        }
    }
}
