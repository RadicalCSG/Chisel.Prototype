using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
    public enum IntersectionResult
    {
        Intersecting,
        Inside,
        Outside
    }

    public static class MathExtensions
    {       
        //private static readonly Vector3 PositiveY = new Vector3(0, 1, 0);
        private static readonly Vector3 NegativeY = new Vector3(0, -1, 0);
        private static readonly Vector3 PositiveZ = new Vector3(0, 0, 1);

        public static Vector3 ClosestTangentAxis(Vector3 vector)
        {
            var absX = Mathf.Abs(vector.x);
            var absY = Mathf.Abs(vector.y);
            var absZ = Mathf.Abs(vector.z);

            if (absY > absX && absY > absZ)
                return PositiveZ;

            return NegativeY;
        }

        public static Matrix4x4 RotateAroundAxis(Vector3 center, Vector3 normal, float angle)
        {
            var rotation = Quaternion.AngleAxis(angle, normal);
            return MathExtensions.RotateAroundPoint(center, rotation);
        }


        public static Matrix4x4 RotateAroundPoint(Vector3 center, Quaternion rotation)
        {
            return Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) *
                   Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one) *
                   Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
        }

        public static void CalculateTangents(Vector3 normal, out Vector3 tangent, out Vector3 binormal)
        {
            tangent = Vector3.Cross(normal, ClosestTangentAxis(normal)).normalized;
            binormal = Vector3.Cross(normal, tangent).normalized;
        }

        public static Matrix4x4 GenerateLocalToPlaneSpaceMatrix(Plane plane)
        {
            Vector3 normal = -plane.normal;
            Vector3 tangent;
            Vector3 biNormal;
            CalculateTangents(normal, out tangent, out biNormal);
            var pointOnPlane = normal * -plane.distance;

            return new Matrix4x4()
            {
                m00 = tangent.x,  m01 = tangent.y,	m02 = tangent.z,	m03 = Vector3.Dot(tangent, pointOnPlane),
                m10 = biNormal.x, m11 = biNormal.y, m12 = biNormal.z,	m13 = Vector3.Dot(biNormal, pointOnPlane),
                m20 = normal.x,   m21 = normal.y,	m22 = normal.z,		m23 = Vector3.Dot(normal, pointOnPlane),
                m30 = 0.0f,		  m31 = 0.0f,		m32 = 0.0f,			m33 = 1.0f
            };
        }

        static bool Intersect(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 intersection)
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



        public static float SignedAngle(Vector3 v1, Vector3 v2, Vector3 n)
        {
            //  Acute angle [0,180]
            var angle = Vector3.Angle(v1, v2);

            //  -Acute angle [180,-179]
            var sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(v1, v2)));
            var signedAngle = angle * sign;

            //  360 angle
            return signedAngle;
        }

        public static Vector2 Lerp(Vector2 A, Vector2 B, float t)
        {
            return new Vector2(
                    Mathf.Lerp(A.x, B.x, t),
                    Mathf.Lerp(A.y, B.y, t)
                );
        }

        public static Vector3 Lerp(Vector3 A, Vector3 B, float t)
        {
            return new Vector3(
                    Mathf.Lerp(A.x, B.x, t),
                    Mathf.Lerp(A.y, B.y, t),
                    Mathf.Lerp(A.z, B.z, t)
                );
        }

        public static Quaternion Lerp(Quaternion A, Quaternion B, float t)
        {
            return Quaternion.Slerp(A, B, t);
        }
        
        // Transforms a plane by this matrix.
        public static Plane Transform(this Matrix4x4 matrix, Plane plane)
        {
            var ittrans = matrix.inverse;

            float x = plane.normal.x, y = plane.normal.y, z = plane.normal.z, w = plane.distance;
            // note: a transpose is part of this transformation
            var a = ittrans.m00 * x + ittrans.m10 * y + ittrans.m20 * z + ittrans.m30 * w;
            var b = ittrans.m01 * x + ittrans.m11 * y + ittrans.m21 * z + ittrans.m31 * w;
            var c = ittrans.m02 * x + ittrans.m12 * y + ittrans.m22 * z + ittrans.m32 * w;
            var d = ittrans.m03 * x + ittrans.m13 * y + ittrans.m23 * z + ittrans.m33 * w;
        
            var normal = new Vector3(a, b, c);
            var magnitude = normal.magnitude;
            return new Plane(normal / magnitude, d / magnitude);
        }
        
        // Transforms a plane by this matrix.
        public static Plane InverseTransform(this Matrix4x4 matrix, Plane plane)
        {
            var ittrans = matrix.transpose;
            var planeEq = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            var result = (ittrans * planeEq);
            var normal = new Vector3(result.x, result.y, result.z);
            var magnitude = normal.magnitude;
            return new Plane(normal / magnitude, result.w / magnitude);
        }

    
        public const float kDistanceEpsilon = 0.00001f;

        // Check if bounds is inside/outside or intersects with plane
        public static IntersectionResult Intersection(this Plane plane, Bounds bounds)
        {
            float[] extends_X = new []{ bounds.min.x, bounds.max.x };
            float[] extends_Y = new []{ bounds.min.y, bounds.max.y };
            float[] extends_Z = new []{ bounds.min.z, bounds.max.z };

            var normal	 = plane.normal;
            var x_octant = (normal.x < 0) ? 1 : 0;
            var y_octant = (normal.y < 0) ? 1 : 0;
            var z_octant = (normal.z < 0) ? 1 : 0;

            float forward = plane.GetDistanceToPoint(new Vector3(extends_X[x_octant], extends_Y[y_octant], extends_Z[z_octant]));
            if (forward > kDistanceEpsilon)
                return IntersectionResult.Outside;	// closest point is outside
        
            float backward = plane.GetDistanceToPoint(new Vector3(extends_X[1 - x_octant], extends_Y[1 - y_octant], extends_Z[1 - z_octant]));
            if (backward < -kDistanceEpsilon)
                return IntersectionResult.Inside;	// closest point is inside

            return IntersectionResult.Intersecting;	// closest point is intersecting
        }

        public static Vector3 Intersection(Vector3 v1, Vector3 v2, float d1, float d2)
        {
            var prevDistance	= d1;
            var currDistance	= d2;
            var prevVertex		= v1;
            var currVertex		= v2;

            var length			= prevDistance - currDistance;

            // make sure we always cut in the same direction to make floating point errors consistent
            if (length > 0) 
            {
                var delta			= prevDistance / length;
                var vector = prevVertex - currVertex;
                return prevVertex - (vector * delta);
            } else
            { 		
                length		= currDistance - prevDistance;
                var delta	= currDistance / length;
                var vector	= currVertex - prevVertex;
                return currVertex - (vector * delta);
            }
        }
    }
}
