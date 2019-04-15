using System;
using UnityEngine;

namespace UnitySceneExtensions
{
    [Serializable]
    public struct Extents1D
    {
        public Extents1D(float value)
        {
            min = value;
            max = value;
        }
        public Extents1D(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
        public float min;
        public float max;
        
        public readonly static Extents1D empty = new Extents1D(0, 0);
        
        public float size	{ get { return max - min; } }
        public float center { get { return (max + min) * 0.5f; } }

        public static Extents1D operator +(Extents1D extents, float offset) { return new Extents1D(extents.min + offset, extents.max + offset); }
        public static Extents1D operator -(Extents1D extents, float offset) { return new Extents1D(extents.min - offset, extents.max - offset); }
        
        public static Extents1D operator +(Extents1D extents, Extents1D other) { return new Extents1D(extents.min + other.min, extents.max + other.max); }
        public static Extents1D operator -(Extents1D extents, Extents1D other) { return new Extents1D(extents.min - other.min, extents.max - other.max); }
        

        public static Extents1D GetExtentsOfPointArray(Vector3[] points, Vector3 direction, Vector3 origin)
        {
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                var distance = SnappingUtility.WorldPointToDistance(points[i], direction, origin);
                min = Mathf.Min(min, distance);
                max = Mathf.Max(max, distance);
            }
            return new Extents1D(min, max);
        }

        public static Extents1D GetExtentsOfPointArray(Vector3[] points, Vector3 direction) { return GetExtentsOfPointArray(points, direction, Vector3.zero); }


        public override string ToString()
        {
            return string.Format("(Min: {0}, Max: {1})", min, max);
        }
    }
}
