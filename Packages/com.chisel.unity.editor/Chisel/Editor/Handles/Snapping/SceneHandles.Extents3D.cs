using System;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    [Serializable]
    public struct Extents3D
    {
        public Extents3D(Vector3 point)
        {
            min = point;
            max = point;
        }
        public Extents3D(Vector3 min, Vector3 max)
        {
            this.min = min;
            this.max = max;
        }
        public Extents3D(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            min.x = minX;
            min.y = minY;
            min.z = minZ;

            max.x = maxX;
            max.y = maxY;
            max.z = maxZ;
        }
        public Vector3 min;
        public Vector3 max;

        public Extents1D x { get { return new Extents1D(min.x, max.x); } set { min.x = value.min; max.x = value.max; } }
        public Extents1D y { get { return new Extents1D(min.y, max.y); } set { min.y = value.min; max.y = value.max; } }
        public Extents1D z { get { return new Extents1D(min.z, max.z); } set { min.z = value.min; max.z = value.max; } }


        public Extents1D this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new ArgumentException("index must be 0,1 or 2");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; return;
                    case 1: y = value; return;
                    case 2: z = value; return;
                    default: throw new ArgumentException("index must be 0,1 or 2");
                }
            }
        }

        public Vector3 size		{ get { return max - min; } }
        public Vector3 center	{ get { return (max + min) * 0.5f; } }
        
        public readonly static Extents3D empty = new Extents3D(Vector3.zero, Vector3.zero);

        public static Extents3D operator +(Extents3D extents, Vector3 offset) { return new Extents3D(extents.min + offset, extents.max + offset); }
        public static Extents3D operator -(Extents3D extents, Vector3 offset) { return new Extents3D(extents.min - offset, extents.max - offset); }


        public static Extents3D CreateExtent(Vector3 point, Quaternion orientation, Vector3 origin) { return CreateExtent(point, Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one), origin); }
        public static Extents3D CreateExtent(Vector3 point, Matrix4x4 transformation, Vector3 origin) { return CreateExtent(point, transformation.GetColumn(0), transformation.GetColumn(1), transformation.GetColumn(2), origin); }
        public static Extents3D CreateExtent(Vector3 point, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin) { var distance = SnappingUtility.WorldPointToDistances(point, axisX, axisY, axisZ, origin); return new Extents3D(distance, distance); }

        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Quaternion orientation, Vector3 origin) { return GetExtentsOfPointArray(points, Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one), origin); }
        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Quaternion orientation) { return GetExtentsOfPointArray(points, Matrix4x4.TRS(Vector3.zero, orientation, Vector3.one), Vector3.zero); }
        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Matrix4x4 transformation, Vector3 origin) { return GetExtentsOfPointArray(points, transformation.GetColumn(0), transformation.GetColumn(1), transformation.GetColumn(2), Vector3.zero); }
        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Matrix4x4 transformation) { return GetExtentsOfPointArray(points, transformation.GetColumn(0), transformation.GetColumn(1), transformation.GetColumn(2), Vector3.zero); }

        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 origin)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < points.Length; i++)
            {
                var distance = SnappingUtility.WorldPointToDistances(points[i], axisX, axisY, axisZ, origin);
                min.x = Mathf.Min(min.x, distance.x);
                min.y = Mathf.Min(min.y, distance.y);
                min.z = Mathf.Min(min.z, distance.z);

                max.x = Mathf.Max(max.x, distance.x);
                max.y = Mathf.Max(max.y, distance.y);
                max.z = Mathf.Max(max.z, distance.z);
            }
            return new Extents3D(min, max);
        }

        public static Extents3D GetExtentsOfPointArray(Vector3[] points, Vector3 axisX, Vector3 axisY, Vector3 axisZ) { return GetExtentsOfPointArray(points, axisX, axisY, axisZ, Vector3.zero); }


        public override string ToString()
        {
            return string.Format("(Min: {0}, Max: {1})", min, max);
        }
    }
}
