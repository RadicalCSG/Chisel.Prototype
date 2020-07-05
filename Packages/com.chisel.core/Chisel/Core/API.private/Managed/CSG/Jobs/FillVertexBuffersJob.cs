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

    public struct VertexBufferContents
    {
        public NativeListArray<GeneratedSubMesh> subMeshes;
        public NativeListArray<int> 	indices;
        public NativeListArray<int> 	brushIndices;
        public NativeListArray<float3>  positions;        
        public NativeListArray<float4>  tangents;
        public NativeListArray<float3>  normals;
        public NativeListArray<float2>  uv0;

        public void EnsureAllocated()
        {
            if (!subMeshes   .IsCreated) subMeshes    = new NativeListArray<GeneratedSubMesh>(Allocator.Persistent);
            if (!tangents    .IsCreated) tangents     = new NativeListArray<float4>(Allocator.Persistent);
            if (!normals     .IsCreated) normals      = new NativeListArray<float3>(Allocator.Persistent);
            if (!uv0         .IsCreated) uv0          = new NativeListArray<float2>(Allocator.Persistent);
            if (!positions   .IsCreated) positions    = new NativeListArray<float3>(Allocator.Persistent);
            if (!indices     .IsCreated) indices      = new NativeListArray<int>(Allocator.Persistent);
            if (!brushIndices.IsCreated) brushIndices = new NativeListArray<int>(Allocator.Persistent);
        }

        public void Dispose()
        {
            if (subMeshes   .IsCreated) subMeshes.Dispose();
            if (indices     .IsCreated) indices.Dispose();
            if (brushIndices.IsCreated) brushIndices.Dispose();
            if (positions   .IsCreated) positions.Dispose();
            if (tangents    .IsCreated) tangents.Dispose();
            if (normals     .IsCreated) normals.Dispose();
            if (uv0         .IsCreated) uv0.Dispose();

            subMeshes       = default;
            indices         = default;
            brushIndices    = default;
            positions       = default;
            tangents        = default;
            normals         = default;
            uv0             = default;
        }
    };

    public struct VertexBufferInit
    {
        public LayerParameterIndex layerParameterIndex;
        public int startIndex;
        public int endIndex;
        public int totalVertexCount;
        public int totalIndexCount;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct AllocateVertexBuffersJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<VertexBufferInit>        subMeshSections;

        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<GeneratedSubMesh>   subMeshesArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<int> 	            indicesArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<int> 	            brushIndicesArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float3>             positionsArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float4>             tangentsArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float3>             normalsArray;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float2>             uv0Array;

        public void Execute()
        {
            if (subMeshSections.Length == 0)
                return;

            subMeshesArray.      ResizeExact(subMeshSections.Length);
            indicesArray.        ResizeExact(subMeshSections.Length);
            brushIndicesArray.   ResizeExact(subMeshSections.Length);
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

                if (section.layerParameterIndex == LayerParameterIndex.RenderMaterial)
                { 
                    subMeshesArray   .AllocateWithCapacityForIndex(i, numberOfSubMeshes);
                    brushIndicesArray.AllocateWithCapacityForIndex(i, totalIndexCount / 3);
                    indicesArray     .AllocateWithCapacityForIndex(i, totalIndexCount);
                    positionsArray   .AllocateWithCapacityForIndex(i, totalVertexCount);
                    tangentsArray    .AllocateWithCapacityForIndex(i, totalVertexCount);
                    normalsArray     .AllocateWithCapacityForIndex(i, totalVertexCount);
                    uv0Array         .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    subMeshesArray   [i].Clear();
                    brushIndicesArray[i].Clear();
                    indicesArray     [i].Clear();
                    positionsArray   [i].Clear();
                    tangentsArray    [i].Clear();
                    normalsArray     [i].Clear();
                    uv0Array         [i].Clear();

                    subMeshesArray   [i].Resize(numberOfSubMeshes, NativeArrayOptions.ClearMemory);
                    brushIndicesArray[i].Resize(totalIndexCount / 3, NativeArrayOptions.ClearMemory);
                    indicesArray     [i].Resize(totalIndexCount, NativeArrayOptions.ClearMemory);
                    positionsArray   [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    tangentsArray    [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    normalsArray     [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                    uv0Array         [i].Resize(totalVertexCount, NativeArrayOptions.ClearMemory);
                } else
                if (section.layerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                {
                    brushIndicesArray.AllocateWithCapacityForIndex(i, totalIndexCount / 3);
                    indicesArray     .AllocateWithCapacityForIndex(i, totalIndexCount);
                    positionsArray   .AllocateWithCapacityForIndex(i, totalVertexCount);
                        
                    brushIndicesArray[i].Clear();
                    indicesArray     [i].Clear();
                    positionsArray   [i].Clear();

                    brushIndicesArray[i].Resize(totalIndexCount / 3, NativeArrayOptions.ClearMemory);
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
        [NoAlias, ReadOnly] public NativeArray<VertexBufferInit>    subMeshSections;
        [NoAlias, ReadOnly] public NativeArray<SubMeshCounts>       subMeshCounts;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>      subMeshSurfaces;

        // Read / Write 
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<GeneratedSubMesh>      subMeshesArray;     // numberOfSubMeshes
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<int>                   brushIndicesArray;  // indexCount / 3
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<int>		           indicesArray;       // indexCount
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float3>                positionsArray;     // vertexCount
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float4>                tangentsArray;      // vertexCount
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float2>                uv0Array;           // vertexCount
        [NativeDisableContainerSafetyRestriction]
        [NoAlias, WriteOnly] public NativeListArray<float3>                normalsArray;       // vertexCount

        public void Execute(int index)
        {
            var vertexBufferInit = subMeshSections[index];
            if (vertexBufferInit.endIndex - vertexBufferInit.startIndex == 0)
                return;

            var layerParameterIndex = vertexBufferInit.layerParameterIndex;
            var startIndex          = vertexBufferInit.startIndex;
            var endIndex            = vertexBufferInit.endIndex;
            var totalVertexCount    = vertexBufferInit.totalVertexCount;
            var totalIndexCount     = vertexBufferInit.totalVertexCount;
            if (layerParameterIndex == LayerParameterIndex.RenderMaterial)
            { 
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

                var subMeshes    = subMeshesArray   [index].AsArray();
                var brushIndices = brushIndicesArray[index].AsArray();
                var indices      = indicesArray     [index].AsArray();
                var tangents     = tangentsArray    [index].AsArray();
                var positions    = positionsArray   [index].AsArray();
                var uv0          = uv0Array         [index].AsArray();
                var normals      = normalsArray     [index].AsArray();
                

                int currentBaseVertex = 0;
                int currentBaseIndex = 0;

                for (int subMeshIndex = 0, d = startIndex; d < endIndex; d++, subMeshIndex++)
                {
                    var subMeshCount        = subMeshCounts[d];
                    var vertexCount		    = subMeshCount.vertexCount;
                    var indexCount		    = subMeshCount.indexCount;
                    var surfacesOffset      = subMeshCount.surfacesOffset;
                    var surfacesCount       = subMeshCount.surfacesCount;

                    subMeshes[subMeshIndex] = new GeneratedSubMesh
                    { 
                        baseVertex          = currentBaseVertex,
                        baseIndex           = currentBaseIndex,
                        indexCount          = indexCount,
                        vertexCount         = vertexCount,
                        surfacesOffset      = surfacesOffset,
                        surfacesCount       = surfacesCount,
                    };

                    // copy all the vertices & indices to the sub-meshes for each material
                    for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = currentBaseIndex / 3, indexOffset = currentBaseIndex, indexVertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                            surfaceIndex < lastSurfaceIndex;
                            ++surfaceIndex)
                    {
                        var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                        var brushNodeID         = subMeshSurface.brushNodeID;
                        ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                            
                        ref var sourceIndices   = ref sourceBuffer.indices;
                        ref var sourceVertices  = ref sourceBuffer.vertices;
                        ref var sourceUV0       = ref sourceBuffer.uv0;
                        ref var sourceNormals   = ref sourceBuffer.normals;

                        var sourceIndexCount    = sourceIndices.Length;
                        var sourceVertexCount   = sourceVertices.Length;
                        var sourceBrushCount    = sourceIndexCount / 3;

                        if (sourceIndexCount == 0 ||
                            sourceVertexCount == 0)
                            continue;

                        for (int i = 0; i < sourceBrushCount; i ++)
                            brushIndices[brushIDIndexOffset] = brushNodeID;
                        brushIDIndexOffset += sourceBrushCount;

                        for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                            indices[indexOffset] = (int)(sourceIndices[i] + indexVertexOffset);

                        var vertexOffset = currentBaseVertex + indexVertexOffset;
                        positions   .CopyFrom(vertexOffset, ref sourceVertices, 0, sourceVertexCount);
                        uv0         .CopyFrom(vertexOffset, ref sourceUV0,      0, sourceVertexCount);
                        normals     .CopyFrom(vertexOffset, ref sourceNormals,  0, sourceVertexCount);
                        indexVertexOffset += sourceVertexCount;
                    }

                    currentBaseVertex += vertexCount;
                    currentBaseIndex += indexCount;
                }

                ComputeTangents(indices,
                                positions,
                                uv0,
                                normals,
                                tangents,
                                totalIndices:  currentBaseIndex,
                                totalVertices: currentBaseVertex);

            } else
            if (layerParameterIndex == LayerParameterIndex.PhysicsMaterial)
            {
                var subMeshCount    = subMeshCounts[startIndex];
                var meshIndex		= subMeshCount.meshQueryIndex;
                var subMeshIndex	= subMeshCount.subMeshQueryIndex;

                var surfacesOffset  = subMeshCount.surfacesOffset;
                var surfacesCount   = subMeshCount.surfacesCount;
                
                var brushIndices    = this.brushIndicesArray[index];
                var indices         = this.indicesArray[index];
                var positions       = this.positionsArray[index];
                
                var brushIndicesArray  = brushIndices.AsArray();
                var indicesArray       = indices.AsArray();
                var positionsArray     = positions.AsArray();

                // copy all the vertices & indices to a mesh for the collider
                for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                        surfaceIndex < lastSurfaceIndex;
                        ++surfaceIndex)
                {
                    var subMeshSurface      = subMeshSurfaces[surfaceIndex];
                    var brushNodeID         = subMeshSurface.brushNodeID;
                    ref var sourceBuffer    = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                    ref var srcIndices      = ref sourceBuffer.indices;
                    ref var srcVertices     = ref sourceBuffer.vertices;

                    var sourceIndexCount    = srcIndices.Length;
                    var sourceVertexCount   = srcVertices.Length;
                    var sourceBrushCount    = sourceIndexCount / 3;

                    if (sourceIndexCount == 0 ||
                        sourceVertexCount == 0)
                        continue;


                    for (int i = 0; i < sourceBrushCount; i++)
                        brushIndicesArray[brushIDIndexOffset] = brushNodeID;
                    brushIDIndexOffset += sourceBrushCount;

                    for (int i = 0; i < sourceIndexCount; i++, indexOffset++)
                        indicesArray[indexOffset] = (int)(srcIndices[i] + vertexOffset); 

                    positionsArray.CopyFrom(vertexOffset, ref srcVertices, 0, sourceVertexCount);

                    vertexOffset += sourceVertexCount;
                }
            }
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
