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

        public void CopyFrom(BrushMesh other)
        {
            if (other.vertices != null)
            {
                if (vertices == null || vertices.Length != other.vertices.Length)                
                    vertices = new float3[other.vertices.Length];
                other.vertices.CopyTo(vertices, 0);
            } else
                vertices = null;

            if (other.halfEdges != null)
            {
                if (halfEdges == null || halfEdges.Length != other.halfEdges.Length)
                    halfEdges = new HalfEdge[other.halfEdges.Length];
                other.halfEdges.CopyTo(halfEdges, 0);
            } else
                halfEdges = null;

            if (other.halfEdgePolygonIndices != null)
            {
                if (halfEdgePolygonIndices == null || halfEdgePolygonIndices.Length != other.halfEdgePolygonIndices.Length)
                    halfEdgePolygonIndices = new int[other.halfEdgePolygonIndices.Length];
                other.halfEdgePolygonIndices.CopyTo(halfEdgePolygonIndices, 0);
            } else
                halfEdgePolygonIndices = null;

            if (other.polygons != null)
            {
                if (polygons == null || polygons.Length != other.polygons.Length)
                    polygons = new Polygon[other.polygons.Length];
                other.polygons.CopyTo(polygons, 0);
            } else
                polygons = null;

            if (other.planes != null)
            {
                if (planes == null || planes.Length != other.planes.Length)
                    planes = new float4[other.planes.Length];
                other.planes.CopyTo(planes, 0);
            } else
                planes = null;
        }

        internal void Reset()
        {
            this.vertices  = null;
            this.halfEdges = null;
            this.polygons  = null;
        }
    }
}
