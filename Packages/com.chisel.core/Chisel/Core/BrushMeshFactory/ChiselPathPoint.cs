using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselPathPoint
    {
        static readonly Vector4		unitX		= new Vector4(1,0,0,0);
        static readonly Vector4		unitY		= new Vector4(0,1,0,0);
        static readonly Vector4		unitZ		= new Vector4(0,0,1,0);
        static readonly Vector4		unitW		= new Vector4(0,0,0,1);
        static readonly Matrix4x4	swizzleYZ;

        static ChiselPathPoint()
        {
            swizzleYZ.SetColumn(0, unitX);
            swizzleYZ.SetColumn(1, unitZ);
            swizzleYZ.SetColumn(2, unitY);
            swizzleYZ.SetColumn(3, unitW);
        }

        public ChiselPathPoint(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position	= position;
            this.rotation	= rotation;
            this.scale		= scale;
        }
        
        public ChiselPathPoint(Vector3 position)
        {
            this.position	= position;
            this.rotation	= Quaternion.identity;
            this.scale		= Vector3.one;
        }

        [PositionValue] public Vector3      position;
        [EulerValue   ] public Quaternion   rotation;
        [ScaleValue   ] public Vector2      scale;
        
        static Matrix4x4 ToMatrix(Vector3 position, Quaternion rotation, Vector2 scale)
        {
            return	Matrix4x4.TRS(position, rotation, Vector3.one) *
                    swizzleYZ *
                    Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale.x,scale.y,1)); 
        }

        public Matrix4x4 ToMatrix()
        {
            return ToMatrix(position, rotation, scale); 
        }

        public static Matrix4x4 Lerp(ref ChiselPathPoint A, ref ChiselPathPoint B, float t)
        {
            var position = MathExtensions.Lerp(A.position, B.position, t);
            var rotation = MathExtensions.Lerp(A.rotation, B.rotation, t);
            var scale    = MathExtensions.Lerp(A.scale, B.scale, t);
            return ToMatrix(position, rotation, scale);
        }
    }

}
