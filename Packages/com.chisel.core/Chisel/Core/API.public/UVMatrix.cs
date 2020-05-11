using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    /// <summary>A 2x4 matrix to calculate the UV coordinates for the vertices of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct UVMatrix
    {
        public const double kScaleStep  = 1.0 / 10000.0;
        public const double kMinScale   = kScaleStep;
        const double kSnapEpsilon       = kScaleStep / 10.0f;

        public UVMatrix(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); }
        public UVMatrix(Vector4 u, Vector4 v) { U = u; V = v; }

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public Vector4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public Vector4 V;


        // TODO: add description
        public Matrix4x4 ToMatrix() { return (Matrix4x4)ToFloat4x4(); }
        
        // TODO: add description
        public float4x4 ToFloat4x4() 
        { 
            return math.transpose(new float4x4 { c0 = U, c1 = V, c2 = new float4(0, 0, 1, 0), c3 = new Vector4(0, 0, 0, 1) }); 
        }

        // TODO: add description
        public UVMatrix Set(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); return this; }

        // TODO: add description
        public static implicit operator Matrix4x4(UVMatrix input) { return input.ToMatrix(); }
        public static implicit operator UVMatrix(Matrix4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        public static implicit operator float4x4(UVMatrix input) { return input.ToFloat4x4(); }
        public static implicit operator UVMatrix(float4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        public static readonly UVMatrix identity = new UVMatrix(new Vector4(1,0,0,0.0f), new Vector4(0,1,0,0.0f));
        public static readonly UVMatrix centered = new UVMatrix(new Vector4(1,0,0,0.5f), new Vector4(0,1,0,0.5f));

        public static UVMatrix TRS(Vector2 translation, float rotation, Vector2 scale)
        {
            var rotation2d      = Quaternion.AngleAxis(rotation, Vector3.forward);
            var scale3d         = new Vector3(scale.x, scale.y, 1.0f);

            // TODO: optimize
            return (UVMatrix)
                    (Matrix4x4.TRS(translation, Quaternion.identity, Vector3.one) *
                     Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale3d) *
                     Matrix4x4.TRS(Vector3.zero, rotation2d, Vector3.one));
        }
        
        public void Decompose(out Vector2 translation, out float rotation, out Vector2 scale)
        {
            var normal  = new float3(0,0,1);

            var u       = (Vector3)U;
            var v       = (Vector3)V;

            if (Math.Abs(u.x) < kSnapEpsilon) u.x = 0;
            if (Math.Abs(u.y) < kSnapEpsilon) u.y = 0;
            if (Math.Abs(u.z) < kSnapEpsilon) u.z = 0;

            if (Math.Abs(v.x) < kSnapEpsilon) v.x = 0;
            if (Math.Abs(v.y) < kSnapEpsilon) v.y = 0;
            if (Math.Abs(v.z) < kSnapEpsilon) v.z = 0;

            //var inverted = Vector3.Cross(U, V).z < 0.0f;

            var rotationU = -Vector3.SignedAngle(Vector3.right, u, normal);
            var rotationV = -Vector3.SignedAngle(Vector3.up,    v, normal);


            rotation    = Math.Abs(rotationU) < Math.Abs(rotationV) ? rotationU : rotationV;

            const double min_rotate = 1.0 / 10000.0;
            rotation = (float)(Math.Round(rotation / min_rotate) * min_rotate);

            var rotation2d  = Quaternion.AngleAxis(rotation, normal);
            var invRotate   = Quaternion.Inverse(rotation2d);

            u = (invRotate * U).normalized;
            v = (invRotate * V).normalized;
            
            var udir        = Vector3.right;
            var vdir        = Vector3.up;

            var uScale      = Vector3.Dot(udir, u);
            var vScale      = Vector3.Dot(vdir, v);
            
            scale = new Vector2(uScale, vScale);

            scale.x = (float)(Math.Round(scale.x / kScaleStep) * kScaleStep);
            scale.y = (float)(Math.Round(scale.y / kScaleStep) * kScaleStep);

            scale.x = (float)Math.Max(Math.Abs(scale.x), kMinScale) * Math.Sign(scale.x);
            scale.y = (float)Math.Max(Math.Abs(scale.y), kMinScale) * Math.Sign(scale.y);


            //var rotation2d  = Quaternion.AngleAxis(-rotation, Vector3.forward);
            translation     = new Vector2(U.w, V.w);

            const double min_translation = 1.0 / 32768.0;
            translation.x = (float)(Math.Round(translation.x / min_translation) * min_translation);
            translation.y = (float)(Math.Round(translation.y / min_translation) * min_translation);


            // TODO: figure out a better way to find if we scale negatively
            var newUvMatrix = UVMatrix.TRS(translation, rotation, scale);            
            if (Vector3.Dot((Vector3)V, (Vector3)newUvMatrix.V) < 0) scale.y = -scale.y;
            if (Vector3.Dot((Vector3)U, (Vector3)newUvMatrix.U) < 0) scale.x = -scale.x;
        }

        public override string ToString()
        {
            return $@"{{U: {U}, V: {V}}}";  
        }
    }
}