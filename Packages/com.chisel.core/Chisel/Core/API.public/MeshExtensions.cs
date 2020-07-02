using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static class MeshExtensions
    {
        public static bool CopyFromPositionOnly(this UnityEngine.Mesh mesh, GeneratedMeshContents contents)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");
            
            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions);
            mesh.SetTriangles(contents.indices.ToArray(), 0, false);
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
            /*
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
            */
            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions);
            if (contents.normals .IsCreated) mesh.SetNormals(contents.normals);
            if (contents.tangents.IsCreated) mesh.SetTangents(contents.tangents);
            if (contents.uv0     .IsCreated) mesh.SetUVs(0, contents.uv0);

            triangleBrushes.AddRange(contents.brushIndices);

            mesh.subMeshCount = contents.subMeshes.Length;
            for (int i = 0,n=0; i < contents.subMeshes.Length; i++)
            {
                if (contents.subMeshes[i].indexCount == 0)
                    continue;
                
                //triangleBrushes.AddRange(contents[i].brushIndices);
                var triangles       = contents.indices.Slice(contents.subMeshes[i].baseIndex, contents.subMeshes[i].indexCount).ToArray();
                var submesh         = n;
                var calculateBounds = false;
                int baseVertex      = contents.subMeshes[i].baseVertex;//sBaseVertices[n];
                //mesh.SetSubMesh()
                mesh.SetTriangles(triangles: triangles, submesh: submesh, calculateBounds: calculateBounds, baseVertex: baseVertex);
                n++;
            }
            mesh.RecalculateBounds();
            return true;
        }
    }
}