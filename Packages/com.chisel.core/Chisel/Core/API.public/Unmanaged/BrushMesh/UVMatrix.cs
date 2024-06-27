using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public Vector4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public Vector4 V;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UVMatrix(float4 u, float4 v) 
        { 
            U = u; 
            V = v;
            Validate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UVMatrix(Matrix4x4 input) 
        { 
            U = input.GetRow(0); 
            V = input.GetRow(1);
            Validate();
        }

        void Validate()
        {
            if (math.any(!math.isfinite(U)) || math.any(!math.isfinite(V)))
            {
                Debug.LogError("Resetting UV values since they are set to invalid floating point numbers.");
                U = new float4(1, 0, 0, 0);
                V = new float4(0, 1, 0, 0);
            }
        }

        // TODO: add description
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ToMatrix4x4() { return (Matrix4x4)ToFloat4x4(); }

        // TODO: add description
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float4x4 ToFloat4x4()
        {
            Validate();
            var w = new float4(math.cross(((float4)U).xyz, ((float4)V).xyz), 0);
            return math.transpose(new float4x4 { c0 = U, c1 = V, c2 = w, c3 = new float4(0, 0, 0, 1) }); 
        }

        // TODO: add description
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Matrix4x4(UVMatrix input) { return input.ToMatrix4x4(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UVMatrix(Matrix4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4x4(UVMatrix input) { return input.ToFloat4x4(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UVMatrix(float4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        public static readonly UVMatrix identity = new(new float4(1,0,0,0.0f), new float4(0,1,0,0.0f));
        public static readonly UVMatrix centered = new(new float4(1,0,0,0.5f), new float4(0,1,0,0.5f));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UVMatrix TRS(float2 translation, float rotation, float2 scale)
        {
            var scale3d = new float3(scale, 1.0f);
            var rotation2d = quaternion.AxisAngle(new float3(0, 0, 1), math.radians(rotation));
            return (UVMatrix)(float4x4.TRS(new float3(translation, 0), rotation2d, scale3d));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decompose(out Vector2 translation, out float rotation, out Vector2 scale)
        {
            translation     = new Vector2(U.w, V.w);

            const double min_translation = 1.0 / 32768.0;
            translation.x = (float)(math.round(translation.x / min_translation) * min_translation);
            translation.y = (float)(math.round(translation.y / min_translation) * min_translation);

            var matrix = ToFloat4x4();

            var q = new quaternion(math.transpose((float3x3)matrix));
            var q_i = math.inverse(q);
            var q_u = ((UnityEngine.Quaternion)q_i);

            rotation = q_u.eulerAngles[2];

            const double min_rotate = 1.0 / 10000.0;
            rotation = (float)(Math.Round(rotation / min_rotate) * min_rotate);
            
            var uScale = math.length(math.mul(q, matrix.c0.xyz));
            var vScale = math.length(math.mul(q, matrix.c1.xyz));
            
            scale = new float2(uScale, vScale);

            scale.x = (float)(math.round(scale.x / kScaleStep) * kScaleStep);
            scale.y = (float)(math.round(scale.y / kScaleStep) * kScaleStep);

            scale.x = (float)math.max(math.abs(scale.x), kMinScale) * Math.Sign(scale.x);
            scale.y = (float)math.max(math.abs(scale.y), kMinScale) * Math.Sign(scale.y);
            
            var newUvMatrix = UVMatrix.TRS(translation, rotation, scale);
            if (math.dot(((float4)V).xyz, ((float4)newUvMatrix.V).xyz) < 0) scale.y = -scale.y;
            if (math.dot(((float4)U).xyz, ((float4)newUvMatrix.U).xyz) < 0) scale.x = -scale.x;
            Validate();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString() { return $@"{{U: {U}, V: {V}}}"; }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(UVMatrix left, UVMatrix right) { return left.U == right.U && left.V == right.V; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(UVMatrix left, UVMatrix right) { return left.U != right.U || left.V != right.V; }

        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object obj) { if (obj is not UVMatrix) return false; var uv = (UVMatrix)obj; return this == uv; }
        [EditorBrowsable(EditorBrowsableState.Never), MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() { unchecked { return (int)math.hash(new uint2(math.hash(U), math.hash(V))); } }
        #endregion
    }
}