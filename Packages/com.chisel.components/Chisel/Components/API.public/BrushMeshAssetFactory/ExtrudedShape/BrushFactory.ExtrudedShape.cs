using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        static Vector2 PointOnBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
        }

        public static void GetPathVertices(Curve2D shape, int shapeCurveSegments, List<Vector2> shapeVertices, List<int> shapeSegmentIndices)
        {
            var points = shape.controlPoints;
            var length = points.Length;

            for (int i = 0; i < points.Length; i++)
            {
                var index1 = i;
                var index2 = (i + 1) % points.Length;
                var p1 = points[index1];
                var p2 = points[index2];
                var v1 = p1.position;
                var v2 = p2.position;

                if (shapeCurveSegments == 0 ||
                    (points[index1].constraint2 == ControlPointConstraint.Straight &&
                     points[index2].constraint1 == ControlPointConstraint.Straight))
                {
                    shapeVertices.Add(v1);
                    shapeSegmentIndices.Add(i);
                    continue;
                }

                Vector2 v0, v3;

                if (p1.constraint2 != ControlPointConstraint.Straight)
                    v0 = v1 - p1.tangent2;
                else
                    v0 = v1;
                if (p2.constraint1 != ControlPointConstraint.Straight)
                    v3 = v2 - p2.tangent1;
                else
                    v3 = v2;

                int first_index = shapeVertices.Count;
                shapeSegmentIndices.Add(i);
                shapeVertices.Add(v1);
                for (int n = 1; n < shapeCurveSegments; n++)
                {
                    shapeSegmentIndices.Add(i);
                    shapeVertices.Add(PointOnBezier(v1, v0, v3, v2, n / (float)shapeCurveSegments));
                }
            }
        }

        static bool GetExtrudedVertices(Vector2[] shapeVertices, Matrix4x4 matrix0, Matrix4x4 matrix1, out Vector3[] vertices)
        {
            var pathSegments = 2;
            var shapeSegments = shapeVertices.Length;
            var vertexCount = shapeSegments * pathSegments;
            vertices = new Vector3[vertexCount];

            for (int s = 0; s < shapeSegments; s++)
            {
                vertices[s] = matrix0.MultiplyPoint(shapeVertices[s]);
                vertices[shapeSegments + s] = matrix1.MultiplyPoint(shapeVertices[s]);
            }
            return true;
        }

        public static bool GenerateExtrudedShapeAsset(ChiselGeneratedBrushes brushMeshAsset, Curve2D shape, Path path, int curveSegments, ChiselBrushMaterial[] brushMaterials, ref SurfaceDescription[] surfaceDescriptions)
        {
            var shapeVertices = new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            GetPathVertices(shape, curveSegments, shapeVertices, shapeSegmentIndices);

            Vector2[][] polygonVerticesArray;
            int[][] polygonIndicesArray;

            if (!Decomposition.ConvexPartition(shapeVertices, shapeSegmentIndices,
                                                out polygonVerticesArray,
                                                out polygonIndicesArray))
                return false;

            // TODO: make each extruded quad split into two triangles when it's not a perfect plane,
            //			split it to make sure it's convex

            // TODO: make it possible to smooth (parts) of the shape

            // TODO: make materials work well
            // TODO: make it possible to 'draw' shapes on any surface

            // TODO: make path work as a spline, with subdivisions
            // TODO:	make this work well with twisted rotations
            // TODO: make shape/path subdivisions be configurable / automatic



            var subMeshes = new List<ChiselGeneratedBrushes.ChiselGeneratedBrush>();
            for (int p = 0; p < polygonVerticesArray.Length; p++)
            {
                var polygonVertices = polygonVerticesArray[p];
                var segmentIndices = polygonIndicesArray[p];
                var shapeSegments = polygonVertices.Length;

                for (int s = 0; s < path.segments.Length - 1; s++)
                {
                    var pathPointA = path.segments[s];
                    var pathPointB = path.segments[s + 1];
                    int subSegments = 1;
                    var offsetQuaternion = pathPointB.rotation * Quaternion.Inverse(pathPointA.rotation);
                    var offsetEuler = offsetQuaternion.eulerAngles;
                    if (offsetEuler.x > 180) offsetEuler.x = 360 - offsetEuler.x;
                    if (offsetEuler.y > 180) offsetEuler.y = 360 - offsetEuler.y;
                    if (offsetEuler.z > 180) offsetEuler.z = 360 - offsetEuler.z;
                    var maxAngle = Mathf.Max(offsetEuler.x, offsetEuler.y, offsetEuler.z);
                    if (maxAngle != 0)
                        subSegments = Mathf.Max(1, (int)Mathf.Ceil(maxAngle / 5));

                    if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                        (subSegments & 1) == 1)
                        subSegments += 1;

                    for (int n = 0; n < subSegments; n++)
                    {
                        var matrix0 = PathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], n / (float)subSegments);
                        var matrix1 = PathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], (n + 1) / (float)subSegments);

                        // TODO: this doesn't work if top and bottom polygons intersect
                        //			=> need to split into two brushes then, invert one of the two brushes
                        var invertDot = Vector3.Dot(matrix0.MultiplyVector(Vector3.forward).normalized, (matrix1.MultiplyPoint(shapeVertices[0]) - matrix0.MultiplyPoint(shapeVertices[0])).normalized);

                        if (invertDot == 0.0f)
                            continue;

                        Vector3[] vertices;
                        if (invertDot < 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }
                        if (!GetExtrudedVertices(polygonVertices, matrix0, matrix1, out vertices))
                            continue;

                        var subMesh = new ChiselGeneratedBrushes.ChiselGeneratedBrush();
                        CreateExtrudedSubMesh(ref subMesh.brushMesh, shapeSegments, segmentIndices, 0, 1, vertices, brushMaterials, surfaceDescriptions);
                        subMeshes.Add(subMesh);
                    }
                }
            }

            brushMeshAsset.SubMeshes = subMeshes.ToArray();
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.OnValidate();
            brushMeshAsset.SetDirty();
            return true;
        }


        static bool CreateExtrudedSubMesh(ref BrushMesh brushMesh, int segments, int[] segmentDescriptionIndices, int segmentTopIndex, int segmentBottomIndex, Vector3[] vertices, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            return CreateExtrudedSubMesh(ref brushMesh, segments, segmentDescriptionIndices, null, segmentTopIndex, segmentBottomIndex, vertices, brushMaterials, surfaceDescriptions);
        }

        enum SegmentTopology : sbyte
        {
            Quad = 0, 
            TrianglesNegative = -1,
            TrianglesPositive = 1,
            TriangleNegative = -2,
            TrianglePositive = 2,
            None = 3
        }

        static bool CreateExtrudedSubMesh(ref BrushMesh brushMesh, int segments, int[] segmentDescriptionIndices, int[] segmentAssetIndices, int segmentTopIndex, int segmentBottomIndex, Vector3[] vertices, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (vertices.Length < 3)
                return false;

            // TODO: vertex reverse winding when it's not clockwise
            // TODO: handle duplicate vertices, remove them or avoid them being created in the first place (maybe use indices?)
            
            var segmentTopology = new SegmentTopology[segments];
            var edgeIndices		= new int[segments * 2];

            var polygonCount = 2;
            var edgeOffset = segments + segments;
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //			 	  t0	 
                //	 ------>   ------->   -----> 
                //        v0 *          * v1
                //	 <-----    <-------   <-----
                //			^ |   e2   ^ |
                //          | |        | |
                //	   q1 p0| |e3 q2 e5| |n0 q3
                //          | |        | |
                //			| v   e4   | v
                //	 ------>   ------->   ----->
                //        v3 *          * v2
                //	 <-----    <-------   <-----  
                //			      b0    
                //
                var v0 = vertices[p];
                var v1 = vertices[e];
                var v2 = vertices[e + segments];
                var v3 = vertices[p + segments];

                var equals03 = (v0 - v3).sqrMagnitude < 0.0001f;
                var equals12 = (v1 - v2).sqrMagnitude < 0.0001f;
                if (equals03)
                {
                    if (equals12)
                        segmentTopology[n] = SegmentTopology.None;
                    else
                        segmentTopology[n] = SegmentTopology.TriangleNegative;
                } else
                if (equals12)
                {
                    segmentTopology[n] = SegmentTopology.TrianglePositive;
                } else
                {

                    var plane = new Plane(v0, v1, v3);
                    var dist = plane.GetDistanceToPoint(v2);
                    if (UnityEngine.Mathf.Abs(dist) < 0.001f)
                        segmentTopology[n] = SegmentTopology.Quad;
                    else
                        segmentTopology[n] = (SegmentTopology)UnityEngine.Mathf.Sign(dist);
                }
                
                switch (segmentTopology[n])
                {
                    case SegmentTopology.Quad:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 4;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount += 2;
                        edgeOffset += 6;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        edgeIndices[(n * 2) + 0] = edgeOffset + 1;
                        polygonCount++;
                        edgeOffset += 3;
                        edgeIndices[(n * 2) + 1] = edgeOffset - 1;
                        break;
                    }
                    case SegmentTopology.None:
                        edgeIndices[(n * 2) + 0] = 0;
                        edgeIndices[(n * 2) + 1] = 0;
                        break;
                }
            }

            var polygons = new BrushMesh.Polygon[polygonCount];

            var surfaceDescription0 = surfaceDescriptions[segmentTopIndex];
            var surfaceDescription1 = surfaceDescriptions[segmentBottomIndex];
            var brushMaterial0 = brushMaterials[segmentTopIndex];
            var brushMaterial1 = brushMaterials[segmentBottomIndex];

            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge = 0, edgeCount = segments, description = surfaceDescription0, brushMaterial = brushMaterial0 };
            polygons[1] = new BrushMesh.Polygon { surfaceID = 1, firstEdge = segments, edgeCount = segments, description = surfaceDescription1, brushMaterial = brushMaterial1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s]);
                var assetIndex = (segmentAssetIndices == null) ? 2 : (segmentAssetIndices[s]);
                var firstEdge = edgeIndices[(s * 2) + 0] - 1;
                switch (segmentTopology[s])
                {
                    case SegmentTopology.Quad:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 4, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                        polygons[surfaceID].description.smoothingGroup = (uint)0;
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    case SegmentTopology.TrianglesPositive:
                    {
                        var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                            //			unless there's already a smoothing group for this edge; then use that

                        polygons[surfaceID + 0] = new BrushMesh.Polygon { surfaceID = surfaceID + 0, firstEdge = firstEdge, edgeCount = 3, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                        polygons[surfaceID + 0].description.smoothingGroup = (uint)smoothingGroup;
                        polygons[surfaceID + 1] = new BrushMesh.Polygon { surfaceID = surfaceID + 1, firstEdge = firstEdge + 3, edgeCount = 3, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                        polygons[surfaceID + 1].description.smoothingGroup = (uint)smoothingGroup;
                        surfaceID += 2;
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    case SegmentTopology.TrianglePositive:
                    {
                        polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 3, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                        polygons[surfaceID].description.smoothingGroup = (uint)0;
                        surfaceID++;
                        break;
                    }
                    case SegmentTopology.None:
                    {
                        break;
                    }
                }
            }

            var halfEdges = new BrushMesh.HalfEdge[edgeOffset];
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    default:
                    {
                        halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                        halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
                        break;
                    }
                }
            }

            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                var nn = (n + 1) % segments;

                var p0 = edgeIndices[(e  * 2) + 1];
                var n0 = edgeIndices[(nn * 2) + 0];

                switch (segmentTopology[n])
                {
                    case SegmentTopology.None:
                    {
                        continue;
                    }
                    case SegmentTopology.Quad:
                    {
                        //			 	  t0	 
                        //	 ------>   ------->   -----> 
                        //        v0 *          * v1
                        //	 <-----    <-------   <-----
                        //			^ |   e2   ^ |
                        //          | |        | |
                        //	   q1 p0| |e3 q2 e5| |n0 q3
                        //          | |        | |
                        //			| v   e4   | v
                        //	 ------>   ------->   ----->
                        //        v3 *          * v2
                        //	 <-----    <-------   <-----  
                        //			      b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesNegative:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |     e2   ^ ^ |
                        //          | |        / / | |
                        //          | |    e4/ /   | |
                        //	   q1 p0| |e3   / /  e7| |n0 q3
                        //          | |   / /e5    | |
                        //          | | / /        | |
                        //			| v/v   e6     | v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e6;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglesPositive:
                    {
                        //			 	    t0	 
                        //	 ------>   ----------->   -----> 
                        //        v0 *              * v1
                        //	 <-----    <-----------   <-----
                        //			^ |^\   e5     ^ |
                        //          | | \ \        | |
                        //          | |   \ \ e6   | |
                        //	   q1 p0| |e3 e2\ \  e7| |n0 q3
                        //          | |       \ \  | |
                        //          | |        \ \ | |
                        //			| v   e4    \ v| v
                        //	 ------>   ----------->   ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------   <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;
                        var e5 = q2 + 2;
                        var e6 = q2 + 3;
                        var e7 = q2 + 4;

                        halfEdges[t0].twinIndex = e5;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                        halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                        halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TriangleNegative:
                    {
                        //			 	  t0	 
                        //	 --->                      -------> 
                        //       \                  ^ * v1
                        //	 <--- \                /  ^   <-----
                        //	     \ \              / / | |
                        //	      \ \            / /  | |
                        //	       \ \      t0  / /   | |
                        //	        \ \        / /e2  | | n0
                        //           \ \      / /     | |
                        //	          \ \    / / q2 e4| |   q3
                        //             \ \  / /       | |
                        //	            v \/ v   e3   | v
                        //	 ------------>   ------->   ----->
                        //              v3 *          * v2
                        //	 <-----------    <-------   <-----  
                        //			            b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e3;

                        // vi0 / vi3
                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = t0 };

                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                        break;
                    }
                    case SegmentTopology.TrianglePositive:
                    {
                        //			 	      	 
                        //	 ------->                            ---->
                        //        v0 * \                       ^ 
                        //	 <-----     \                     /  <---
                        //			^ |^ \                   / /
                        //          | | \ \                 / /
                        //          | |  \ \               / /
                        //          | |   \ \ t0          / /
                        //   q1     | | e2 \ \           / /
                        //          | |     \ \         / /
                        //	      p0| |e3    \ \       / /
                        //          | |   q2  \ \     / /
                        //          | |        \ \   / /
                        //			| v   e4    \ v / v 
                        //	 ------>   ----------->    ----->
                        //        v3 *              * v2
                        //	 <-----    <-----------    <-----  
                        //			        b0    
                        //

                        var q2 = edgeIndices[(n * 2) + 0];
                        var e2 = q2 - 1;
                        var e3 = q2 + 0;
                        var e4 = q2 + 1;

                        halfEdges[t0].twinIndex = e2;
                        halfEdges[b0].twinIndex = e4;

                        halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                        halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                        // vi1 / vi2
                        halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                        break;
                    }
                }
            }

            brushMesh.polygons = polygons;
            brushMesh.halfEdges = halfEdges;
            brushMesh.vertices = vertices;
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
            return true;
        }

        static void CreateExtrudedSubMesh(ref BrushMesh brushMesh, Vector3[] sideVertices, Vector3 extrusion, int[] segmentDescriptionIndices, int[] segmentAssetIndices, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            const float distanceEpsilon = 0.0000001f;
            for (int i = sideVertices.Length - 1; i >= 0; i--)
            {
                var j = (i - 1 + sideVertices.Length) % sideVertices.Length;
                var magnitude = (sideVertices[j] - sideVertices[i]).sqrMagnitude;
                if (magnitude < distanceEpsilon)
                {
                    // TODO: improve on this
                    var tmp = sideVertices.ToList();
                    tmp.RemoveAt(i);
                    sideVertices = tmp.ToArray();
                }
            }

            var segments		= sideVertices.Length;
            var isSegmentConvex = new sbyte[segments];
            var edgeIndices		= new int[segments * 2];
            var vertices		= new Vector3[segments * 2];

            var polygonCount	= 2;
            var edgeOffset		= segments + segments;
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                //			 	  t0	 
                //	 ------>   ------->   -----> 
                //        v0 *          * v1
                //	 <-----    <-------   <-----
                //			^ |   e2   ^ |
                //          | |        | |
                //	   q1 p0| |e3 q2 e5| |n0 q3
                //          | |        | |
                //			| v   e4   | v
                //	 ------>   ------->   ----->
                //        v3 *          * v2
                //	 <-----    <-------   <-----  
                //			      b0    
                //

                var v0 = sideVertices[p];
                var v1 = sideVertices[e];
                var v2 = sideVertices[e] + extrusion;
                var v3 = sideVertices[p] + extrusion;

                vertices[p] = v0;
                vertices[e] = v1;
                vertices[e + segments] = v2;
                vertices[p + segments] = v3;

                var plane = new Plane(v0, v1, v3);
                var dist = plane.GetDistanceToPoint(v2);

                if (UnityEngine.Mathf.Abs(dist) < 0.001f)
                    isSegmentConvex[n] = 0;
                else
                    isSegmentConvex[n] = (sbyte)UnityEngine.Mathf.Sign(dist);

                edgeIndices[(n * 2) + 0] = edgeOffset + 1;

                if (isSegmentConvex[n] == 0)
                {
                    polygonCount++;
                    edgeOffset += 4;
                } else
                {
                    polygonCount += 2;
                    edgeOffset += 6;
                }
                edgeIndices[(n * 2) + 1] = edgeOffset - 1;
            }

            var polygons = new BrushMesh.Polygon[polygonCount];
            
            var descriptionIndex0 = (segmentDescriptionIndices == null) ? 0 : (segmentDescriptionIndices[0]);
            var descriptionIndex1 = (segmentDescriptionIndices == null) ? 1 : (segmentDescriptionIndices[1]);
            var assetIndex0		  = (segmentAssetIndices       == null) ? 0 : (segmentAssetIndices[0]);
            var assetIndex1		  = (segmentAssetIndices       == null) ? 1 : (segmentAssetIndices[1]);
            var surfaceDescription0 = surfaceDescriptions[descriptionIndex0];
            var surfaceDescription1 = surfaceDescriptions[descriptionIndex1];
            var brushMaterial0 = brushMaterials[assetIndex0];
            var brushMaterial1 = brushMaterials[assetIndex1];

            polygons[0] = new BrushMesh.Polygon { surfaceID = 0, firstEdge =        0, edgeCount = segments, description = surfaceDescription0, brushMaterial = brushMaterial0 };
            polygons[1] = new BrushMesh.Polygon { surfaceID = 1, firstEdge = segments, edgeCount = segments, description = surfaceDescription1, brushMaterial = brushMaterial1 };

            for (int s = 0, surfaceID = 2; s < segments; s++)
            {
                var descriptionIndex = (segmentDescriptionIndices == null) ? s + 2 : (segmentDescriptionIndices[s + 2]);
                var assetIndex		 = (segmentAssetIndices       == null) ? 2     : (segmentAssetIndices[s + 2]);
                var firstEdge		 = edgeIndices[(s * 2) + 0] - 1;
                if (isSegmentConvex[s] == 0)
                {
                    polygons[surfaceID] = new BrushMesh.Polygon { surfaceID = surfaceID, firstEdge = firstEdge, edgeCount = 4, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                    polygons[surfaceID].description.smoothingGroup = (uint)0;
                    surfaceID++;
                } else
                {
                    var smoothingGroup = surfaceID + 1; // TODO: create an unique smoothing group for faceted surfaces that are split in two, 
                                                        //			unless there's already a smoothing group for this edge; then use that

                    polygons[surfaceID + 0] = new BrushMesh.Polygon { surfaceID = surfaceID + 0, firstEdge = firstEdge, edgeCount = 3, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                    polygons[surfaceID + 0].description.smoothingGroup = (uint)smoothingGroup;
                    polygons[surfaceID + 1] = new BrushMesh.Polygon { surfaceID = surfaceID + 1, firstEdge = firstEdge + 3, edgeCount = 3, description = surfaceDescriptions[descriptionIndex], brushMaterial = brushMaterials[assetIndex] };
                    polygons[surfaceID + 1].description.smoothingGroup = (uint)smoothingGroup;
                    surfaceID += 2;
                }
            }

            var halfEdges = new BrushMesh.HalfEdge[edgeOffset];
            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi1 = e; 
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                halfEdges[t0] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = -1 };
                halfEdges[b0] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = -1 };
            }

            for (int p = segments - 2, e = segments - 1, n = 0; n < segments; p = e, e = n, n++)
            {
                var vi0 = p;
                var vi1 = e;
                var vi2 = e + segments;
                var vi3 = p + segments;

                var t0 = e;
                var b0 = ((segments - 1) - e) + segments;

                var p0 = edgeIndices[(p * 2) + 1];
                var n0 = edgeIndices[(n * 2) + 0];

                if (isSegmentConvex[e] == 0)
                {
                    //			 	  t0	 
                    //	 ------>   ------->   -----> 
                    //        v0 *          * v1
                    //	 <-----    <-------   <-----
                    //			^ |   e2   ^ |
                    //          | |        | |
                    //	   q1 p0| |e3 q2 e5| |n0 q3
                    //          | |        | |
                    //			| v   e4   | v
                    //	 ------>   ------->   ----->
                    //        v3 *          * v2
                    //	 <-----    <-------   <-----  
                    //			      b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;

                    halfEdges[t0].twinIndex = e2;
                    halfEdges[b0].twinIndex = e4;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };

                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                } else
                if (isSegmentConvex[e] == -1)
                {
                    //			 	    t0	 
                    //	 ------>   ----------->   -----> 
                    //        v0 *              * v1
                    //	 <-----    <-----------   <-----
                    //			^ |     e2   ^ ^ |
                    //          | |        / / | |
                    //          | |    e4/ /   | |
                    //	   q1 p0| |e3   / /  e7| |n0 q3
                    //          | |   / /e5    | |
                    //          | | / /        | |
                    //			| v/v   e6     | v
                    //	 ------>   ----------->   ----->
                    //        v3 *              * v2
                    //	 <-----    <-----------   <-----  
                    //			        b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;
                    var e6 = q2 + 3;
                    var e7 = q2 + 4;

                    halfEdges[t0].twinIndex = e2;
                    halfEdges[b0].twinIndex = e6;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = e5 };

                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = e4 };
                    halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };
                    halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                } else
                if (isSegmentConvex[e] == 1)
                {
                    //			 	    t0	 
                    //	 ------>   ----------->   -----> 
                    //        v0 *              * v1
                    //	 <-----    <-----------   <-----
                    //			^ |^\   e5     ^ |
                    //          | | \ \        | |
                    //          | |   \ \ e6   | |
                    //	   q1 p0| |e3 e2\ \  e7| |n0 q3
                    //          | |       \ \  | |
                    //          | |        \ \ | |
                    //			| v   e4    \ v| v
                    //	 ------>   ----------->   ----->
                    //        v3 *              * v2
                    //	 <-----    <-----------   <-----  
                    //			        b0    
                    //

                    var q2 = edgeIndices[(e * 2) + 0];
                    var e2 = q2 - 1;
                    var e3 = q2 + 0;
                    var e4 = q2 + 1;
                    var e5 = q2 + 2;
                    var e6 = q2 + 3;
                    var e7 = q2 + 4;

                    halfEdges[t0].twinIndex = e5;
                    halfEdges[b0].twinIndex = e4;

                    halfEdges[e2] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = e6 };
                    halfEdges[e3] = new BrushMesh.HalfEdge { vertexIndex = vi3, twinIndex = p0 };
                    halfEdges[e4] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = b0 };

                    halfEdges[e5] = new BrushMesh.HalfEdge { vertexIndex = vi0, twinIndex = t0 };
                    halfEdges[e6] = new BrushMesh.HalfEdge { vertexIndex = vi2, twinIndex = e2 };
                    halfEdges[e7] = new BrushMesh.HalfEdge { vertexIndex = vi1, twinIndex = n0 };
                }
            }

            brushMesh.polygons	= polygons;
            brushMesh.halfEdges	= halfEdges;
            brushMesh.vertices	= vertices;
            if (!brushMesh.Validate(logErrors: true))
                brushMesh.Clear();
        }
}
}