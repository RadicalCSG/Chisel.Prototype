using UnityEngine;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

// TODO: remove redundancy, clean up
namespace Chisel.Core
{
    public static class GeometryMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            var cp1A = math.cross(c - b, p - b);
            var cp2A = math.cross(c - b, a - b);
            var dotA = math.dot(cp1A, cp2A);
            var sameSideA = dotA > 0;

            var cp1B = math.cross(c - a, p - a);
            var cp2B = math.cross(c - a, b - a);
            var dotB = math.dot(cp1B, cp2B);
            var sameSideB = dotB > 0;

            var cp1C = math.cross(b - a, p - a);
            var cp2C = math.cross(b - a, c - a);
            var dotC = math.dot(cp1C, cp2C);
            var sameSideC = dotC > 0;

            return sameSideA &&
                    sameSideB &&
                    sameSideC;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetTriangleArraySize(int size)
        {
            int n = size - 1;
            return ((n * n) + n) / 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetTriangleArrayIndex(int index, int size)
        {
            int n = size - 1;
            int xFactor = 2 * n + 1;
            int yFactor = (4 * (n * n)) + (4 * n) - 7;

            var y = n - 1 - ((int)(math.sqrt(yFactor - (index * 8)) - 1)) / 2;
            var x = index + ((y * (y - xFactor + 2)) / 2) + 1;
            return new int2(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ProjectPointLine(in Vector3 point, in Vector3 lineStart, in Vector3 lineEnd)
        {
            Vector3 relativePoint = point - lineStart;
            Vector3 lineDirection = lineEnd - lineStart;
            float length = lineDirection.magnitude;
            Vector3 normalizedLineDirection = lineDirection;
            if (length > Vector3.kEpsilon)
                normalizedLineDirection /= length;

            float dot = Vector3.Dot(normalizedLineDirection, relativePoint);
            dot = Mathf.Clamp(dot, 0.0F, length);

            return lineStart + normalizedLineDirection * dot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float unsignedAngle = Vector3.Angle(from, to);
            float sign = Mathf.Sign(Vector3.Dot(axis, Vector3.Cross(from, to)));
            return unsignedAngle * sign;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SignedAngle(Vector2 from, Vector2 to)
        {
            float unsigned_angle = Vector3.Angle(from, to);
            float sign = Mathf.Sign(from.x * to.y - from.y * to.x);
            return unsigned_angle * sign;
        }


        // Finds the minimum 3D distance from a point to a line segment

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointOnLineSegment(float3 point, float3 lineVertexA, float3 lineVertexB, float vertexEpsilon, float edgeEpsilon)
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
                return true;
            }

            var d = b / c;
            if (d <= 0.0 || d >= 1.0)
            {
                // Closest point to line is not on the segment, so it is one of the end points
                dx = lineVertexA.x - point.x;
                dy = lineVertexA.y - point.y;
                dz = lineVertexA.z - point.z;
                var e = dx * dx + dy * dy + dz * dz;

                dx = lineVertexB.x - point.x;
                dy = lineVertexB.y - point.y;
                dz = lineVertexB.z - point.z;

                var f = dx * dx + dy * dy + dz * dz;
                if (e < f)
                    return e < vertexEpsilon;
                else
                    return f < vertexEpsilon;
            }
            
            // Closest point to line is on the segment
            var d1 = (lineVertexB.y - lineVertexA.y) * (point.z - lineVertexA.z) - (point.y - lineVertexA.y) * (lineVertexB.z - lineVertexA.z);
            var d2 = (lineVertexB.x - lineVertexA.x) * (point.z - lineVertexA.z) - (point.x - lineVertexA.x) * (lineVertexB.z - lineVertexA.z);
            var d3 = (lineVertexB.x - lineVertexA.x) * (point.y - lineVertexA.y) - (point.x - lineVertexA.x) * (lineVertexB.y - lineVertexA.y);
            var a = math.sqrt(d1 * d1 + d2 * d2 + d3 * d3);
            var csqrt = math.sqrt(c);
            a /= csqrt;
            return (a * a) < edgeEpsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointOnLineSegmentButNotOnVertex(float3 point, float3 lineVertexA, float3 lineVertexB, float edgeEpsilon)
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
                return false;
            }

            var d = b / c;
            if (d <= 0.0 || d >= 1.0)
            {
                return false;
            }

            // Closest point to line is on the segment
            var d1 = (lineVertexB.y - lineVertexA.y) * (point.z - lineVertexA.z) - (point.y - lineVertexA.y) * (lineVertexB.z - lineVertexA.z);
            var d2 = (lineVertexB.x - lineVertexA.x) * (point.z - lineVertexA.z) - (point.x - lineVertexA.x) * (lineVertexB.z - lineVertexA.z);
            var d3 = (lineVertexB.x - lineVertexA.x) * (point.y - lineVertexA.y) - (point.x - lineVertexA.x) * (lineVertexB.y - lineVertexA.y);
            var a = math.sqrt(d1 * d1 + d2 * d2 + d3 * d3);
            var csqrt = math.sqrt(c);
            a /= csqrt;
            return (a * a) < edgeEpsilon;
        }


        // Finds the minimum 3D distance from a point to a line segment

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                var e = dx * dx + dy * dy + dz * dz;

                dx = lineVertexB.x - point.x;
                dy = lineVertexB.y - point.y;
                dz = lineVertexB.z - point.z;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LineLineIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4,
                                                out Vector3 intersectionPoint1, double epsilon)
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
                return false;
            }
            
            if (double.IsNaN(mub) || double.IsInfinity(mub) || mub < 0.0 || mub > 1.0)
            {
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
                return false;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsPoint(Vector2[] polyPoints, Vector2 p)
        {
            var j = polyPoints.Length - 1;
            var inside = false;
            for (int i = 0; i < polyPoints.Length; j = i++)
            {
                var pi = polyPoints[i];
                var pj = polyPoints[j];
                if (((pi.y <= p.y && p.y < pj.y) || (pj.y <= p.y && p.y < pi.y)) &&
                    (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y) + pi.x))
                    inside = !inside;
            }
            return inside;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Plane CalculatePlane(Vector3[] vertices)
        {        
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var normal		= Vector3.zero;
            var prevVertex	= vertices[vertices.Length - 1];
            for (int n = 0; n < vertices.Length; n++)
            {
                var currVertex = vertices[n];
                normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                prevVertex = currVertex;
            }
            normal.Normalize();

            var d = 0.0f;
            for (int n = 0; n < vertices.Length; n++)
                d -= Vector3.Dot(normal, vertices[n]);
            d /= vertices.Length;

            return new Plane(normal, d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FindCircleHorizon(Matrix4x4 matrix, float diameter, Vector3 center, Vector3 normal, out Vector3 pointA, out Vector3 pointB)
        {
            pointA = Vector3.zero;
            pointB = Vector3.zero;
            /*
            var radius = diameter * 0.5f;

            // Since the geometry is transfromed by Handles.matrix during rendering, we transform the camera position
            // by the inverse matrix so that the two-shaded wireframe will have the proper orientation.
            var invMatrix = matrix.inverse;

            var planeNormal = center - invMatrix.MultiplyPoint(Camera.current.transform.position); // vector from camera to center
            float sqrDist = planeNormal.sqrMagnitude; // squared distance from camera to center
            float sqrRadius = radius * radius; // squared radius
            float sqrOffset = sqrRadius * sqrRadius / sqrDist; // squared distance from actual center to drawn disc center
            float insideAmount = sqrOffset / sqrRadius;

            if (insideAmount >= 1)
                return false;

            float Q = Vector3.Angle(planeNormal, Vector3.up);
            Q = 90 - Mathf.Min(Q, 180 - Q);
            float f = Mathf.Tan(Q * Mathf.Deg2Rad);
            float g = Mathf.Sqrt(sqrOffset + f * f * sqrOffset) / radius;
            if (g >= 1)
                return false;
        
            float e = Mathf.Asin(g) * Mathf.Rad2Deg;
            var from = Vector3.Cross(Vector3.up, planeNormal).normalized;
            from = Quaternion.AngleAxis(e, Vector3.up) * from;

            var rotation = Quaternion.AngleAxis((90 - e) * 2, Vector3.up);
            var to = rotation * from;
            pointA = center + from * radius; //((rotation * from) * radius);
            pointB = center + to   * radius;// ((Quaternion.Inverse(rotation) * from) * radius);
            return true;

            //DrawTwoShadedWireDisc(position, dirs[i], from, (90 - e) * 2, radius);


            /*/
            var camera              = UnityEngine.Camera.current;
            var cameraTransform     = camera.transform;
            var cameraPosition      = (matrix.inverse).MultiplyPoint(cameraTransform.position);
            
            var radius              = diameter * 0.5f;
            var plane               = new Plane(normal, center);
            var closestPointOnPlane = plane.ClosestPointOnPlane(cameraPosition);
            var vectorToCenter      = closestPointOnPlane - center;
            var distanceToCenter    = vectorToCenter.sqrMagnitude;
            if (distanceToCenter < (radius * radius))
                return false;

            distanceToCenter = Mathf.Sqrt(distanceToCenter);
            var forwardVector   = vectorToCenter / distanceToCenter;

            var OB              = distanceToCenter - radius;
            var OA              = OB + diameter;
            var OC              = Mathf.Sqrt(OA * OB);

            var c            = distanceToCenter;
            var a            = OC;
            var b            = radius;
            var cos_angle    = ((b * b) + (c * c) - (a * a)) / (2 * b * c);
            var angle_offset = Mathf.Acos(cos_angle) * Mathf.Rad2Deg;


            var rotation    = Quaternion.AngleAxis(angle_offset, normal);
            pointA = center + ((rotation * forwardVector) * radius);
            pointB = center + ((Quaternion.Inverse(rotation) * forwardVector) * radius);
            return true;
            //*/
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInCameraCircle(Matrix4x4 transform, Vector3 point, float diameter, Vector3 center, Vector3 normal)
        {
            var camera          = UnityEngine.Camera.current;
            var cameraTransform = camera.transform;
            var cameraPosition  = transform.inverse.MultiplyPoint(cameraTransform.position);

            var plane           = new Plane(normal, center);
            var ray             = new Ray(cameraPosition, point - cameraPosition);

            if (!plane.Raycast(ray, out var intersectionDist))
                return false;

            var intersectionPoint = ray.GetPoint(intersectionDist);

            var radius = diameter * 0.5f;
            var dist = (intersectionPoint - center).sqrMagnitude;
            return (dist < (radius * radius));
        }
    }
}
