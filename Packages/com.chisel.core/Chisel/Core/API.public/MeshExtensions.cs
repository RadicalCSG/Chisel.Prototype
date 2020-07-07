using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Chisel.Core
{
    public static class MeshExtensions
    {
        public static bool CopyFromPositionOnly(this UnityEngine.Mesh mesh, ref VertexBufferContents contents, int contentsIndex)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");
            
            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions[contentsIndex].AsArray());
            mesh.SetIndexBufferParams(contents.indices[contentsIndex].Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(contents.indices[contentsIndex].AsArray(), 0, 0, contents.indices[contentsIndex].Length, MeshUpdateFlags.Default);
            mesh.subMeshCount = 0;
            mesh.RecalculateBounds();
            return true;
        }

        public static bool CopyMeshFrom(this UnityEngine.Mesh mesh, ref VertexBufferContents contents, int contentsIndex, List<int> triangleBrushes)
        {
            var subMeshes       = contents.subMeshes[contentsIndex].AsArray();
            var positions       = contents.positions[contentsIndex].AsArray();
            var indices         = contents.indices[contentsIndex].AsArray();
            var brushIndices    = contents.brushIndices[contentsIndex].AsArray();
            var normals         = contents.normals[contentsIndex].AsArray();
            var tangents        = contents.tangents[contentsIndex].AsArray();
            var uv0             = contents.uv0[contentsIndex].AsArray();

            var vertexCount = positions.Length;
            var indexCount = indices.Length;

            if (subMeshes.Length == 0 ||
                indexCount == 0 ||
                vertexCount == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true; 
            }

            mesh.Clear(keepVertexLayout: true);
            Profiler.BeginSample("SetVertices");
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv0);
            Profiler.EndSample();

            Profiler.BeginSample("SetTriangleBrushes");
            triangleBrushes.AddRange(brushIndices);
            Profiler.EndSample();

            Profiler.BeginSample("SetIndexBuffer");
            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indexCount, MeshUpdateFlags.Default);
            Profiler.EndSample();

            mesh.subMeshCount = subMeshes.Length;
            Profiler.BeginSample("SetSubMesh");
            for (int i = 0; i < subMeshes.Length; i++)
            {
                mesh.SetSubMesh(i, new SubMeshDescriptor
                {
                    baseVertex  = subMeshes[i].baseVertex,
                    firstVertex = 0,
                    vertexCount = subMeshes[i].vertexCount,
                    indexStart	= subMeshes[i].baseIndex,
                    indexCount	= subMeshes[i].indexCount,
                    bounds	    = new Bounds(),
                    topology	= MeshTopology.Triangles,
                }, MeshUpdateFlags.Default);
            }
            Profiler.EndSample();
            
            Profiler.BeginSample("Recalculate");
            mesh.RecalculateBounds();
            Profiler.EndSample();
            return true;
        }
    }
}