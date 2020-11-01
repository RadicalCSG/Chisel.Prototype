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


        public static JobHandle UpdateMeshes(NativeList<Mesh.MeshData> meshDatas, 
                                             ref VertexBufferContents vertexBufferContents, 
                                             NativeList<ChiselMeshUpdate> colliderMeshUpdates,
                                             NativeList<ChiselMeshUpdate> debugHelperMeshes,
                                             NativeList<ChiselMeshUpdate> renderMeshes,
                                             JobHandle dependencies)
        {
            var assignMeshesJob = new AssignMeshesJob
            {
                // Read
                meshDescriptions    = vertexBufferContents.meshDescriptions,
                subMeshSections     = vertexBufferContents.subMeshSections,
                meshDatas           = meshDatas,

                // Write
                meshes              = vertexBufferContents.meshes,
                colliderMeshUpdates = colliderMeshUpdates,
                debugHelperMeshes   = debugHelperMeshes,
                renderMeshes        = renderMeshes,
            };  
            var assignMeshesJobHandle = assignMeshesJob.Schedule(dependencies);
            dependencies = JobHandle.CombineDependencies(assignMeshesJobHandle, dependencies);
            

            // TODO: - find a way to keep the list of used physicMaterials in each particular model
            //       - keep a list of meshes around, one for each physicMaterial
            //       - the number of meshes is now fixed as long as no physicMaterial is added/removed
            //       - the number of meshColliders could be the same size, just some meshColliders enabled/disabled
            //       - our number of meshes (colliders + renderers) is now predictable
            //
            // PROBLEM: Still wouldn't know in advance _which_ of these meshes would actually not change at all ...
            //          ... and don't want to change ALL of them, ALL the time. 
            //          So the mesh count would still be an unknown until we do a Complete

            var currentJobHandle = (JobHandle)assignMeshesJobHandle;

            // Start jobs to copy mesh data from our generated meshes to unity meshes

            Profiler.BeginSample("Renderers.ScheduleMeshCopy");
            { 
                var copyToMeshJob = new CopyToRenderMeshJob
                {
                    // Read
                    renderMeshes        = renderMeshes,
                    renderDescriptors   = vertexBufferContents.renderDescriptors,
                    subMeshes           = vertexBufferContents.subMeshes,
                    indices             = vertexBufferContents.indices,
                    vertices            = vertexBufferContents.renderVertices,
                
                    // Read/Write
                    meshes = vertexBufferContents.meshes,
                };
                var copyToMeshJobHandle = copyToMeshJob.Schedule(renderMeshes, 1, dependencies);
                currentJobHandle = JobHandle.CombineDependencies(currentJobHandle, copyToMeshJobHandle);
            }

            { 
                var copyToMeshJob = new CopyToRenderMeshJob
                {
                    // Read
                    renderMeshes        = debugHelperMeshes,
                    renderDescriptors   = vertexBufferContents.renderDescriptors,
                    subMeshes           = vertexBufferContents.subMeshes,
                    indices             = vertexBufferContents.indices,
                    vertices            = vertexBufferContents.renderVertices,

                    // Read/Write
                    meshes = vertexBufferContents.meshes,
                };
                var copyToMeshJobHandle = copyToMeshJob.Schedule(debugHelperMeshes, 1, dependencies);
                currentJobHandle = JobHandle.CombineDependencies(currentJobHandle, copyToMeshJobHandle);
            }
            Profiler.EndSample();

            Profiler.BeginSample("Colliders.ScheduleMeshCopy");
            {    
                var copyToMeshJob = new CopyToColliderMeshJob
                {
                    // Read
                    colliderMeshes      = colliderMeshUpdates,
                    colliderDescriptors = vertexBufferContents.colliderDescriptors,
                    subMeshes           = vertexBufferContents.subMeshes,
                    indices             = vertexBufferContents.indices,
                    vertices            = vertexBufferContents.colliderVertices,

                    // Read/Write
                    meshes = vertexBufferContents.meshes,
                };
                var copyToMeshJobHandle = copyToMeshJob.Schedule(colliderMeshUpdates, 16, dependencies);
                currentJobHandle = JobHandle.CombineDependencies(currentJobHandle, copyToMeshJobHandle);
            }
            Profiler.EndSample();

            return currentJobHandle;
        }
    };


    public struct ChiselMeshUpdate
    {
        public int contentsIndex;
        public int meshIndex;
        public int objectIndex;
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public struct AssignMeshesJob : IJob
    {
        public const int kDebugHelperCount = 6;
        public struct DebugRenderFlags { public LayerUsageFlags Item1; public LayerUsageFlags Item2; };
        public static readonly DebugRenderFlags[] kGeneratedDebugRendererFlags = new DebugRenderFlags[kDebugHelperCount]
        {
            new DebugRenderFlags{ Item1 = LayerUsageFlags.None                  , Item2 = LayerUsageFlags.Renderable },              // is explicitly set to "not visible"
            new DebugRenderFlags{ Item1 = LayerUsageFlags.RenderCastShadows     , Item2 = LayerUsageFlags.RenderCastShadows },       // casts Shadows and is renderered
            new DebugRenderFlags{ Item1 = LayerUsageFlags.CastShadows           , Item2 = LayerUsageFlags.RenderCastShadows },       // casts Shadows and is NOT renderered (shadowOnly)
            new DebugRenderFlags{ Item1 = LayerUsageFlags.RenderReceiveShadows  , Item2 = LayerUsageFlags.RenderReceiveShadows },    // any surface that receives shadows (must be rendered)
            new DebugRenderFlags{ Item1 = LayerUsageFlags.Collidable            , Item2 = LayerUsageFlags.Collidable },              // collider surfaces
            new DebugRenderFlags{ Item1 = LayerUsageFlags.Culled                , Item2 = LayerUsageFlags.Culled }                   // all surfaces removed by the CSG algorithm
        };

        [NoAlias, ReadOnly] public NativeList<GeneratedMeshDescription> meshDescriptions;
        [NoAlias, ReadOnly] public NativeList<SubMeshSection>           subMeshSections;
        [NoAlias, ReadOnly] public NativeList<Mesh.MeshData>            meshDatas;

        [NoAlias, WriteOnly] public NativeList<Mesh.MeshData>        meshes;
        [NoAlias, WriteOnly] public NativeList<ChiselMeshUpdate>     debugHelperMeshes;
        [NoAlias, WriteOnly] public NativeList<ChiselMeshUpdate>     renderMeshes;

        [NoAlias] public NativeList<ChiselMeshUpdate> colliderMeshUpdates;


        [BurstDiscard]
        public static void InvalidQuery(LayerUsageFlags query, LayerUsageFlags mask)
        {
            Debug.Assert(false, $"Invalid helper query used (query: {query}, mask: {mask})");

        }

        public void Execute() 
        {
            int meshIndex = 0;
            int colliderCount = 0;
            if (meshDescriptions.IsCreated)
            {
                for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.None)
                    {
                        int helperIndex = -1;
                        var query   = subMeshSection.meshQuery.LayerQuery;
                        var mask    = subMeshSection.meshQuery.LayerQueryMask;
                        for (int f = 0; f < kGeneratedDebugRendererFlags.Length; f++)
                        {
                            if (kGeneratedDebugRendererFlags[f].Item1 != query ||
                                kGeneratedDebugRendererFlags[f].Item2 != mask)
                                continue;

                            helperIndex = f;
                            break;
                        }
                        if (helperIndex == -1)
                        {
                            InvalidQuery(query, mask);
                            continue;
                        }

                        meshes.Add(meshDatas[meshIndex]);
                        debugHelperMeshes.Add(new ChiselMeshUpdate
                        {
                            contentsIndex       = i,
                            meshIndex           = meshIndex,
                            objectIndex         = helperIndex
                        });
                        meshIndex++; 
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.RenderMaterial)
                    {
                        var renderIndex = (int)(subMeshSection.meshQuery.LayerQuery & LayerUsageFlags.RenderReceiveCastShadows);
                        meshes.Add(meshDatas[meshIndex]);
                        renderMeshes.Add(new ChiselMeshUpdate
                        {
                            contentsIndex       = i,
                            meshIndex           = meshIndex,
                            objectIndex         = renderIndex
                        });
                        meshIndex++;
                    } else
                    if (subMeshSection.meshQuery.LayerParameterIndex == LayerParameterIndex.PhysicsMaterial)
                        colliderCount++;
                }
            }

            if (colliderMeshUpdates.Capacity < colliderCount)
                colliderMeshUpdates.Capacity = colliderCount;
            var colliderIndex = 0;
            if (meshDescriptions.IsCreated)
            {
                for (int i = 0; i < subMeshSections.Length; i++)
                {
                    var subMeshSection = subMeshSections[i];
                    if (subMeshSection.meshQuery.LayerParameterIndex != LayerParameterIndex.PhysicsMaterial)
                        continue;

                    var surfaceParameter = meshDescriptions[subMeshSection.startIndex].surfaceParameter;

                    meshes.Add(meshDatas[meshIndex]);
                    colliderMeshUpdates.Add(new ChiselMeshUpdate
                    {
                        contentsIndex   = colliderIndex,
                        meshIndex       = meshIndex,
                        objectIndex     = surfaceParameter
                    }); 
                    colliderIndex++;
                    meshIndex++;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct CopyToRenderMeshJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate>             renderMeshes;
        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   renderDescriptors;
        [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>        subMeshes;
        [NoAlias, ReadOnly] public NativeListArray<int> 	                indices;
        [NoAlias, ReadOnly] public NativeListArray<RenderVertex>            vertices;

        // Read (get meshData)/Write (write to meshData)
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeList<Mesh.MeshData> meshes;

        public void Execute(int renderIndex)
        {
            var update          = renderMeshes[renderIndex];
            var contentsIndex   = update.contentsIndex;
            var meshIndex       = update.meshIndex;
            var meshData        = meshes[meshIndex];

            if (!this.subMeshes.IsIndexCreated(contentsIndex) ||
                !this.vertices.IsIndexCreated(contentsIndex) ||
                !this.indices.IsIndexCreated(contentsIndex))
            {
                meshData.SetVertexBufferParams(0, renderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
                
            var subMeshesArray      = this.subMeshes[contentsIndex].AsArray();
            var verticesArray       = this.vertices[contentsIndex].AsArray();
            var indicesArray        = this.indices[contentsIndex].AsArray();


            meshData.SetVertexBufferParams(verticesArray.Length, renderDescriptors);
            meshData.SetIndexBufferParams(indicesArray.Length, IndexFormat.UInt32);

            var dstVertices = meshData.GetVertexData<RenderVertex>(stream: 0);
            dstVertices.CopyFrom(verticesArray);
            
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
    public struct CopyToColliderMeshJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeList<ChiselMeshUpdate>             colliderMeshes;
        [NoAlias, ReadOnly] public NativeArray<VertexAttributeDescriptor>   colliderDescriptors;
        [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>        subMeshes;
        [NoAlias, ReadOnly] public NativeListArray<int> 	                indices;
        [NoAlias, ReadOnly] public NativeListArray<float3>                  vertices;
        [NoAlias, ReadOnly] public int contentsIndex;
        [NoAlias, ReadOnly] public int meshIndex;

        // Read (get meshData)/Write (write to meshData)
        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public NativeList<Mesh.MeshData> meshes;
        
        public void Execute(int colliderIndex)
        {
            var update          = colliderMeshes[colliderIndex];
            var contentsIndex   = update.contentsIndex;
            var meshIndex       = update.meshIndex;
            var meshData        = meshes[meshIndex];

            if (!this.subMeshes.IsIndexCreated(contentsIndex) ||
                !this.vertices.IsIndexCreated(contentsIndex) ||
                !this.indices.IsIndexCreated(contentsIndex))
            {
                meshData.SetVertexBufferParams(0, colliderDescriptors);
                meshData.SetIndexBufferParams(0, IndexFormat.UInt32);
                meshData.subMeshCount = 0;
                return;
            }
                
            var subMeshesArray  = this.subMeshes[contentsIndex].AsArray();
            var verticesArray   = this.vertices[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();

            meshData.SetVertexBufferParams(verticesArray.Length, colliderDescriptors);
            meshData.SetIndexBufferParams(indicesArray.Length, IndexFormat.UInt32);

            var dstVertices = meshData.GetVertexData<float3>(stream: 0);
            dstVertices.CopyFrom(verticesArray);
                
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