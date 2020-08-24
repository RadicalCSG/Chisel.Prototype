//#define RUN_IN_SERIAL
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;

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

        public bool CopyToMesh(int contentsIndex, UnityEngine.Mesh mesh, List<UnityEngine.Material> materials, List<int> triangleBrushes)
        {
            var subMeshesArray      = this.subMeshes[contentsIndex].AsArray();
            var positionsArray      = this.positions[contentsIndex].AsArray();
            var indicesArray        = this.indices[contentsIndex].AsArray();
            var brushIndicesArray   = this.brushIndices[contentsIndex].AsArray();
            var normalsArray        = this.normals[contentsIndex].AsArray();
            var tangentsArray       = this.tangents[contentsIndex].AsArray();
            var uv0Array            = this.uv0[contentsIndex].AsArray();

            var vertexCount = positionsArray.Length;
            var indexCount = indicesArray.Length;

            if (subMeshesArray.Length == 0 ||
                indexCount == 0 ||
                vertexCount == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true; 
            }
             
            // TODO: store somewhere else
            var startIndex  = subMeshSections[contentsIndex].startIndex;
            var endIndex    = subMeshSections[contentsIndex].endIndex;

            /*
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

            Profiler.BeginSample("Collect Materials");
            var desiredCapacity = materials.Count + (endIndex - startIndex);
            if (materials.Capacity < desiredCapacity)
                materials.Capacity = desiredCapacity;
            for (int i = startIndex; i < endIndex; i++)
            {
                var meshDescription = meshDescriptions[i];
                var renderMaterial = ChiselBrushMaterialManager.GetRenderMaterialByInstanceID(meshDescription.surfaceParameter);

                materials.Add(renderMaterial);
            }
            Profiler.EndSample();
            
            mesh.Clear(keepVertexLayout: true);
            Profiler.BeginSample("SetVertices");
            mesh.SetVertices(positionsArray);
            mesh.SetNormals(normalsArray);
            mesh.SetTangents(tangentsArray);
            mesh.SetUVs(0, uv0Array);
            Profiler.EndSample();

            Profiler.BeginSample("SetTriangleBrushes");
            if (triangleBrushes.Capacity < brushIndicesArray.Length)
                triangleBrushes.Capacity = brushIndicesArray.Length;
            triangleBrushes.Clear();
            for (int i = 0; i < brushIndicesArray.Length; i++)
                triangleBrushes.Add(brushIndicesArray[i]);
            Profiler.EndSample();

            Profiler.BeginSample("SetIndexBuffer");
            mesh.SetIndexBufferParams(indexCount, UnityEngine.Rendering.IndexFormat.UInt32);
            mesh.SetIndexBufferData(indicesArray, 0, 0, indexCount, UnityEngine.Rendering.MeshUpdateFlags.Default);
            Profiler.EndSample();

            mesh.subMeshCount = subMeshesArray.Length;
            Profiler.BeginSample("SetSubMesh");
            for (int i = 0; i < subMeshesArray.Length; i++)
            {
                mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor
                {
                    baseVertex  = subMeshesArray[i].baseVertex,
                    firstVertex = 0,
                    vertexCount = subMeshesArray[i].vertexCount,
                    indexStart	= subMeshesArray[i].baseIndex,
                    indexCount	= subMeshesArray[i].indexCount,
                    bounds	    = new UnityEngine.Bounds(),
                    topology	= UnityEngine.MeshTopology.Triangles,
                }, UnityEngine.Rendering.MeshUpdateFlags.Default);
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("Recalculate");
            mesh.RecalculateBounds();
            Profiler.EndSample();
            return true;
        }

        public bool CopyPositionOnlyToMesh(int contentsIndex, UnityEngine.Mesh mesh)
        {
            //if (geometryHashValue != meshDescription.geometryHashValue)
            //{
                //geometryHashValue = meshDescription.geometryHashValue;

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
            mesh.SetIndexBufferParams(indices[contentsIndex].Length, UnityEngine.Rendering.IndexFormat.UInt32);
            mesh.SetIndexBufferData(indicesArray, 0, 0, indices[contentsIndex].Length, UnityEngine.Rendering.MeshUpdateFlags.Default);
            mesh.subMeshCount = 0;
            mesh.RecalculateBounds();
            return true;
        }
    };
}