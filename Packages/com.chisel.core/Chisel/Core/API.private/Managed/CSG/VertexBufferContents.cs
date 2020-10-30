//#define RUN_IN_SERIAL
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    public struct VertexBufferContents
    {
        public NativeList<GeneratedMeshDescription> meshDescriptions;
        public NativeList<SubMeshSection>           subMeshSections;
        public NativeListArray<GeneratedSubMesh>    subMeshes;
        
        public NativeListArray<int> 	    indices;
        public NativeListArray<int> 	    triangleBrushIndices;
        public NativeListArray<float3>      positions;        
        public NativeListArray<float4>      tangents;
        public NativeListArray<float3>      normals;
        public NativeListArray<float2>      uv0;
        public NativeList<Mesh.MeshData>    meshes;

        public NativeArray<VertexAttributeDescriptor> renderDescriptors;
        public NativeArray<VertexAttributeDescriptor> colliderDescriptors;

        public void EnsureInitialized()
        {
            if (!meshDescriptions.IsCreated) meshDescriptions = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
            else meshDescriptions.Clear();
            if (!subMeshSections.IsCreated) subMeshSections = new NativeList<SubMeshSection>(Allocator.Persistent);
            else subMeshSections.Clear();
            if (!subMeshes           .IsCreated) subMeshes            = new NativeListArray<GeneratedSubMesh>(Allocator.Persistent);
            if (!meshes              .IsCreated) meshes               = new NativeList<Mesh.MeshData>(Allocator.Persistent);
            if (!indices             .IsCreated) indices              = new NativeListArray<int>(Allocator.Persistent);
            if (!triangleBrushIndices.IsCreated) triangleBrushIndices = new NativeListArray<int>(Allocator.Persistent);
            if (!positions           .IsCreated) positions            = new NativeListArray<float3>(Allocator.Persistent);
            if (!tangents            .IsCreated) tangents             = new NativeListArray<float4>(Allocator.Persistent);
            if (!normals             .IsCreated) normals              = new NativeListArray<float3>(Allocator.Persistent);
            if (!uv0                 .IsCreated) uv0                  = new NativeListArray<float2>(Allocator.Persistent);

            if (!renderDescriptors.IsCreated)
                renderDescriptors = new NativeArray<VertexAttributeDescriptor>(s_RenderDescriptors, Allocator.Persistent);
            if (!colliderDescriptors.IsCreated)
                colliderDescriptors = new NativeArray<VertexAttributeDescriptor>(s_ColliderDescriptors, Allocator.Persistent);
        }

        public void Clear()
        {
            if (meshDescriptions.IsCreated) meshDescriptions.Clear();
            if (subMeshSections.IsCreated) subMeshSections.Clear();
        }

        public JobHandle Dispose(JobHandle dependency) 
        {
            JobHandle lastJobHandle = default;
            if (meshDescriptions    .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshDescriptions.Dispose(dependency));
            if (subMeshSections     .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSections.Dispose(dependency));
            if (subMeshes           .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshes.Dispose(dependency));
            if (meshes              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshes.Dispose(dependency));
            if (indices             .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, indices.Dispose(dependency));
            if (triangleBrushIndices.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, triangleBrushIndices.Dispose(dependency));
            if (positions           .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, positions.Dispose(dependency));
            if (tangents            .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, tangents.Dispose(dependency));
            if (normals             .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, normals.Dispose(dependency));
            if (uv0                 .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, uv0.Dispose(dependency));

            if (renderDescriptors   .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, renderDescriptors  .Dispose(dependency));
            if (colliderDescriptors .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, colliderDescriptors.Dispose(dependency));
            
            meshDescriptions     = default;
            subMeshSections      = default;
            subMeshes            = default;
            meshes               = default;
            indices              = default;
            triangleBrushIndices = default;
            positions            = default;
            tangents             = default;
            normals              = default;
            uv0                  = default;
            renderDescriptors    = default;
            colliderDescriptors  = default;
            return lastJobHandle;
        }

        readonly static VertexAttributeDescriptor[] s_RenderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 1),
            new VertexAttributeDescriptor(VertexAttribute.Normal,    dimension: 3, stream: 2),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,   dimension: 4, stream: 3)
        };

        readonly static VertexAttributeDescriptor[] s_ColliderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0)
        };

        public bool IsEmpty(int contentsIndex)
        {
            var subMeshesArray  = this.subMeshes[contentsIndex].AsArray();
            var positionsArray  = this.positions[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();

            var vertexCount     = positionsArray.Length;
            var indexCount      = indicesArray.Length;

            return (subMeshesArray.Length == 0 || indexCount == 0 || vertexCount == 0);
        }
    };
    

    [BurstCompile(CompileSynchronously = true)]
    public struct CopyToRenderMeshJob : IJob
    {
        //Read
        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   renderDescriptors;
        [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>        subMeshes;
        [NoAlias, ReadOnly] public NativeListArray<int> 	                indices;
        [NoAlias, ReadOnly] public NativeListArray<float3>                  positions;
        [NoAlias, ReadOnly] public NativeListArray<float4>                  tangents;
        [NoAlias, ReadOnly] public NativeListArray<float3>                  normals;
        [NoAlias, ReadOnly] public NativeListArray<float2>                  uv0;
        [NoAlias, ReadOnly] public int contentsIndex;
        [NoAlias, ReadOnly] public int meshIndex;

        // Read (get meshData)/Write (write to meshData)
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<Mesh.MeshData> meshes;

        public void Execute()
        {
            var meshData = meshes[meshIndex];

            if (!this.subMeshes.IsIndexCreated(contentsIndex) ||
                !this.positions.IsIndexCreated(contentsIndex) ||
                !this.indices.IsIndexCreated(contentsIndex))
            {
                meshData.SetVertexBufferParams(0, renderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
                
            var subMeshesArray      = this.subMeshes[contentsIndex].AsArray();
            var positionsArray      = this.positions[contentsIndex].AsArray();
            var indicesArray        = this.indices[contentsIndex].AsArray();
            var normalsArray        = this.normals[contentsIndex].AsArray();
            var tangentsArray       = this.tangents[contentsIndex].AsArray();
            var uv0Array            = this.uv0[contentsIndex].AsArray();


            meshData.SetVertexBufferParams(positionsArray.Length, renderDescriptors);
            meshData.SetIndexBufferParams(indicesArray.Length, IndexFormat.UInt32);

            var dstPositions = meshData.GetVertexData<float3>(stream: 0);
            dstPositions.CopyFrom(positionsArray);
            
            var dstTexCoord0 = meshData.GetVertexData<float2>(stream: 1);
            dstTexCoord0.CopyFrom(uv0Array);

            var dstNormals = meshData.GetVertexData<float3>(stream: 2);
            dstNormals.CopyFrom(normalsArray);

            var dstTangents = meshData.GetVertexData<float4>(stream: 3);
            dstTangents.CopyFrom(tangentsArray);
                
            var dstIndices = meshData.GetIndexData<int>();
            dstIndices.CopyFrom(indicesArray);

            meshData.subMeshCount = subMeshesArray.Length;
            for (int i = 0; i < subMeshesArray.Length; i++)
            {
                var srcBounds   = subMeshesArray[i].bounds;
                var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
                var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
                var dstBounds   = new Bounds(center, size);
                meshData.SetSubMesh(i, new SubMeshDescriptor
                {
                    baseVertex  = subMeshesArray[i].baseVertex,
                    firstVertex = 0,
                    vertexCount = subMeshesArray[i].vertexCount,
                    indexStart  = subMeshesArray[i].baseIndex,
                    indexCount  = subMeshesArray[i].indexCount,
                    bounds      = dstBounds,
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct CopyToColliderMeshJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   colliderDescriptors;
        [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>        subMeshes;
        [NoAlias, ReadOnly] public NativeListArray<int> 	                indices;
        [NoAlias, ReadOnly] public NativeListArray<float3>                  positions;
        [NoAlias, ReadOnly] public int contentsIndex;
        [NoAlias, ReadOnly] public int meshIndex;

        // Read (get meshData)/Write (write to meshData)
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeArray<Mesh.MeshData> meshes;

        public void Execute()
        {
            var meshData = meshes[meshIndex];

            if (!this.subMeshes.IsIndexCreated(contentsIndex) ||
                !this.positions.IsIndexCreated(contentsIndex) ||
                !this.indices.IsIndexCreated(contentsIndex))
            {
                meshData.SetVertexBufferParams(0, colliderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
                
            var subMeshesArray  = this.subMeshes[contentsIndex].AsArray();
            var positionsArray  = this.positions[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();

            meshData.SetVertexBufferParams(positionsArray.Length, colliderDescriptors);
            meshData.SetIndexBufferParams(indicesArray.Length, IndexFormat.UInt32);

            var dstPositions = meshData.GetVertexData<float3>(stream: 0);
            dstPositions.CopyFrom(positionsArray);
                
            var dstIndices = meshData.GetIndexData<int>();
            dstIndices.CopyFrom(indicesArray);
                
            meshData.subMeshCount = subMeshesArray.Length;
            for (int i = 0; i < subMeshesArray.Length; i++)
            {
                var srcBounds   = subMeshesArray[i].bounds;
                var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
                var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
                var dstBounds   = new Bounds(center, size);

                meshData.SetSubMesh(i, new SubMeshDescriptor
                {
                    baseVertex  = subMeshesArray[i].baseVertex,
                    firstVertex = 0,
                    vertexCount = subMeshesArray[i].vertexCount,
                    indexStart  = subMeshesArray[i].baseIndex,
                    indexCount  = subMeshesArray[i].indexCount,
                    bounds      = dstBounds,
                    topology    = UnityEngine.MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds);
            }

            // TODO: Figure out why sometimes setting a mesh on a MeshCollider causes BakeMesh to be called by unity
            //       (in which case this would happen serially on the main thread, which would be slower than calling it here)
            //       And sometimes it's not called? (in which case calling BakeMesh here would be *slower*)
            //       Also, if we use Unity.Physics then this wouldn't make sense at all
            //Physics.BakeMesh(instanceID, false);
        }
    }
}