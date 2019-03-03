using System;
using System.Linq;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using Chisel.Assets;
using Chisel.Core;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
	{
		public static bool GenerateSphereAsset(CSGBrushMeshAsset brushMeshAsset, CSGSphereDefinition definition)
		{
			var subMesh = new CSGBrushSubMesh();
			if (!GenerateSphereSubMesh(subMesh, definition))
			{
				brushMeshAsset.Clear();
				return false;
			}

			brushMeshAsset.SubMeshes = new[] { subMesh };
			brushMeshAsset.CalculatePlanes();
			brushMeshAsset.SetDirty();
			return true;
		}

		public static bool GenerateSphereSubMesh(CSGBrushSubMesh subMesh, CSGSphereDefinition definition)
		{
			definition.Validate();
			var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
			return GenerateSphereSubMesh(subMesh, definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, definition.surfaceAssets, definition.surfaceDescriptions);
		}

		public static bool GenerateSphereVertices(CSGSphereDefinition definition, ref Vector3[] vertices)
		{
			definition.Validate();
			var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
			return GenerateSphereVertices(definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, ref vertices);
		}

		public static bool GenerateSphereVertices(Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ref Vector3[] vertices)
		{
			//var lastVertSegment	= vertSegments - 1;

			int vertexCount		= (horzSegments * (vertSegments - 1)) + 2;
			
			if (vertices == null ||
				vertices.Length != vertexCount)
				vertices = new Vector3[vertexCount];
			
			var radius			= 0.5f * diameterXYZ;

			vertices[0] = Vector3.down * radius.y;
			vertices[1] = Vector3.up   * radius.y;
			var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
			var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : ((360.0f / horzSegments) * 0.5f) * Mathf.Deg2Rad;
			for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
			{
				var segmentFactor	= ((v - (vertSegments / 2.0f)) / vertSegments);		// [-0.5f ... 0.5f]
				var segmentDegree	= (segmentFactor * 180);							// [-90 .. 90]
				var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad);
				var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);			// [0 .. 0.707 .. 1 .. 0.707 .. 0]
				for (int h = 0; h < horzSegments; h++, vertexIndex++)
				{
					var hRad = (h * degreePerSegment) + angleOffset;
					vertices[vertexIndex] = new Vector3(Mathf.Cos(hRad) * segmentRadius * radius.x, 
														segmentHeight                   * radius.y, 
														Mathf.Sin(hRad) * segmentRadius * radius.z);
				}
			}
			return true;
		}

		public static bool GenerateSphereSubMesh(CSGBrushSubMesh subMesh, Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, CSGSurfaceAsset[] surfaceAssets, SurfaceDescription[] surfaceDescriptions)
		{
			var lastVertSegment	= vertSegments - 1;

			//int vertexCount		= (horzSegments * (vertSegments - 1)) + 2;
			var triangleCount	= horzSegments + horzSegments;	// top & bottom
			var quadCount		= horzSegments * (vertSegments - 2);
			int polygonCount	= triangleCount + quadCount;
			int halfEdgeCount	= (triangleCount * 3) + (quadCount * 4);

			Vector3[] vertices = null;
			if (!GenerateSphereVertices(diameterXYZ, transform, horzSegments, vertSegments, ref vertices))
				return false;

			var	polygons		= new CSGBrushSubMesh.Polygon[polygonCount];
			var halfEdges		= new BrushMesh.HalfEdge[halfEdgeCount];

			//var radius			= 0.5f * diameterXYZ;
			
			var edgeIndex = 0;
			var polygonIndex = 0;
			var startVertex = 2;
			for (int v = 0; v < vertSegments; v++)
			{
				var startEdge   = edgeIndex;
				for (int h = 0, p = horzSegments - 1; h < horzSegments; p=h, h++)
				{
					var n = (h + 1) % horzSegments;
					int polygonEdgeCount;
					if (v == 0) // top
					{
						//          0
						//          *
						//         ^ \
						//     p1 /0 1\ n0
						//       /  2  v
						//		*<------*  
						//     2    t    1
						polygonEdgeCount = 3;
						var p1 = (p * 3) + 1;
						var n0 = (n * 3) + 0;
						var t  = ((vertSegments == 2) ? (startEdge + (horzSegments * 3) + (h * 3) + 1) : (startEdge + (horzSegments * 3) + (h * 4) + 1));
						halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = 0 };
						halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h};
						halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - p};
					} else
					if (v == lastVertSegment)
					{
						//     0    t    1
						//		*------>*
						//       ^  1  /  
						//     p1 \0 2/ n0
						//         \ v
						//          *
						//          2
						polygonEdgeCount = 3;
						var p2 = startEdge + (p * 3) + 2;
						var n0 = startEdge + (n * 3) + 0;
						var t  = ((vertSegments == 2) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
						halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p};
						halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
						halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = 1 };
					} else
					{
						//     0    t3   1
						//		*------>*
						//      ^   1   |  
						//   p1 |0     2| n0
						//      |   3   v
						//		*<------*
						//     3    t1   2
						polygonEdgeCount = 4;
						var p1 = startEdge + (p * 4) + 2;
						var n0 = startEdge + (n * 4) + 0;
						var t3 = ((v ==                   1) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
						var t1 = ((v == lastVertSegment - 1) ? (startEdge + (horzSegments * 4) + (h * 3) + 1) : (startEdge + (horzSegments * 4) + (h * 4) + 1));
						halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p};
						halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex = t3, vertexIndex = startVertex + (horzSegments - 1) - h};
						halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments};
						halfEdges[edgeIndex + 3] = new BrushMesh.HalfEdge { twinIndex = t1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments};
					}
					polygons[polygonIndex] = new CSGBrushSubMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, description = surfaceDescriptions[0], surfaceAsset = surfaceAssets[0] };
					edgeIndex += polygonEdgeCount;
					polygonIndex++;
				}
				if (v > 0)
					startVertex += horzSegments;
			}
			
			subMesh.Polygons	= polygons;
			subMesh.HalfEdges	= halfEdges;
			subMesh.Vertices	= vertices;
			return true;
		}
	}
}