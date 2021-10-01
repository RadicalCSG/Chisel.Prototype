//#define RUN_IN_SERIAL
using System;
using System.Collections.Generic;
using Unity.Jobs;
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

    public struct SubMeshSection
    {
        public MeshQuery meshQuery;
        public int startIndex;
        public int endIndex;
        public int totalVertexCount;
        public int totalIndexCount;
    }

    public struct VertexBufferContents
    {
        public readonly static VertexAttributeDescriptor[] s_RenderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal,    dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,   dimension: 4, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 0),
        };

        public readonly static VertexAttributeDescriptor[] s_ColliderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0)
        };

        public NativeList<GeneratedMeshDescription> meshDescriptions;
        public NativeList<SubMeshSection>           subMeshSections;

        public NativeList<UnsafeList<CompactNodeID>>    triangleBrushIndices;
        public NativeList<Mesh.MeshData>                meshes;

        public NativeArray<VertexAttributeDescriptor> renderDescriptors;
        public NativeArray<VertexAttributeDescriptor> colliderDescriptors;

        public void EnsureInitialized()
        {
            if (!meshDescriptions.IsCreated) meshDescriptions   = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
            else meshDescriptions.Clear();
            if (!subMeshSections.IsCreated) subMeshSections     = new NativeList<SubMeshSection>(Allocator.Persistent);
            else subMeshSections.Clear();
            if (!meshes              .IsCreated) meshes               = new NativeList<Mesh.MeshData>(Allocator.Persistent);
            if (!triangleBrushIndices.IsCreated) triangleBrushIndices = new NativeList<UnsafeList<CompactNodeID>>(Allocator.Persistent);

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

        public bool IsCreated
        {
            get
            {
                return meshDescriptions.IsCreated &&
                        subMeshSections.IsCreated &&
                        triangleBrushIndices.IsCreated &&
                        meshes.IsCreated &&
                        renderDescriptors.IsCreated &&
                        colliderDescriptors.IsCreated;
            }
        }

        public JobHandle Dispose(JobHandle dependency) 
        {
            JobHandle lastJobHandle = default;
            if (meshDescriptions    .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshDescriptions.Dispose(dependency));
            if (subMeshSections     .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSections.Dispose(dependency));
            if (meshes              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshes.Dispose(dependency));
            if (triangleBrushIndices.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, NativeCollection.DisposeDeep(triangleBrushIndices, dependency));

            if (renderDescriptors   .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, renderDescriptors  .Dispose(dependency));
            if (colliderDescriptors .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, colliderDescriptors.Dispose(dependency));
            
            meshDescriptions     = default;
            subMeshSections      = default;
            meshes               = default;
            triangleBrushIndices = default;
            renderDescriptors    = default;
            colliderDescriptors  = default;
            return lastJobHandle;
        }
    };
}