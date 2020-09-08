using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;
using Debug = UnityEngine.Debug;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: propertly encapusulate this w/ license etc.

    // From https://github.com/craftworkgames/FarseerPhysics.Portable

    // From phed rev 36: http://code.google.com/p/phed/source/browse/trunk/Polygon.cpp

    /// <summary>
    /// Convex decomposition algorithm created by Mark Bayazit (http://mnbayazit.com/)
    /// 
    /// Properties:
    /// - Tries to decompose using polygons instead of triangles.
    /// - Tends to produce optimal results with low processing time.
    /// - Running time is O(nr), n = number of vertices, r = reflex vertices.
    /// - Does not support holes.
    /// 
    /// For more information about this algorithm, see http://mnbayazit.com/406/bayazit
    /// </summary>
    internal static class BayazitDecomposer
    {
        const float kConvexTestEpsilon  = 0.00001f;
        const float kDistanceEpsilon    = 0.0006f;

        /// <summary>
        /// Gets the signed area.
        /// If the area is less than 0, it indicates that the polygon is clockwise winded.
        /// </summary>
        /// <returns>The signed area</returns>
        static float GetSignedArea(List<Vector2> vertices)
        {
            //The simplest polygon which can exist in the Euclidean plane has 3 sides.
            if (vertices.Count < 3)
                return 0;

            float area = 0;
            for (int i = 0, count = vertices.Count; i < count; i++)
            {
                int j = (i + 1) % count;

                Vector2 vi = vertices[i];
                Vector2 vj = vertices[j];

                area += vi.x * vj.y;
                area -= vi.y * vj.x;
            }
            area /= 2.0f;
            return area;
        }

        /// <summary>
        /// Indicates if the vertices are in counter clockwise order.
        /// Warning: If the area of the polygon is 0, it is unable to determine the winding.
        /// </summary>
        static bool IsCounterClockWise(List<Vector2> vertices)
        {
            //The simplest polygon which can exist in the Euclidean plane has 3 sides.
            if (vertices.Count < 3)
                return false;

            return (GetSignedArea(vertices) > 0.0f);
        }

        /// <summary>
        /// Decompose the polygon into several smaller non-concave polygon.
        /// If the polygon is already convex, it will return the original polygon, unless it is over MaxPolygonVertices.
        /// </summary>
        public static Vector2[][] ConvexPartition(List<Vector2> vertices)
        {
            Debug.Assert(vertices.Count > 3);

            if (!IsCounterClockWise(vertices))
                vertices.Reverse();

            var result = TriangulatePolygon(vertices);
            if (result == null)
                return null;
            var returnValue = new Vector2[result.Count][];
            for (int i = 0; i < result.Count; i++)
            {
                returnValue[i] = result[i].ToArray();
            }
            return returnValue;
        }

        static List<List<Vector2>> TriangulatePolygon(List<Vector2> srcVertices)
        {
            List<List<Vector2>> list    = new List<List<Vector2>>();
	        List<List<Vector2>> srcList = new List<List<Vector2>>();
	        srcList.Add(srcVertices);

            var originalVertexCount = srcVertices.Count;


            int counter = 0;
	        while (counter < srcList.Count)
	        {
		        var vertices = srcList[counter];
		        counter++;
		        if (counter > originalVertexCount)
		        {
			        Debug.LogWarning($"counter > {originalVertexCount}");
			        return null;
		        }

		        int lowerIndex = 0, upperIndex = 0;
                var lowerPoly = new List<Vector2>();
                var upperPoly = new List<Vector2>();
		        float d, lowerDist, upperDist;
		        Vector2 lowerInt = Vector2.zero, upperInt = Vector2.zero;

		        ForceCounterClockWise(vertices);

		        bool is_convex = true;

		        for (int i = 0; i < vertices.Count; ++i)
		        {
			        if (Reflex(i, vertices))
			        {
				        is_convex = false;
				        lowerDist = upperDist = float.PositiveInfinity;
				        for (int j = 0; j < vertices.Count; ++j)
				        {
					        // if line intersects with an edge
					        if (Left   (At(i - 1, vertices), At(i, vertices), At(j    , vertices)) &&
						        RightOn(At(i - 1, vertices), At(i, vertices), At(j - 1, vertices)))
					        {
						        // find the point of intersection
						        var p = LineIntersect(At(i - 1, vertices), At(i, vertices), At(j, vertices), At(j - 1, vertices));
						        if (RightOn(At(i + 1, vertices), At(i, vertices), p))
						        {
							        // make sure it's inside the poly
							        d = SquareDist(At(i, vertices), p);
							        if (d < lowerDist)
							        {
								        // keep only the closest intersection
								        lowerDist = d;
								        lowerInt = p;
								        lowerIndex = j;
							        }
						        }
					        }

					        if (Left   (At(i + 1, vertices), At(i, vertices), At(j + 1, vertices)) &&
						        RightOn(At(i + 1, vertices), At(i, vertices), At(j    , vertices)))
					        {
						        var p = LineIntersect(At(i + 1, vertices), At(i, vertices), At(j, vertices), At(j + 1, vertices));
						        if (LeftOn(At(i - 1, vertices), At(i, vertices), p))
						        {
							        d = SquareDist(At(i, vertices), p);
							        if (d < upperDist)
							        {
								        upperDist = d;
								        upperIndex = j;
								        upperInt = p;
							        }
						        }
					        }
				        }

				        // if there are no vertices to connect to, choose a Vector2 in the middle
				        if (lowerIndex == (upperIndex + 1) % vertices.Count)
				        {
					        Vector2 sp = ((lowerInt + upperInt) / 2.0f);

					        Copy(i, upperIndex, vertices, ref lowerPoly);
					        lowerPoly.Add(sp);

					        Copy(lowerIndex, i, vertices, ref upperPoly);
					        upperPoly.Add(sp);
				        } else
				        {
					        double highestScore = 0;
					        double bestIndex = lowerIndex;
					        while (upperIndex < lowerIndex)
						        upperIndex += vertices.Count;

					        for (int j = lowerIndex; j <= upperIndex; ++j)
					        {
						        if (CanSee(i, j, vertices))
						        {
							        double score = 1 / (SquareDist(At(i, vertices), At(j, vertices)) + 1);
							        if (Reflex(j, vertices))
							        {
								        if (RightOn(At(j - 1, vertices), At(j, vertices), At(i, vertices)) &&
									        LeftOn(At(j + 1, vertices), At(j, vertices), At(i, vertices)))
									        score += 3;
								        else
									        score += 2;
							        } else
								        score += 1;

							        if (score > highestScore)
							        {
								        bestIndex = j;
								        highestScore = score;
							        }
						        }
					        }
					        Copy(i, (int)(bestIndex), vertices, ref lowerPoly);
					        Copy((int)(bestIndex), i, vertices, ref upperPoly);
				        }

				        srcList.Add(lowerPoly);
				        srcList.Add(upperPoly);
				        /*
				        auto lower = ConvexPartition(lowerPoly);
				        for (auto p : lower)
					        srcList.emplace_back(p);

				        auto upper = ConvexPartition(upperPoly);
				        for (auto p : upper)
					        srcList.emplace_back(p);*/
				        break;
			        }
		        }

		        if (is_convex)
		        {
			        // polygon is already convex
			        if (Mathf.Abs(GetSignedArea(vertices)) > 0)
				        list.Add(vertices);
		        }
	        }
	        return list;
        }


        static Vector2 At(int i, List<Vector2> vertices)
        {
	        var s = vertices.Count;
	        var index = (i < 0) ? ((s + (i % s)) % s) : (i % s);
	        return vertices[index];
        }

        static void Copy(int i, int j, List<Vector2> vertices, ref List<Vector2> to)
        {
	        to.Clear();

	        var count = vertices.Count;
	        while (j < i)
		        j += count;

	        for (; i <= j; ++i)
		        to.Add(At(i, vertices));
        }

        static bool AlmostZero(float value1)
        {
	        return Mathf.Abs(value1) <= kConvexTestEpsilon;
        }


        // From Mark Bayazit's convex decomposition algorithm
        static Vector2 LineIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
	        var i = Vector2.zero;
	        var a1 = p2.y - p1.y;
            var b1 = p1.x - p2.x;
            var c1 = a1 * p1.x + b1 * p1.y;
            var a2 = q2.y - q1.y;
            var b2 = q1.x - q2.x;
            var c2 = a2 * q1.x + b2 * q1.y;
            var det = a1 * b2 - a2 * b1;

	        if (!AlmostZero(det)) 
	        {
		        // lines are not parallel
		        i.x = (b2 * c1 - b1 * c2) / det;
		        i.y = (a1 * c2 - a2 * c1) / det;
	        }
	        return i;
        }

        // From Eric Jordan's convex decomposition library, it checks if the lines a0->a1 and b0->b1 cross.
        // Grazing lines should not return true.
        static bool IsLineIntersecting(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
	        //intersectionPoint = Vector2.zero;

	        if (a0 == b0 || a0 == b1 || a1 == b0 || a1 == b1)
		        return false;

            var x1 = a0.x;
            var x2 = a1.x;
            var x3 = b0.x;
            var x4 = b1.x;

	        //AABB early exit
	        if (Mathf.Max(x1, x2) < Mathf.Min(x3, x4) || Mathf.Max(x3, x4) < Mathf.Min(x1, x2))
		        return false;

            var y1 = a0.y;
            var y2 = a1.y;
            var y3 = b0.y;
	        var y4 = b1.y;

	        if (Mathf.Max(y1, y2) < Mathf.Min(y3, y4) || Mathf.Max(y3, y4) < Mathf.Min(y1, y2))
		        return false;

            var denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
	        if (Mathf.Abs(denom) < kDistanceEpsilon)
	        {
		        //Lines are too close to parallel to call
		        return false;
	        }

            var ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
            var ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denom;

	        if ((0 < ua) && (ua < 1) && (0 < ub) && (ub < 1))
	        {
		        //intersectionPoint.x = (x1 + ua * (x2 - x1));
		        //intersectionPoint.y = (y1 + ua * (y2 - y1));
		        return true;
	        }

	        return false;
        }

        private static bool CanSee(int i, int j, List<Vector2> vertices)
        {
            if (Reflex(i, vertices))
            {
                if (LeftOn(At(i, vertices), At(i - 1, vertices), At(j, vertices)) &&
                    RightOn(At(i, vertices), At(i + 1, vertices), At(j, vertices))) return false;
            } else
            {
                if (RightOn(At(i, vertices), At(i + 1, vertices), At(j, vertices)) ||
                    LeftOn(At(i, vertices), At(i - 1, vertices), At(j, vertices))) return false;
            }
            if (Reflex(j, vertices))
            {
                if (LeftOn(At(j, vertices), At(j - 1, vertices), At(i, vertices)) &&
                    RightOn(At(j, vertices), At(j + 1, vertices), At(i, vertices))) return false;
            } else
            {
                if (RightOn(At(j, vertices), At(j + 1, vertices), At(i, vertices)) ||
                    LeftOn(At(j, vertices), At(j - 1, vertices), At(i, vertices))) return false;
            }
            for (int k = 0, count = vertices.Count; k < count; ++k)
            {
                if ((k + 1) % count == i || k == i || (k + 1) % count == j || k == j)
                    continue; // ignore incident edges

                if (IsLineIntersecting(At(i, vertices), At(j, vertices), At(k, vertices), At(k + 1, vertices)))
                    return false;
            }
            return true;
        }

        private static bool Reflex(int i, List<Vector2> vertices)
        {
            return Right(i, vertices);
        }

        private static bool Right(int i, List<Vector2> vertices)
        {
	        return Right(At(i - 1, vertices), At(i, vertices), At(i + 1, vertices));
        }

        /// <summary>
        /// Returns a positive number if c is to the left of the line going from a to b.
        /// </summary>
        /// <returns>Positive number if point is left, negative if point is right, 
        /// and 0 if points are collinear.</returns>
        private static float Area(Vector2 a, Vector2 b, Vector2 c)
        {
            return a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y);
        }

        private static bool Left(Vector2 a, Vector2 b, Vector2 c)
        {
	        return Area(a, b, c) > 0;
        }

        private static bool LeftOn(Vector2 a, Vector2 b, Vector2 c)
        {
	        return Area(a, b, c) >= 0;
        }

        private static bool Right(Vector2 a, Vector2 b, Vector2 c)
        {
	        return Area(a, b, c) < 0;
        }

        private static bool RightOn(Vector2 a, Vector2 b, Vector2 c)
        {
	        return Area(a, b, c) <= 0;
        }

        private static float SquareDist(Vector2 a, Vector2 b)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            return dx * dx + dy * dy;
        }

        //forces counter clock wise order.
        private static void ForceCounterClockWise(List<Vector2> vertices)
        {
	        if (!IsCounterClockWise(vertices))
	        {
		        vertices.Reverse();
	        }
        }
    }
}
