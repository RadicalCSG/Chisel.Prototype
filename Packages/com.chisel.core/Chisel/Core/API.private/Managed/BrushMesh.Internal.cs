using System;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class BrushMesh
    {
        internal bool Set(float3[]			   inVertices, 
                          BrushMesh.HalfEdge[] inHalfEdges, 
                          BrushMesh.Polygon[]  inPolygons)
        {
            if (this.vertices == null || this.vertices.Length != inVertices.Length)
                this.vertices = new float3[inVertices.Length];
            Array.Copy(inVertices, this.vertices, inVertices.Length);

            if (this.halfEdges == null || this.halfEdges.Length != inHalfEdges.Length)
                this.halfEdges = new HalfEdge[inHalfEdges.Length];
            Array.Copy(inHalfEdges, this.halfEdges, inHalfEdges.Length);

            if (this.polygons == null || this.polygons.Length != inPolygons.Length)
                this.polygons = new Polygon[inPolygons.Length];
            Array.Copy(inPolygons, this.polygons, inPolygons.Length);

            UpdateHalfEdgePolygonIndices();
            CalculatePlanes();

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int v = 0; v < inVertices.Length; v++)
            {
                var vertex = inVertices[v];
                min.x = Mathf.Min(min.x, vertex.x);
                min.y = Mathf.Min(min.y, vertex.y);
                min.z = Mathf.Min(min.z, vertex.z);

                max.x = Mathf.Max(max.x, vertex.x);
                max.y = Mathf.Max(max.y, vertex.y);
                max.z = Mathf.Max(max.z, vertex.z);
            }

            localBounds = new Bounds((max + min) * 0.5f, (max - min));
            return true;
        }

        internal void Reset()
        {
            this.vertices  = null;
            this.halfEdges = null;
            this.polygons  = null;
        }
    }
}
