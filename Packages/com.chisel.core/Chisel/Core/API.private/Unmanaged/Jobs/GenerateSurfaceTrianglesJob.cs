using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)] // Fails for some reason    
    struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
    {
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              allUpdateBrushIndexOrders;
        
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BasePolygonsBlob>>  basePolygonCache;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                         transformationCache;
        [NoAlias, ReadOnly] public NativeStream.Reader                                      input;        
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>.ReadOnly                          meshQueries;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>               surfaceColliderVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<RenderVertex>         surfaceRenderVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>                  indexRemap;
        [NativeDisableContainerSafetyRestriction] NativeList<int>                   loops;
        [NativeDisableContainerSafetyRestriction] NativeList<ChiselQuerySurface>    querySurfaceList;

        [NativeDisableContainerSafetyRestriction] HashedVertices            brushVertices;
        [NativeDisableContainerSafetyRestriction] NativeListArray<int>      surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>  surfaceLoopAllInfos;
        [NativeDisableContainerSafetyRestriction] NativeListArray<Edge>     surfaceLoopAllEdges;
        [NativeDisableContainerSafetyRestriction] NativeList<int>           surfaceIndexList;
        [NativeDisableContainerSafetyRestriction] NativeList<int>           outputSurfaceIndicesArray;
        [NativeDisableContainerSafetyRestriction] NativeArray<float2>       context_points;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>          context_edges;
        [NativeDisableContainerSafetyRestriction] NativeList<int>           context_sortedPoints;
        [NativeDisableContainerSafetyRestriction] NativeList<bool>          context_triangleInterior;
        [NativeDisableContainerSafetyRestriction] NativeList<Edge>          context_inputEdgesCopy; 
        [NativeDisableContainerSafetyRestriction] NativeListArray<Chisel.Core.Edge> context_edgeLookupEdges;
        [NativeDisableContainerSafetyRestriction] NativeHashMap<int, int>           context_edgeLookups;
        [NativeDisableContainerSafetyRestriction] NativeListArray<Chisel.Core.Edge> context_foundLoops;
        [NativeDisableContainerSafetyRestriction] NativeListArray<int>              context_children;
        [NativeDisableContainerSafetyRestriction] NativeList<Poly2Tri.DTSweep.DirectedEdge>         context_allEdges;
        [NativeDisableContainerSafetyRestriction] NativeList<Poly2Tri.DTSweep.DelaunayTriangle>     context_triangles;
        [NativeDisableContainerSafetyRestriction] NativeList<Poly2Tri.DTSweep.AdvancingFrontNode>   context_advancingFrontNodes;

        [BurstDiscard]
        public static void InvalidFinalCategory(CategoryIndex _interiorCategory)
        {
            Debug.Assert(false, $"Invalid final category {_interiorCategory}");
        }

        struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
        {
            public int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
            {
                var diff = x.surfaceParameter - y.surfaceParameter;
                if (diff != 0)
                    return diff;
                return x.surfaceIndex - y.surfaceIndex;
            }
        }

        static readonly CompareSortByBasePlaneIndex compareSortByBasePlaneIndex = new CompareSortByBasePlaneIndex();

        public void Execute(int index)
        {
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder = brushIndexOrder.nodeOrder;
            var vertexCount = input.Read<int>();
            NativeCollectionHelpers.EnsureCapacityAndClear(ref brushVertices, vertexCount);
            for (int v = 0; v < vertexCount; v++)
            {
                var vertex = input.Read<float3>();
                brushVertices.AddNoResize(vertex);
            }


            var surfaceOuterCount = input.Read<int>();
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceOuterCount);
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
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref surfaceLoopAllInfos, surfaceLoopCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopAllEdges, surfaceLoopCount);
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



            if (!basePolygonCache[brushNodeOrder].IsCreated)
                return;

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


            ref var baseSurfaces            = ref basePolygonCache[brushNodeOrder].Value.surfaces;
            var brushTransformations        = transformationCache[brushNodeOrder];
            var treeToNode                  = brushTransformations.treeToNode;
            var nodeToTreeInverseTransposed = math.transpose(treeToNode);                

            var pointCount                  = brushVertices.Length + 2;

            NativeCollectionHelpers.EnsureMinimumSize(ref context_points, pointCount);
            NativeCollectionHelpers.EnsureMinimumSize(ref context_edges, pointCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_allEdges, pointCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_sortedPoints, pointCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_triangles, pointCount * 3);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_triangleInterior, pointCount * 3);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_advancingFrontNodes, pointCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref context_edgeLookupEdges, pointCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref context_foundLoops, pointCount);
            NativeCollectionHelpers.EnsureConstantSizeAndClear(ref context_children, 64);
            NativeCollectionHelpers.EnsureConstantSizeAndClear(ref context_inputEdgesCopy, 64);
            NativeCollectionHelpers.EnsureConstantSizeAndClear(ref context_inputEdgesCopy, 64);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref context_edgeLookups, pointCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref loops, maxLoops);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref surfaceIndexList, maxIndices);

            var builder = new ChiselBlobBuilder(Allocator.Temp, 4096);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaceRenderBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

            for (int s = 0; s < surfaceLoopIndices.Length; s++)
            {
                ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];
                var surfaceIndex = s;
                surfaceRenderBuffer.surfaceIndex = surfaceIndex;

                if (!surfaceLoopIndices.IsIndexCreated(s))
                    continue;

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




                    NativeCollectionHelpers.EnsureCapacityAndClear(ref outputSurfaceIndicesArray, loopEdges.Length * 3);
                    
                    var context = new Poly2Tri.DTSweep
                    {
                        vertices            = brushVertices,
                        points              = context_points,
                        edgeLength          = pointCount,
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
                        surfaceIndicesArray = outputSurfaceIndicesArray
                    };
                    context.Execute();



                    if (outputSurfaceIndicesArray.Length >= 3)
                    {
                        if (interiorCategory == CategoryIndex.ValidReverseAligned ||
                            interiorCategory == CategoryIndex.ReverseAligned)
                        {
                            var maxCount = outputSurfaceIndicesArray.Length - 1;
                            for (int n = (maxCount / 2); n >= 0; n--)
                            {
                                var t = outputSurfaceIndicesArray[n];
                                outputSurfaceIndicesArray[n] = outputSurfaceIndicesArray[maxCount - n];
                                outputSurfaceIndicesArray[maxCount - n] = t;
                            }
                        }

                        for (int n = 0; n < outputSurfaceIndicesArray.Length; n++)
                            surfaceIndexList.Add(outputSurfaceIndicesArray[n]);
                    }
                    outputSurfaceIndicesArray.Dispose();
                }

                if (surfaceIndexList.Length == 0)
                    continue;

                var surfaceIndicesCount = surfaceIndexList.Length;
                NativeCollectionHelpers.EnsureMinimumSize(ref surfaceColliderVertices, brushVertices.Length);
                NativeCollectionHelpers.EnsureMinimumSize(ref surfaceRenderVertices, brushVertices.Length);                
                NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref indexRemap, brushVertices.Length);


                if (interiorCategory == CategoryIndex.ValidReverseAligned || interiorCategory == CategoryIndex.ReverseAligned)
                    normal = -normal;

                // Only use the vertices that we've found in the indices
                var surfaceVerticesCount = 0;
                for (int i = 0; i < surfaceIndicesCount; i++)
                {
                    var vertexIndexSrc = surfaceIndexList[i];
                    var vertexIndexDst = indexRemap[vertexIndexSrc];
                    if (vertexIndexDst == 0)
                    {
                        vertexIndexDst = surfaceVerticesCount;
                        var position = brushVertices[vertexIndexSrc];
                        surfaceColliderVertices[surfaceVerticesCount] = position;

                        var uv0 = math.mul(uv0Matrix, new float4(position, 1)).xy;
                        surfaceRenderVertices[surfaceVerticesCount] = new RenderVertex
                        {
                            position    = position,
                            normal      = normal,
                            uv0         = uv0
                        };
                        surfaceVerticesCount++;
                        indexRemap[vertexIndexSrc] = vertexIndexDst + 1;
                    } else
                        vertexIndexDst--;
                    surfaceIndexList[i] = vertexIndexDst;
                }

                var vertexHash = surfaceColliderVertices.Hash(surfaceVerticesCount);
                var indicesHash = surfaceIndexList.Hash(surfaceIndicesCount);
                var geometryHash = math.hash(new uint2(vertexHash, indicesHash));

                

                ComputeTangents(surfaceIndexList.AsArray(),
                                surfaceRenderVertices,
                                surfaceIndicesCount,
                                surfaceVerticesCount);


                var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int i = 0; i < surfaceVerticesCount; i++)
                {
                    min = math.min(min, surfaceColliderVertices[i]);
                    max = math.max(max, surfaceColliderVertices[i]);
                }

                surfaceRenderBuffer.surfaceLayers = surfaceLayers;

                surfaceRenderBuffer.vertexCount = surfaceVerticesCount;
                surfaceRenderBuffer.indexCount = surfaceIndexList.Length;

                // TODO: properly compute hash again, AND USE IT
                surfaceRenderBuffer.surfaceHash = 0;// math.hash(new uint3(normalHash, tangentHash, uv0Hash));
                surfaceRenderBuffer.geometryHash = geometryHash;

                surfaceRenderBuffer.min = min;
                surfaceRenderBuffer.max = max;

                var outputIndices = builder.Construct(ref surfaceRenderBuffer.indices, surfaceIndexList, surfaceIndicesCount);
                var outputVertices = builder.Construct(ref surfaceRenderBuffer.colliderVertices, surfaceColliderVertices, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.renderVertices, surfaceRenderVertices, surfaceVerticesCount);


                Debug.Assert(outputVertices.Length == surfaceRenderBuffer.vertexCount);
                Debug.Assert(outputIndices.Length == surfaceRenderBuffer.indexCount);
            }

            NativeCollectionHelpers.EnsureCapacityAndClear(ref querySurfaceList, surfaceRenderBuffers.Length);

            var querySurfaces = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var meshQuery       = meshQueries[t];
                var layerQueryMask  = meshQuery.LayerQueryMask;
                var layerQuery      = meshQuery.LayerQuery;
                var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1 && 
                                             meshQuery.LayerParameterIndex <= LayerParameterIndex.MaxLayerParameterIndex) ?
                                             (int)meshQuery.LayerParameterIndex - 1 : -1;

                querySurfaceList.Clear();

                for (int s = 0; s < surfaceRenderBuffers.Length; s++)
                {
                    var surfaceLayers       = surfaceRenderBuffers[s].surfaceLayers;
                    var core_surface_flags  = surfaceLayers.layerUsage;
                    if ((core_surface_flags & layerQueryMask) != layerQuery)
                        continue;

                    querySurfaceList.AddNoResize(new ChiselQuerySurface
                    {
                        surfaceIndex        = surfaceRenderBuffers[s].surfaceIndex,
                        surfaceParameter    = surfaceParameterIndex < 0 ? 0 : surfaceLayers.layerParameters[surfaceParameterIndex],
                        vertexCount         = surfaceRenderBuffers[s].vertexCount,
                        indexCount          = surfaceRenderBuffers[s].indexCount,
                        surfaceHash         = surfaceRenderBuffers[s].surfaceHash,
                        geometryHash        = surfaceRenderBuffers[s].geometryHash
                    });
                }
                querySurfaceList.Sort(compareSortByBasePlaneIndex);

                builder.Construct(ref querySurfaces[t].surfaces, querySurfaceList);
                querySurfaces[t].brushNodeID = brushIndexOrder.compactNodeID;
            }

            root.surfaceOffset = 0;
            root.surfaceCount = surfaceRenderBuffers.Length;

            var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);

            if (brushRenderBufferCache[brushNodeOrder].IsCreated)
                brushRenderBufferCache[brushNodeOrder].Dispose();

            brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
        }

        
        static void ComputeTangents(NativeArray<int>            indices,
                                    NativeArray<RenderVertex>   vertices,
                                    int totalIndices,
                                    int totalVertices) 
        {

            var triTangents     = new NativeArray<double3>(totalVertices, Allocator.Temp);
            var triBinormals    = new NativeArray<double3>(totalVertices, Allocator.Temp);

            for (int i = 0; i < totalIndices; i += 3)
            {
                var index0 = indices[i + 0];
                var index1 = indices[i + 1];
                var index2 = indices[i + 2];

                var vertex0 = vertices[index0];
                var vertex1 = vertices[index1];
                var vertex2 = vertices[index2];
                var position0 = vertex0.position;
                var position1 = vertex1.position;
                var position2 = vertex2.position;
                var uv0 = vertex0.uv0;
                var uv1 = vertex1.uv0;
                var uv2 = vertex2.uv0;

                var p = new double3(position1.x - position0.x, position1.y - position0.y, position1.z - position0.z );
                var q = new double3(position2.x - position0.x, position2.y - position0.y, position2.z - position0.z );
                var s = new double2(uv1.x - uv0.x, uv2.x - uv0.x);
                var t = new double2(uv1.y - uv0.y, uv2.y - uv0.y);

                var scale       = s.x * t.y - s.y * t.x;
                var absScale    = math.abs(scale);
                p *= scale; q *= scale;

                var tangent  = math.normalize(t.y * p - t.x * q) * absScale;
                var binormal = math.normalize(s.x * q - s.y * p) * absScale;

                var edge20 = math.normalize(position2 - position0);
                var edge01 = math.normalize(position0 - position1);
                var edge12 = math.normalize(position1 - position2);

                var angle0 = math.dot(edge20, -edge01);
                var angle1 = math.dot(edge01, -edge12);
                var angle2 = math.dot(edge12, -edge20);
                var weight0 = math.acos(math.clamp(angle0, -1.0, 1.0));
                var weight1 = math.acos(math.clamp(angle1, -1.0, 1.0));
                var weight2 = math.acos(math.clamp(angle2, -1.0, 1.0));

                triTangents[index0] = weight0 * tangent;
                triTangents[index1] = weight1 * tangent;
                triTangents[index2] = weight2 * tangent;

                triBinormals[index0] = weight0 * binormal;
                triBinormals[index1] = weight1 * binormal;
                triBinormals[index2] = weight2 * binormal;
            }

            for (int v = 0; v < totalVertices; ++v)
            {
                var originalTangent  = triTangents[v];
                var originalBinormal = triBinormals[v];
                var vertex           = vertices[v];
                var normal           = (double3)vertex.normal;

                var dotTangent = math.dot(normal, originalTangent);
                var newTangent = new double3(originalTangent.x - dotTangent * normal.x, 
                                                originalTangent.y - dotTangent * normal.y, 
                                                originalTangent.z - dotTangent * normal.z);
                var tangentMagnitude = math.length(newTangent);
                newTangent /= tangentMagnitude;

                var dotBinormal = math.dot(normal, originalBinormal);
                dotTangent      = math.dot(newTangent, originalBinormal) * tangentMagnitude;
                var newBinormal = new double3(originalBinormal.x - dotBinormal * normal.x - dotTangent * newTangent.x,
                                                originalBinormal.y - dotBinormal * normal.y - dotTangent * newTangent.y,
                                                originalBinormal.z - dotBinormal * normal.z - dotTangent * newTangent.z);
                var binormalMagnitude = math.length(newBinormal);
                newBinormal /= binormalMagnitude;

                const double kNormalizeEpsilon = 1e-6;
                if (tangentMagnitude <= kNormalizeEpsilon || binormalMagnitude <= kNormalizeEpsilon)
                {
                    var dpXN = math.abs(math.dot(new double3(1, 0, 0), normal));
                    var dpYN = math.abs(math.dot(new double3(0, 1, 0), normal));
                    var dpZN = math.abs(math.dot(new double3(0, 0, 1), normal));

                    double3 axis1, axis2;
                    if (dpXN <= dpYN && dpXN <= dpZN)
                    {
                        axis1 = new double3(1,0,0);
                        axis2 = (dpYN <= dpZN) ? new double3(0, 1, 0) : new double3(0, 0, 1);
                    }
                    else if (dpYN <= dpXN && dpYN <= dpZN)
                    {
                        axis1 = new double3(0, 1, 0);
                        axis2 = (dpXN <= dpZN) ? new double3(1, 0, 0) : new double3(0, 0, 1);
                    }
                    else
                    {
                        axis1 = new double3(0, 0, 1);
                        axis2 = (dpXN <= dpYN) ? new double3(1, 0, 0) : new double3(0, 1, 0);
                    }

                    newTangent  = axis1 - math.dot(normal, axis1) * normal;
                    newBinormal = axis2 - math.dot(normal, axis2) * normal - math.dot(newTangent, axis2) * math.normalizesafe(newTangent);

                    newTangent  = math.normalizesafe(newTangent);
                    newBinormal = math.normalizesafe(newBinormal);
                }

                var dp = math.dot(math.cross(normal, newTangent), newBinormal);
                var tangent = new float4((float3)newTangent.xyz, (dp > 0) ? 1 : -1);
                
                vertex.tangent = tangent;
                vertices[v] = vertex;
            }
        }
    }
}
