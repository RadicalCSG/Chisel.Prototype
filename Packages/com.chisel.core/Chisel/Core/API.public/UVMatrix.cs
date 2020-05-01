using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>A 2x4 matrix to calculate the UV coordinates for the vertices of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct UVMatrix
    {
        public UVMatrix(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); }
        public UVMatrix(float4 u, float4 v) { U = u; V = v; }

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public float4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public float4 V;


        // TODO: add description
        public Matrix4x4 ToMatrix() { return (Matrix4x4)ToFloat4x4(); }
        
        // TODO: add description
        public float4x4 ToFloat4x4() { var W = planeNormal; return new float4x4 { c0 = U, c1 = V, c2 = new float4(W, 0), c3 = new Vector4(0, 0, 0, 1) }; }

        public float3 planeNormal { get { return math.normalizesafe(math.cross(U.xyz, V.xyz)); } }

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

        public static UVMatrix TRS(Vector2 translation, Vector3 normal, float rotation, Vector2 scale)
        {
            var orientation     = Quaternion.Inverse(Quaternion.LookRotation(normal));
            var rotation2d      = Quaternion.AngleAxis(rotation, Vector3.forward);
            var scale3d         = new Vector3(scale.x, scale.y, 1.0f);

            // TODO: optimize
            return (UVMatrix)
                    (Matrix4x4.TRS(translation, Quaternion.identity, Vector3.one) *
                     Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale3d) *
                     Matrix4x4.TRS(Vector3.zero, rotation2d, Vector3.one) *

                     Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one));
        }
        
        public void Decompose(out Vector2 translation, out Vector3 normal, out float rotation, out Vector2 scale)
        {
            normal              = planeNormal;
            var orientation     = Quaternion.LookRotation(normal);
            var inv_orientation = Quaternion.Inverse(orientation);

            var u = inv_orientation * U.xyz;
            var v = inv_orientation * V.xyz;

            rotation = -Vector3.SignedAngle(Vector3.right, u, Vector3.forward);

            const double min_rotate = 1.0 / 10000.0;
            rotation = (float)(Math.Round(rotation / min_rotate) * min_rotate);

            scale = new Vector2(u.magnitude, v.magnitude);

            const double min_scale = 1.0 / 10000.0;
            scale.x = (float)(Math.Round(scale.x / min_scale) * min_scale);
            scale.y = (float)(Math.Round(scale.y / min_scale) * min_scale);

            //var rotation2d  = Quaternion.AngleAxis(-rotation, Vector3.forward);
            translation     = new Vector2(U.w, V.w);

            const double min_translation = 1.0 / 32768.0;
            translation.x = (float)(Math.Round(translation.x / min_translation) * min_translation);
            translation.y = (float)(Math.Round(translation.y / min_translation) * min_translation);


            // TODO: figure out a better way to find if we scale negatively
            var newUvMatrix = UVMatrix.TRS(translation, normal, rotation, scale);
            if (Vector3.Dot(V.xyz, newUvMatrix.V.xyz) < 0) scale.y = -scale.y;
            if (Vector3.Dot(U.xyz, newUvMatrix.U.xyz) < 0) scale.x = -scale.x;
        }

        public override string ToString()
        {
            return $@"{{U: {U}, V: {V}}}";  
        }
    }
}