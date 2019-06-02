using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>A 2x4 matrix to calculate the UV coordinates for the vertices of a <see cref="Chisel.Core.BrushMesh"/>.</summary>
    /// <seealso cref="Chisel.Core.BrushMesh.Polygon"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct UVMatrix
    {
        public UVMatrix(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); }
        public UVMatrix(Vector4 u, Vector4 v) { U = u; V = v; }

        /// <value>Used to convert a vertex coordinate to a U texture coordinate</value>
        public Vector4 U;

        /// <value>Used to convert a vertex coordinate to a V texture coordinate</value>
        public Vector4 V;


        // TODO: add description
        public Matrix4x4 ToMatrix() { var W = planeNormal; return new Matrix4x4(U, V, W, new Vector4(0, 0, 0, 1)).transpose; }

        public Vector3 planeNormal { get { return Vector3.Cross(((Vector3)U).normalized, ((Vector3)V).normalized).normalized; } }

        // TODO: add description
        public UVMatrix Set(Matrix4x4 input) { U = input.GetRow(0); V = input.GetRow(1); return this; }

        // TODO: add description
        public static implicit operator Matrix4x4(UVMatrix input) { return input.ToMatrix(); }
        public static implicit operator UVMatrix(Matrix4x4 input) { return new UVMatrix(input); }

        // TODO: add description
        public static readonly UVMatrix identity = new UVMatrix(new Vector4(1,0,0,0.0f), new Vector4(0,1,0,0.0f));
        public static readonly UVMatrix centered = new UVMatrix(new Vector4(1,0,0,0.5f), new Vector4(0,1,0,0.5f));

        public static UVMatrix TRS(Vector2 translation, Vector3 normal, float rotation, Vector2 scale)
        {
            var orientation = Quaternion.Inverse(Quaternion.LookRotation(normal, Vector3.forward));
            var rotation2d  = Quaternion.AngleAxis(rotation, Vector3.forward);
            var scale3d     = new Vector3(scale.x, scale.y, 1.0f);

            return (UVMatrix)
                    (Matrix4x4.TRS(translation, rotation2d, scale3d) *
                    Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one));
        }
        
        public void Decompose(out Vector2 translation, out Vector3 normal, out float rotation, out Vector2 scale)
        {            
            normal              = planeNormal;
            var orientation     = Quaternion.LookRotation(normal, Vector3.forward);
            
            // TODO: simplify this part to not require a Matrix4x4
            var transform = ToMatrix();
            transform.SetColumn(3, new Vector4(0, 0, 0, 1));
            transform *= Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one);
            var u = transform.MultiplyVector(Vector3.right);
            var v = transform.MultiplyVector(Vector3.up);
            
            rotation        = Vector3.SignedAngle(Vector3.right, u, Vector3.forward);
            scale           = new Vector2(u.magnitude, v.magnitude);
            translation     = new Vector2(U.w, V.w);
        }

        public override string ToString()
        {
            return $@"{{U: {U}, V: {V}}}";  
        }
    }
}