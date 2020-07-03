using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{ 
    internal struct SubMeshSurface
    {
        public int surfaceIndex;
        public int surfaceParameter;
        public int brushNodeID;
        public BlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    struct GenerateVertexBuffersJob : IJob
    {   
        [NoAlias, ReadOnly] public MeshQuery    meshQuery;
        
        [NoAlias, ReadOnly] public int		    subMeshIndexCount;
        [NoAlias, ReadOnly] public int		    subMeshVertexCount;

        [NoAlias, ReadOnly] public int          surfacesOffset;
        [NoAlias, ReadOnly] public int          surfacesCount;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface> subMeshSurfaces;

        [NoAlias, WriteOnly] public NativeArray<int> generatedMeshBrushIndices;

        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<int>		generatedMeshIndices;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<float3>    generatedMeshPositions;

        //optional
        [NativeDisableContainerSafetyRestriction] [NoAlias, WriteOnly] public NativeArray<float4>    generatedMeshTangents;
        [NativeDisableContainerSafetyRestriction] [NoAlias, WriteOnly] public NativeArray<float3>    generatedMeshNormals;
        [NativeDisableContainerSafetyRestriction] [NoAlias, WriteOnly] public NativeArray<float2>    generatedMeshUV0; 
            
        static void ComputeTangents(NativeArray<int>        meshIndices,
                                    NativeArray<float3>	    positions,
                                    NativeArray<float2>	    uvs,
                                    NativeArray<float3>	    normals,
                                    NativeArray<float4>	    tangents) 
        {
            if (meshIndices.Length == 0 ||
                positions.Length == 0)
                return;

            var tangentU = new NativeArray<float3>(positions.Length, Allocator.Temp);
            var tangentV = new NativeArray<float3>(positions.Length, Allocator.Temp);

            for (int i = 0; i < meshIndices.Length; i+=3) 
            {
                int i0 = meshIndices[i + 0];
                int i1 = meshIndices[i + 1];
                int i2 = meshIndices[i + 2];

                var v1 = positions[i0];
                var v2 = positions[i1];
                var v3 = positions[i2];
        
                var w1 = uvs[i0];
                var w2 = uvs[i1];
                var w3 = uvs[i2];

                var edge1 = v2 - v1;
                var edge2 = v3 - v1;

                var uv1 = w2 - w1;
                var uv2 = w3 - w1;
        
                var r = 1.0f / (uv1.x * uv2.y - uv1.y * uv2.x);
                if (math.isnan(r) || math.isfinite(r))
                    r = 0.0f;

                var udir = new float3(
                    ((edge1.x * uv2.y) - (edge2.x * uv1.y)) * r,
                    ((edge1.y * uv2.y) - (edge2.y * uv1.y)) * r,
                    ((edge1.z * uv2.y) - (edge2.z * uv1.y)) * r
                );

                var vdir = new float3(
                    ((edge1.x * uv2.x) - (edge2.x * uv1.x)) * r,
                    ((edge1.y * uv2.x) - (edge2.y * uv1.x)) * r,
                    ((edge1.z * uv2.x) - (edge2.z * uv1.x)) * r
                );

                tangentU[i0] += udir;
                tangentU[i1] += udir;
                tangentU[i2] += udir;

                tangentV[i0] += vdir;
                tangentV[i1] += vdir;
                tangentV[i2] += vdir;
            }

            for (int i = 0; i < positions.Length; i++) 
            {
                var n	= normals[i];
                var t0	= tangentU[i];
                var t1	= tangentV[i];

                n = math.normalizesafe(n);
                var t = t0 - (n * math.dot(n, t0));
                t = math.normalizesafe(t);

                var c = math.cross(n, t0);
                float w = (math.dot(c, t1) < 0) ? 1.0f : -1.0f;
                tangents[i] = new float4(t.x, t.y, t.z, w);
            }
        }

        public void Execute()
        {
            if (subMeshIndexCount == 0 || subMeshVertexCount == 0)
                return;

            bool useTangents		= (meshQuery.UsedVertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent;
            bool useNormals		    = (meshQuery.UsedVertexChannels & VertexChannelFlags.Normal ) == VertexChannelFlags.Normal;
            bool useUV0s			= (meshQuery.UsedVertexChannels & VertexChannelFlags.UV0    ) == VertexChannelFlags.UV0;
            bool needTempNormals	= useTangents && !useNormals;
            bool needTempUV0		= useTangents && !useUV0s;
            
            var generatedMeshIndicesSlice       = generatedMeshIndices      ;
            var generatedMeshBrushIndicesSlice  = generatedMeshBrushIndices ;
            var generatedMeshPositionsSlice     = generatedMeshPositions    ;
            var generatedMeshTangentsSlice      = generatedMeshTangents.IsCreated ? generatedMeshTangents : default;
            var generatedMeshNormalsSlice       = generatedMeshNormals .IsCreated ? generatedMeshNormals  : default;
            var generatedMeshUV0Slice           = generatedMeshUV0     .IsCreated ? generatedMeshUV0      : default;

            var normals	= needTempNormals ? new NativeArray<float3>(subMeshVertexCount, Allocator.Temp) : generatedMeshNormalsSlice;
            var uv0s	= needTempUV0     ? new NativeArray<float2>(subMeshVertexCount, Allocator.Temp) : generatedMeshUV0Slice;

            // double snap_size = 1.0 / ants.SnapDistance();

            { 
                // copy all the vertices & indices to the sub-meshes for each material
                for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                        surfaceIndex < lastSurfaceIndex;
                        ++surfaceIndex)
                {
                    var subMeshSurface = subMeshSurfaces[surfaceIndex];
                    ref var sourceBuffer = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                    if (sourceBuffer.indices.Length == 0 ||
                        sourceBuffer.vertices.Length == 0)
                        continue;

                    var brushNodeID = subMeshSurface.brushNodeID;

                    for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i += 3)
                    {
                        generatedMeshBrushIndicesSlice[brushIDIndexOffset] = brushNodeID; brushIDIndexOffset++;
                    }

                    for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i ++)
                    {
                        generatedMeshIndicesSlice[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset); indexOffset++;
                    }

                    var sourceVertexCount = sourceBuffer.vertices.Length;

                    generatedMeshPositionsSlice.CopyFrom(vertexOffset, ref sourceBuffer.vertices, 0, sourceVertexCount);

                    if (useUV0s || needTempUV0) uv0s.CopyFrom(vertexOffset, ref sourceBuffer.uv0, 0, sourceVertexCount);
                    if (useNormals || needTempNormals) normals.CopyFrom(vertexOffset, ref sourceBuffer.normals, 0, sourceVertexCount);
                    vertexOffset += sourceVertexCount;
                }
            }

            if (useTangents)
            {
                ComputeTangents(generatedMeshIndicesSlice,
                                generatedMeshPositionsSlice,
                                uv0s,
                                normals,
                                generatedMeshTangentsSlice);
            }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct GenerateVertexBuffersPositionOnlyJob : IJob
    {   
        [NoAlias, ReadOnly] public int		    subMeshIndexCount;
        [NoAlias, ReadOnly] public int		    subMeshVertexCount;

        [NoAlias, ReadOnly] public int          surfacesOffset;
        [NoAlias, ReadOnly] public int          surfacesCount;
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface> subMeshSurfaces;

        [NoAlias, WriteOnly] public NativeArray<int> generatedMeshBrushIndices;

        [NoAlias] public NativeArray<int>		generatedMeshIndices;
        [NoAlias] public NativeArray<float3>    generatedMeshPositions;

        public void Execute()
        {
            if (subMeshIndexCount == 0 || subMeshVertexCount == 0)
                return;
            
            // double snap_size = 1.0 / ants.SnapDistance();

            { 
                // copy all the vertices & indices to the sub-meshes for each material
                for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                        surfaceIndex < lastSurfaceIndex;
                        ++surfaceIndex)
                {
                    var subMeshSurface = subMeshSurfaces[surfaceIndex];
                    ref var sourceBuffer = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                    if (sourceBuffer.indices.Length == 0 ||
                        sourceBuffer.vertices.Length == 0)
                        continue;

                    var brushNodeID = subMeshSurface.brushNodeID;

                    for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i += 3)
                    {
                        generatedMeshBrushIndices[brushIDIndexOffset] = brushNodeID; brushIDIndexOffset++;
                    }

                    for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i ++)
                    {
                        generatedMeshIndices[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset); indexOffset++;
                    }

                    var sourceVertexCount = sourceBuffer.vertices.Length;

                    generatedMeshPositions.CopyFrom(vertexOffset, ref sourceBuffer.vertices, 0, sourceVertexCount);

                    vertexOffset += sourceVertexCount;
                }
            }
        }
    }
    

    [BurstCompile(CompileSynchronously = true)]
    struct GenerateVertexBuffersSlicedJob : IJob
    {
        // Read Only
        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface>      subMeshSurfaces;
        [NoAlias, ReadOnly] public NativeArray<GeneratedSubMesh>    subMeshes;

        // Write only
        [NoAlias, WriteOnly] public NativeArray<int>    generatedMeshBrushIndices;
        [NoAlias, WriteOnly] public NativeArray<float4> generatedMeshTangents;

        // Read / Write (reading during tangent generation)
        [NoAlias] public NativeArray<int>		generatedMeshIndices;
        [NoAlias] public NativeArray<float3>    generatedMeshPositions;
        [NoAlias] public NativeArray<float2>    generatedMeshUV0; 
        [NoAlias] public NativeArray<float3>    generatedMeshNormals;
            
        static void ComputeTangents(NativeSlice<int>        meshIndices,
                                    NativeSlice<float3>	    positions,
                                    NativeSlice<float2>	    uvs,
                                    NativeSlice<float3>	    normals,
                                    NativeSlice<float4>	    tangents) 
        {
            if (meshIndices.Length == 0 ||
                positions.Length == 0)
                return;

            var tangentU = new NativeArray<float3>(positions.Length, Allocator.Temp);
            var tangentV = new NativeArray<float3>(positions.Length, Allocator.Temp);

            for (int i = 0; i < meshIndices.Length; i+=3) 
            {
                int i0 = meshIndices[i + 0];
                int i1 = meshIndices[i + 1];
                int i2 = meshIndices[i + 2];

                var v1 = positions[i0];
                var v2 = positions[i1];
                var v3 = positions[i2];
        
                var w1 = uvs[i0];
                var w2 = uvs[i1];
                var w3 = uvs[i2];

                var edge1 = v2 - v1;
                var edge2 = v3 - v1;

                var uv1 = w2 - w1;
                var uv2 = w3 - w1;
        
                var r = 1.0f / (uv1.x * uv2.y - uv1.y * uv2.x);
                if (math.isnan(r) || math.isfinite(r))
                    r = 0.0f;

                var udir = new float3(
                    ((edge1.x * uv2.y) - (edge2.x * uv1.y)) * r,
                    ((edge1.y * uv2.y) - (edge2.y * uv1.y)) * r,
                    ((edge1.z * uv2.y) - (edge2.z * uv1.y)) * r
                );

                var vdir = new float3(
                    ((edge1.x * uv2.x) - (edge2.x * uv1.x)) * r,
                    ((edge1.y * uv2.x) - (edge2.y * uv1.x)) * r,
                    ((edge1.z * uv2.x) - (edge2.z * uv1.x)) * r
                );

                tangentU[i0] += udir;
                tangentU[i1] += udir;
                tangentU[i2] += udir;

                tangentV[i0] += vdir;
                tangentV[i1] += vdir;
                tangentV[i2] += vdir;
            }

            for (int i = 0; i < positions.Length; i++) 
            {
                var n	= normals[i];
                var t0	= tangentU[i];
                var t1	= tangentV[i];

                n = math.normalizesafe(n);
                var t = t0 - (n * math.dot(n, t0));
                t = math.normalizesafe(t);

                var c = math.cross(n, t0);
                float w = (math.dot(c, t1) < 0) ? 1.0f : -1.0f;
                tangents[i] = new float4(t.x, t.y, t.z, w);
            }
        }

        public void Execute()
        {
            // Would love to do this in parallel, since all slices are sequential, but yeah, can't.
            for (int index = 0; index < subMeshes.Length; index++)
            { 
                var currentBaseIndex    = subMeshes[index].baseIndex;
                var indexCount          = subMeshes[index].indexCount;
                var currentBaseVertex   = subMeshes[index].baseVertex;
                var vertexCount         = subMeshes[index].vertexCount;
            
                var surfacesOffset      = subMeshes[index].surfacesOffset;
                var surfacesCount       = subMeshes[index].surfacesCount;

                var generatedMeshIndicesSlice       = generatedMeshIndices      .Slice(currentBaseIndex, indexCount);
                var generatedMeshBrushIndicesSlice  = generatedMeshBrushIndices .Slice(currentBaseIndex / 3, indexCount / 3);
                var generatedMeshPositionsSlice     = generatedMeshPositions    .Slice(currentBaseVertex, vertexCount);
                var generatedMeshTangentsSlice      = generatedMeshTangents     .Slice(currentBaseVertex, vertexCount);
                var generatedMeshNormalsSlice       = generatedMeshNormals      .Slice(currentBaseVertex, vertexCount);
                var generatedMeshUV0Slice           = generatedMeshUV0          .Slice(currentBaseVertex, vertexCount);

                // double snap_size = 1.0 / ants.SnapDistance();

                { 
                    // copy all the vertices & indices to the sub-meshes for each material
                    for (int surfaceIndex = surfacesOffset, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, lastSurfaceIndex = surfacesCount + surfacesOffset;
                            surfaceIndex < lastSurfaceIndex;
                            ++surfaceIndex)
                    {
                        var subMeshSurface = subMeshSurfaces[surfaceIndex];
                        ref var sourceBuffer = ref subMeshSurface.brushRenderBuffer.Value.surfaces[subMeshSurface.surfaceIndex];
                        if (sourceBuffer.indices.Length == 0 ||
                            sourceBuffer.vertices.Length == 0)
                            continue;

                        var brushNodeID = subMeshSurface.brushNodeID;

                        for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i += 3)
                        {
                            generatedMeshBrushIndicesSlice[brushIDIndexOffset] = brushNodeID; brushIDIndexOffset++;
                        }

                        for (int i = 0, sourceIndexCount = sourceBuffer.indices.Length; i < sourceIndexCount; i ++)
                        {
                            generatedMeshIndicesSlice[indexOffset] = (int)(sourceBuffer.indices[i] + vertexOffset); indexOffset++;
                        }

                        var sourceVertexCount = sourceBuffer.vertices.Length;

                        generatedMeshPositionsSlice.CopyFrom(vertexOffset, ref sourceBuffer.vertices, 0, sourceVertexCount);

                        generatedMeshUV0Slice.CopyFrom(vertexOffset, ref sourceBuffer.uv0, 0, sourceVertexCount);
                        generatedMeshNormalsSlice.CopyFrom(vertexOffset, ref sourceBuffer.normals, 0, sourceVertexCount);
                        vertexOffset += sourceVertexCount;
                    }
                }

                ComputeTangents(generatedMeshIndicesSlice,
                                generatedMeshPositionsSlice,
                                generatedMeshUV0Slice,
                                generatedMeshNormalsSlice,
                                generatedMeshTangentsSlice);
            }
        }
    }
}