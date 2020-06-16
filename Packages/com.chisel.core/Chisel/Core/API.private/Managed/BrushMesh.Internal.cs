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
