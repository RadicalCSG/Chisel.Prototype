using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static class MeshExtensions
    {
        public static bool CopyFromPositionOnly(this UnityEngine.Mesh mesh, ref ulong geometryHashValue, GeneratedMeshContents contents)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");
            
            if (contents.description.vertexCount < 3 ||
                contents.description.indexCount < 3)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear(keepVertexLayout: true);
                return true;
            }

            if (geometryHashValue == contents.description.geometryHashValue)
                return false;

            geometryHashValue = contents.description.geometryHashValue;

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(contents.positions);
            mesh.SetTriangles(contents.indices.ToArray(), 0, false);
            mesh.bounds = contents.bounds;
            return true;
        }

        static readonly List<Vector3>   sPositionsList  = new List<Vector3>();
        static readonly List<Vector3>   sNormalsList    = new List<Vector3>();
        static readonly List<Vector4>   sTangentsList   = new List<Vector4>();
        static readonly List<Vector2>   sUV0List        = new List<Vector2>();
        static readonly List<int>       sBaseVertices   = new List<int>();
        
        public static bool CopyFrom(this UnityEngine.Mesh mesh, ref ulong geometryHashValue, ref ulong surfaceHashValue, List<GeneratedMeshContents> contents, List<int> triangleBrushes)
        { 
            if (object.ReferenceEquals(contents, null))
                throw new ArgumentNullException("contents");

            if (contents.Count == 0)
            {
                if (mesh.vertexCount == 0)
                    return false;
                mesh.Clear();
                return true; 
            }

            var bounds = new Bounds();

            sPositionsList  .Clear();
            sNormalsList    .Clear();
            sTangentsList   .Clear();
            sUV0List        .Clear();
            sBaseVertices   .Clear();

            const long kHashMagicValue = (long)1099511628211ul;
            UInt64 combinedGeometryHashValue = 0;
            UInt64 combinedSurfaceHashValue = 0;

            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i] == null ||
                    contents[i].positions.Length == 0 ||
                    contents[i].indices.Length == 0)
                    continue;

                combinedGeometryHashValue   = (combinedGeometryHashValue ^ contents[i].description.geometryHashValue) * kHashMagicValue;
                combinedSurfaceHashValue    = (combinedSurfaceHashValue ^ contents[i].description.surfaceHashValue) * kHashMagicValue;

                sBaseVertices.Add(sPositionsList.Count);
                sPositionsList.AddRange(contents[i].positions);
                if (contents[i].normals .IsCreated) sNormalsList .AddRange(contents[i].normals);
                if (contents[i].tangents.IsCreated) sTangentsList.AddRange(contents[i].tangents);
                if (contents[i].uv0     .IsCreated) sUV0List     .AddRange(contents[i].uv0);
                
                if (i == 0) bounds = contents[i].bounds;
                else bounds.Encapsulate(contents[i].bounds);
            }

            if (geometryHashValue == combinedGeometryHashValue &&
                surfaceHashValue == combinedSurfaceHashValue)
                return false;

            geometryHashValue = combinedGeometryHashValue;
            surfaceHashValue = combinedSurfaceHashValue;

            mesh.Clear(keepVertexLayout: true);
            mesh.SetVertices(sPositionsList);
            if (sNormalsList.Count  == sPositionsList.Count) mesh.SetNormals(sNormalsList);
            if (sTangentsList.Count == sPositionsList.Count) mesh.SetTangents(sTangentsList);
            if (sUV0List.Count      == sPositionsList.Count) mesh.SetUVs(0, sUV0List);

            mesh.subMeshCount = sBaseVertices.Count;
            for (int i = 0,n=0; i < contents.Count; i++)
            {
                if (contents[i] == null ||
                    contents[i].indices.Length == 0)
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
            mesh.bounds = bounds;
            return true;
        }
    }
}