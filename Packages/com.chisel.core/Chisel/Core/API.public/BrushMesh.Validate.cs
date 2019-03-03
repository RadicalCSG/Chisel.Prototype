using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
	public sealed partial class BrushMesh
	{
		public bool Validate(bool logErrors = false)
		{
			var vertices = this.vertices;
			if (vertices == null || vertices.Length == 0)
			{
                if (logErrors) Debug.LogError("BrushMesh has no vertices set");
				return false;
			}

			var halfEdges = this.halfEdges;
			if (halfEdges == null || halfEdges.Length == 0)
			{
                if (logErrors) Debug.LogError("BrushMesh has no halfEdges set");
				return false;
			}

			var polygons = this.polygons;
			if (polygons == null || polygons.Length == 0)
			{
                if (logErrors) Debug.LogError("BrushMesh has no polygons set");
				return false;
			}

			bool fail = false;

			for (int h = 0; h < halfEdges.Length; h++)
			{
				if (halfEdges[h].vertexIndex < 0)
				{
                    if (logErrors) Debug.LogError("halfEdges[" + h + "].vertexIndex is " + halfEdges[h].vertexIndex);
					fail = true;
				} else
				if (halfEdges[h].vertexIndex >= vertices.Length)
				{
                    if (logErrors) Debug.LogError("halfEdges[" + h + "].vertexIndex is " + halfEdges[h].vertexIndex + ", but there are " + vertices.Length + " vertices.");
					fail = true;
				}

				if (halfEdges[h].twinIndex < 0)
				{
                    if (logErrors) Debug.LogError("halfEdges[" + h + "].twinIndex is " + halfEdges[h].twinIndex);
					fail = true;
					continue;
				} else
				if (halfEdges[h].twinIndex >= halfEdges.Length)
				{
                    if (logErrors) Debug.LogError("halfEdges[" + h + "].twinIndex is " + halfEdges[h].twinIndex + ", but there are " + halfEdges.Length + " edges.");
					fail = true;
					continue;
				}

				var twinIndex	= halfEdges[h].twinIndex;
				var twin		= halfEdges[twinIndex];
				if (twin.twinIndex != h)
				{
                    if (logErrors) Debug.LogError("halfEdges[" + h + "].twinIndex is " + halfEdges[h].twinIndex + ", but the twinIndex of its twin is " + twin.twinIndex + " instead of " + h + ".");
					fail = true;
				}
			}

			for (int p = 0; p < polygons.Length; p++)
			{
				var firstEdge = polygons[p].firstEdge;
				var count     = polygons[p].edgeCount;
				if (firstEdge < 0)
				{
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge is " + firstEdge);
					fail = true;
				} else
				if (firstEdge >= halfEdges.Length)
				{
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge is " + firstEdge + ", but there are " + halfEdges.Length + " edges.");
					fail = true;
				}
				if (count <= 0)
				{
                    if (logErrors) Debug.LogError("polygons[" + p + "].edgeCount is " + count);
					fail = true;
				} else
				if (firstEdge + count > halfEdges.Length)
				{
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge + polygons[" + p + "].edgeCount is " + (firstEdge + count) + ", but there are " + halfEdges.Length + " edges.");
					fail = true;
				} else
				if (p < polygons.Length - 1 &&
					polygons[p + 1].firstEdge != firstEdge + count)
				{
                    if (logErrors) Debug.LogError("polygons[" + (p + 1) + "].firstEdge does not equal polygons[" + p + "].firstEdge + polygons[" + p + "].edgeCount.");
					fail = true;
				}

				for (int i1 = 0, i0 = count - 1; i1 < count; i0 = i1, i1++)
				{
					var h0 = halfEdges[i0 + firstEdge];	// curr
					var h1 = halfEdges[i1 + firstEdge]; // curr.prev
					var t1 = halfEdges[h1.twinIndex];	// curr.prev.twin
					
					if (h0.vertexIndex != t1.vertexIndex)
					{
                        if (logErrors) Debug.LogError("halfEdges[" + (i0 + firstEdge) + "].vertexIndex (" + h0.vertexIndex + ") is not equal to halfEdges[" + h1.twinIndex + "].vertexIndex (" + t1.vertexIndex + ").");
						fail = true;
					}
				}
			}
			return !fail;
		}
	}
}