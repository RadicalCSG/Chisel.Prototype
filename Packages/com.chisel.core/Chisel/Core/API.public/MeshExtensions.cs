using System;
using System.Collections.Generic;
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

        static readonly List<Vector3>   sPositionsList  = new List<Vector3>();
        static readonly List<Vector3>   sNormalsList    = new List<Vector3>();
        static readonly List<Vector4>   sTangentsList   = new List<Vector4>();
        static readonly List<Vector2>   sUV0List        = new List<Vector2>();
        static readonly List<int>       sBaseVertices   = new List<int>();
        
        public static bool CopyMeshFrom(this UnityEngine.Mesh mesh, List<GeneratedMeshContents> contents, List<int> triangleBrushes)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");

            if (contents.Count == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true; 
            }

            sPositionsList  .Clear();
            sNormalsList    .Clear();
            sTangentsList   .Clear();
            sUV0List        .Clear();
            sBaseVertices   .Clear();

            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i].positions.Length == 0 ||
                    contents[i].indices.Length == 0)
                    continue;

                sBaseVertices.Add(sPositionsList.Count);
                sPositionsList.AddRange(contents[i].positions);
                if (contents[i].normals .IsCreated) sNormalsList .AddRange(contents[i].normals);
                if (contents[i].tangents.IsCreated) sTangentsList.AddRange(contents[i].tangents);
                if (contents[i].uv0     .IsCreated) sUV0List     .AddRange(contents[i].uv0);
            }

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(sPositionsList);
            if (sNormalsList.Count  == sPositionsList.Count) mesh.SetNormals(sNormalsList);
            if (sTangentsList.Count == sPositionsList.Count) mesh.SetTangents(sTangentsList);
            if (sUV0List.Count      == sPositionsList.Count) mesh.SetUVs(0, sUV0List);

            mesh.subMeshCount = sBaseVertices.Count;
            for (int i = 0,n=0; i < contents.Count; i++)
            {
                if (contents[i].indices.Length == 0)
                    continue;
                triangleBrushes.AddRange(contents[i].brushIndices);
                var triangles       = contents[i].indices.ToArray();
                var submesh         = n;
                var calculateBounds = false;
                int baseVertex      = sBaseVertices[n];
                //mesh.SetSubMesh()
                mesh.SetTriangles(triangles: triangles, submesh: submesh, calculateBounds: calculateBounds, baseVertex: baseVertex);
                n++;
            }
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
            return true;
        }
    }
}