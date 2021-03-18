using System;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class BrushMesh
    {
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
    }
}
