using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
    public struct Edge : IEquatable<Edge>
    {
        public ushort index1;
        public ushort index2;

        public bool Equals(Edge other) => (index1 == other.index1 && index2 == other.index2);
        public override int GetHashCode() => (int)math.hash(new int2(index1, index2));
        public override string ToString() => $"({index1}, {index2})";
    }

    [BurstCompile(CompileSynchronously = true)] // Fails for some reason    
    struct GenerateSurfaceTrianglesJob : IJobParallelFor
    {
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              treeBrushNodeIndexOrders;
        
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BasePolygonsBlob>>    basePolygons;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                     transformations;
        [NoAlias, ReadOnly] public NativeStream.Reader input;

        // Write
        [NoAlias, WriteOnly] public NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>.ParallelWriter brushRenderBufferCache;

        //[NoAlias, WriteOnly] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBuffers;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>   surfaceVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>   surfaceNormals;
        [NativeDisableContainerSafetyRestriction] NativeArray<float2>   surfaceUV0;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>      indexRemap;

        [BurstDiscard]
        public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
        {
            Debug.Assert(false, $"Invalid final category {_interiorCategory}");
        }

        const Allocator allocator = Allocator.Temp;

        public void Execute(int index)
        {
            //var brushNodeIndex = treeBrushNodeIndices[index];
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            HashedVertices brushVertices;
            NativeListArray<int> surfaceLoopIndices;
            NativeList<SurfaceInfo> surfaceLoopAllInfos;
            NativeListArray<Edge> surfaceLoopAllEdges;


            var brushNodeIndex = input.Read<int>();
            var brushNodeOrder = input.Read<int>();
            var vertexCount = input.Read<int>();
            brushVertices = new HashedVertices(vertexCount, allocator);
            for (int v = 0; v < vertexCount; v++)
            {
                var vertex = input.Read<float3>();
                brushVertices.AddNoResize(vertex);
            }


            var surfaceOuterCount = input.Read<int>();
            surfaceLoopIndices = new NativeListArray<int>(surfaceOuterCount, allocator);
            surfaceLoopIndices.ResizeExact(surfaceOuterCount);
            for (int o = 0; o < surfaceOuterCount; o++)
            {
                var surfaceInnerCount = input.Read<int>();
                if (surfaceInnerCount > 0)
                {
                    var inner = surfaceLoopIndices.AllocateWithCapacityForIndex(o, surfaceInnerCount);
                    //inner.ResizeUninitialized(surfaceInnerCount);
                    for (int i = 0; i < surfaceInnerCount; i++)
                    {
                        inner.AddNoResize(input.Read<int>());
                    }
                }
            }

            var surfaceLoopCount = input.Read<int>();
            surfaceLoopAllInfos = new NativeList<SurfaceInfo>(surfaceLoopCount, allocator);
            surfaceLoopAllEdges = new NativeListArray<Edge>(surfaceLoopCount, allocator);

            surfaceLoopAllInfos.ResizeUninitialized(surfaceLoopCount);
            surfaceLoopAllEdges.ResizeExact(surfaceLoopCount);
            for (int l = 0; l < surfaceLoopCount; l++)
            {
                surfaceLoopAllInfos[l] = input.Read<SurfaceInfo>();
                var edgeCount   = input.Read<int>();
                if (edgeCount > 0)
                { 
                    var edgesInner  = surfaceLoopAllEdges.AllocateWithCapacityForIndex(l, edgeCount);
                    //edgesInner.ResizeUninitialized(edgeCount);
                    for (int e = 0; e < edgeCount; e++)
                    {
                        edgesInner.AddNoResize(input.Read<Edge>());
                    }
                }
            }
            input.EndForEachIndex();





            var maxLoops = 0;
            var maxIndices = 0;
            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                if (!surfaceLoopIndices.IsIndexCreated(s))
                    continue;
                var length = surfaceLoopIndices[s].Length;
                maxIndices += length;
                maxLoops = math.max(maxLoops, length);
            }


            ref var baseSurfaces                = ref basePolygons[brushNodeOrder].Value.surfaces;
            var brushTransformations            = transformations[brushNodeOrder];
            var treeToNode                      = brushTransformations.treeToNode;
            var nodeToTreeInverseTransposed     = math.transpose(treeToNode);
                

            var pointCount                  = brushVertices.Length + 2;
            var context_points              = new NativeArray<float2>(pointCount, allocator);
            var context_edges               = new NativeArray<int>(pointCount, allocator);
            var context_allEdges            = new NativeList<Poly2Tri.DTSweep.DirectedEdge>(pointCount, allocator);
            var context_sortedPoints        = new NativeList<int>(pointCount, allocator);
            var context_triangles           = new NativeList<Poly2Tri.DTSweep.DelaunayTriangle>(pointCount * 3, allocator);
            var context_triangleInterior    = new NativeList<bool>(pointCount * 3, allocator);
            var context_advancingFrontNodes = new NativeList<Poly2Tri.DTSweep.AdvancingFrontNode>(pointCount, allocator);
            var context_edgeLookupEdges     = new NativeListArray<Chisel.Core.Edge>(pointCount, allocator);
            var context_edgeLookups         = new NativeHashMap<int, int>(pointCount, allocator);
            var context_foundLoops          = new NativeListArray<Chisel.Core.Edge>(pointCount, allocator);

            var context_children            = new NativeListArray<int>(64, allocator);
            var context_inputEdgesCopy      = new NativeList<Edge>(64, allocator);


            var builder = new BlobBuilder(allocator, 4096);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaceRenderBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

            var loops               = new NativeList<int>(maxLoops, allocator);
            var surfaceIndexList    = new NativeList<int>(maxIndices, allocator);
            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                if (!surfaceLoopIndices.IsIndexCreated(s))
                    continue;

                ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];
                loops.Clear();

                var loopIndices = surfaceLoopIndices[s];
                for (int l = 0; l < loopIndices.Length; l++)
                {
                    var surfaceLoopIndex    = loopIndices[l];
                    var surfaceLoopEdges    = surfaceLoopAllEdges[surfaceLoopIndex];

                    // TODO: verify that this never happens, check should be in previous job
                    Debug.Assert(surfaceLoopEdges.Length >= 3);
                    if (surfaceLoopEdges.Length < 3)
                        continue;

                    loops.Add(surfaceLoopIndex);
                }

                // TODO: why are we doing this in tree-space? better to do this in brush-space, then we can more easily cache this
                var surfaceIndex            = s;
                var surfaceLayers           = baseSurfaces[surfaceIndex].layers;
                var localSpacePlane         = baseSurfaces[surfaceIndex].localPlane;
                var UV0                     = baseSurfaces[surfaceIndex].UV0;
                var localSpaceToPlaneSpace  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(localSpacePlane);
                var treeSpaceToPlaneSpace   = math.mul(localSpaceToPlaneSpace, treeToNode);
                var uv0Matrix               = math.mul(UV0.ToFloat4x4(), treeSpaceToPlaneSpace);

                var surfaceTreeSpacePlane   = math.mul(nodeToTreeInverseTransposed, localSpacePlane);

                // Ensure we have the rotation properly calculated, and have a valid normal
                float3 normal = surfaceTreeSpacePlane.xyz;
                quaternion rotation;
                if (((Vector3)normal) == Vector3.forward)
                    rotation = quaternion.identity;
                else
                    rotation = (quaternion)Quaternion.FromToRotation(normal, Vector3.forward);

                surfaceIndexList.Clear();

                CategoryIndex interiorCategory = CategoryIndex.ValidAligned;

                for (int l = 0; l < loops.Length; l++)
                {
                    var loopIndex   = loops[l];
                    var loopEdges   = surfaceLoopAllEdges[loopIndex];
                    var loopInfo    = surfaceLoopAllInfos[loopIndex];
                    interiorCategory = (CategoryIndex)loopInfo.interiorCategory;

                    Debug.Assert(surfaceIndex == loopInfo.basePlaneIndex, "surfaceIndex != loopInfo.basePlaneIndex");




                    var surfaceIndicesArray = new NativeList<int>(allocator);
                    
                    var context = new Poly2Tri.DTSweep
                    {
                        vertices            = brushVertices,
                        points              = context_points,
                        edges               = context_edges,
                        allEdges            = context_allEdges,
                        triangles           = context_triangles,
                        triangleInterior    = context_triangleInterior,
                        sortedPoints        = context_sortedPoints,
                        advancingFrontNodes = context_advancingFrontNodes,
                        edgeLookupEdges     = context_edgeLookupEdges,
                        edgeLookups         = context_edgeLookups,
                        foundLoops          = context_foundLoops,
                        children            = context_children,
                        inputEdgesCopy      = context_inputEdgesCopy,
                        rotation            = rotation,
                        normal              = normal,
                        inputEdges          = loopEdges,
                        surfaceIndicesArray = surfaceIndicesArray
                    };
                    context.Execute();



                    if (surfaceIndicesArray.Length >= 3)
                    {
                        if (interiorCategory == CategoryIndex.ValidReverseAligned ||
                            interiorCategory == CategoryIndex.ReverseAligned)
                        {
                            var maxCount = surfaceIndicesArray.Length - 1;
                            for (int n = (maxCount / 2); n >= 0; n--)
                            {
                                var t = surfaceIndicesArray[n];
                                surfaceIndicesArray[n] = surfaceIndicesArray[maxCount - n];
                                surfaceIndicesArray[maxCount - n] = t;
                            }
                        }

                        for (int n = 0; n < surfaceIndicesArray.Length; n++)
                            surfaceIndexList.Add(surfaceIndicesArray[n]);
                    }
                    surfaceIndicesArray.Dispose();
                }

                if (surfaceIndexList.Length == 0)
                    continue;

                var surfaceIndicesCount = surfaceIndexList.Length;
                if (!surfaceVertices.IsCreated || surfaceVertices.Length < brushVertices.Length)
                {
                    if (surfaceVertices.IsCreated) surfaceVertices.Dispose();
                    surfaceVertices = new NativeArray<float3>(brushVertices.Length, Allocator.Temp);
                }
                if (!indexRemap.IsCreated || indexRemap.Length < brushVertices.Length)
                {
                    if (indexRemap.IsCreated) indexRemap.Dispose();
                    indexRemap = new NativeArray<int>(brushVertices.Length, Allocator.Temp);
                } else
                    indexRemap.ClearValues();


                // Only use the vertices that we've found in the indices
                var surfaceVerticesCount = 0;
                //var surfaceVertices = stackalloc float3[brushVertices.Length];
                //var indexRemap = stackalloc int[brushVertices.Length];
                for (int i = 0; i < surfaceIndicesCount; i++)
                {
                    var vertexIndexSrc = surfaceIndexList[i];
                    var vertexIndexDst = indexRemap[vertexIndexSrc];
                    if (vertexIndexDst == 0)
                    {
                        vertexIndexDst = surfaceVerticesCount;
                        surfaceVertices[surfaceVerticesCount] = brushVertices[vertexIndexSrc];
                        surfaceVerticesCount++;
                        indexRemap[vertexIndexSrc] = vertexIndexDst + 1;
                    }
                    else
                        vertexIndexDst--;
                    surfaceIndexList[i] = vertexIndexDst;
                }

                var vertexHash = surfaceVertices.Hash(surfaceVerticesCount);
                var indicesHash = surfaceIndexList.Hash(surfaceIndicesCount);
                var geometryHash = math.hash(new uint2(vertexHash, indicesHash));

                if (!surfaceNormals.IsCreated || surfaceNormals.Length < surfaceVerticesCount)
                {
                    if (surfaceNormals.IsCreated) surfaceNormals.Dispose();
                    surfaceNormals = new NativeArray<float3>(surfaceVerticesCount, Allocator.Temp);
                }
                //var surfaceNormals = stackalloc float3[surfaceVerticesCount];
                {
                    if (interiorCategory == CategoryIndex.ValidReverseAligned || interiorCategory == CategoryIndex.ReverseAligned)
                        normal = -normal;
                    for (int i = 0; i < surfaceVerticesCount; i++)
                        surfaceNormals[i] = normal;
                }
                var normalHash = surfaceNormals.Hash(surfaceVerticesCount);

                if (!surfaceUV0.IsCreated || surfaceUV0.Length < surfaceVerticesCount)
                {
                    if (surfaceUV0.IsCreated) surfaceUV0.Dispose();
                    surfaceUV0 = new NativeArray<float2>(surfaceVerticesCount, Allocator.Temp);
                }
                //var surfaceUV0 = stackalloc float2[surfaceVerticesCount];
                {
                    for (int v = 0; v < surfaceVerticesCount; v++)
                        surfaceUV0[v] = math.mul(uv0Matrix, new float4(surfaceVertices[v], 1)).xy;
                }
                var uv0Hash = surfaceUV0.Hash(surfaceVerticesCount);

                builder.Construct(ref surfaceRenderBuffer.indices, surfaceIndexList, surfaceIndicesCount);
                builder.Construct(ref surfaceRenderBuffer.vertices, surfaceVertices, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.normals, surfaceNormals, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.uv0, surfaceUV0, surfaceVerticesCount);

                surfaceRenderBuffer.surfaceHash = math.hash(new uint2(normalHash, uv0Hash));
                surfaceRenderBuffer.geometryHash = geometryHash;
                surfaceRenderBuffer.surfaceLayers = surfaceLayers;
                surfaceRenderBuffer.surfaceIndex = surfaceIndex;
            }

            var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);

            //brushRenderBuffers[brushNodeOrder] = brushRenderBuffer;
            brushRenderBufferCache.TryAdd(brushNodeIndex, brushRenderBuffer);

            // Allocated using Temp, so do not need to dispose
            /*
            builder.Dispose();
            loops.Dispose();
            surfaceIndexList.Dispose();


            context_children.Dispose();
            context_inputEdgesCopy.Dispose();

            context_points.Dispose();
            context_edges.Dispose();

            context_allEdges.Dispose();
            context_sortedPoints.Dispose();
            context_triangles.Dispose();
            context_triangleInterior.Dispose();
            context_advancingFrontNodes.Dispose();
            context_edgeLookupEdges.Dispose();
            context_edgeLookups.Dispose();
            context_foundLoops.Dispose();
            */
        }
    }
}
