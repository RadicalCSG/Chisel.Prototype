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

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GeneratePathedStairs(ref ChiselBrushContainer brushContainer, ref ChiselPathedStairsDefinition definition)
        {
            definition.Validate();

            var shapeVertices		= new List<Vector2>();
            var shapeSegmentIndices = new List<int>();
            GetPathVertices(definition.shape, definition.curveSegments, shapeVertices, shapeSegmentIndices);

            var totalSubMeshCount = 0;
            for (int i = 0; i < shapeVertices.Count; i++)
            {
                if (i == 0 && !definition.shape.closed)
                    continue;

                var leftSide  = (!definition.shape.closed && i ==                       1) ? definition.stairs.leftSide  : StairsSideType.None;
                var rightSide = (!definition.shape.closed && i == shapeVertices.Count - 1) ? definition.stairs.rightSide : StairsSideType.None;

                totalSubMeshCount += BrushMeshFactory.GetLinearStairsSubMeshCount(definition.stairs, leftSide, rightSide);
            }
            if (totalSubMeshCount == 0)
                return false;

            //			var stairDirections = definition.shape.closed ? shapeVertices.Count : (shapeVertices.Count - 1);

            brushContainer.EnsureSize(totalSubMeshCount);

            var depth		= definition.stairs.absDepth;
            var height		= definition.stairs.absHeight;

            var halfDepth	= depth  * 0.5f;
            var halfHeight	= height * 0.5f;

            int subMeshIndex = 0;
            for (int vi0 = shapeVertices.Count - 3, vi1 = shapeVertices.Count - 2, vi2 = shapeVertices.Count - 1, vi3 = 0; vi3 < shapeVertices.Count; vi0 = vi1, vi1 = vi2, vi2 = vi3, vi3++)
            {
                if (vi2 == 0 && !definition.shape.closed)
                    continue;

                // TODO: optimize this, we're probably redoing a lot of stuff for every iteration
                var v0 = shapeVertices[vi0];
                var v1 = shapeVertices[vi1];
                var v2 = shapeVertices[vi2];
                var v3 = shapeVertices[vi3];

                var m0 = (v0 + v1) * 0.5f;
                var m1 = (v1 + v2) * 0.5f;
                var m2 = (v2 + v3) * 0.5f;

                var d0 = (v1 - v0);
                var d1 = (v2 - v1);
                var d2 = (v3 - v2);

                var maxWidth0 = d0.magnitude;
                var maxWidth1 = d1.magnitude;
                var maxWidth2 = d2.magnitude;
                var halfWidth1 = d1 * 0.5f;

                d0 /= maxWidth0;
                d1 /= maxWidth1;
                d2 /= maxWidth2;

                var depthVector = new Vector3(d1.y, 0, -d1.x);
                var lineCenter  = new Vector3(m1.x, halfHeight, m1.y) - (depthVector * halfDepth);

                var depthVector0 = new Vector2(d0.y, -d0.x) * depth;
                var depthVector1 = new Vector2(d1.y, -d1.x) * depth;
                var depthVector2 = new Vector2(d2.y, -d2.x) * depth;

                m0 -= depthVector0;
                m1 -= depthVector1;
                m2 -= depthVector2;

                Vector2 output;
                var leftShear	= Intersect(m1, d1, m0, d0, out output) ?  Vector2.Dot(d1, (output - (m1 - halfWidth1))) : 0;
                var rightShear	= Intersect(m1, d1, m2, d2, out output) ? -Vector2.Dot(d1, (output - (m1 + halfWidth1))) : 0;

                var transform = Matrix4x4.TRS(lineCenter, // move to center of line
                                              Quaternion.LookRotation(depthVector, Vector3.up),	// rotate to align with line
                                              Vector3.one);
                
                // set the width to the width of the line
                definition.stairs.width = maxWidth1 * (definition.stairs.width < 0 ? -1 : 1);
                definition.stairs.nosingWidth = 0;

                var leftSide  = (!definition.shape.closed && vi2 ==                       1) ? definition.stairs.leftSide  : StairsSideType.None;
                var rightSide = (!definition.shape.closed && vi2 == shapeVertices.Count - 1) ? definition.stairs.rightSide : StairsSideType.None;
                var subMeshCount = BrushMeshFactory.GetLinearStairsSubMeshCount(definition.stairs, leftSide, rightSide);
                if (subMeshCount == 0)
                    continue;

                if (!BrushMeshFactory.GenerateLinearStairsSubMeshes(ref brushContainer, definition.stairs, leftSide, rightSide, subMeshIndex))
                    return false;

                var halfWidth = maxWidth1 * 0.5f;
                for (int m = 0; m < subMeshCount; m++)
                {
                    var vertices = brushContainer.brushMeshes[subMeshIndex + m].vertices;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        // TODO: is it possible to put all of this in a single matrix?
                        // lerp the stairs to go from less wide to wider depending on the depth of the vertex
                        var depthFactor = 1.0f - ((vertices[v].z / depth) + 0.5f);
                        var wideFactor  = (vertices[v].x / halfWidth) + 0.5f;
                        var scale		= (vertices[v].x / halfWidth);

                        // lerp the stairs width depending on if it's on the left or right side of the stairs
                        vertices[v].x = Mathf.Lerp( scale * (halfWidth - (rightShear * depthFactor)),
                                                    scale * (halfWidth - (leftShear  * depthFactor)),
                                                    wideFactor);
                        vertices[v] = transform.MultiplyPoint(vertices[v]);
                    }
                }

                subMeshIndex += subMeshCount;
            }
            return false;
        }


        // TODO: move somewhere else
        static bool Intersect(Vector2 p1, Vector2 d1,
                                     Vector2 p2, Vector2 d2, out Vector2 intersection)
        {
            const float kEpsilon = 0.0001f;

            var f = d1.y * d2.x - d1.x * d2.y;
            // check if the rays are parallel
            if (f >= -kEpsilon && f <= kEpsilon)
            {
                intersection = Vector2.zero;
                return false;
            }

            var c0 = p1 - p2;
            var t = (d2.y * c0.x - d2.x * c0.y) / f;
            intersection = p1 + (t * d1);
            return true;
        }

    }
}