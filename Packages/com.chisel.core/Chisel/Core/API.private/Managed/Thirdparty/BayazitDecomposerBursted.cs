using System;
//using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;
//using UnityEngine;
//using Vector2 = UnityEngine.Vector2;
using Debug = UnityEngine.Debug;
using System.Runtime.CompilerServices;
using Unity.Burst;
using UnitySceneExtensions;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core.External
{
    // TODO: propertly encapusulate this w/ license etc.

    // From https://github.com/craftworkgames/FarseerPhysics.Portable

    // From phed rev 36: http://code.google.com/p/phed/source/browse/trunk/Polygon.cpp

    // Modified to work with Burst / Optimized

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
    internal static class BayazitDecomposerBursted
    {
        const float kConvexTestEpsilon = 0.00001f;
        const float kDistanceEpsilon = 0.0006f;

        /// <summary>
        /// Decompose the polygon into several smaller non-concave polygon.
        /// If the polygon is already convex, it will return the original polygon, unless it is over MaxPolygonVertices.
        /// </summary>
        [BurstCompile]
        public static bool ConvexPartition(NativeList<SegmentVertex> srcVertices, NativeList<SegmentVertex> outputVertices, NativeList<int> outputRanges, int defaultSegment = 0)
        {
            Debug.Assert(srcVertices.Length > 3);

            var allVertices = new NativeList<SegmentVertex>(Allocator.Temp);
            var ranges      = new NativeList<Range>(Allocator.Temp);

            try
            {
                allVertices.AddRange(srcVertices);
                ranges.Add(new Range { start = 0, end = srcVertices.Length });

                var originalVertexCount = srcVertices.Length * 2;

                int counter = 0;
                while (counter < ranges.Length)
                {
                    var vertices = allVertices;
                    var range = ranges[counter];

                    counter++;
                    if (counter > originalVertexCount)
                    {
                        Debug.LogWarning($"counter > {originalVertexCount}");
                        return false;
                    }

                    var count = range.Length;
                    if (count == 0)
                        continue;

                    ForceCounterClockWise(vertices, range);

                    int reflexIndex;
                    for (reflexIndex = 0; reflexIndex < count; ++reflexIndex)
                    {
                        if (Reflex(reflexIndex, vertices, range))
                            break;
                    }

                    // Check if polygon is already convex
                    if (reflexIndex == count)
                    {
                        // Check if polygon is degenerate, in which case we skip it
                        if (math.abs(GetSignedDoubleArea(vertices, range)) <= 0.0f)
                            continue;

                        var desiredCapacity = outputVertices.Length + range.Length;
                        if (outputVertices.Capacity < desiredCapacity)
                            outputVertices.Capacity = desiredCapacity;
                        for (int n = range.start; n < range.end; n++)
                            outputVertices.Add(vertices[n]);
                        outputRanges.Add(outputVertices.Length);
                        continue;
                    }

                    int i = reflexIndex;

                    var lowerDist = float.PositiveInfinity;
                    var lowerInt = float2.zero;
                    var lowerIndex = 0;

                    var upperDist = float.PositiveInfinity;
                    var upperInt = float2.zero;
                    var upperIndex = 0;
                    for (int j = 0; j < count; ++j)
                    {
                        // if line intersects with an edge
                        if (Left   (At(i - 1, vertices, range).position, At(i, vertices, range).position, At(j    , vertices, range).position) &&
                            RightOn(At(i - 1, vertices, range).position, At(i, vertices, range).position, At(j - 1, vertices, range).position))
                        {
                            // find the point of intersection
                            var p = LineIntersect(At(i - 1, vertices, range).position, 
                                                  At(i    , vertices, range).position, 
                                                  At(j    , vertices, range).position, 
                                                  At(j - 1, vertices, range).position);
                            if (RightOn(At(i + 1, vertices, range).position, 
                                        At(i    , vertices, range).position, p))
                            {
                                // make sure it's inside the poly
                                var d = SquareDist(At(i, vertices, range).position, p);
                                if (d < lowerDist)
                                {
                                    // keep only the closest intersection
                                    lowerDist = d;
                                    lowerInt = p;
                                    lowerIndex = j;
                                }
                            }
                        }

                        if (Left   (At(i + 1, vertices, range).position, At(i, vertices, range).position, At(j + 1, vertices, range).position) &&
                            RightOn(At(i + 1, vertices, range).position, At(i, vertices, range).position, At(j    , vertices, range).position))
                        {
                            var p = LineIntersect(At(i + 1, vertices, range).position, 
                                                  At(i    , vertices, range).position, 
                                                  At(j    , vertices, range).position, 
                                                  At(j + 1, vertices, range).position);
                            if (LeftOn(At(i - 1, vertices, range).position, 
                                       At(i    , vertices, range).position, p))
                            {
                                var d = SquareDist(At(i, vertices, range).position, p);
                                if (d < upperDist)
                                {
                                    upperDist = d;
                                    upperIndex = j;
                                    upperInt = p;
                                }
                            }
                        }
                    }


                    // if there are no vertices to connect to, choose a float2 in the middle
                    if (lowerIndex == (upperIndex + 1) % count)
                    {
                        var sp = ((lowerInt + upperInt) / 2.0f);

                        var newRange = Copy(i, upperIndex, vertices, range, allVertices);
                        allVertices.Add(new SegmentVertex { position = sp, segmentIndex = defaultSegment });
                        newRange.end++;
                        ranges.Add(newRange);

                        newRange = Copy(lowerIndex, i, vertices, range, allVertices);
                        allVertices.Add(new SegmentVertex { position = sp, segmentIndex = defaultSegment });
                        newRange.end++;
                        ranges.Add(newRange);
                    } else
                    {
                        double highestScore = 0;
                        double bestIndex = lowerIndex;
                        while (upperIndex < lowerIndex)
                            upperIndex += count;

                        for (int j = lowerIndex; j <= upperIndex; ++j)
                        {
                            if (!CanSee(i, j, vertices, range))
                                continue;

                            double score = 1 / (SquareDist(At(i, vertices, range).position, 
                                                           At(j, vertices, range).position) + 1);
                            if (Reflex(j, vertices, range))
                            {
                                if (RightOn(At(j - 1, vertices, range).position, At(j, vertices, range).position, At(i, vertices, range).position) &&
                                    LeftOn (At(j + 1, vertices, range).position, At(j, vertices, range).position, At(i, vertices, range).position))
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

                        ranges.Add(Copy(i, (int)(bestIndex), vertices, range, allVertices));
                        ranges.Add(Copy((int)(bestIndex), i, vertices, range, allVertices));
                    }

                }
                return outputRanges.Length > 0;
            }
            finally
            {
                allVertices.Dispose();
                ranges.Dispose();
            }
        }
        

        /// <summary>
        /// Decompose the polygon into several smaller non-concave polygon.
        /// If the polygon is already convex, it will return the original polygon, unless it is over MaxPolygonVertices.
        /// </summary>
        [BurstCompile]
        public static bool ConvexPartition(NativeList<SegmentVertex> srcVertices, ref UnsafeList<SegmentVertex> outputVertices, ref UnsafeList<int> outputRanges, int defaultSegment = 0)
        {
            Debug.Assert(srcVertices.Length > 3);

            var allVertices = new NativeList<SegmentVertex>(Allocator.Temp);
            var ranges      = new NativeList<Range>(Allocator.Temp);

            try
            {
                allVertices.AddRange(srcVertices);
                ranges.Add(new Range { start = 0, end = srcVertices.Length });

                var originalVertexCount = srcVertices.Length * 2;

                int counter = 0;
                while (counter < ranges.Length)
                {
                    var vertices = allVertices;
                    var range = ranges[counter];

                    counter++;
                    if (counter > originalVertexCount)
                    {
                        Debug.LogWarning($"counter > {originalVertexCount}");
                        return false;
                    }

                    var count = range.Length;
                    if (count == 0)
                        continue;

                    ForceCounterClockWise(vertices, range);

                    int reflexIndex;
                    for (reflexIndex = 0; reflexIndex < count; ++reflexIndex)
                    {
                        if (Reflex(reflexIndex, vertices, range))
                            break;
                    }

                    // Check if polygon is already convex
                    if (reflexIndex == count)
                    {
                        // Check if polygon is degenerate, in which case we skip it
                        if (math.abs(GetSignedDoubleArea(vertices, range)) <= 0.0f)
                            continue;

                        var desiredCapacity = outputVertices.Length + range.Length;
                        if (outputVertices.Capacity < desiredCapacity)
                            outputVertices.Capacity = desiredCapacity;
                        for (int n = range.start; n < range.end; n++)
                            outputVertices.Add(vertices[n]);
                        outputRanges.Add(outputVertices.Length);
                        continue;
                    }

                    int i = reflexIndex;

                    var lowerDist = float.PositiveInfinity;
                    var lowerInt = float2.zero;
                    var lowerIndex = 0;

                    var upperDist = float.PositiveInfinity;
                    var upperInt = float2.zero;
                    var upperIndex = 0;
                    for (int j = 0; j < count; ++j)
                    {
                        // if line intersects with an edge
                        if (Left   (At(i - 1, vertices, range).position, At(i, vertices, range).position, At(j    , vertices, range).position) &&
                            RightOn(At(i - 1, vertices, range).position, At(i, vertices, range).position, At(j - 1, vertices, range).position))
                        {
                            // find the point of intersection
                            var p = LineIntersect(At(i - 1, vertices, range).position, 
                                                  At(i    , vertices, range).position, 
                                                  At(j    , vertices, range).position, 
                                                  At(j - 1, vertices, range).position);
                            if (RightOn(At(i + 1, vertices, range).position, 
                                        At(i    , vertices, range).position, p))
                            {
                                // make sure it's inside the poly
                                var d = SquareDist(At(i, vertices, range).position, p);
                                if (d < lowerDist)
                                {
                                    // keep only the closest intersection
                                    lowerDist = d;
                                    lowerInt = p;
                                    lowerIndex = j;
                                }
                            }
                        }

                        if (Left   (At(i + 1, vertices, range).position, At(i, vertices, range).position, At(j + 1, vertices, range).position) &&
                            RightOn(At(i + 1, vertices, range).position, At(i, vertices, range).position, At(j    , vertices, range).position))
                        {
                            var p = LineIntersect(At(i + 1, vertices, range).position, 
                                                  At(i    , vertices, range).position, 
                                                  At(j    , vertices, range).position, 
                                                  At(j + 1, vertices, range).position);
                            if (LeftOn(At(i - 1, vertices, range).position, 
                                       At(i    , vertices, range).position, p))
                            {
                                var d = SquareDist(At(i, vertices, range).position, p);
                                if (d < upperDist)
                                {
                                    upperDist = d;
                                    upperIndex = j;
                                    upperInt = p;
                                }
                            }
                        }
                    }


                    // if there are no vertices to connect to, choose a float2 in the middle
                    if (lowerIndex == (upperIndex + 1) % count)
                    {
                        var sp = ((lowerInt + upperInt) / 2.0f);

                        var newRange = Copy(i, upperIndex, vertices, range, allVertices);
                        allVertices.Add(new SegmentVertex { position = sp, segmentIndex = defaultSegment });
                        newRange.end++;
                        ranges.Add(newRange);

                        newRange = Copy(lowerIndex, i, vertices, range, allVertices);
                        allVertices.Add(new SegmentVertex { position = sp, segmentIndex = defaultSegment });
                        newRange.end++;
                        ranges.Add(newRange);
                    } else
                    {
                        double highestScore = 0;
                        double bestIndex = lowerIndex;
                        while (upperIndex < lowerIndex)
                            upperIndex += count;

                        for (int j = lowerIndex; j <= upperIndex; ++j)
                        {
                            if (!CanSee(i, j, vertices, range))
                                continue;

                            double score = 1 / (SquareDist(At(i, vertices, range).position, 
                                                           At(j, vertices, range).position) + 1);
                            if (Reflex(j, vertices, range))
                            {
                                if (RightOn(At(j - 1, vertices, range).position, At(j, vertices, range).position, At(i, vertices, range).position) &&
                                    LeftOn (At(j + 1, vertices, range).position, At(j, vertices, range).position, At(i, vertices, range).position))
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

                        ranges.Add(Copy(i, (int)(bestIndex), vertices, range, allVertices));
                        ranges.Add(Copy((int)(bestIndex), i, vertices, range, allVertices));
                    }

                }
                return outputRanges.Length > 0;
            }
            finally
            {
                allVertices.Dispose();
                ranges.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static SegmentVertex At(int i, NativeList<SegmentVertex> vertices, Range range)
        {
            var s = range.Length;
            var index = (i < 0) ? ((s + (i % s)) % s) : (i % s);
            return vertices[range.start + index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool AlmostZero(float value1)
        {
            return math.abs(value1) <= kConvexTestEpsilon;
        }

        // From Mark Bayazit's convex decomposition algorithm
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float2 LineIntersect(float2 p1, float2 p2, float2 q1, float2 q2)
        {
            var i = float2.zero;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool AreLinesIntersecting(float2 a0, float2 a1, float2 b0, float2 b1)
        {
            if (math.all(a0 == b0) || math.all(a0 == b1) || math.all(a1 == b0) || math.all(a1 == b1))
                return false;

            var x1 = a0.x;
            var x2 = a1.x;
            var x3 = b0.x;
            var x4 = b1.x;

            // AABB early exit
            if (math.max(x1, x2) < math.min(x3, x4) || math.max(x3, x4) < math.min(x1, x2))
                return false;

            var y1 = a0.y;
            var y2 = a1.y;
            var y3 = b0.y;
            var y4 = b1.y;

            if (math.max(y1, y2) < math.min(y3, y4) || math.max(y3, y4) < math.min(y1, y2))
                return false;

            var denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);
            if (math.abs(denom) < kDistanceEpsilon)
            {
                // Lines are too close to parallel to call
                return false;
            }

            var ua = ((x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)) / denom;
            var ub = ((x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)) / denom;

            return (0 < ua) && (ua < 1) && (0 < ub) && (ub < 1);
        }

        private static bool CanSee(int i, int j, NativeList<SegmentVertex> vertices, Range range)
        {
            if (Reflex(i, vertices, range))
            {
                if (LeftOn (At(i, vertices, range).position, At(i - 1, vertices, range).position, At(j, vertices, range).position) &&
                    RightOn(At(i, vertices, range).position, At(i + 1, vertices, range).position, At(j, vertices, range).position)) return false;
            } else
            {
                if (RightOn(At(i, vertices, range).position, At(i + 1, vertices, range).position, At(j, vertices, range).position) ||
                    LeftOn (At(i, vertices, range).position, At(i - 1, vertices, range).position, At(j, vertices, range).position)) return false;
            }
            if (Reflex(j, vertices, range))
            {
                if (LeftOn (At(j, vertices, range).position, At(j - 1, vertices, range).position, At(i, vertices, range).position) &&
                    RightOn(At(j, vertices, range).position, At(j + 1, vertices, range).position, At(i, vertices, range).position)) return false;
            } else
            {
                if (RightOn(At(j, vertices, range).position, At(j + 1, vertices, range).position, At(i, vertices, range).position) ||
                    LeftOn (At(j, vertices, range).position, At(j - 1, vertices, range).position, At(i, vertices, range).position)) return false;
            }

            for (int k = 0, count = range.Length; k < count; ++k)
            {
                if ((k + 1) % count == i || k == i || (k + 1) % count == j || k == j)
                    continue; // ignore incident edges

                if (AreLinesIntersecting(At(i    , vertices, range).position, 
                                         At(j    , vertices, range).position, 
                                         At(k    , vertices, range).position, 
                                         At(k + 1, vertices, range).position))
                    return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Reflex(int i, NativeList<SegmentVertex> vertices, Range range)
        {
            return Right(i, vertices, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Right(int i, NativeList<SegmentVertex> vertices, Range range)
        {
            return Right(At(i - 1, vertices, range).position,
                         At(i    , vertices, range).position,
                         At(i + 1, vertices, range).position);
        }

        /// <summary>
        /// Returns a positive number if c is to the left of the line going from a to b.
        /// </summary>
        /// <returns>Positive number if point is left, negative if point is right, 
        /// and 0 if points are collinear.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Area(float2 a, float2 b, float2 c)
        {
            return a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Left(float2 a, float2 b, float2 c)
        {
            return Area(a, b, c) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool LeftOn(float2 a, float2 b, float2 c)
        {
            return Area(a, b, c) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Right(float2 a, float2 b, float2 c)
        {
            return Area(a, b, c) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RightOn(float2 a, float2 b, float2 c)
        {
            return Area(a, b, c) <= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SquareDist(float2 a, float2 b)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Gets the signed area.
        /// If the area is less than 0, it indicates that the polygon is clockwise winded.
        /// </summary>
        /// <returns>The signed area * 2</returns>
        static float GetSignedDoubleArea(NativeList<SegmentVertex> vertices, Range range)
        {
            // The simplest polygon which can exist in the Euclidean plane has 3 sides.
            var count = range.Length;
            if (count < 3)
                return 0;

            float area = 0;
            for (int i = range.start; i < range.end; i++)
            {
                int j = range.start + ((i - range.start + 1) % count);

                var vi = vertices[i].position;
                var vj = vertices[j].position;

                area += vi.x * vj.y;
                area -= vi.y * vj.x;
            }
            //area /= 2.0f;
            return area;
        }

        /// <summary>
        /// Indicates if the vertices are in counter clockwise order.
        /// Warning: If the area of the polygon is 0, it is unable to determine the winding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsCounterClockWise(NativeList<SegmentVertex> vertices, Range range)
        {
            // The simplest polygon which can exist in the Euclidean plane has 3 sides.
            if (range.Length < 3)
                return false;

            return (GetSignedDoubleArea(vertices, range) > 0.0f);
        }

        // Forces counter clock wise order.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ForceCounterClockWise(NativeList<SegmentVertex> vertices, Range range)
        {
            if (!IsCounterClockWise(vertices, range))
                Reverse(vertices, range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Range Copy(int lowerIndex, int upperIndex, NativeList<SegmentVertex> vertices, Range range, NativeList<SegmentVertex> dstVertices)
        {
            // TODO: do this with math instead
            var count = range.Length;
            while (upperIndex < lowerIndex)
                upperIndex += count;

            var start = dstVertices.Length;
            for (; lowerIndex <= upperIndex; ++lowerIndex)
                dstVertices.Add(At(lowerIndex, vertices, range));
            return new Range { start = start, end = dstVertices.Length };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse(NativeList<SegmentVertex> vertices, Range range)
        {
            for (int i1 = range.start, i2 = range.end - 1, center = range.Center; i1 < center; i1++, i2--)
            {
                var temp = vertices[i1];
                vertices[i1] = vertices[i2];
                vertices[i2] = temp;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reverse(UnsafeList<SegmentVertex> vertices, Range range)
        {
            for (int i1 = range.start, i2 = range.end - 1, center = range.Center; i1 < center; i1++, i2--)
            {
                var temp = vertices[i1];
                vertices[i1] = vertices[i2];
                vertices[i2] = temp;
            }
        }
    }
}
