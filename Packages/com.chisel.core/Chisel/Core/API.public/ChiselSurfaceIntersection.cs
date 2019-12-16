using System;
using System.Runtime.InteropServices;
using Vector3	= UnityEngine.Vector3;
using Vector2	= UnityEngine.Vector2;
using Plane		= UnityEngine.Plane;

namespace Chisel.Core
{
    /// <summary>
    /// This class defines an intersection into a specific surface of a brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChiselSurfaceIntersection
    {
        public Plane	localPlane;
        public Plane	treePlane;
        public Plane	worldPlane;

        public Vector3	worldIntersection;
        public Vector2	surfaceIntersection;

        public float	distance;

        public readonly static ChiselSurfaceIntersection None = new ChiselSurfaceIntersection()
        {
            localPlane			= new Plane(Vector3.zero, 0),
            treePlane			= new Plane(Vector3.zero, 0),
            worldPlane			= new Plane(Vector3.zero, 0),
            worldIntersection	= Vector3.zero,
            surfaceIntersection = Vector2.zero,
            distance			= float.PositiveInfinity
        };
    };
}
