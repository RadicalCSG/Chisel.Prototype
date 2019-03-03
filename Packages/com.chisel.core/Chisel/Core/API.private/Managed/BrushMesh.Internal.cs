using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
	public sealed partial class BrushMesh
	{
#if USE_MANAGED_CSG_IMPLEMENTATION
		private static void ConvertTo(Mesh mesh, BrushMesh[] brushMeshes)
		{
			var totalVertices	= new List<Vector3>();
			var totalTriangles	= new List<int>();
			foreach (var brushMesh in brushMeshes)
			{
				var startIndex	= totalVertices.Count;
				totalVertices.AddRange(brushMesh.vertices);
				var polygons	= brushMesh.polygons;
				var halfEdges	= brushMesh.halfEdges;
				for (int p = 0; p < polygons.Length; p++)
				{
					var polygon		 = polygons[p];
					var firstEdge	 = polygon.firstEdge;
					var edgeCount	 = polygon.edgeCount;
					var vertexIndex0 = halfEdges[firstEdge].vertexIndex;
					for (int e = firstEdge + 2; e < firstEdge + edgeCount; e++)
					{
						var vertexIndex1 = halfEdges[e - 1].vertexIndex;
						var vertexIndex2 = halfEdges[e].vertexIndex;
						totalTriangles.Add(startIndex + vertexIndex0);
						totalTriangles.Add(startIndex + vertexIndex1);
						totalTriangles.Add(startIndex + vertexIndex2);
					}
				}
			}
			mesh.vertices  = totalVertices .ToArray();
			mesh.triangles = totalTriangles.ToArray();
		}

		internal bool Set(Vector3[]			   inVertices, 
						  BrushMesh.HalfEdge[] inHalfEdges, 
						  BrushMesh.Polygon[]  inPolygons)
		{
			this.vertices = new Vector3[inVertices.Length];
			Array.Copy(inVertices, this.vertices, inVertices.Length);

			this.halfEdges = new HalfEdge[inHalfEdges.Length];
			Array.Copy(inHalfEdges, this.halfEdges, inHalfEdges.Length);

			this.polygons = new Polygon[inPolygons.Length];
			Array.Copy(inPolygons, this.polygons, inPolygons.Length);


			var surfaceIndicesAroundVertex = new SortedSet<int>[vertices.Length];
			for (int v = 0; v < surfaceIndicesAroundVertex.Length; v++)
				surfaceIndicesAroundVertex[v] = new SortedSet<int>();

			surfaces = new Surface[polygons.Length];
			for (int p = 0; p < polygons.Length; p++)
			{
				var normal = Vector3.zero;
				var firstEdge = polygons[p].firstEdge;
				var lastEdge = polygons[p].edgeCount + firstEdge;
				var prevVertex = vertices[halfEdges[lastEdge - 1].vertexIndex];
				for (var e = firstEdge; e < lastEdge; e++)
				{
					halfEdges[e].polygonIndex = p;

					var currVertexIndex = halfEdges[e].vertexIndex;
					surfaceIndicesAroundVertex[currVertexIndex].Add(p);

					var currVertex = vertices[currVertexIndex];
					normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
					normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
					normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));


					prevVertex = currVertex;
				}
				normal.Normalize();
				surfaces[p].plane = new Plane(normal, prevVertex);
			}

			var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

			surfacesAroundVertex = new int[vertices.Length][];
			for (int v = 0; v < surfaceIndicesAroundVertex.Length; v++)
			{
				surfacesAroundVertex[v] = surfaceIndicesAroundVertex[v].ToArray();
				if (surfaceIndicesAroundVertex[v].Count == 0)
					continue;

				var vertex = vertices[v];
				min.x = Mathf.Min(min.x, vertex.x);
				min.y = Mathf.Min(min.y, vertex.y);
				min.z = Mathf.Min(min.z, vertex.z);

				max.x = Mathf.Min(max.x, vertex.x);
				max.y = Mathf.Min(max.y, vertex.y);
				max.z = Mathf.Min(max.z, vertex.z);
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
#endif
    }
}
