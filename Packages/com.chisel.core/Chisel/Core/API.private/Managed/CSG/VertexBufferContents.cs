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
    public struct RenderVertex
    {
        public float3 position;
        public float3 normal;
        public float4 tangent;
        public float2 uv0;
    }

    public struct VertexBufferContents
    {
        readonly static VertexAttributeDescriptor[] s_RenderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal,    dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,   dimension: 4, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 0),
        };

        readonly static VertexAttributeDescriptor[] s_ColliderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0)
        };

        public NativeList<GeneratedMeshDescription> meshDescriptions;
        public NativeList<SubMeshSection>           subMeshSections;
        public NativeListArray<GeneratedSubMesh>    subMeshes;
        
        public NativeListArray<int> 	        indices;
        public NativeListArray<int> 	        triangleBrushIndices;
        public NativeListArray<float3>          colliderVertices;
        public NativeListArray<RenderVertex>    renderVertices;
        public NativeList<Mesh.MeshData>        meshes;

        public NativeArray<VertexAttributeDescriptor> renderDescriptors;
        public NativeArray<VertexAttributeDescriptor> colliderDescriptors;

        public void EnsureInitialized()
        {
            if (!meshDescriptions.IsCreated) meshDescriptions   = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
            else meshDescriptions.Clear();
            if (!subMeshSections.IsCreated) subMeshSections     = new NativeList<SubMeshSection>(Allocator.Persistent);
            else subMeshSections.Clear();
            if (!subMeshes           .IsCreated) subMeshes            = new NativeListArray<GeneratedSubMesh>(Allocator.Persistent);
            if (!meshes              .IsCreated) meshes               = new NativeList<Mesh.MeshData>(Allocator.Persistent);
            if (!indices             .IsCreated) indices              = new NativeListArray<int>(Allocator.Persistent);
            if (!triangleBrushIndices.IsCreated) triangleBrushIndices = new NativeListArray<int>(Allocator.Persistent);
            if (!colliderVertices    .IsCreated) colliderVertices     = new NativeListArray<float3>(Allocator.Persistent);
            if (!renderVertices      .IsCreated) renderVertices       = new NativeListArray<RenderVertex>(Allocator.Persistent);

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
            if (colliderVertices    .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, colliderVertices.Dispose(dependency));
            if (renderVertices      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, renderVertices.Dispose(dependency));

            if (renderDescriptors   .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, renderDescriptors  .Dispose(dependency));
            if (colliderDescriptors .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, colliderDescriptors.Dispose(dependency));
            
            meshDescriptions     = default;
            subMeshSections      = default;
            subMeshes            = default;
            meshes               = default;
            indices              = default;
            triangleBrushIndices = default;
            colliderVertices     = default;
            renderVertices       = default;
            renderDescriptors    = default;
            colliderDescriptors  = default;
            return lastJobHandle;
        }
    };
}