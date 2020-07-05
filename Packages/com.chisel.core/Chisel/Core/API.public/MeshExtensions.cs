using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Core
{
    public static class MeshExtensions
    {
        public static bool CopyFromPositionOnly(this UnityEngine.Mesh mesh, GeneratedMeshContents contents)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");
            
            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions.AsArray());
            mesh.SetIndexBufferParams(contents.indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(contents.indices.AsArray(), 0, 0, contents.indexCount, MeshUpdateFlags.Default);
            mesh.subMeshCount = 0;
            mesh.RecalculateBounds();
            return true;
        }

        public static bool CopyMeshFrom(this UnityEngine.Mesh mesh, GeneratedMeshContents contents, List<int> triangleBrushes)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");

            if (contents.subMeshes.Length == 0 ||
                contents.indexCount == 0 ||
                contents.vertexCount == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true; 
            }

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions.AsArray());
            if (contents.normals .IsCreated) mesh.SetNormals(contents.normals.AsArray());
            if (contents.tangents.IsCreated) mesh.SetTangents(contents.tangents.AsArray());
            if (contents.uv0     .IsCreated) mesh.SetUVs(0, contents.uv0.AsArray());

            triangleBrushes.AddRange(contents.brushIndices.AsArray());


            mesh.SetIndexBufferParams(contents.indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(contents.indices.AsArray(), 0, 0, contents.indexCount, MeshUpdateFlags.Default);

            mesh.subMeshCount = contents.subMeshes.Length;
            for (int i = 0; i < contents.subMeshes.Length; i++)
            {
                mesh.SetSubMesh(i, new SubMeshDescriptor
                {
                    baseVertex  = contents.subMeshes[i].baseVertex,
                    firstVertex = 0,
                    vertexCount = contents.subMeshes[i].vertexCount,
                    indexStart	= contents.subMeshes[i].baseIndex,
                    indexCount	= contents.subMeshes[i].indexCount,
                    bounds	    = new Bounds(),
                    topology	= MeshTopology.Triangles,
                }, MeshUpdateFlags.Default);
            }
            mesh.RecalculateBounds();
            //mesh.RecalculateNormals();
            //mesh.RecalculateTangents();
            return true;
        }
    }
}