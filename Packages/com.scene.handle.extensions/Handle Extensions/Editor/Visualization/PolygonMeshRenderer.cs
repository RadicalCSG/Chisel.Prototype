﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace UnitySceneExtensions
{
    public sealed class PolygonMeshManager
    {
        sealed class TriangleMesh
        {
            public const int MaxVertexCount = 65000 - 3;
            public const int MaxIndexCount  = MaxVertexCount * 3;

            public int VertexCount { get { return vertexCount; } }

            public Vector3[]		vertices	= new Vector3[MaxVertexCount];
            public Color[]			colors		= new Color	 [MaxVertexCount];
            public int[]			indices		= new int    [MaxIndexCount];
            public int				vertexCount	= 0;
            public int				indexCount	= 0;
        
            internal Mesh mesh;

            public TriangleMesh() { Clear(); }
        
            public void Clear()
            {
                vertexCount	= 0;
                indexCount	= 0;
            }

            public void AddTriangles(Matrix4x4 matrix, Vector3[] triangleVertices, int[] triangleIndices, Color color)
            {
                if (triangleIndices.Length < 3 ||
                    triangleIndices.Length % 3 != 0)
                    return;
                
                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    int v = vertexCount;
                    for (int t = 0; t < triangleVertices.Length; t++, v++)
                    {
                        vertices[v] = triangleVertices[t];
                        colors  [v] = color;
                    }
                    vertexCount = v;
                } else
                {
                    int v = vertexCount;
                    for (int t = 0; t < triangleVertices.Length; t++, v++)
                    {
                        vertices[v] = matrix.MultiplyPoint(triangleVertices[t]);
                        colors  [v] = color;
                    }
                    vertexCount = v;
                }
                int i = indexCount;
                for (int t = 0; t < triangleIndices.Length; t++, i++)
                {
                    indices [i] = triangleIndices[t] + startIndex;
                }
                indexCount = i;
            }
        
            public void AddPolygon(Matrix4x4 matrix, Vector3[] polyVertices, int[] polyIndices, Color color)
            {
                if (polyIndices.Length < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyIndices.Length; i++)
                    {
                        vertices[vertexCount] = polyVertices[polyIndices[i]];
                        colors  [vertexCount] = color;
                        vertexCount++;
                    }
                } else
                {
                    for (int i = 0; i < polyIndices.Length; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[polyIndices[i]]);
                        colors  [vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyIndices.Length; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Matrix4x4 matrix, float3[] polyVertices, int[] polyIndices, Color color)
            {
                if (polyIndices.Length < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyIndices.Length; i++)
                    {
                        vertices[vertexCount] = polyVertices[polyIndices[i]];
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                else
                {
                    for (int i = 0; i < polyIndices.Length; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[polyIndices[i]]);
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyIndices.Length; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Matrix4x4 matrix, Vector3[] polyVertices, Color color)
            {
                if (polyVertices.Length < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyVertices.Length; i++)
                    {
                        vertices[vertexCount] = polyVertices[i];
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                } else
                {
                    for (int i = 0; i < polyVertices.Length; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[i]);
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyVertices.Length; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Matrix4x4 matrix, float3[] polyVertices, Color color)
            {
                if (polyVertices.Length < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyVertices.Length; i++)
                    {
                        vertices[vertexCount] = polyVertices[i];
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                } else
                {
                    for (int i = 0; i < polyVertices.Length; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[i]);
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyVertices.Length; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Matrix4x4 matrix, List<Vector3> polyVertices, Color color)
            {
                if (polyVertices.Count < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyVertices.Count; i++)
                    {
                        vertices[vertexCount] = polyVertices[i];
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                } else
                {
                    for (int i = 0; i < polyVertices.Count; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[i]);
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyVertices.Count; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Matrix4x4 matrix, List<float3> polyVertices, Color color)
            {
                if (polyVertices.Count < 3)
                    return;

                int startIndex = vertexCount;
                if (matrix.isIdentity)
                {
                    for (int i = 0; i < polyVertices.Count; i++)
                    {
                        vertices[vertexCount] = polyVertices[i];
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                } else
                {
                    for (int i = 0; i < polyVertices.Count; i++)
                    {
                        vertices[vertexCount] = matrix.MultiplyPoint(polyVertices[i]);
                        colors[vertexCount] = color;
                        vertexCount++;
                    }
                }
                for (int i = 2; i < polyVertices.Count; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }

            public void AddPolygon(Vector3[] polyVertices, int[] polyIndices, Color color)
            {
                if (polyIndices.Length < 3)
                    return;

                int startIndex = vertexCount;
                for (int i = 0; i < polyIndices.Length; i++)
                {
                    vertices[vertexCount] = polyVertices[polyIndices[i]];
                    colors  [vertexCount] = color;
                    vertexCount++;
                }
                for (int i = 2; i < polyIndices.Length; i++)
                {
                    indices[indexCount + 0] = startIndex + 0;
                    indices[indexCount + 1] = startIndex + i - 1;
                    indices[indexCount + 2] = startIndex + i;
                    indexCount += 3;
                }
            }


            public void CommitMesh()
            {
                if (vertexCount == 0)
                {
                    if (mesh != null && mesh.vertexCount != 0)
                    {
                        mesh.Clear(true);
                    }
                    return;
                }

                if (mesh)
                {
                    mesh.Clear(true);
                } else
                {
                    mesh = new Mesh();
                    mesh.MarkDynamic();
                }
                
                Vector3[]	newVertices;
                Color[]		newColors;
                int[]		newIndices;

                if (vertexCount == MaxVertexCount)
                {
                    newVertices	= vertices;
                    newColors	= colors;
                } else
                { 
                    newVertices = vertices.Take(vertexCount).ToArray();
                    newColors   = colors  .Take(vertexCount).ToArray();
                }

                if (indexCount == MaxIndexCount) newIndices = indices;
                else							 newIndices = indices.Take(indexCount).ToArray();
                
                mesh.vertices = newVertices;
                mesh.colors = newColors;
                mesh.SetIndices(newIndices, MeshTopology.Triangles, 0, calculateBounds: false);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
            }

            internal void Destroy()
            {
                if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
                vertices	= null;
                colors		= null;
                indices		= null;
                vertexCount = 0;
                indexCount  = 0;
            }
        }
        
        public void Begin()
        {
            if (triangleMeshes == null || triangleMeshes.Count == 0)
                return;
            
            currentTriangleMesh = 0;
            for (int i = 0; i < triangleMeshes.Count; i++) triangleMeshes[i].Clear();
        }

        public void End()
        {
            if (triangleMeshes == null || triangleMeshes.Count == 0)
                return;

            var max = Mathf.Min(currentTriangleMesh, triangleMeshes.Count);
            for (int i = 0; i <= max; i++)
                triangleMeshes[i].CommitMesh();
        }

        public void Render(Camera camera, Material polygonMaterial)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (triangleMeshes == null || triangleMeshes.Count == 0 || !polygonMaterial)
                return;

            var max = Mathf.Min(currentTriangleMesh, triangleMeshes.Count - 1);
            for (int i = 0; i <= max; i++)
            {
                var mesh = triangleMeshes[i].mesh;
                if (triangleMeshes[i].vertexCount == 0 || !mesh)
                    continue;
                Graphics.DrawMesh(mesh, Matrix4x4.identity, polygonMaterial, 0, camera, 0, null, false, false);
                Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            }
        }

        List<TriangleMesh> triangleMeshes = new List<TriangleMesh>();
        int currentTriangleMesh = 0;
        
        public PolygonMeshManager()
        {
            triangleMeshes.Add(new TriangleMesh());
        }
        
        public void DrawTriangles(Matrix4x4 matrix, Vector3[] vertices, int[] indices, Color color)
        {
            var triangleMeshIndex	= currentTriangleMesh;
            var triangleMesh		= triangleMeshes[currentTriangleMesh];
            
            if (triangleMesh.VertexCount + vertices.Length >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear(); 
            }

            triangleMesh.AddTriangles(matrix, vertices, indices, color);

            currentTriangleMesh = triangleMeshIndex;
        }
        
        public void DrawPolygon(Matrix4x4 matrix, List<Vector3> vertices, Color color)
        {
            var triangleMeshIndex = currentTriangleMesh;
            var triangleMesh = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + vertices.Count >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear();
            }

            triangleMesh.AddPolygon(matrix, vertices, color);

            currentTriangleMesh = triangleMeshIndex;
        }
        
        public void DrawPolygon(float4x4 matrix, List<float3> vertices, Color color)
        {
            var triangleMeshIndex = currentTriangleMesh;
            var triangleMesh = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + vertices.Count >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear();
            }

            triangleMesh.AddPolygon(matrix, vertices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void DrawPolygon(float4x4 matrix, float3[] vertices, Color color)
        {
            var triangleMeshIndex = currentTriangleMesh;
            var triangleMesh = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + vertices.Length >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear();
            }

            triangleMesh.AddPolygon(matrix, vertices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void DrawPolygon(Matrix4x4 matrix, Vector3[] vertices, Color color)
        {
            var triangleMeshIndex = currentTriangleMesh;
            var triangleMesh = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + vertices.Length >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear();
            }

            triangleMesh.AddPolygon(matrix, vertices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void DrawPolygon(Matrix4x4 matrix, Vector3[] vertices, int[] indices, Color color)
        {
            var triangleMeshIndex   = currentTriangleMesh;
            var triangleMesh        = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + ((indices.Length * 3) - 2) >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear(); 
            }

            triangleMesh.AddPolygon(matrix, vertices, indices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void DrawPolygon(float4x4 matrix, float3[] vertices, int[] indices, Color color)
        {
            var triangleMeshIndex   = currentTriangleMesh;
            var triangleMesh        = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + ((indices.Length * 3) - 2) >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
                triangleMesh.Clear(); 
            }

            triangleMesh.AddPolygon(matrix, vertices, indices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void DrawPolygon(Vector3[] vertices, int[] indices, Color color)
        {
            var triangleMeshIndex   = currentTriangleMesh;
            var triangleMesh        = triangleMeshes[currentTriangleMesh];

            if (triangleMesh.VertexCount + ((indices.Length * 3) - 2) >= TriangleMesh.MaxVertexCount)
            {
                currentTriangleMesh++;
                if (currentTriangleMesh >= triangleMeshes.Count)
                    triangleMeshes.Add(new TriangleMesh());
                triangleMesh = triangleMeshes[currentTriangleMesh];
            }

            triangleMesh.AddPolygon(vertices, indices, color);

            currentTriangleMesh = triangleMeshIndex;
        }

        public void Destroy()
        {
            for (int i = 0; i < triangleMeshes.Count; i++)
                triangleMeshes[i].Destroy();
            triangleMeshes.Clear();
            currentTriangleMesh = 0;
        }
        
        public void Clear()
        {
            currentTriangleMesh = 0;
            for (int i = 0; i < triangleMeshes.Count; i++) triangleMeshes[i].Clear();
        }
    }
}
