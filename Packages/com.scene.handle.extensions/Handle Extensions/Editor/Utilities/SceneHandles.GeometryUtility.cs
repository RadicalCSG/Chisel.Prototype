using UnityEngine;

namespace UnitySceneExtensions
{
    public class GeometryUtility
    {		
//		private static readonly Vector3 PositiveY = new Vector3(0, 1, 0);
        private static readonly Vector3 NegativeY = new Vector3(0, -1, 0);
        private static readonly Vector3 PositiveZ = new Vector3(0, 0, 1);

        public static Vector3 CalculateTangent(Vector3 vector)
        {
            var absX = Mathf.Abs(vector.x);
            var absY = Mathf.Abs(vector.y);
            var absZ = Mathf.Abs(vector.z);
            
            if (absY > absX && absY > absZ)
                return PositiveZ;
            
            return NegativeY;
        }

        public static void CalculateTangents(Vector3 normal, out Vector3 tangent, out Vector3 binormal)
        {
            tangent		= Vector3.Cross(normal, GeometryUtility.CalculateTangent(normal)).normalized;
            binormal	= Vector3.Cross(normal, tangent).normalized;
        }

        public static Vector3 ProjectPointPlane(Vector3 point, Plane plane)
        {
            float px = point.x;
            float py = point.y;
            float pz = point.z;

            float nx = plane.normal.x;
            float ny = plane.normal.y;
            float nz = plane.normal.z;

            float ax  = (px + (nx * plane.distance)) * nx;
            float ay  = (py + (ny * plane.distance)) * ny;
            float az  = (pz + (nz * plane.distance)) * nz;
            float dot = ax + ay + az;

            float rx = px - (dot * nx);
            float ry = py - (dot * ny);
            float rz = pz - (dot * nz);

            return new Vector3(rx, ry, rz);
        }

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

    }
}
