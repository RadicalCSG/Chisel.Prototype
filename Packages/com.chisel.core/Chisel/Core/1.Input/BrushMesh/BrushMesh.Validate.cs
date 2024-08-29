using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class BrushMesh
	{
        public bool Validate(bool logErrors = false)
        {
            if (!ValidateData(out var errorMessage))
            {
                if (logErrors) Debug.LogError(errorMessage);
                return false;
            }
            if (planes == null)
                CalculatePlanes();
			if (!ValidateShape(out errorMessage))
			{
				if (logErrors) Debug.LogError(errorMessage);
				return false;
			}
            return true;
        }

        public const uint kMinimumVertices = 4;
		public const uint kMinimumPolygons = 3;
		public const uint kMinimumHalfEdges = 3 * 3;

		static System.Text.StringBuilder errorMessageBuilder = new System.Text.StringBuilder();
		public bool ValidateData(out string errorMessage)
        {
            errorMessage = null;
			var vertices = this.vertices;
            if (vertices == null || vertices.Length == 0)
            {
				errorMessage = "BrushMesh: BrushMesh has no vertices set";
                return false;
            }

            var halfEdges = this.halfEdges;
            if (halfEdges == null || halfEdges.Length == 0)
            {
				errorMessage = "BrushMesh: BrushMesh has no halfEdges set";
                return false;
            }

            var polygons = this.polygons;
            if (polygons == null || polygons.Length == 0)
            {
				errorMessage = "BrushMesh: BrushMesh has no polygons set";
                return false;
            }

			if (vertices.Length < kMinimumVertices)
			{
				errorMessage = $"BrushMesh must have at least {kMinimumVertices} vertices, but has {vertices.Length} vertices";
				return false;
			}

			if (polygons.Length < kMinimumPolygons)
			{
				errorMessage = $"BrushMesh must have at least {kMinimumPolygons} polygons, but has {polygons.Length} polygons";
				return false;
			}

			if (halfEdges.Length < kMinimumHalfEdges)
			{
				errorMessage = $"BrushMesh must have at least {kMinimumHalfEdges} halfedges, but has {halfEdges.Length} halfedges";
				return false;
			}

			bool fail = false;
			errorMessageBuilder.Clear();

			for (int h = 0; h < halfEdges.Length; h++)
            {
                if (halfEdges[h].vertexIndex < 0)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{h}].vertexIndex is {halfEdges[h].vertexIndex}");
                    fail = true;
                } else
                if (halfEdges[h].vertexIndex >= vertices.Length)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{h}].vertexIndex is {halfEdges[h].vertexIndex}, but there are {vertices.Length} vertices.");
                    fail = true;
                }

                if (halfEdges[h].twinIndex < 0)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}");
                    fail = true;
                    continue;
                } else
                if (halfEdges[h].twinIndex >= halfEdges.Length)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}, but there are {halfEdges.Length} edges.");
                    fail = true;
                    continue;
                }

                var twinIndex	= halfEdges[h].twinIndex;
                var twin		= halfEdges[twinIndex];
                if (twin.twinIndex != h)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{h}].twinIndex is {halfEdges[h].twinIndex}, but the twinIndex of its twin is {twin.twinIndex} instead of {h}.");
                    fail = true;
                }
            }

            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var count     = polygons[p].edgeCount;
                var polygonFail = false;
                if (firstEdge < 0)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: polygons[{p}].firstEdge is {firstEdge}.");
                    polygonFail = true;
                } else
                if (firstEdge >= halfEdges.Length)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: polygons[{p}].firstEdge is {firstEdge}, but there are {halfEdges.Length} edges.");
                    polygonFail = true;
                }
                if (count <= 2)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: polygons[{p}].edgeCount is {count}.");
                    polygonFail = true;
                } else
                if (firstEdge + count - 1 >= halfEdges.Length)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: polygons[{p}].firstEdge + polygons[{p}].edgeCount is {(firstEdge + count)}, but there are {halfEdges.Length} edges.");
                    polygonFail = true;
                } else
                if (p < polygons.Length - 1 &&
                    polygons[p + 1].firstEdge != firstEdge + count)
                {
					errorMessageBuilder.AppendLine($"BrushMesh: polygons[{(p + 1)}].firstEdge does not equal polygons[{p}].firstEdge + polygons[{p}].edgeCount.");
                    polygonFail = true;
                }

                fail = fail || polygonFail;
                if (polygonFail)
                    continue;
                
                for (int i0 = count - 1, i1 = 0; i1 < count; i0 = i1, i1++)
                {
                    var h0 = halfEdges[i0 + firstEdge];	// curr
                    var h1 = halfEdges[i1 + firstEdge]; // curr.next
                    if (h1.twinIndex < 0 || h1.twinIndex >= halfEdges.Length)
                    {
                        fail = true;
                        continue;
                    }
                    var t1 = halfEdges[h1.twinIndex];   // curr.next.twin

                    if (h0.vertexIndex != t1.vertexIndex)
                    {
						errorMessageBuilder.AppendLine($"BrushMesh: halfEdges[{(i0 + firstEdge)}].vertexIndex ({h0.vertexIndex}) is not equal to halfEdges[halfEdges[{(i1 + firstEdge)}].twinIndex({h1.twinIndex})].vertexIndex ({t1.vertexIndex}).");
                        fail = true;
                    }
                }
            }

			if (fail)
            {
                if (errorMessageBuilder.Length > 0)
                    errorMessage = errorMessageBuilder.ToString();
                else
                    errorMessage = "BrushMesh: Unknown failure";
				return false;
			}

			if (planes == null || polygons.Length != planes.Length)
			{
				// Try to fix this
				CalculatePlanes();
                if (planes == null || polygons.Length != planes.Length)
                {
                    if (planes == null)
                        errorMessage = $"BrushMesh: number of polygons ({polygons.Length}) must be equal to number of planes (null)";
                    else
                        errorMessage = $"BrushMesh: number of polygons ({polygons.Length}) must be equal to number of planes ({planes.Length})";
					return false;
				}
			}

			if (halfEdgePolygonIndices.Length != halfEdges.Length)
			{
				// Try to fix this
				UpdateHalfEdgePolygonIndices();
				if (halfEdgePolygonIndices.Length != halfEdges.Length)
				{
					errorMessage = $"BrushMesh: number of halfEdgePolygonIndices ({halfEdgePolygonIndices.Length}) must be equal to number of halfEdges ({halfEdges.Length})";
					return false;
				}
			}

			fail = false;
			for (int h = 0; h < halfEdgePolygonIndices.Length; h++)
			{
				var polygonIndex = halfEdgePolygonIndices[h];
				if (polygonIndex < 0 ||
					polygonIndex >= polygons.Length)
				{
					errorMessageBuilder.AppendLine($"BrushMesh: halfEdgePolygonIndices value is out of range (must be between 0 and {polygons.Length}, and is {polygonIndex})");
					fail = true;
				} else 
                {
                    var firstEdge = polygons[polygonIndex].firstEdge;
                    var lastEdge = polygons[polygonIndex].firstEdge + polygons[polygonIndex].edgeCount;
                    if (h < firstEdge ||
                        h >= lastEdge)
					{
						errorMessageBuilder.AppendLine($"BrushMesh: halfEdgePolygonIndices[{h}] leads to wrong polygon ({polygonIndex}) with range ({firstEdge}, {lastEdge}]");
                        fail = true;
					}
				}
			}

			if (fail)
			{
				if (errorMessageBuilder.Length > 0)
					errorMessage = errorMessageBuilder.ToString();
				else
					errorMessage = "BrushMesh: Unknown failure";
				return false;
			}

			return true;
        }
        
		public bool ValidateShape(out string errorMessage)
        {
            errorMessage = null;			
			if (!HasVolume())
            {
				errorMessage = "BrushMesh: Brush has no volume";
                return false;
            }

            if (IsConcave())           // TODO: eventually allow concave shapes
			{
				errorMessage = "BrushMesh: Brush is concave";
				return false;
			}

			if (IsSelfIntersecting())    // TODO: in which case this needs to be implemented
			{
				errorMessage = "BrushMesh: Brush is self intersecting";
				return false;
			}
			return true;
        }
    }
}