using UnityEngine;

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
                var polygonFail = false;
                if (firstEdge < 0)
                {
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge is " + firstEdge);
                    polygonFail = true;
                } else
                if (firstEdge >= halfEdges.Length)
                {
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge is " + firstEdge + ", but there are " + halfEdges.Length + " edges.");
                    polygonFail = true;
                }
                if (count <= 2)
                {
                    if (logErrors) Debug.LogError("polygons[" + p + "].edgeCount is " + count);
                    polygonFail = true;
                } else
                if (firstEdge + count - 1 >= halfEdges.Length)
                {
                    if (logErrors) Debug.LogError("polygons[" + p + "].firstEdge + polygons[" + p + "].edgeCount is " + (firstEdge + count) + ", but there are " + halfEdges.Length + " edges.");
                    polygonFail = true;
                } else
                if (p < polygons.Length - 1 &&
                    polygons[p + 1].firstEdge != firstEdge + count)
                {
                    if (logErrors) Debug.LogError("polygons[" + (p + 1) + "].firstEdge does not equal polygons[" + p + "].firstEdge + polygons[" + p + "].edgeCount.");
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
                        if (logErrors)
                        {
                            Debug.LogError("halfEdges[" + (i0 + firstEdge) + "].vertexIndex (" + h0.vertexIndex + ") is not equal to halfEdges[halfEdges[" + (i1 + firstEdge) + "].twinIndex(" + h1.twinIndex + ")].vertexIndex (" + t1.vertexIndex + ").");
                        }
                        fail = true;
                    }
                }
            }
            if (fail)
                return false;

            if (IsSelfIntersecting())
            {
                if (logErrors)
                {
                    Debug.LogError("Brush is self intersecting");
                }
                return false;
            }

            if (!HasVolume())
            {
                if (logErrors)
                {
                    Debug.LogError("Brush has no volume");
                }
                return false;
            }

            return true;
        }
    }
}