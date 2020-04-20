using System;
using System.Linq;
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
using Unity.Mathematics;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        static float CalculateOrientation(Vector2[] vertices)
        {        
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var direction = 0.0f;
            var prevVertex	= vertices[vertices.Length - 1];
            for (int n = 0; n < vertices.Length; n++)
            {
                var currVertex = vertices[n];
                direction += ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                prevVertex = currVertex;
            }
            return direction;
        }

        public static bool GenerateExtrudedShape(ref ChiselBrushContainer brushContainer, ref ChiselExtrudedShapeDefinition definition)
        {
            definition.Validate();

            ref readonly var shape               = ref definition.shape;
            int              curveSegments       = definition.curveSegments;

            var shapeVertices       = new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            GetPathVertices(shape, curveSegments, shapeVertices, shapeSegmentIndices);

            Vector2[][]  polygonVerticesArray;
            int[][]     polygonIndicesArray;
            if (shapeVertices.Count == 3)
            {
                polygonVerticesArray = new [] { shapeVertices.ToArray() };
                polygonIndicesArray = new [] { shapeSegmentIndices.ToArray() };

            } else
            { 
                if (!Decomposition.ConvexPartition(shapeVertices, shapeSegmentIndices,
                                                    out polygonVerticesArray,
                                                    out polygonIndicesArray))
                    return false;
            }

            ref readonly var path                = ref definition.path;

            // TODO: make each extruded quad split into two triangles when it's not a perfect plane,
            //			split it to make sure it's convex

            // TODO: make it possible to smooth (parts) of the shape

            // TODO: make materials work well
            // TODO: make it possible to 'draw' shapes on any surface

            // TODO: make path work as a spline, with subdivisions
            // TODO:	make this work well with twisted rotations
            // TODO: make shape/path subdivisions be configurable / automatic



            var brushMeshesList = new List<BrushMesh>();
            for (int p = 0; p < polygonVerticesArray.Length; p++)
            {
                var polygonVertices = polygonVerticesArray[p];
                var segmentIndices = polygonIndicesArray[p];
                var shapeSegments = polygonVertices.Length;

                if (CalculateOrientation(polygonVertices) < 0)
                {
                    Array.Reverse(segmentIndices);
                    Array.Reverse(polygonVertices);
                }


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
                    var maxAngle = math.max(math.max(offsetEuler.x, offsetEuler.y), offsetEuler.z);
                    if (maxAngle != 0)
                        subSegments = math.max(1, (int)math.ceil(maxAngle / 5));

                    if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                        (subSegments & 1) == 1)
                        subSegments += 1;

                    for (int n = 0; n < subSegments; n++)
                    {
                        var matrix0 = ChiselPathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], n / (float)subSegments);
                        var matrix1 = ChiselPathPoint.Lerp(ref path.segments[s], ref path.segments[s + 1], (n + 1) / (float)subSegments);

                        // TODO: this doesn't work if top and bottom polygons intersect
                        //			=> need to split into two brushes then, invert one of the two brushes
                        var invertDot = math.dot(matrix0.MultiplyVector(new Vector3(0,0,1)).normalized, (matrix1.MultiplyPoint(new Vector3(polygonVertices[0].x, polygonVertices[0].y, 0)) - matrix0.MultiplyPoint(new Vector3(polygonVertices[0].x, polygonVertices[0].y, 0))).normalized);

                        if (invertDot == 0.0f)
                            continue;

                        Vector3[] vertices;
                        if (invertDot < 0) { var m = matrix0; matrix0 = matrix1; matrix1 = m; }
                        if (!GetExtrudedVertices(polygonVertices, matrix0, matrix1, out vertices))
                            continue;

                        var brushMesh = new BrushMesh();
                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, shapeSegments, segmentIndices, 0, 1, vertices, definition.surfaceDefinition);
                        brushMeshesList.Add(brushMesh);
                    }
                }
            }

            brushContainer.CopyFrom(brushMeshesList);
            return true;
        }
        
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
                vertices[s] = matrix0.MultiplyPoint(new Vector3(shapeVertices[s].x, shapeVertices[s].y, 0));
                vertices[shapeSegments + s] = matrix1.MultiplyPoint(new Vector3(shapeVertices[s].x, shapeVertices[s].y, 0));
            }
            return true;
        }
    }
}