using System.Collections.Generic;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed class PointMeshManager
    {
        sealed class PointMesh
        {
            public const int MaxVertexCount		= 65000 - 4;
            public const int MaxPointIndexCount = MaxVertexCount / 4 * 6;
            public const int MaxLineIndexCount	= MaxVertexCount / 4 * 8; 

            public int VertexCount { get { return vertexCount; } }

            public Vector3[]	vertices		= new Vector3[MaxVertexCount];

            public int[]		pointIndices	= new int    [MaxPointIndexCount];
            public Color[]		pointColors		= new Color  [MaxVertexCount];

            public int[]		lineIndices		= new int    [MaxLineIndexCount];
            public Color[]		lineColors		= new Color  [MaxVertexCount];
            
            public int vertexCount = 0;
            public int pointIndexCount = 0;
            public int lineIndexCount = 0;

            internal Mesh pointMesh;
            internal Mesh lineMesh;
        
            public void Clear()
            {
                vertexCount = 0;
                pointIndexCount = 0;
                lineIndexCount = 0;
            }
            
            public void CommitMesh()
            {
                if (vertexCount == 0)
                {
                    if (pointMesh != null && pointMesh.vertexCount != 0)
                    {
                        pointMesh.Clear(true);
                    }
                    if (lineMesh != null && lineMesh.vertexCount != 0)
                    {
                        lineMesh.Clear(true);
                    }
                    return;
                }

                if (pointMesh != null) pointMesh.Clear(true); else { pointMesh = new Mesh(); pointMesh.MarkDynamic(); }
                if (lineMesh  != null) lineMesh .Clear(true); else { lineMesh  = new Mesh(); lineMesh.MarkDynamic(); }
                
                if (pointIndexCount > 0)
                { 
                    pointMesh.SetVertices(vertices, 0, vertexCount);
                    pointMesh.SetColors(pointColors, 0, vertexCount);
                    pointMesh.SetIndices(pointIndices, 0, pointIndexCount, MeshTopology.Triangles, 0, calculateBounds: false);
                    pointMesh.RecalculateBounds();
                    pointMesh.UploadMeshData(false);
                }
                
                if (lineIndexCount > 0)
                { 
                    lineMesh.SetVertices(vertices, 0, vertexCount);
                    lineMesh.SetColors(lineColors, 0, vertexCount);
                    lineMesh.SetIndices(lineIndices, 0, lineIndexCount, MeshTopology.Lines, 0, calculateBounds: false);
                    lineMesh.RecalculateBounds();
                    lineMesh.UploadMeshData(false);
                }
            }

            internal void Destroy()
            {
                if (pointMesh) UnityEngine.Object.DestroyImmediate(pointMesh);
                if (lineMesh)  UnityEngine.Object.DestroyImmediate(lineMesh);
                pointMesh = null;
                lineMesh = null;
            }
        }

        public void Begin()
        {
            currentPointMesh = 0;
            for (int i = 0; i < pointMeshes.Count; i++) pointMeshes[i].Clear();
        }

        public void End()
        {
            for (int i = 0; i <= currentPointMesh; i++) pointMeshes[i].CommitMesh();
        }

        public void Render(Camera camera, Material pointMaterial, Material lineMaterial)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (pointMaterial)
            {
                if (pointMaterial.SetPass(0))
                {
                    for (int i = 0; i <= currentPointMesh; i++)
                    {
                        if (pointMeshes[i].vertexCount == 0 ||
                            pointMeshes[i].pointIndexCount == 0)
                            continue;
                        Graphics.DrawMeshNow(pointMeshes[i].pointMesh, Matrix4x4.identity, 0);
                    }
                }
            }
            if (lineMaterial)
            {
                if (lineMaterial.SetPass(0))
                {
                    for (int i = 0; i <= currentPointMesh; i++)
                    {
                        if (pointMeshes[i].vertexCount == 0 ||
                            pointMeshes[i].lineIndexCount == 0)
                            continue;
                        Graphics.DrawMeshNow(pointMeshes[i].lineMesh, Matrix4x4.identity, 0);
                    }
                }
            }
        }

        List<PointMesh> pointMeshes = new List<PointMesh>();
        int currentPointMesh = 0;
        
        public PointMeshManager()
        {
            pointMeshes.Add(new PointMesh());
        }

        public void DrawPoint(Vector3 position, float size, Color innerColor, Color outerColor)
        {
            var camera	= Camera.current;
            var right	= camera.transform.right;
            var up		= camera.transform.up;
                
            var p0		= (  right + up);
            var p1		= (  right - up);
            var p2		= (- right - up);
            var p3		= (- right + up);

            var pointMesh		= pointMeshes[currentPointMesh];
            var dstVertexCount	= pointMesh.vertexCount;
            if (dstVertexCount + 4 >= PointMesh.MaxVertexCount) { currentPointMesh++; if (currentPointMesh >= pointMeshes.Count) pointMeshes.Add(new PointMesh()); pointMesh = pointMeshes[currentPointMesh]; dstVertexCount = pointMesh.vertexCount; }
            var dstVertices		= pointMesh.vertices;
            var dstLineColors	= pointMesh.lineColors;
            var dstPointColors	= pointMesh.pointColors;
            var dstPointIndices	= pointMesh.pointIndices;
            var dstLineIndices	= pointMesh.lineIndices;


            var index		= dstVertexCount;
            var dstPointIndexCount	= pointMesh.pointIndexCount;
            var dstLineIndexCount	= pointMesh.lineIndexCount;

            dstVertices		[dstVertexCount] = position + (p0 * size); 
            dstPointColors	[dstVertexCount] = innerColor;
            dstLineColors	[dstVertexCount] = outerColor;
            dstVertexCount++;

            dstVertices		[dstVertexCount] = position + (p1 * size); 
            dstPointColors	[dstVertexCount] = innerColor;
            dstLineColors	[dstVertexCount] = outerColor;			
            dstVertexCount++;

            dstVertices		[dstVertexCount] = position + (p2 * size); 
            dstPointColors	[dstVertexCount] = innerColor;
            dstLineColors	[dstVertexCount] = outerColor;
            dstVertexCount++;

            dstVertices		[dstVertexCount] = position + (p3 * size); 
            dstPointColors	[dstVertexCount] = innerColor;
            dstLineColors	[dstVertexCount] = outerColor;
            dstVertexCount++;

            
            dstPointIndices[dstPointIndexCount] = index + 0; dstPointIndexCount++;
            dstPointIndices[dstPointIndexCount] = index + 1; dstPointIndexCount++;
            dstPointIndices[dstPointIndexCount] = index + 2; dstPointIndexCount++;
            dstPointIndices[dstPointIndexCount] = index + 0; dstPointIndexCount++;
            dstPointIndices[dstPointIndexCount] = index + 2; dstPointIndexCount++;
            dstPointIndices[dstPointIndexCount] = index + 3; dstPointIndexCount++;

            dstLineIndices[dstLineIndexCount] = index + 0; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 1; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 1; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 2; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 2; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 3; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 3; dstLineIndexCount++;
            dstLineIndices[dstLineIndexCount] = index + 0; dstLineIndexCount++;
            
            pointMesh.vertexCount     = dstVertexCount;
            pointMesh.pointIndexCount = dstPointIndexCount;
            pointMesh.lineIndexCount  = dstLineIndexCount;
        }

        public void DrawPoints(Vector3[] vertices, float[] sizes, Color[] colors)
        {
            var camera	= Camera.current;
            var right	= camera.transform.right;
            var up		= camera.transform.up;
                
            var p0		= (  right + up);
            var p1		= (  right - up);
            var p2		= (- right - up);
            var p3		= (- right + up);

            var pointMeshIndex		= currentPointMesh;
            var pointMesh			= pointMeshes[currentPointMesh];
            var dstVertices			= pointMesh.vertices;
            var dstLineColors		= pointMesh.lineColors;
            var dstPointColors		= pointMesh.pointColors;
            var dstPointIndices		= pointMesh.pointIndices;
            var dstLineIndices		= pointMesh.lineIndices;
            var dstVertexCount		= pointMesh.vertexCount;
            var dstPointIndexCount	= pointMesh.pointIndexCount;
            var dstLineIndexCount	= pointMesh.lineIndexCount;
            for (int i = 0, c = 0; i < vertices.Length; i ++, c += 2)
            {
                var index				= dstVertexCount;
                if (dstVertexCount + 4 >= PointMesh.MaxVertexCount)
                {
                    pointMesh.vertexCount     = dstVertexCount;
                    pointMesh.pointIndexCount = dstPointIndexCount;
                    pointMesh.lineIndexCount  = dstLineIndexCount;

                    currentPointMesh++;
                    if (currentPointMesh >= pointMeshes.Count) pointMeshes.Add(new PointMesh());
                    pointMesh			= pointMeshes[currentPointMesh];
                    pointMesh.Clear();
                     
                    dstVertices			= pointMesh.vertices;
                    dstLineColors		= pointMesh.lineColors;
                    dstPointColors		= pointMesh.pointColors;
                    dstPointIndices		= pointMesh.pointIndices;
                    dstLineIndices		= pointMesh.lineIndices;
                    dstVertexCount		= pointMesh.vertexCount;
                    dstPointIndexCount	= pointMesh.pointIndexCount;
                    dstLineIndexCount	= pointMesh.lineIndexCount;
                    index				= dstVertexCount;
                }
                var position	= vertices[i];
                var innerColor	= colors[c + 1];
                var outerColor	= colors[c + 0];
                var size		= sizes[i];

                dstVertices   [dstVertexCount] = position + (p0 * size); 
                dstPointColors[dstVertexCount] = innerColor;
                dstLineColors [dstVertexCount] = outerColor;
                dstVertexCount++;

                dstVertices   [dstVertexCount] = position + (p1 * size); 
                dstPointColors[dstVertexCount] = innerColor;
                dstLineColors [dstVertexCount] = outerColor;
                dstVertexCount++;

                dstVertices   [dstVertexCount] = position + (p2 * size); 
                dstPointColors[dstVertexCount] = innerColor;
                dstLineColors [dstVertexCount] = outerColor;
                dstVertexCount++;

                dstVertices   [dstVertexCount] = position + (p3 * size); 
                dstPointColors[dstVertexCount] = innerColor;
                dstLineColors [dstVertexCount] = outerColor;
                dstVertexCount++;
                
                dstPointIndices[dstPointIndexCount] = index + 0; dstPointIndexCount++;
                dstPointIndices[dstPointIndexCount] = index + 1; dstPointIndexCount++;
                dstPointIndices[dstPointIndexCount] = index + 2; dstPointIndexCount++;
                dstPointIndices[dstPointIndexCount] = index + 0; dstPointIndexCount++;
                dstPointIndices[dstPointIndexCount] = index + 2; dstPointIndexCount++;
                dstPointIndices[dstPointIndexCount] = index + 3; dstPointIndexCount++;

                dstLineIndices[dstLineIndexCount] = index + 0; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 1; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 1; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 2; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 2; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 3; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 3; dstLineIndexCount++;
                dstLineIndices[dstLineIndexCount] = index + 0; dstLineIndexCount++;
            }
            pointMesh.vertexCount     = dstVertexCount;
            pointMesh.pointIndexCount = dstPointIndexCount;
            pointMesh.lineIndexCount  = dstLineIndexCount;
            currentPointMesh = pointMeshIndex;
        }

        public void Destroy()
        {
            for (int i = 0; i < pointMeshes.Count; i++)
                pointMeshes[i].Destroy();
            pointMeshes.Clear();
            currentPointMesh = 0;
        }

        public void Clear()
        {
            currentPointMesh = 0;
            for (int i = 0; i < pointMeshes.Count; i++) pointMeshes[i].Clear();
        }
    }
}
