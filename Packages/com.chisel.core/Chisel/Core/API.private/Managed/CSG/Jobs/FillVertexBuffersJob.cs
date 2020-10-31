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
    internal struct SubMeshCounts
    {
        public MeshQuery meshQuery;
        public int		surfaceParameter;

        public int		meshQueryIndex;
        public int		subMeshQueryIndex;
            
        public uint	    geometryHashValue;  // used to detect changes in vertex positions  
        public uint	    surfaceHashValue;   // used to detect changes in color, normal, tangent or uv (doesn't effect lighting)
            
        public int		vertexCount;
        public int		indexCount;
            
        public int      surfacesOffset;
        public int      surfacesCount;
    };

    public struct SubMeshSection
    {
        public MeshQuery meshQuery;
        public int startIndex;
        public int endIndex;
        public int totalVertexCount;
        public int totalIndexCount;
    }
    
    public struct BrushData
    {
        public IndexOrder                                   brushIndexOrder; //<- TODO: if we use NodeOrder maybe this could be explicit based on the order in array?
        public int                                          brushSurfaceCount;
        public BlobAssetReference<ChiselBrushRenderBuffer>  brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct FindBrushRenderBuffersJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public int meshQueryLength;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                   allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBufferCache;

        // Write
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<BrushData>         brushRenderData;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<SubMeshSurface>    subMeshSurfaces;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<SubMeshCounts>     subMeshCounts;
        [NativeDisableParallelForRestriction, NoAlias] public NativeList<SubMeshSection>    subMeshSections;

        public void Execute()
        {
            //if (brushRenderData.Capacity < allTreeBrushIndexOrders.Length)
            //    brushRenderData.Capacity = allTreeBrushIndexOrders.Length;

            int surfaceCount = 0;
            for (int b = 0, count_b = allTreeBrushIndexOrders.Length; b < count_b; b++)
            {
                var brushIndexOrder     = allTreeBrushIndexOrders[b];
                var brushNodeOrder      = allTreeBrushIndexOrders[b].nodeOrder;
                var brushRenderBuffer   = brushRenderBufferCache[brushNodeOrder];
                if (!brushRenderBuffer.IsCreated)
                    continue;

                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;
                ref var surfaces = ref brushRenderBufferRef.surfaces;

                var brushSurfaceCount = surfaces.Length;
                if (brushSurfaceCount == 0)
                    continue;

                brushRenderData.AddNoResize(new BrushData{
                    brushIndexOrder     = brushIndexOrder,
                    brushSurfaceCount   = brushSurfaceCount,
                    brushRenderBuffer   = brushRenderBuffer
                });

                surfaceCount += surfaces.Length;
            }
            
            var surfaceCapacity = surfaceCount * meshQueryLength;
            if (subMeshSurfaces.Capacity < surfaceCapacity)
                subMeshSurfaces.Capacity = surfaceCapacity;

            var subMeshCapacity = surfaceCount * meshQueryLength;
            if (subMeshCounts.Capacity < subMeshCapacity)
                subMeshCounts.Capacity = subMeshCapacity;
            
            if (subMeshSections.Capacity < subMeshCapacity)
                subMeshSections.Capacity = subMeshCapacity;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AllocateVertexBuffersJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>        subMeshSections;

        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<GeneratedSubMesh> subMeshesArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int> 	            indicesArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int> 	            triangleBrushIndices;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           positionsArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float4>           tangentsArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           normalsArray;
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float2>           uv0Array;

        public void Execute()
        {
            if (subMeshSections.Length == 0)
                return;

            subMeshesArray.      ResizeExact(subMeshSections.Length);
            indicesArray.        ResizeExact(subMeshSections.Length);
            triangleBrushIndices.ResizeExact(subMeshSections.Length);
            positionsArray.      ResizeExact(subMeshSections.Length);
            tangentsArray.       ResizeExact(subMeshSections.Length);
            normalsArray.        ResizeExact(subMeshSections.Length);
            uv0Array.            ResizeExact(subMeshSections.Length);
            for (int i = 0; i < subMeshSections.Length; i++)
            {
                var section = subMeshSections[i];
                var numberOfSubMeshes   = section.endIndex - section.startIndex;
                var totalVertexCount    = section.totalVertexCount;
                var totalIndexCount     = section.totalIndexCount;
                
                if (section.meshQuery.LayerParameterIndex == LayerParameterIndex.None ||
                    section.meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                { 
                    subMeshesArray      .AllocateWithCapacityForIndex(i, numberOfSubMeshes);
                    triangleBrushIndices.AllocateWithCapacityForIndex(i, totalIndexCount / 3);
                    indicesArray        .AllocateWithCapacityForIndex(i, totalIndexCount);
                    positionsArray      .AllocateWithCapacityForIndex(i, totalVertexCount);
                    tangentsArray       .AllocateWithCapacityForIndex(i, totalVertexCount);
                    normalsArray        .AllocateWithCapacityForIndex(i, totalVertexCount);
                    uv0Array            .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    subMeshesArray      [i].Clear();
                    triangleBrushIndices[i].Clear();
                    indicesArray        [i].Clear();
                    positionsArray      [i].Clear();
                    tangentsArray       [i].Clear();
                    normalsArray        [i].Clear();
                    uv0Array            [i].Clear();

                    subMeshesArray      [i].Resize(numberOfSubMeshes, NativeArrayOptions.ClearMemory);
                    triangleBrushIndices[i].Resize(totalIndexCount / 3, NativeArrayOptions.ClearMemory);
                    indicesArray        [i].Resize(totalIndexCount, NativeArrayOptions.ClearMemory);
                    positionsArray      [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    tangentsArray       [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    normalsArray        [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    uv0Array            [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                } else
                if (section.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                {
                    subMeshesArray   .AllocateWithCapacityForIndex(i, 1);
                    indicesArray     .AllocateWithCapacityForIndex(i, totalIndexCount);
                    positionsArray   .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    subMeshesArray   [i].Clear();
                    indicesArray     [i].Clear();
                    positionsArray   [i].Clear();
                    

                    subMeshesArray   [i].Resize(1, NativeArrayOptions.ClearMemory);
                    indicesArray     [i].Resize(totalIndexCount, NativeArrayOptions.ClearMemory);
                    positionsArray   [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                }
            }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct FillVertexBuffersJob : IJobParallelFor
    {
        // Read Only
        [NoAlias, ReadOnly] public NativeArray<SubMeshSection>  subMeshSections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>   subMeshCounts;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>  subMeshSurfaces;

        // Read / Write 
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<GeneratedSubMesh> subMeshesArray;         // numberOfSubMeshes
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int>              triangleBrushIndices;   // indexCount / 3
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<int>		        indicesArray;           // indexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           positionsArray;         // vertexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float4>           tangentsArray;          // vertexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float3>           normalsArray;           // vertexCount
        [NativeDisableParallelForRestriction, NoAlias] public NativeListArray<float2>           uv0Array;               // vertexCount

        public void Execute(int index)
        {
            var vertexBufferInit = subMeshSections[index];

            var layerParameterIndex = vertexBufferInit.meshQuery.LayerParameterIndex;
            var startIndex          = vertexBufferInit.startIndex;
            var endIndex            = vertexBufferInit.endIndex;
            var totalVertexCount    = vertexBufferInit.totalVertexCount;
            var totalIndexCount     = vertexBufferInit.totalVertexCount;
            if (layerParameterIndex == LayerParameterIndex.None ||
                layerParameterIndex == LayerParameterIndex.RenderMaterial)
            {
                if (vertexBufferInit.endIndex - vertexBufferInit.startIndex == 0)
                    return;
                var numberOfSubMeshes = endIndex - startIndex;


#if false
                const long kHashMagicValue = (long)1099511628211ul;
                UInt64 combinedGeometryHashValue = 0;
                UInt64 combinedSurfaceHashValue = 0;

                for (int i = startIndex; i < endIndex; i++)
                {
                    ref var meshDescription = ref subMeshCounts[i];
                    if (meshDescription.vertexCount < 3 ||
                        meshDescription.indexCount < 3)
                        continue;

                    combinedGeometryHashValue   = (combinedGeometryHashValue ^ meshDescription.geometryHashValue) * kHashMagicValue;
                    combinedSurfaceHashValue    = (combinedSurfaceHashValue  ^ meshDescription.surfaceHashValue) * kHashMagicValue;
                }
                        
                if (geometryHashValue != combinedGeometryHashValue ||
                    surfaceHashValue != combinedSurfaceHashValue)
                {
                    geometryHashValue != combinedGeometryHashValue ||
                    surfaceHashValue != combinedSurfaceHashValue)
#endif

                var subMeshes            = this.subMeshesArray      [index].AsArray();
                var triangleBrushIndices = this.triangleBrushIndices[index].AsArray();
                var indices              = this.indicesArray        [index].AsArray();
                var tangents             = this.tangentsArray       [index].AsArray();
                var positions            = this.positionsArray      [index].AsArray();
                var uv0                  = this.uv0Array            [index].AsArray();
                var normals              = this.normalsArray        [index].AsArray();

                int currentBaseVertex = 0;
                int currentBaseIndex = 0;

                for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
                {
                    var subMeshCount        = subMeshCounts[d];
                    var vertexCount		    = subMeshCount.vertexCount;
                    var indexCount		    = subMeshCount.indexCount;
                    var surfacesOffset      = subMeshCount.surfacesOffset;
                    var surfacesCount       = subMeshCount.surfacesCount;

                    var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                    // copy all the vertices & indices to the sub-meshes for each material
                    for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = currentBaseIndex / 3, indexOffset = currentBaseIndex, indexVertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                            surfaceIndex < lastSurfaceIndex;
                            ++surfaceIndex)
                    {
                        var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                        var brushNodeIndex      = subMeshSurface.brushNodeIndex;
                        ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                        ref var sourceIndices   = ref sourceBuffer.indices;
                        ref var sourceVertices  = ref sourceBuffer.vertices;

                        var sourceIndexCount    = sourceIndices.Length;
                        var sourceVertexCount   = sourceVertices.Length;
                        var sourceBrushCount    = sourceIndexCount / 3;

                        if (sourceIndexCount == 0 ||
                            sourceVertexCount == 0)
                            continue;
                        
                        ref var sourceUV0       = ref sourceBuffer.uv0;
                        ref var sourceNormals   = ref sourceBuffer.normals;
                        ref var sourceTangents  = ref sourceBuffer.tangents;

                        var brushNodeID = brushNodeIndex + 1;                        
                        for (int last = brushIDIndexOffset + sourceBrushCount; brushIDIndexOffset < last; brushIDIndexOffset++)
                            triangleBrushIndices[brushIDIndexOffset] = brushNodeID;

                        for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                            indices[indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);

                        var vertexOffset = currentBaseVertex + indexVertexOffset;
                        positions   .CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);
                        normals     .CopyFrom(vertexOffset, ref sourceNormals,  0, sourceVertexCount);
                        tangents    .CopyFrom(vertexOffset, ref sourceTangents, 0, sourceVertexCount);
                        uv0         .CopyFrom(vertexOffset, ref sourceUV0,      0, sourceVertexCount);

                        min = math.min(min, sourceBuffer.min);
                        max = math.max(max, sourceBuffer.max);

                        indexVertexOffset += sourceVertexCount;
                    }
                    
                    subMeshes[subMeshIndex] = new GeneratedSubMesh
                    { 
                        baseVertex          = currentBaseVertex,
                        baseIndex           = currentBaseIndex,
                        indexCount          = indexCount,
                        vertexCount         = vertexCount,
                        bounds              = new MinMaxAABB { Min = min, Max = max }
                    };

                    currentBaseVertex += vertexCount;
                    currentBaseIndex += indexCount;
                }
            } else
            if (layerParameterIndex == LayerParameterIndex.PhysicsMaterial)
            {
                var subMeshCount    = subMeshCounts[startIndex];
                //var meshIndex		= subMeshCount.meshQueryIndex;
                var subMeshIndex	= subMeshCount.subMeshQueryIndex;

                var surfacesOffset  = subMeshCount.surfacesOffset;
                var surfacesCount   = subMeshCount.surfacesCount;
                var vertexCount		= subMeshCount.vertexCount;
                var indexCount		= subMeshCount.indexCount;
                
                var subMeshes       = this.subMeshesArray   [index].AsArray();
                var indices         = this.indicesArray     [index].AsArray();
                var positions       = this.positionsArray   [index].AsArray();

                var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

                // copy all the vertices & indices to a mesh for the collider
                for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                        surfaceIndex < lastSurfaceIndex;
                        ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                    ref var sourceIndices   = ref sourceBuffer.indices;
                    ref var sourceVertices  = ref sourceBuffer.vertices;

                    var sourceIndexCount    = sourceIndices.Length;
                    var sourceVertexCount   = sourceVertices.Length;
                    var sourceBrushCount    = sourceIndexCount / 3;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;

                    brushIDIndexOffset += sourceBrushCount;

                    for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                        indices[indexOffset] = (int)(sourceIndices[i] + vertexOffset); 

                    positions.CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);

                    min = math.min(min, sourceBuffer.min);
                    max = math.max(max, sourceBuffer.max);

                    vertexOffset += sourceVertexCount;
                }

                subMeshes[0] = new GeneratedSubMesh
                { 
                    baseVertex          = 0,
                    baseIndex           = 0,
                    indexCount          = indexCount,
                    vertexCount         = vertexCount,
                    bounds              = new MinMaxAABB { Min = min, Max = max }
                };
            }
        }
        /*
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
        */
    }

    [BurstCompile(CompileSynchronously = true)]
    struct GenerateMeshDescriptionJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts> subMeshCounts;

        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<GeneratedMeshDescription> meshDescriptions;

        public void Execute()
        {
            if (meshDescriptions.Capacity < subMeshCounts.Length)
                meshDescriptions.Capacity = subMeshCounts.Length;

            for (int i = 0; i < subMeshCounts.Length; i++)
            {
                var subMesh = subMeshCounts[i];

                var description = new GeneratedMeshDescription
                {
                    meshQuery           = subMesh.meshQuery,
                    surfaceParameter    = subMesh.surfaceParameter,
                    meshQueryIndex      = subMesh.meshQueryIndex,
                    subMeshQueryIndex   = subMesh.subMeshQueryIndex,

                    geometryHashValue   = subMesh.geometryHashValue,
                    surfaceHashValue    = subMesh.surfaceHashValue,

                    vertexCount         = subMesh.vertexCount,
                    indexCount          = subMesh.indexCount
                };

                meshDescriptions.Add(description);
            }
        }
    }
    
    public struct SectionData
    {
        public int surfacesOffset;
        public int surfacesCount;
        public MeshQuery meshQuery;
    }

    struct SurfaceInstance
    {
        public SurfaceLayers                                surfaceLayers;
        public int                                          surfaceIndex;
        public int                                          brushNodeIndex;
        public BlobAssetReference<ChiselBrushRenderBuffer>  brushRenderBuffer;
    }
    

    [BurstCompile(CompileSynchronously = true)]
    struct PrepareSubSectionsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<MeshQuery>.ReadOnly  meshQueries;
        [NoAlias, ReadOnly] public NativeArray<BrushData>           brushRenderData;

        // Read, Write
        [NoAlias] public NativeList<SectionData>            sections;
        [NoAlias] public NativeList<SubMeshSurface>         subMeshSurfaces;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<SurfaceInstance> inorderSurfaceInstances;

        public void Execute()
        {
            int requiredSurfaceCount = 0;
            for (int b = 0, count_b = brushRenderData.Length; b < count_b; b++)
                requiredSurfaceCount += brushRenderData[b].brushSurfaceCount;


            NativeCollectionHelpers.EnsureMinimumSize(ref inorderSurfaceInstances, requiredSurfaceCount);

            //Debug.Log($"{brushRenderData.Length}");//5657
            requiredSurfaceCount = 0;
            for (int b = 0, count_b = brushRenderData.Length; b < count_b; b++)
            {
                var brushData           = brushRenderData[b];
                var brushNodeIndex      = brushData.brushIndexOrder.nodeIndex;
                var brushRenderBuffer   = brushData.brushRenderBuffer;
                ref var brushRenderBufferRef = ref brushRenderBuffer.Value;
                ref var surfaces             = ref brushRenderBufferRef.surfaces;

                //Debug.Log($"    x {surfaces.Length}");//~6
                for (int j = 0, count_j = (int)surfaces.Length; j < count_j; j++)
                {
                    inorderSurfaceInstances[requiredSurfaceCount] = new SurfaceInstance
                    {
                        surfaceLayers       = surfaces[j].surfaceLayers,
                        surfaceIndex        = j,
                        brushNodeIndex      = brushNodeIndex,
                        brushRenderBuffer   = brushRenderBuffer
                    };
                    requiredSurfaceCount++;
                }
            }

            var maximumLength = meshQueries.Length * requiredSurfaceCount;
            if (subMeshSurfaces.Capacity < maximumLength)
                subMeshSurfaces.Capacity = maximumLength;
            subMeshSurfaces.ResizeUninitialized(maximumLength);
            var subMeshSurfaceArray = subMeshSurfaces.AsArray();
            sections.ResizeUninitialized(meshQueries.Length);
            var sectionsArray = sections.AsArray();

            var surfacesLength = 0;
            var sectionCount = 0;
            //Debug.Log($"{meshQueries.Length} x {requiredSurfaceCount}");//11 x 33761
            for (int t = 0; t < meshQueries.Length; t++)
            {
                var surfacesOffset  = surfacesLength;
                var meshQuery       = meshQueries[t];
                var layerQueryMask  = meshQuery.LayerQueryMask;
                var layerQuery      = meshQuery.LayerQuery;
                var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1 && 
                                             meshQuery.LayerParameterIndex <= LayerParameterIndex.MaxLayerParameterIndex) ?
                                             (int)meshQuery.LayerParameterIndex - 1 : -1;
                for (int n = 0; n < requiredSurfaceCount; n++) 
                {
                    var surfaceLayers       = inorderSurfaceInstances[n].surfaceLayers;
                    var core_surface_flags  = surfaceLayers.layerUsage;
                    if ((core_surface_flags & layerQueryMask) == layerQuery)
                    {
                        subMeshSurfaceArray[surfacesLength] = new SubMeshSurface
                        {
                            surfaceIndex        = inorderSurfaceInstances[n].surfaceIndex,
                            brushNodeIndex      = inorderSurfaceInstances[n].brushNodeIndex,
                            surfaceParameter    = surfaceParameterIndex < 0 ? 0 : surfaceLayers.layerParameters[surfaceParameterIndex],
                            brushRenderBuffer   = inorderSurfaceInstances[n].brushRenderBuffer
                        };
                        surfacesLength++;
                    }
                }

                var surfacesCount = surfacesLength - surfacesOffset;
                if (surfacesCount == 0)
                    continue;

                sectionsArray[sectionCount] = new SectionData
                { 
                    surfacesOffset  = surfacesOffset,
                    surfacesCount   = surfacesCount,
                    meshQuery       = meshQuery
                };
                sectionCount++;
            }
            subMeshSurfaces.ResizeUninitialized(surfacesLength);
            sections.ResizeUninitialized(sectionCount);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesJob : IJob
    {
        // Read
        [NativeMatchesParallelForLength]
        [NoAlias, ReadOnly] public NativeArray<SectionData> sections;

        // Read Write (Sort)
        [NoAlias] public NativeArray<SubMeshSurface>        subMeshSurfaces;
            
        struct SubMeshSurfaceComparer : IComparer<SubMeshSurface>
        {
            public int Compare(SubMeshSurface x, SubMeshSurface y)
            {
                return x.surfaceParameter.CompareTo(y.surfaceParameter);
            }
        }
        
        static readonly SubMeshSurfaceComparer  subMeshSurfaceComparer  = new SubMeshSurfaceComparer();


        public void Execute()
        {
            for (int t = 0; t < sections.Length; t++)
            {
                var section = sections[t];
                if (section.surfacesCount == 0)
                    return;
                var slice = subMeshSurfaces.Slice(section.surfacesOffset, section.surfacesCount);
                slice.Sort(subMeshSurfaceComparer);
            }
        }
    }
    
    
    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesParallelJob : IJob
    {
        const int kMaxPhysicsVertexCount = 64000;

        // Read
        [NoAlias, ReadOnly] public NativeArray<SectionData>     sections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>  subMeshSurfaces;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshCounts>   subMeshCounts;
        
        public void Execute()
        {
            for (int t = 0; t < sections.Length; t++)
            {
                var section = sections[t];
                if (section.surfacesCount == 0)
                    return;

                var meshQuery       = section.meshQuery;
                var queryOffset     = section.surfacesOffset;
                var queryCount      = section.surfacesCount;
                var isPhysics       = meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial;

                var currentSubMesh  = new SubMeshCounts
                {
                    meshQueryIndex      = t,
                    subMeshQueryIndex   = 0,
                    meshQuery           = meshQuery,
                    surfaceParameter    = subMeshSurfaces[queryOffset].surfaceParameter
                };
                for (int b = 0; b < queryCount; b++)
                {
                    var subMeshSurface              = subMeshSurfaces[queryOffset + b];
                    var surfaceParameter            = subMeshSurface.surfaceParameter;
                    ref var brushRenderBufferRef    = ref subMeshSurface.brushRenderBuffer.Value;
                    ref var brushSurfaceBuffer      = ref brushRenderBufferRef.surfaces[subMeshSurface.surfaceIndex];
                    var surfaceVertexCount          = brushSurfaceBuffer.vertices.Length;
                    var surfaceIndexCount           = brushSurfaceBuffer.indices.Length;

                    if (currentSubMesh.surfaceParameter != surfaceParameter || 
                        (isPhysics && currentSubMesh.vertexCount >= kMaxPhysicsVertexCount))
                    {
                        // Store the previous subMeshCount
                        if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                            subMeshCounts.AddNoResize(currentSubMesh);
                        
                        // Create the new SubMeshCount
                        currentSubMesh.surfaceParameter     = surfaceParameter;
                        currentSubMesh.subMeshQueryIndex++;
                        currentSubMesh.surfaceHashValue     = 0;
                        currentSubMesh.geometryHashValue    = 0;
                        currentSubMesh.indexCount           = 0;
                        currentSubMesh.vertexCount          = 0;
                        currentSubMesh.surfacesCount        = 0;
                    } 

                    currentSubMesh.indexCount   += surfaceIndexCount;
                    currentSubMesh.vertexCount  += surfaceVertexCount;
                    currentSubMesh.surfaceHashValue  = math.hash(new uint2(currentSubMesh.surfaceHashValue, brushSurfaceBuffer.surfaceHash));
                    currentSubMesh.geometryHashValue = math.hash(new uint2(currentSubMesh.geometryHashValue, brushSurfaceBuffer.geometryHash));
                    currentSubMesh.surfacesCount++;
                }
                // Store the last subMeshCount
                if (currentSubMesh.indexCount > 0 && currentSubMesh.vertexCount > 0)
                    subMeshCounts.AddNoResize(currentSubMesh);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct SortSurfacesGatherJob : IJob
    {
        // Read Write (Sort)
        [NoAlias] public NativeList<SubMeshCounts>                subMeshCounts;

        // Write
        [NoAlias, WriteOnly] public NativeList<SubMeshSection>    subMeshSections;
            
        struct SubMeshCountsComparer : IComparer<SubMeshCounts>
        {
            public int Compare(SubMeshCounts x, SubMeshCounts y)
            {
                if (x.meshQuery.LayerParameterIndex != y.meshQuery.LayerParameterIndex) return ((int)x.meshQuery.LayerParameterIndex) - ((int)y.meshQuery.LayerParameterIndex);
                if (x.meshQuery.LayerQuery != y.meshQuery.LayerQuery) return ((int)x.meshQuery.LayerQuery) - ((int)y.meshQuery.LayerQuery);
                if (x.surfaceParameter != y.surfaceParameter) return ((int)x.surfaceParameter) - ((int)y.surfaceParameter);
                if (x.geometryHashValue != y.geometryHashValue) return ((int)x.geometryHashValue) - ((int)y.geometryHashValue);
                return 0;
            }
        }

        static readonly SubMeshCountsComparer   subMeshCountsComparer   = new SubMeshCountsComparer();


        public void Execute()
        {
            if (subMeshCounts.Length == 0)
                return;

            for (int i = 1; i < subMeshCounts.Length; i++)
            {
                var currCount = subMeshCounts[i];
                currCount.surfacesOffset = subMeshCounts[i - 1].surfacesOffset + subMeshCounts[i - 1].surfacesCount;
                subMeshCounts[i] = currCount;
            }

            // Sort all meshDescriptions so that meshes that can be merged are next to each other
            subMeshCounts.Sort(subMeshCountsComparer);


            int descriptionIndex = 0;
            //var contentsIndex = 0;
            if (subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.None ||
                subMeshCounts[0].meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
            {
                var prevQuery = subMeshCounts[0].meshQuery;
                var startIndex = 0;
                for (; descriptionIndex < subMeshCounts.Length; descriptionIndex++)
                {
                    var subMeshCount = subMeshCounts[descriptionIndex];
                    // Exit when layerParameterIndex is no longer LayerParameter1/None
                    if (subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.None &&
                        subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.RenderMaterial)
                        break;

                    var currQuery = subMeshCount.meshQuery;
                    if (prevQuery == currQuery)
                    {
                        continue;
                    }

                    int totalVertexCount = 0;
                    int totalIndexCount = 0;
                    for (int i = startIndex; i < descriptionIndex; i++)
                    {
                        totalVertexCount += subMeshCounts[i].vertexCount;
                        totalIndexCount += subMeshCounts[i].indexCount;
                    }

                    // Group by all subMeshCounts with same query
                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCounts[startIndex].meshQuery,
                        startIndex          = startIndex, 
                        endIndex            = descriptionIndex,
                        totalVertexCount    = totalVertexCount,
                        totalIndexCount     = totalIndexCount,
                    });

                    startIndex = descriptionIndex;
                    prevQuery = currQuery;
                }

                {
                    int totalVertexCount = 0;
                    int totalIndexCount = 0;
                    for (int i = startIndex; i < descriptionIndex; i++)
                    {
                        totalVertexCount += subMeshCounts[i].vertexCount;
                        totalIndexCount  += subMeshCounts[i].indexCount;
                    }

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCounts[startIndex].meshQuery,
                        startIndex          = startIndex,
                        endIndex            = descriptionIndex,
                        totalVertexCount    = totalVertexCount,
                        totalIndexCount     = totalIndexCount
                    });
                }
            }
                

            if (descriptionIndex < subMeshCounts.Length &&
                subMeshCounts[descriptionIndex].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
            {
                Debug.Assert(subMeshCounts[subMeshCounts.Length - 1].meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial);

                // Loop through all subMeshCounts with LayerParameter2, and create collider meshes from them
                for (int i = 0; descriptionIndex < subMeshCounts.Length; descriptionIndex++, i++)
                {
                    var subMeshCount = subMeshCounts[descriptionIndex];

                    // Exit when layerParameterIndex is no longer LayerParameter2
                    if (subMeshCount.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                        break;

                    subMeshSections.AddNoResize(new SubMeshSection
                    {
                        meshQuery           = subMeshCount.meshQuery,
                        startIndex          = descriptionIndex,
                        endIndex            = descriptionIndex,
                        totalVertexCount    = subMeshCount.vertexCount,
                        totalIndexCount     = subMeshCount.indexCount
                    });
                }
            }
        }
    }
}
