using System;
using System.Collections.Generic;
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
        public int brushNodeID;
        public BlobAssetReference<ChiselBrushRenderBuffer> brushRenderBuffer;
    }

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct GenerateVertexBuffersJob : IJob
    {   
        [NoAlias, ReadOnly] public MeshQuery    meshQuery;
        [NoAlias, ReadOnly] public int		    surfaceIdentifier;

        [NoAlias, ReadOnly] public int		    submeshIndexCount;
        [NoAlias, ReadOnly] public int		    submeshVertexCount;

        [NoAlias, ReadOnly] public NativeArray<SubMeshSurface> submeshSurfaces;

        [NoAlias] public NativeArray<int>		generatedMeshIndices; 
        [NoAlias] public NativeArray<int>		generatedMeshBrushIndices; 
        [NoAlias] public NativeArray<float3>    generatedMeshPositions;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<float4>    generatedMeshTangents;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<float3>    generatedMeshNormals;
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<float2>    generatedMeshUV0; 
            
        static void ComputeTangents(NativeArray<int>        meshIndices,
                                    NativeArray<float3>	    positions,
                                    NativeArray<float2>	    uvs,
                                    NativeArray<float3>	    normals,
                                    NativeArray<float4>	    tangents) 
        {
            if (!meshIndices.IsCreated || !positions.IsCreated || !uvs.IsCreated || !tangents.IsCreated ||
                meshIndices.Length == 0 ||
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
            bool useTangents		= (meshQuery.UsedVertexChannels & VertexChannelFlags.Tangent) == VertexChannelFlags.Tangent;
            bool useNormals		    = (meshQuery.UsedVertexChannels & VertexChannelFlags.Normal ) == VertexChannelFlags.Normal;
            bool useUV0s			= (meshQuery.UsedVertexChannels & VertexChannelFlags.UV0    ) == VertexChannelFlags.UV0;
            bool needTempNormals	= useTangents && !useNormals;
            bool needTempUV0		= useTangents && !useUV0s;

            var normals	= needTempNormals ? new NativeArray<float3>(submeshVertexCount, Allocator.Temp) : generatedMeshNormals;
            var uv0s	= needTempUV0     ? new NativeArray<float2>(submeshVertexCount, Allocator.Temp) : generatedMeshUV0;

            // double snap_size = 1.0 / ants.SnapDistance();

            var dstVertices = (float3*)generatedMeshPositions.GetUnsafePtr();
            { 
                // copy all the vertices & indices to the sub-meshes for each material
                for (int surfaceIndex = 0, brushIDIndexOffset = 0, indexOffset = 0, vertexOffset = 0, surfaceCount = (int)submeshSurfaces.Length;
                        surfaceIndex < surfaceCount;
                        ++surfaceIndex)
                {
                    var subMeshSurface = submeshSurfaces[surfaceIndex];
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

                    var srcVertices = (float3*)sourceBuffer.vertices.GetUnsafePtr();
                    //fixed (float3* srcVertices = &sourceBuffer.vertices[0])
                    {
                        UnsafeUtility.MemCpy(dstVertices + vertexOffset, srcVertices, sourceVertexCount * UnsafeUtility.SizeOf<float3>());
                        //Array.Copy(sourceBuffer.vertices, 0, generatedMeshPositions, vertexOffset, sourceVertexCount);
                    }

                    if (useUV0s || needTempUV0)
                    {
                        var dstUV0 = (float2*)uv0s.GetUnsafePtr();
                        var srcUV0 = (float2*)sourceBuffer.uv0.GetUnsafePtr();
                        {
                            UnsafeUtility.MemCpy(dstUV0 + vertexOffset, srcUV0, sourceVertexCount * UnsafeUtility.SizeOf<float2>());
                        }
                    }
                    if (useNormals || needTempNormals)
                    {
                        var dstNormals = (float3*)normals.GetUnsafePtr();
                        var srcNormals = (float3*)sourceBuffer.normals.GetUnsafePtr();
                        {
                            UnsafeUtility.MemCpy(dstNormals + vertexOffset, srcNormals, sourceVertexCount * UnsafeUtility.SizeOf<float3>());
                        }
                    }
                    vertexOffset += sourceVertexCount;
                }
            }

            if (useTangents)
            {
                ComputeTangents(generatedMeshIndices,
                                generatedMeshPositions,
                                uv0s,
                                normals,
                                generatedMeshTangents);
            }
        }
    }
}