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
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              allUpdateBrushIndexOrders;
        
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BasePolygonsBlob>>    basePolygons;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                     transformationCache;
        [NoAlias, ReadOnly] public NativeStream.Reader input;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>       surfaceVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<float3>       surfaceNormals;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       surfaceTangents;
        [NativeDisableContainerSafetyRestriction] NativeArray<float2>       surfaceUV0;
        [NativeDisableContainerSafetyRestriction] NativeArray<int>          indexRemap;

        [NativeDisableContainerSafetyRestriction] HashedVertices            brushVertices;
        [NativeDisableContainerSafetyRestriction] NativeListArray<int>      surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>  surfaceLoopAllInfos;
        [NativeDisableContainerSafetyRestriction] NativeListArray<Edge>     surfaceLoopAllEdges;
        [NativeDisableContainerSafetyRestriction] NativeList<int>           loops;
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

        public void Execute(int index)
        {
            //var brushNodeIndex = treeBrushNodeIndices[index];
            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;

            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder = brushIndexOrder.nodeOrder;
            var vertexCount = input.Read<int>();
            if (!brushVertices.IsCreated || brushVertices.Capacity < vertexCount)
            {
                if (brushVertices.IsCreated) brushVertices.Dispose();
                brushVertices = new HashedVertices(vertexCount, Allocator.Temp);
            } else
                brushVertices.Clear();
            for (int v = 0; v < vertexCount; v++)
            {
                var vertex = input.Read<float3>();
                brushVertices.AddNoResize(vertex);
            }


            var surfaceOuterCount = input.Read<int>();
            if (!surfaceLoopIndices.IsCreated || surfaceLoopIndices.Capacity < surfaceOuterCount)
            {
                if (surfaceLoopIndices.IsCreated) surfaceLoopIndices.Dispose();
                surfaceLoopIndices = new NativeListArray<int>(surfaceOuterCount, Allocator.Temp);
            } else
                surfaceLoopIndices.ClearChildren();
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
            if (!surfaceLoopAllInfos.IsCreated || surfaceLoopAllInfos.Length < surfaceLoopCount)
            {
                if (surfaceLoopAllInfos.IsCreated) surfaceLoopAllInfos.Dispose();
                surfaceLoopAllInfos = new NativeArray<SurfaceInfo>(surfaceLoopCount, Allocator.Temp);
            }
            
            if (!surfaceLoopAllEdges.IsCreated || surfaceLoopAllEdges.Capacity < surfaceLoopCount)
            {
                if (surfaceLoopAllEdges.IsCreated) surfaceLoopAllEdges.Dispose();
                surfaceLoopAllEdges = new NativeListArray<Edge>(surfaceLoopCount, Allocator.Temp);
            } else
                surfaceLoopAllEdges.ClearChildren();


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



            if (!basePolygons[brushNodeOrder].IsCreated)
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


            ref var baseSurfaces                = ref basePolygons[brushNodeOrder].Value.surfaces;
            var brushTransformations            = transformationCache[brushNodeOrder];
            var treeToNode                      = brushTransformations.treeToNode;
            var nodeToTreeInverseTransposed     = math.transpose(treeToNode);
                

            var pointCount                  = brushVertices.Length + 2;

            if (!context_points.IsCreated || context_points.Length < pointCount)
            {
                if (context_points.IsCreated) context_points.Dispose();
                context_points = new NativeArray<float2>(pointCount, Allocator.Temp);
            }

            if (!context_edges.IsCreated || context_edges.Length < pointCount)
            {
                if (context_edges.IsCreated) context_edges.Dispose();
                context_edges = new NativeArray<int>(pointCount, Allocator.Temp);
            }

            if (!context_allEdges.IsCreated)
            {
                context_allEdges = new NativeList<Poly2Tri.DTSweep.DirectedEdge>(pointCount, Allocator.Temp);
            } else
            {
                context_allEdges.Clear();
                if (context_allEdges.Capacity < pointCount)
                    context_allEdges.Capacity = pointCount;
            }

            if (!context_sortedPoints.IsCreated)
            {
                context_sortedPoints = new NativeList<int>(pointCount, Allocator.Temp);
            } else
            {
                context_sortedPoints.Clear();
                if (context_sortedPoints.Capacity < pointCount)
                    context_sortedPoints.Capacity = pointCount;
            }

            if (!context_triangles.IsCreated)
            {
                context_triangles = new NativeList<Poly2Tri.DTSweep.DelaunayTriangle>(pointCount * 3, Allocator.Temp);
            } else
            {
                context_triangles.Clear();
                if (context_triangles.Capacity < pointCount * 3)
                    context_triangles.Capacity = pointCount * 3;
            }

            if (!context_triangleInterior.IsCreated)
            {
                context_triangleInterior = new NativeList<bool>(pointCount * 3, Allocator.Temp);
            } else
            {
                context_triangleInterior.Clear();
                if (context_triangleInterior.Capacity < pointCount * 3)
                    context_triangleInterior.Capacity = pointCount * 3;
            }

            if (!context_advancingFrontNodes.IsCreated)
            {
                context_advancingFrontNodes = new NativeList<Poly2Tri.DTSweep.AdvancingFrontNode>(pointCount, Allocator.Temp);
            } else
            {
                context_advancingFrontNodes.Clear();
                if (context_advancingFrontNodes.Capacity < pointCount)
                    context_advancingFrontNodes.Capacity = pointCount;
            }


            if (!context_edgeLookupEdges.IsCreated || context_edgeLookupEdges.Capacity < pointCount)
            {
                if (context_edgeLookupEdges.IsCreated) context_edgeLookupEdges.Dispose();
                context_edgeLookupEdges = new NativeListArray<Chisel.Core.Edge>(pointCount, Allocator.Temp);
            } else
                context_edgeLookupEdges.ClearChildren();

            if (!context_foundLoops.IsCreated || context_foundLoops.Capacity < pointCount)
            {
                if (context_foundLoops.IsCreated) context_foundLoops.Dispose();
                context_foundLoops = new NativeListArray<Chisel.Core.Edge>(pointCount, Allocator.Temp);
            } else
                context_foundLoops.ClearChildren();

            if (!context_children.IsCreated)
            {
                context_children = new NativeListArray<int>(64, Allocator.Temp);
            } else
                context_children.ClearChildren();

            if (!context_inputEdgesCopy.IsCreated)
            {
                context_inputEdgesCopy = new NativeList<Edge>(64, Allocator.Temp);
            } else
            {
                context_inputEdgesCopy.Clear();
            }

            if (!context_edgeLookups.IsCreated)
            {
                context_edgeLookups = new NativeHashMap<int, int>(pointCount, Allocator.Temp);
            } else
            {
                context_edgeLookups.Clear();
                if (context_edgeLookups.Capacity < pointCount)
                    context_edgeLookups.Capacity = pointCount;
            }

            if (!loops.IsCreated)
            {
                loops = new NativeList<int>(maxLoops, Allocator.Temp);
            } else
            {
                loops.Clear();
                if (loops.Capacity < maxLoops)
                    loops.Capacity = maxLoops;
            }

            if (!surfaceIndexList.IsCreated)
            {
                surfaceIndexList = new NativeList<int>(maxIndices, Allocator.Temp);
            } else
            {
                surfaceIndexList.Clear();
                if (surfaceIndexList.Capacity < maxIndices)
                    surfaceIndexList.Capacity = maxIndices;
            }

            var builder = new BlobBuilder(Allocator.Temp, 4096);
            ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
            var surfaceRenderBuffers = builder.Allocate(ref root.surfaces, surfaceLoopIndices.Length);

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




                    if (!outputSurfaceIndicesArray.IsCreated)
                    {
                        outputSurfaceIndicesArray = new NativeList<int>(loopEdges.Length * 3, Allocator.Temp);
                    } else
                    {
                        outputSurfaceIndicesArray.Clear();
                        if (outputSurfaceIndicesArray.Capacity < loopEdges.Length * 3)
                            outputSurfaceIndicesArray.Capacity = loopEdges.Length * 3;
                    }
                    
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
                    } else
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

                if (!surfaceTangents.IsCreated || surfaceTangents.Length < surfaceVerticesCount)
                {
                    if (surfaceTangents.IsCreated) surfaceTangents.Dispose();
                    surfaceTangents = new NativeArray<float4>(surfaceVerticesCount, Allocator.Temp);
                }

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

                {
                    for (int v = 0; v < surfaceVerticesCount; v++)
                        surfaceUV0[v] = math.mul(uv0Matrix, new float4(surfaceVertices[v], 1)).xy;
                }
                var uv0Hash = surfaceUV0.Hash(surfaceVerticesCount);


                ComputeTangents(surfaceIndexList.AsArray(),
                                surfaceVertices,
                                surfaceUV0,
                                surfaceNormals,
                                surfaceTangents,
                                surfaceIndicesCount,
                                surfaceVerticesCount);

                var tangentHash = surfaceTangents.Hash(surfaceVerticesCount);

                builder.Construct(ref surfaceRenderBuffer.indices, surfaceIndexList, surfaceIndicesCount);
                builder.Construct(ref surfaceRenderBuffer.vertices, surfaceVertices, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.normals, surfaceNormals, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.tangents, surfaceTangents, surfaceVerticesCount);
                builder.Construct(ref surfaceRenderBuffer.uv0, surfaceUV0, surfaceVerticesCount);

                surfaceRenderBuffer.surfaceHash = math.hash(new uint3(normalHash, tangentHash, uv0Hash));
                surfaceRenderBuffer.geometryHash = geometryHash;
                surfaceRenderBuffer.surfaceLayers = surfaceLayers;
                surfaceRenderBuffer.surfaceIndex = surfaceIndex;
            }

            var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);

            if (brushRenderBufferCache[brushNodeOrder].IsCreated)
                brushRenderBufferCache[brushNodeOrder].Dispose();

            brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
        }

        
        static void ComputeTangents(NativeArray<int>        indices,
                                    NativeArray<float3>	    positions,
                                    NativeArray<float2>	    uvs,
                                    NativeArray<float3>	    normals,
                                    NativeArray<float4>	    tangents,
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

                var vertices0 = positions[index0];
                var vertices1 = positions[index1];
                var vertices2 = positions[index2];
                var uvs0 = uvs[index0];
                var uvs1 = uvs[index1];
                var uvs2 = uvs[index2];

                var p = new double3(vertices1.x - vertices0.x, vertices1.y - vertices0.y, vertices1.z - vertices0.z );
                var q = new double3(vertices2.x - vertices0.x, vertices2.y - vertices0.y, vertices2.z - vertices0.z );
                var s = new double2(uvs1.x - uvs0.x, uvs2.x - uvs0.x);
                var t = new double2(uvs1.y - uvs0.y, uvs2.y - uvs0.y);

                var scale       = s.x * t.y - s.y * t.x;
                var absScale    = math.abs(scale);
                p *= scale; q *= scale;

                var tangent  = math.normalize(t.y * p - t.x * q) * absScale;
                var binormal = math.normalize(s.x * q - s.y * p) * absScale;

                var edge20 = math.normalize(vertices2 - vertices0);
                var edge01 = math.normalize(vertices0 - vertices1);
                var edge12 = math.normalize(vertices1 - vertices2);

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
                var normal           = (double3)normals[v];

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
                tangents[v] = new float4((float3)newTangent.xyz, (dp > 0) ? 1 : -1);
            }
        }
    }
}
