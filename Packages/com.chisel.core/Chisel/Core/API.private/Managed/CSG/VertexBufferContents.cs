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

namespace Chisel.Core
{
    public struct VertexBufferContents
    {
        public NativeList<GeneratedMeshDescription> meshDescriptions;
        public NativeList<SubMeshSection>           subMeshSections;
        public NativeListArray<GeneratedSubMesh>    subMeshes;

        public NativeListArray<int> 	indices;
        public NativeListArray<int> 	brushIndices;
        public NativeListArray<float3>  positions;        
        public NativeListArray<float4>  tangents;
        public NativeListArray<float3>  normals;
        public NativeListArray<float2>  uv0;

        [BurstCompile]
        struct CopyToMeshJob : IJob
        {
            [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>    subMeshes;
            [NoAlias, ReadOnly] public NativeListArray<int> 	            indices;
            [NoAlias, ReadOnly] public NativeListArray<float3>              positions;
            [NoAlias, ReadOnly] public NativeListArray<float4>              tangents;
            [NoAlias, ReadOnly] public NativeListArray<float3>              normals;
            [NoAlias, ReadOnly] public NativeListArray<float2>              uv0;
            [NoAlias, ReadOnly] public int contentsIndex;

            [NoAlias, WriteOnly] public Mesh.MeshData data;

            public void Execute()
            {
                var subMeshesArray      = this.subMeshes[contentsIndex].AsArray();
                var positionsArray      = this.positions[contentsIndex].AsArray();
                var indicesArray        = this.indices[contentsIndex].AsArray();
                var normalsArray        = this.normals[contentsIndex].AsArray();
                var tangentsArray       = this.tangents[contentsIndex].AsArray();
                var uv0Array            = this.uv0[contentsIndex].AsArray();

                var dstPositions = data.GetVertexData<float3>(stream: 0);
                dstPositions.CopyFrom(positionsArray);
            
                var dstTexCoord0 = data.GetVertexData<float2>(stream: 1);
                dstTexCoord0.CopyFrom(uv0Array);

                var dstNormals = data.GetVertexData<float3>(stream: 2);
                dstNormals.CopyFrom(normalsArray);

                var dstTangents = data.GetVertexData<float4>(stream: 3);
                dstTangents.CopyFrom(tangentsArray);
                
                var dstIndices = data.GetIndexData<int>();
                dstIndices.CopyFrom(indicesArray);
                
                data.subMeshCount = subMeshesArray.Length;
                for (int i = 0; i < subMeshesArray.Length; i++)
                {
                    var srcBounds   = subMeshesArray[i].bounds;
                    var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
                    var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
                    var dstBounds   = new Bounds(center, size);
                    data.SetSubMesh(i, new SubMeshDescriptor
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

        public bool IsEmpty(int contentsIndex)
        {
            var subMeshesArray  = this.subMeshes[contentsIndex].AsArray();
            var positionsArray  = this.positions[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();

            var vertexCount     = positionsArray.Length;
            var indexCount      = indicesArray.Length;

            return (subMeshesArray.Length == 0 || indexCount == 0 || vertexCount == 0);
        }

        readonly static VertexAttributeDescriptor[] s_FullDescriptors = new[] 
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 1),
            new VertexAttributeDescriptor(VertexAttribute.Normal,    dimension: 3, stream: 2),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,   dimension: 4, stream: 3) 
        };
        public bool CopyToMesh(Mesh.MeshDataArray dataArray, int contentsIndex, int meshIndex, ref JobHandle allJobs)
        {
            /*
            // TODO: store somewhere else
            var startIndex  = subMeshSections[contentsIndex].startIndex;
            var endIndex    = subMeshSections[contentsIndex].endIndex;
            const long kHashMagicValue = (long)1099511628211ul;
            UInt64 combinedGeometryHashValue = 0;
            UInt64 combinedSurfaceHashValue = 0;

            ref var meshDescriptions = ref vertexBufferContents.meshDescriptions;

            for (int i = startIndex; i < endIndex; i++)
            {
                var meshDescription = meshDescriptions[i];
                if (meshDescription.vertexCount < 3 ||
                    meshDescription.indexCount < 3)
                    continue;

                combinedGeometryHashValue   = (combinedGeometryHashValue ^ meshDescription.geometryHashValue) * kHashMagicValue;
                combinedSurfaceHashValue    = (combinedSurfaceHashValue  ^ meshDescription.surfaceHashValue) * kHashMagicValue;
            }

            if (geometryHashValue != combinedGeometryHashValue ||
                surfaceHashValue != combinedSurfaceHashValue)
            {

                    geometryHashValue = combinedGeometryHashValue;
                    surfaceHashValue = combinedSurfaceHashValue;
            
            */
            
            //var subMeshesArray      = this.subMeshes[contentsIndex].AsArray();
            var positionsArray      = this.positions[contentsIndex].AsArray();
            var indicesArray        = this.indices[contentsIndex].AsArray();

            //var vertexCount = positionsArray.Length;
            var indexCount  = indicesArray.Length;
             

            Profiler.BeginSample("Init");
            var data = dataArray[meshIndex];
            data.SetVertexBufferParams(positionsArray.Length, s_FullDescriptors);
            data.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            Profiler.EndSample();

            var copyToMeshJob = new CopyToMeshJob
            {
                subMeshes       = subMeshes,
                indices         = indices,
                positions       = positions,
                tangents        = tangents,
                normals         = normals,
                uv0             = uv0,
                contentsIndex   = contentsIndex,
                data            = data
            };
            var copyToMeshJobHandle = copyToMeshJob.Schedule();
            allJobs = JobHandle.CombineDependencies(allJobs, copyToMeshJobHandle);
            return true;
        }

        [BurstCompile]
        struct CopyToMeshColliderJob : IJob
        {
            [NoAlias, ReadOnly] public NativeListArray<GeneratedSubMesh>    subMeshes;
            [NoAlias, ReadOnly] public NativeListArray<int> 	            indices;
            [NoAlias, ReadOnly] public NativeListArray<float3>              positions;
            [NoAlias, ReadOnly] public int contentsIndex;
            [NoAlias, ReadOnly] public int instanceID;

            [NoAlias, WriteOnly] public Mesh.MeshData data;

            public void Execute()
            {
                if (!this.subMeshes.IsIndexCreated(contentsIndex) ||
                    !this.positions.IsIndexCreated(contentsIndex) ||
                    !this.indices.IsIndexCreated(contentsIndex))
                {
                    data.subMeshCount = 0;
                    return;
                }
                
                var subMeshesArray  = this.subMeshes[contentsIndex].AsArray();
                var positionsArray  = this.positions[contentsIndex].AsArray();
                var indicesArray    = this.indices[contentsIndex].AsArray();

                var dstPositions = data.GetVertexData<float3>(stream: 0);
                dstPositions.CopyFrom(positionsArray);
                
                var dstIndices = data.GetIndexData<int>();
                dstIndices.CopyFrom(indicesArray);
                
                data.subMeshCount = subMeshesArray.Length;
                for (int i = 0; i < subMeshesArray.Length; i++)
                {
                    var srcBounds   = subMeshesArray[i].bounds;
                    var center      = (Vector3)((srcBounds.Max + srcBounds.Min) * 0.5f);
                    var size        = (Vector3)(srcBounds.Max - srcBounds.Min);
                    var dstBounds   = new Bounds(center, size);

                    data.SetSubMesh(i, new SubMeshDescriptor
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

                Physics.BakeMesh(instanceID, false);
            }
        }

        readonly static VertexAttributeDescriptor[] s_PositionOnlyDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0)
        };
        public bool CopyPositionOnlyToMesh(Mesh.MeshDataArray dataArray, int contentsIndex, int meshIndex, int instanceID, ref JobHandle allJobs)
        {
            //if (geometryHashValue != meshDescription.geometryHashValue)
            //{
            //geometryHashValue = meshDescription.geometryHashValue;
            /*
            var positionsArray  = this.positions[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();
            if (positionsArray.Length == 0 ||
                indicesArray.Length == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true;
            }

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(positionsArray);
            mesh.SetIndices(indicesArray, 0, indices[contentsIndex].Length, MeshTopology.Triangles, 0, true);
            
            //mesh.SetIndexBufferParams(indices[contentsIndex].Length, UnityEngine.Rendering.IndexFormat.UInt32);
            //mesh.SetIndexBufferData(indicesArray, 0, 0, indices[contentsIndex].Length, MeshUpdateFlags.Default);
            mesh.RecalculateBounds();
            return true;
            
            //var subMeshesArray    = this.subMeshes[contentsIndex].AsArray();
            var positionsArray      = this.positions[contentsIndex].AsArray();
            var indicesArray        = this.indices[contentsIndex].AsArray();

            //var vertexCount = positionsArray.Length;
            var indexCount  = indicesArray.Length;
            */
            
            var positionsArray  = this.positions[contentsIndex].AsArray();
            var indicesArray    = this.indices[contentsIndex].AsArray();

            var indexCount = indicesArray.Length;

            Profiler.BeginSample("Init");
            var data = dataArray[meshIndex];
            data.SetVertexBufferParams(positionsArray.Length, s_PositionOnlyDescriptors);
            data.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            Profiler.EndSample();

            var copyToMeshJob = new CopyToMeshColliderJob
            {
                subMeshes       = subMeshes,
                indices         = indices,
                positions       = positions,
                contentsIndex   = contentsIndex,
                instanceID      = instanceID,
                data            = data
            };
            var copyToMeshJobHandle = copyToMeshJob.Schedule();
            allJobs = JobHandle.CombineDependencies(allJobs, copyToMeshJobHandle);
            return true;
        }
    };
}