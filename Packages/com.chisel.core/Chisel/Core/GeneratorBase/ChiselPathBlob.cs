using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct ChiselPathBlob
    {
        public struct Point
        {
            public float3       position;
            public quaternion   rotation;
            public float2       scale;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float4x4 ToMatrix(float3 position, quaternion rotation, float2 scale)
            {
                return float4x4.TRS(position, math.inverse(rotation), new float3(scale.x,scale.y,-1)); 
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float4x4 ToMatrix()
            {
                return ToMatrix(position, rotation, scale); 
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float4x4 Lerp(ref Point A, ref Point B, float t)
            {
                var position = MathExtensions.Lerp(A.position, B.position, t);
                var rotation = MathExtensions.Lerp(A.rotation, B.rotation, t);
                var scale    = MathExtensions.Lerp(A.scale, B.scale, t);
                return ToMatrix(position, rotation, scale);
            }
        }

        // TODO: move somewhere else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 NormalizeAngles(float3 angles)
        {
            angles.x = NormalizeAngle(angles.x);
            angles.y = NormalizeAngle(angles.y);
            angles.z = NormalizeAngle(angles.z);
            return angles;
        }

        // TODO: move somewhere else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float NormalizeAngle(float angle)
        {
            while (angle > math.PI * 2f)
                angle -= math.PI * 2f;
            while (angle < 0)
                angle += math.PI * 2f;
            return angle;
        }

        // TODO: move somewhere else
        // From: https://forum.unity.com/threads/is-there-a-conversion-method-from-quaternion-to-euler.624007/#post-5805985
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetQuaternionEulerAngles(quaternion rot)
        {
            float4 q1 = rot.value;
            float sqw = q1.w * q1.w;
            float sqx = q1.x * q1.x;
            float sqy = q1.y * q1.y;
            float sqz = q1.z * q1.z;
            float unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            float test = q1.x * q1.w - q1.y * q1.z;
            float3 v;

            if (test > 0.4995f * unit)
            { // singularity at north pole
                v.y = 2f * math.atan2(q1.y, q1.x);
                v.x = math.PI / 2f;
                v.z = 0;
                return NormalizeAngles(v);
            }
            if (test < -0.4995f * unit)
            { // singularity at south pole
                v.y = -2f * math.atan2(q1.y, q1.x);
                v.x = -math.PI / 2;
                v.z = 0;
                return NormalizeAngles(v);
            }

            rot = new quaternion(q1.w, q1.z, q1.x, q1.y);
            v.y = math.atan2(2f * rot.value.x * rot.value.w + 2f * rot.value.y * rot.value.z, 1 - 2f * (rot.value.z * rot.value.z + rot.value.w * rot.value.w));    // Yaw
            v.x = math.asin(2f * (rot.value.x * rot.value.z - rot.value.w * rot.value.y));                                                                          // Pitch
            v.z = math.atan2(2f * rot.value.x * rot.value.y + 2f * rot.value.z * rot.value.w, 1 - 2f * (rot.value.y * rot.value.y + rot.value.z * rot.value.z));    // Roll
            return NormalizeAngles(v);
        }


        public NativeList<float4x4> GetMatrices(Allocator allocator)
        {
            var matrices = new NativeList<float4x4>(allocator)
            {
                segments[0].ToMatrix()
            };

            for (int s = 1; s < segments.Length; s++)
            {
                var pathPointA = segments[s - 1];
                var pathPointB = segments[s    ];

                if (math.all(pathPointA.position == pathPointB.position) &&
                    math.all(pathPointA.rotation.value == pathPointB.rotation.value) &&
                    math.all(pathPointA.scale == pathPointB.scale))
                    continue;

                int subSegments = 1;
                var offsetQuaternion = math.mul(pathPointB.rotation, math.inverse(pathPointA.rotation));
                var offsetEuler = GetQuaternionEulerAngles(offsetQuaternion);
                if (offsetEuler.x > 180) offsetEuler.x = 360 - offsetEuler.x;
                if (offsetEuler.y > 180) offsetEuler.y = 360 - offsetEuler.y;
                if (offsetEuler.z > 180) offsetEuler.z = 360 - offsetEuler.z;
                var maxAngle = math.max(math.max(offsetEuler.x, offsetEuler.y), offsetEuler.z);
                if (maxAngle != 0)
                    subSegments = math.max(1, (int)math.ceil(maxAngle / 5));

                if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                    (subSegments & 1) == 1)
                    subSegments += 1;

                for (int n = 1; n <= subSegments; n++)
                    matrices.Add(ChiselPathBlob.Point.Lerp(ref pathPointA, ref pathPointB, n / (float)subSegments));
            }
            return matrices;
        }

        public UnsafeList<float4x4> GetUnsafeMatrices(Allocator allocator)
        {
            var matrices = new UnsafeList<float4x4>(segments.Length, allocator);
            matrices.Add(segments[0].ToMatrix());

            for (int s = 1; s < segments.Length; s++)
            {
                var pathPointA = segments[s - 1];
                var pathPointB = segments[s    ];

                if (math.all(pathPointA.position == pathPointB.position) &&
                    math.all(pathPointA.rotation.value == pathPointB.rotation.value) &&
                    math.all(pathPointA.scale == pathPointB.scale))
                    continue;

                int subSegments = 1;
                var offsetQuaternion = math.mul(pathPointB.rotation, math.inverse(pathPointA.rotation));
                var offsetEuler = GetQuaternionEulerAngles(offsetQuaternion);
                if (offsetEuler.x > 180) offsetEuler.x = 360 - offsetEuler.x;
                if (offsetEuler.y > 180) offsetEuler.y = 360 - offsetEuler.y;
                if (offsetEuler.z > 180) offsetEuler.z = 360 - offsetEuler.z;
                var maxAngle = math.max(math.max(offsetEuler.x, offsetEuler.y), offsetEuler.z);
                if (maxAngle != 0)
                    subSegments = math.max(1, (int)math.ceil(maxAngle / 5));

                if ((pathPointA.scale.x / pathPointA.scale.y) != (pathPointB.scale.x / pathPointB.scale.y) &&
                    (subSegments & 1) == 1)
                    subSegments += 1;

                for (int n = 1; n <= subSegments; n++)
                    matrices.Add(ChiselPathBlob.Point.Lerp(ref pathPointA, ref pathPointB, n / (float)subSegments));
            }
            return matrices;
        }


        public BlobArray<Point> segments;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Point Convert(ChiselPathPoint srcPoint)
        {
            return new Point
            {
                position = srcPoint.position,
                rotation = srcPoint.rotation,
                scale    = srcPoint.scale,
            };
        }

        public static BlobAssetReference<ChiselPathBlob> Convert(ChiselPath path, Allocator allocator)
        {
            path.UpgradeIfNecessary();
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<ChiselPathBlob>();
                var srcControlPoints = path.segments;
                var dstControlPoints = builder.Allocate(ref root.segments, srcControlPoints.Length);
                for (int i = 0; i < srcControlPoints.Length; i++)
                    dstControlPoints[i] = Convert(srcControlPoints[i]);
                return builder.CreateBlobAssetReference<ChiselPathBlob>(allocator);
            }
        }
    }
}