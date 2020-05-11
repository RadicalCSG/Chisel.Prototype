using System;
using UnityEngine;
using Unity.Mathematics;

namespace Chisel.Core
{
    public enum IntersectionResult : byte
    {
        Intersecting,
        Inside,
        Outside
    }

    public static class MathExtensions
    {
        public const float kDistanceEpsilon         = 0.00001f;

        // We want frustum selection to have a bit more space
        public const float kFrustumDistanceEpsilon  = kDistanceEpsilon * 100;


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

        public static float3 ClosestTangentAxis(float3 vector)
        {
            var absX = math.abs(vector.x);
            var absY = math.abs(vector.y);
            var absZ = math.abs(vector.z);

            if (absY > absX && absY > absZ)
                return new float3(0, 0, 1);

            return new float3(0, -1, 0);
        }

        public static bool Equals(this Vector3 self, Vector3 other, double epsilon)
        {
            return System.Math.Abs(self.x - other.x) <= epsilon &&
                   System.Math.Abs(self.y - other.y) <= epsilon &&
                   System.Math.Abs(self.z - other.z) <= epsilon;
        }

        public static bool Equals(this Vector3 self, Vector3 other, float epsilon)
        {
            return System.Math.Abs(self.x - other.x) <= epsilon &&
                   System.Math.Abs(self.y - other.y) <= epsilon &&
                   System.Math.Abs(self.z - other.z) <= epsilon;
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

        public static void CalculateTangents(float3 normal, out float3 tangent, out float3 binormal)
        {
            tangent     = math.normalizesafe(math.cross(normal, ClosestTangentAxis(normal)));
            binormal    = math.normalizesafe(math.cross(normal, tangent));
        }

        public static Vector3 CalculateTangent(Vector3 normal)
        {
            return Vector3.Cross(normal, ClosestTangentAxis(normal)).normalized;
        }

        public static float4x4 GenerateLocalToPlaneSpaceMatrix(float4 planeVector)
        {
            float3 normal = -planeVector.xyz;
            CalculateTangents(normal, out float3 tangent, out float3 biNormal);
            //var pointOnPlane = normal * planeVector.w;

            return new float4x4
            {
                c0 = new float4(tangent.x, biNormal.x, normal.x, 0.0f),
                c1 = new float4(tangent.y, biNormal.y, normal.y, 0.0f),
                c2 = new float4(tangent.z, biNormal.z, normal.z, 0.0f),
                //c3 = new float4(math.dot(tangent, pointOnPlane), math.dot(biNormal, pointOnPlane), math.dot(normal, pointOnPlane), 1.0f)
                c3 = new float4(0, 0, -planeVector.w, 1.0f)
            };
        }

        public static Vector3 CalculateBinormal(Vector3 normal)
        {
            return Vector3.Cross(normal, CalculateTangent(normal));
        }

        public static bool IsInside(this Plane plane, in Bounds bounds)
		{
            var normal = plane.normal;
			var backward_x = normal.x < 0 ? bounds.min.x : bounds.max.x;
            var backward_y = normal.y < 0 ? bounds.min.y : bounds.max.y;
            var backward_z = normal.z < 0 ? bounds.min.z : bounds.max.z;
            var distance = plane.GetDistanceToPoint(new Vector3(backward_x, backward_y, backward_z));
			return (distance < -kDistanceEpsilon);
		}

        public static bool IsOutside(this Plane plane, Bounds bounds)
		{
            var normal = plane.normal;
			var backward_x = normal.x >= 0 ? bounds.min.x : bounds.max.x;
            var backward_y = normal.y >= 0 ? bounds.min.y : bounds.max.y;
            var backward_z = normal.z >= 0 ? bounds.min.z : bounds.max.z;
            var distance = plane.GetDistanceToPoint(new Vector3(backward_x, backward_y, backward_z));
            return (distance > kDistanceEpsilon);
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
        public static Plane Transform(this Matrix4x4 matrix, in Plane plane)
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

        public static Plane Transform(this Matrix4x4 matrix, in Vector4 planeVector)
        {
            var ittrans = matrix.inverse;

            float x = planeVector.x, y = planeVector.y, z = planeVector.z, w = planeVector.w;
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
        public static Plane InverseTransform(this Matrix4x4 matrix, in Plane plane)
        {
            var ittrans = matrix.transpose;
            var planeEq = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            var result = (ittrans * planeEq);
            var normal = new Vector3(result.x, result.y, result.z);
            var magnitude = normal.magnitude;
            return new Plane(normal / magnitude, result.w / magnitude);
        }

        public static Plane InverseTransform(this Matrix4x4 matrix, in Vector4 planeVector)
        {
            var ittrans = matrix.transpose;
            var result = (ittrans * planeVector);
            var normal = new Vector3(result.x, result.y, result.z);
            var magnitude = normal.magnitude;
            return new Plane(normal / magnitude, result.w / magnitude);
        }

        public static float4 InverseTransform(this float4x4 ittrans, in float4 planeVector)
        {
            // note: a transpose is part of this transformation
            var result = new float4(ittrans.c0.x * planeVector.x + ittrans.c0.y * planeVector.y + ittrans.c0.z * planeVector.z + ittrans.c0.w * planeVector.w,
                                    ittrans.c1.x * planeVector.x + ittrans.c1.y * planeVector.y + ittrans.c1.z * planeVector.z + ittrans.c1.w * planeVector.w,
                                    ittrans.c2.x * planeVector.x + ittrans.c2.y * planeVector.y + ittrans.c2.z * planeVector.z + ittrans.c2.w * planeVector.w,
                                    ittrans.c3.x * planeVector.x + ittrans.c3.y * planeVector.y + ittrans.c3.z * planeVector.z + ittrans.c3.w * planeVector.w);
            var magnitude = math.length(result.xyz);
            return result / magnitude;
        }


        public static void Set(this Transform transform, in Matrix4x4 matrix)
        {
            var position = matrix.GetColumn(3);
            matrix.SetColumn(3, Vector4.zero);

            var columnX = matrix.GetColumn(0);
            var columnY = matrix.GetColumn(1);
            var columnZ = matrix.GetColumn(2);
            var scaleX = columnX.magnitude;
            var scaleY = columnY.magnitude;
            var scaleZ = columnZ.magnitude;

            columnX /= scaleX;
            columnY /= scaleY;
            columnZ /= scaleZ;

            if (Vector3.Dot(Vector3.Cross(columnZ, columnY), columnX) > 0)
            {
                scaleX = -scaleX;
                columnX = -columnX;
            }

            var scale = new Vector3(scaleX, scaleY, scaleZ);
            var rotation = Quaternion.LookRotation(columnZ, columnY);

            transform.localScale = scale;
            transform.localPosition = position;
            transform.localRotation = rotation;
        }

        // Check if bounds is inside/outside or intersects with plane

        public static IntersectionResult Intersection(this Plane plane, Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;

            var normal	= plane.normal;

            var corner  = new Vector3((normal.x < 0) ? max.x : min.x,
                                      (normal.y < 0) ? max.y : min.y,
                                      (normal.z < 0) ? max.z : min.z);
            float forward = Vector3.Dot(normal, corner) + plane.distance;
            if (forward > kDistanceEpsilon)
                return IntersectionResult.Outside;  // closest point is outside

            corner = new Vector3((normal.x >= 0) ? max.x : min.x,
                                 (normal.y >= 0) ? max.y : min.y,
                                 (normal.z >= 0) ? max.z : min.z);
            float backward = Vector3.Dot(normal, corner) + plane.distance;
            if (backward < -kDistanceEpsilon)
                return IntersectionResult.Inside;	// closest point is inside

            return IntersectionResult.Intersecting;	// closest point is intersecting
        }

        public static IntersectionResult Intersection(this Plane plane, Vector3 min, Vector3 max)
        {
            var normal = plane.normal;

            var corner = new Vector3((normal.x < 0) ? max.x : min.x,
                                     (normal.y < 0) ? max.y : min.y,
                                     (normal.z < 0) ? max.z : min.z);
            float forward = Vector3.Dot(normal, corner) + plane.distance;
            if (forward > kDistanceEpsilon)
                return IntersectionResult.Outside;  // closest point is outside

            corner = new Vector3((normal.x >= 0) ? max.x : min.x,
                                 (normal.y >= 0) ? max.y : min.y,
                                 (normal.z >= 0) ? max.z : min.z);
            float backward = Vector3.Dot(normal, corner) + plane.distance;
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

        // http://matthias-mueller-fischer.ch/publications/stablePolarDecomp.pdf
        public static void ExtractRotation(in Matrix4x4 input, out Quaternion output, uint maxIter = 10)
        {
            const float kEpsilon = 1.0e-9f;
            output = Quaternion.identity;
            for (int iter = 0; iter < maxIter; iter++)
            {
                var current = Matrix4x4.TRS(Vector3.zero, output, Vector3.one);
                var O0      = current.GetColumn(0);
                var O1      = current.GetColumn(1);
                var O2      = current.GetColumn(2);

                var I0      = input.GetColumn(0);
                var I1      = input.GetColumn(1);
                var I2      = input.GetColumn(2);

                var omega   = (Vector3.Cross(O0, I0) + Vector3.Cross(O1, I1) + Vector3.Cross(O2, I2)) * 
                              (1.0f / Mathf.Abs(Vector3.Dot(O0, I0) + Vector3.Dot(O1, I1) + Vector3.Dot(O2, I2)) + kEpsilon);
                var w = omega.magnitude;
                if (w < kEpsilon)
                    break;
                output = Quaternion.AngleAxis(w, (1.0f / w) * omega) * output;
                output.Normalize();
            }
        }
    }
}
