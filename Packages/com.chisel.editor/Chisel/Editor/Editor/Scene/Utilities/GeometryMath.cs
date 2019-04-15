using UnityEditor;
using UnityEngine;

// TODO: remove redundancy, clean up
namespace Chisel.Utilities
{
    public static class GeometryMath
    {		

        public static Vector3 ProjectPointLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
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

        public static Vector3 ProjectPointRay(Vector3 point, Vector3 start, Vector3 direction)
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
        

        // Transforms a plane by this matrix.
        public static Plane TransformPlane(this Matrix4x4 matrix, Plane plane)
        {
            var ittrans = matrix.inverse;

            float x = plane.normal.x, y = plane.normal.y, z = plane.normal.z, w = plane.distance;
            // note: a transpose is part of this transformation
            var a = ittrans.m00 * x + ittrans.m10 * y + ittrans.m20 * z + ittrans.m30 * w;
            var b = ittrans.m01 * x + ittrans.m11 * y + ittrans.m21 * z + ittrans.m31 * w;
            var c = ittrans.m02 * x + ittrans.m12 * y + ittrans.m22 * z + ittrans.m32 * w;
            var d = ittrans.m03 * x + ittrans.m13 * y + ittrans.m23 * z + ittrans.m33 * w;

            return new Plane(new Vector3(a, b, c), d);
        }
    }
}
