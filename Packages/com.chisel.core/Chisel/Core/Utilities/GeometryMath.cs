using UnityEngine;
using Unity.Mathematics;

// TODO: remove redundancy, clean up
namespace Chisel.Core
{
    public static class GeometryMath
    {
        public static int GetTriangleArraySize(int size)
        {
            int n = size - 1;
            return ((n * n) + n) / 2;
        }

        public static int2 GetTriangleArrayIndex(int index, int size)
        {
            int n = size - 1;
            int xFactor = 2 * n + 1;
            int yFactor = (4 * (n * n)) + (4 * n) - 7;

            var y = n - 1 - ((int)(math.sqrt(yFactor - (index * 8)) - 1)) / 2;
            var x = index + ((y * (y - xFactor + 2)) / 2) + 1;
            return new int2(x, y);
        }

        public static Vector3 ProjectPointLine(in Vector3 point, in Vector3 lineStart, in Vector3 lineEnd)
        {
            Vector3 relativePoint = point - lineStart;
            Vector3 lineDirection = lineEnd - lineStart;
            //float length = lineDirection.magnitude;
            Vector3 normalizedLineDirection = lineDirection;
            //if (length > Vector3.kEpsilon)
            //    normalizedLineDirection /= length;

            float dot = Vector3.Dot(normalizedLineDirection, relativePoint);
            //dot = Mathf.Clamp(dot, 0.0F, length);

            return lineStart + normalizedLineDirection * dot;
        }

        public static Vector3 ProjectPointRay(in Vector3 point, in Vector3 start, in Vector3 direction)
        {
            Vector3 relativePoint = point - start;
            float length = direction.magnitude;
            Vector3 normalizedDirection = direction;
            if (length > Vector3.kEpsilon)
                normalizedDirection /= length;

            float dot = Vector3.Dot(normalizedDirection, relativePoint);
            return start + normalizedDirection * dot;
        }

        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsignedAngle = Vector3.Angle(from, to);
            float sign = Mathf.Sign(Vector3.Dot(axis, Vector3.Cross(from, to)));
            return unsignedAngle * sign;
        }

        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            float unsigned_angle = Vector3.Angle(from, to);
            float sign = Mathf.Sign(from.x * to.y - from.y * to.x);
            return unsigned_angle * sign;
        }


        // Finds the minimum 3D distance from a point to a line segment

        public static bool IsPointOnLineSegment(float3 point, float3 lineVertexA, float3 lineVertexB)
        {
            const float kEpsilon = 0.00001f;
            var b = (point.x - lineVertexA.x) * (lineVertexB.x - lineVertexA.x) +
                    (point.y - lineVertexA.y) * (lineVertexB.y - lineVertexA.y) +
                    (point.z - lineVertexA.z) * (lineVertexB.z - lineVertexA.z);

            var dx = (lineVertexB.x - lineVertexA.x);
            var dy = (lineVertexB.y - lineVertexA.y);
            var dz = (lineVertexB.z - lineVertexA.z);
            var c = dx * dx + dy * dy + dz * dz;
            if (c == 0.0)
            {
                // Point1 and Point2 are the same
                return true;
            }

            var d = b / c;
            if (d <= 0.0 || d >= 1.0)
            {
                // Closest point to line is not on the segment, so it is one of the end points
                dx = lineVertexA.x - point.x;
                dy = lineVertexA.y - point.y;
                dz = lineVertexA.z - point.z;

                dx = lineVertexB.x - point.x;
                dy = lineVertexB.y - point.y;
                dz = lineVertexB.z - point.z;

                var e = dx * dx + dy * dy + dz * dz;
                var f = dx * dx + dy * dy + dz * dz;
                if (e < f)
                    return e < kEpsilon;
                else
                    return f < kEpsilon;
            }

            // Closest point to line is on the segment
            var d1 = (lineVertexB.y - lineVertexA.y) * (point.z - lineVertexA.z) - (point.y - lineVertexA.y) * (lineVertexB.z - lineVertexA.z);
            var d2 = (lineVertexB.x - lineVertexA.x) * (point.z - lineVertexA.z) - (point.x - lineVertexA.x) * (lineVertexB.z - lineVertexA.z);
            var d3 = (lineVertexB.x - lineVertexA.x) * (point.y - lineVertexA.y) - (point.x - lineVertexA.x) * (lineVertexB.y - lineVertexA.y);
            var a = math.sqrt(d1 * d1 + d2 * d2 + d3 * d3);
            var csqrt = math.sqrt(c);
            a /= csqrt;
            return (a * a) < kEpsilon;
        }


        // Finds the minimum 3D distance from a point to a line segment

        public static float SqrDistanceFromPointToLineSegment(float3 point, float3 lineVertexA, float3 lineVertexB)
        {
            var b = (point.x - lineVertexA.x) * (lineVertexB.x - lineVertexA.x) +
                    (point.y - lineVertexA.y) * (lineVertexB.y - lineVertexA.y) +
                    (point.z - lineVertexA.z) * (lineVertexB.z - lineVertexA.z);

            var dx = (lineVertexB.x - lineVertexA.x);
            var dy = (lineVertexB.y - lineVertexA.y);
            var dz = (lineVertexB.z - lineVertexA.z);
            var c = dx * dx + dy * dy + dz * dz;
            if (c == 0.0)
            {
                // Point1 and Point2 are the same
                return math.lengthsq(point - lineVertexA);
            }

            var d = b / c;
            if (d <= 0.0 || d >= 1.0)
            {
                // Closest point to line is not on the segment, so it is one of the end points
                dx = lineVertexA.x - point.x;
                dy = lineVertexA.y - point.y;
                dz = lineVertexA.z - point.z;

                dx = lineVertexB.x - point.x;
                dy = lineVertexB.y - point.y;
                dz = lineVertexB.z - point.z;

                var e = dx * dx + dy * dy + dz * dz;
                var f = dx * dx + dy * dy + dz * dz;
                if (e < f)
                    return e;
                else
                    return f;
            }

            // Closest point to line is on the segment
            var d1 = (lineVertexB.y - lineVertexA.y) * (point.z - lineVertexA.z) - (point.y - lineVertexA.y) * (lineVertexB.z - lineVertexA.z);
            var d2 = (lineVertexB.x - lineVertexA.x) * (point.z - lineVertexA.z) - (point.x - lineVertexA.x) * (lineVertexB.z - lineVertexA.z);
            var d3 = (lineVertexB.x - lineVertexA.x) * (point.y - lineVertexA.y) - (point.x - lineVertexA.x) * (lineVertexB.y - lineVertexA.y);
            var a = math.sqrt(d1 * d1 + d2 * d2 + d3 * d3);
            var csqrt = math.sqrt(c);
            a /= csqrt;
            return (a * a);
        }

        // Find the point of intersection (pa) between two lines P1:P2 and P3:P4 in 3D.
        // Returns false if the lines don't approach within kEpsilon
        // Finds the line segment Pa:Pb that is the shortest perpendicular between two lines P1: P2 and P3: P4 in 3D.
        // Returns false if two lines (P1:P2 and P3:P4) are parallel
        // Also returns false if bSegment is true and either intersection does not occur within segment

        public static bool LineLineIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4,
                                                out Vector3 intersectionPoint1, double epsilon, bool debug = false)
        {
            var p13x = (double)p1.x - (double)p3.x;
            var p13y = (double)p1.y - (double)p3.y;
            var p13z = (double)p1.z - (double)p3.z;

            var p43x = (double)p4.x - (double)p3.x;
            var p43y = (double)p4.y - (double)p3.y;
            var p43z = (double)p4.z - (double)p3.z;

            intersectionPoint1 = Vector3.zero;
            //*
            if (System.Math.Abs(p43x) <= epsilon &&
                System.Math.Abs(p43y) <= epsilon &&
                System.Math.Abs(p43z) <= epsilon)
                return false;
            //*/
            var p21x = (double)p2.x - (double)p1.x;
            var p21y = (double)p2.y - (double)p1.y;
            var p21z = (double)p2.z - (double)p1.z;
            //*
            if (System.Math.Abs(p21x) <= epsilon &&
                System.Math.Abs(p21y) <= epsilon &&
                System.Math.Abs(p21z) <= epsilon)
                return false;
            //*/
            var d1343 = p13x * p43x + p13y * p43y + p13z * p43z;
            var d4321 = p43x * p21x + p43y * p21y + p43z * p21z;
            var d1321 = p13x * p21x + p13y * p21y + p13z * p21z;
            var d4343 = p43x * p43x + p43y * p43y + p43z * p43z;
            var d2121 = p21x * p21x + p21y * p21y + p21z * p21z;

            var denom = d2121 * d4343 - d4321 * d4321;
            //*
            if (System.Math.Abs(denom) <= epsilon)
                return false;
            //*/
            var numer   = d1343 * d4321 - d1321 * d4343;
            var mua     = numer / denom;                    // Where Pa = P1 + mua (P2 - P1)
            var mub     = (d1343 + d4321 * (mua)) / d4343;  // Where Pb = P3 + mub (P4 - P3)
            
            // Don't intersect within line segments
            if (double.IsNaN(mua) || double.IsInfinity(mua) || mua < 0.0 || mua > 1.0)
            {
                if (debug)
                {
                    Debug.Log($"D {mua}");
                }
                return false;
            }
            
            if (double.IsNaN(mub) || double.IsInfinity(mub) || mub < 0.0 || mub > 1.0)
            {
                if (debug)
                {
                    Debug.Log($"E {mub}");
                }
                return false;
            }

            var intersectionPoint1x = (double)p1.x + (mua * p21x);
            var intersectionPoint1y = (double)p1.y + (mua * p21y);
            var intersectionPoint1z = (double)p1.z + (mua * p21z);

            var intersectionPoint2x = (double)p3.x + (mub * p43x);
            var intersectionPoint2y = (double)p3.y + (mub * p43y);
            var intersectionPoint2z = (double)p3.z + (mub * p43z);


            var dx = intersectionPoint1x - intersectionPoint2x;
            var dy = intersectionPoint1y - intersectionPoint2y;
            var dz = intersectionPoint1z - intersectionPoint2z;

            var sqrMagnitude = (dx * dx) + (dy * dy) + (dz * dz);
            if (double.IsNaN(sqrMagnitude) || double.IsInfinity(sqrMagnitude) || sqrMagnitude > (epsilon * epsilon))
            {
                if (debug)
                {
                    Debug.Log($"F {sqrMagnitude} {(epsilon * epsilon)}");
                }
                return false;
            }

            if (debug)
            {
                Debug.Log("G");
            }
            intersectionPoint1 = new Vector3((float)intersectionPoint1x, (float)intersectionPoint1y, (float)intersectionPoint1z);

            double magnitude;
            double minMagnitude = (epsilon * epsilon);
            magnitude = (intersectionPoint1 - p1).sqrMagnitude; if (magnitude < minMagnitude) { intersectionPoint1 = p1; minMagnitude = magnitude; }
            magnitude = (intersectionPoint1 - p2).sqrMagnitude; if (magnitude < minMagnitude) { intersectionPoint1 = p2; minMagnitude = magnitude; }
            magnitude = (intersectionPoint1 - p3).sqrMagnitude; if (magnitude < minMagnitude) { intersectionPoint1 = p3; minMagnitude = magnitude; }
            magnitude = (intersectionPoint1 - p4).sqrMagnitude; if (magnitude < minMagnitude) { intersectionPoint1 = p4; minMagnitude = magnitude; }

            return true;
        }
    }
}
