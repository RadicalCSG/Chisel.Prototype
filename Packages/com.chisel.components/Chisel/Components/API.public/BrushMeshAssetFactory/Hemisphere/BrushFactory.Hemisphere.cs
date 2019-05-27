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
        public static bool GenerateHemisphereAsset(CSGBrushMeshAsset brushMeshAsset, CSGHemisphereDefinition definition)
        {
            var subMesh = new CSGBrushSubMesh();
            if (!GenerateHemisphereSubMesh(ref subMesh.brushMesh, definition))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SubMeshes = new[] { subMesh };
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }

        public static bool GenerateHemisphereSubMesh(ref BrushMesh brushMesh, CSGHemisphereDefinition definition)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateHemisphereSubMesh(ref brushMesh, definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, definition.brushMaterials, definition.surfaceDescriptions);
        }

        // TODO: clean up
        public static bool GenerateSegmentedSubMesh(ref BrushMesh brushMesh, int horzSegments, int vertSegments, Vector3[] segmentVertices, bool topCap, bool bottomCap, int topVertex, int bottomVertex, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            // FIXME: hack, to fix math below .. 
            vertSegments++;

            //if (bottomCap || topCap)
            //	vertSegments++;

            int triangleCount, quadCount, capCount, extraVertices;

            capCount		= 0;
            triangleCount	= 0;
            extraVertices	= 0;
            if (topCap)
            {
                capCount			+= 1;
            } else
            {
                extraVertices		+= 1;
                triangleCount		+= horzSegments;
            }

            if (bottomCap)
            {
                capCount			+= 1;
            } else
            {
                extraVertices		+= 1;
                triangleCount		+= horzSegments;
            }

            quadCount = horzSegments * (vertSegments - 2);

            var vertexCount			= (horzSegments * (vertSegments - 1)) + extraVertices;
            var assetPolygonCount	= triangleCount + quadCount + capCount;
            var halfEdgeCount		= (triangleCount * 3) + (quadCount * 4) + (capCount * horzSegments);

            if (segmentVertices.Length != vertexCount)
            {
                Debug.LogError("segmentVertices.Length (" + segmentVertices.Length + ") != expectedVertexCount (" + vertexCount + ")");
                brushMesh.Clear();
                return false;
            }

            var vertices		= segmentVertices;
            var	polygons		= new BrushMesh.Polygon[assetPolygonCount];
            var halfEdges		= new BrushMesh.HalfEdge[halfEdgeCount];

            var twins			= new int[horzSegments];

            var edgeIndex		= 0;
            var polygonIndex	= 0;
            var startVertex		= extraVertices;
            var startSegment	= topCap ? 1 : 0;
            var lastVertSegment	= vertSegments - 1;
            var endSegment		= bottomCap ? lastVertSegment : vertSegments;
            
            if (topCap)
            {
                var polygonEdgeCount	= horzSegments;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p = h, h++)
                {
                    var currEdgeIndex = edgeIndex + (horzSegments - 1) - h;
                    halfEdges[currEdgeIndex] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p };
                    twins[h] = currEdgeIndex;
                }
                polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, description = surfaceDescriptions[0], brushMaterial = brushMaterials[0] };
                edgeIndex += polygonEdgeCount;
                polygonIndex++;
            }

            for (int v = startSegment; v < endSegment; v++)
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
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = topVertex };
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p};
                        twins[h] = edgeIndex + 2;
                    } else
                    if (v == lastVertSegment) // bottom
                    {
                        //     0    t    1
                        //		*------>*
                        //       ^  1  /  
                        //     p2 \0 2/ n0
                        //         \ v
                        //          *
                        //          2
                        polygonEdgeCount = 3;
                        var p2 = startEdge + (p * 3) + 2;
                        var n0 = startEdge + (n * 3) + 0;
                        var t  = twins[h];
                        halfEdges[twins[h]].twinIndex = edgeIndex + 1;
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = bottomVertex };
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
                        var t  = twins[h];
                        halfEdges[twins[h]].twinIndex = edgeIndex + 1;
                        halfEdges[edgeIndex + 0] = new BrushMesh.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p};
                        halfEdges[edgeIndex + 1] = new BrushMesh.HalfEdge { twinIndex =  t, vertexIndex = startVertex + (horzSegments - 1) - h};
                        halfEdges[edgeIndex + 2] = new BrushMesh.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments};
                        halfEdges[edgeIndex + 3] = new BrushMesh.HalfEdge { twinIndex = -1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments};
                        twins[h] = edgeIndex + 3;
                    }
                    polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, description = surfaceDescriptions[0], brushMaterial = brushMaterials[0] };
                    edgeIndex += polygonEdgeCount;
                    polygonIndex++;
                }
                if (v > 0)
                    startVertex += horzSegments;
            }
            if (bottomCap)
            {
                var polygonEdgeCount	= horzSegments;
                for (int h = 0; h < horzSegments; h++)
                {
                    var currEdgeIndex = edgeIndex + h; 
                    halfEdges[twins[h]].twinIndex = currEdgeIndex;
                    halfEdges[currEdgeIndex] = new BrushMesh.HalfEdge { twinIndex = twins[h], vertexIndex = startVertex + (horzSegments - 1) - h };
                }
                polygons[polygonIndex] = new BrushMesh.Polygon { surfaceID = polygonIndex, firstEdge = edgeIndex, edgeCount = polygonEdgeCount, description = surfaceDescriptions[0], brushMaterial = brushMaterials[0] };
            }
            
            brushMesh.polygons	= polygons;
            brushMesh.halfEdges	= halfEdges;
            brushMesh.vertices	= vertices;
            return true;
        }

        public static bool GenerateHemisphereVertices(CSGHemisphereDefinition definition, ref Vector3[] vertices)
        {
            definition.Validate();
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateHemisphereVertices(definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, ref vertices);
        }

        public static bool GenerateHemisphereVertices(Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            var bottomCap		= true;
            var topCap			= false;
            var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

            var rings			= (vertSegments) + (topCap ? 1 : 0);
            var vertexCount		= (horzSegments * rings) + extraVertices;

            var topVertex		= 0;
            var bottomVertex	= (!topCap) ? 1 : 0;
            var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                              diameterXYZ.y, 
                                              diameterXYZ.z * 0.5f);

            if (vertices == null || 
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];
            if (!topCap   ) vertices[topVertex   ] = transform.MultiplyPoint(Vector3.up * radius.y);  // top
            if (!bottomCap) vertices[bottomVertex] = transform.MultiplyPoint(Vector3.zero);					 // bottom
            var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
            var vertexIndex			= extraVertices;
            {
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,  
                                                                                0.0f, 
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            }
            for (int v = 1; v < rings; v++)
            {
                var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f;			// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);								// [0 .. 90]
                var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad) * radius.y;
                var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + extraVertices].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + extraVertices].z * segmentRadius; 
                }
            }
            return true;
        }

        public static bool GenerateHemisphereSubMesh(ref BrushMesh brushMesh, Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
            {
                brushMesh.Clear();
                return false;
            }

            var bottomCap		= true;
            var topCap			= false;
            var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

            var rings			= (vertSegments) + (topCap ? 1 : 0);
            var vertexCount		= (horzSegments * rings) + extraVertices;

            var topVertex		= 0;
            var bottomVertex	= (!topCap) ? 1 : 0;
            var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                              diameterXYZ.y, 
                                              diameterXYZ.z * 0.5f);

            var heightY = radius.y;
            float topY, bottomY;
            if (heightY < 0)
            {
                topY = 0;
                bottomY = heightY;
            } else
            {
                topY = heightY;
                bottomY = 0;
            }

            var vertices = new Vector3[vertexCount];
            if (!topCap   ) vertices[topVertex   ] = transform.MultiplyPoint(Vector3.up * topY);    // top
            if (!bottomCap) vertices[bottomVertex] = transform.MultiplyPoint(Vector3.up * bottomY); // bottom
            var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
            var vertexIndex			= extraVertices;
            if (heightY < 0)
            {
                for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,
                                                                                0.0f,
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            } else
            {
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(Mathf.Cos(hRad) * radius.x,  
                                                                                0.0f, 
                                                                                Mathf.Sin(hRad) * radius.z));
                }
            }
            for (int v = 1; v < rings; v++)
            {
                var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f;			// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);								// [0 .. 90]
                var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad) * heightY;
                var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);		// [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + extraVertices].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + extraVertices].z * segmentRadius; 
                }
            }

            return GenerateSegmentedSubMesh(ref brushMesh, horzSegments, vertSegments, vertices, bottomCap, topCap, bottomVertex, topVertex, brushMaterials, surfaceDescriptions);
        }
    }
}