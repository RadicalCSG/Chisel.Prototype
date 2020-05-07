using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static class MeshExtensions
    {
        public static void CopyFromPositionOnly(this UnityEngine.Mesh mesh, GeneratedMeshContents contents)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");
            
            if (contents.description.vertexCount < 3 ||
                contents.description.indexCount < 3)
            {
                mesh.Clear();
                return;
            }
            
            mesh.SetVertices(contents.positions);

            mesh.SetTriangles(contents.indices.ToArray(), 0, false);
            mesh.bounds = contents.bounds; 
        }

        static readonly List<Vector3>   sPositionsList  = new List<Vector3>();
        static readonly List<Vector3>   sNormalsList    = new List<Vector3>();
        static readonly List<Vector4>   sTangentsList   = new List<Vector4>();
        static readonly List<Vector2>   sUV0List        = new List<Vector2>();
        static readonly List<int>       sBaseVertices   = new List<int>();
        public static void CopyFrom(this UnityEngine.Mesh mesh, List<GeneratedMeshContents> contents)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");

            if (contents.Count == 0)
            {
                mesh.Clear();
                return;
            }

            var bounds = new Bounds();

            sPositionsList  .Clear();
            sNormalsList    .Clear();
            sTangentsList   .Clear();
            sUV0List        .Clear();
            sBaseVertices   .Clear();
            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i] == null ||
                    contents[i].positions.Length == 0 ||
                    contents[i].indices.Length == 0)
                    continue;

                sBaseVertices.Add(sPositionsList.Count);
                sPositionsList.AddRange(contents[i].positions);
                if (contents[i].normals .IsCreated) sNormalsList .AddRange(contents[i].normals);
                if (contents[i].tangents.IsCreated) sTangentsList.AddRange(contents[i].tangents);
                if (contents[i].uv0     .IsCreated) sUV0List     .AddRange(contents[i].uv0);
                
                if (i == 0) bounds = contents[i].bounds;
                else bounds.Encapsulate(contents[i].bounds);
            }

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(sPositionsList);
            if (sNormalsList.Count  == sPositionsList.Count) mesh.SetNormals(sNormalsList);
            if (sTangentsList.Count == sPositionsList.Count) mesh.SetTangents(sTangentsList);
            if (sUV0List.Count      == sPositionsList.Count) mesh.SetUVs(0, sUV0List);

            mesh.subMeshCount = sBaseVertices.Count;
            for (int i = 0,n=0; i < contents.Count; i++)
            {
                if (contents[i] == null)
                    continue;
                var triangles       = contents[i].indices.ToArray();
                var submesh         = n;
                var calculateBounds = false;
                int baseVertex      = sBaseVertices[n];
                //mesh.SetSubMesh()
                mesh.SetTriangles(triangles: triangles, submesh: submesh, calculateBounds: calculateBounds, baseVertex: baseVertex);
                n++;
            }
            mesh.bounds = bounds; 
        }
    }
}