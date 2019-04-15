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
    public struct CSGSurfaceIntersection
    {
        public Plane	localPlane;
        public Plane	modelPlane;
        public Plane	worldPlane;	// TODO: is not in worldSpace???

        public Vector3	worldIntersection;
        public Vector2	surfaceIntersection;

        public float	distance;

        public readonly static CSGSurfaceIntersection None = new CSGSurfaceIntersection()
        {
            localPlane			= new Plane(Vector3.zero, 0),
            modelPlane			= new Plane(Vector3.zero, 0),
            worldPlane			= new Plane(Vector3.zero, 0),
            worldIntersection	= Vector3.zero,
            surfaceIntersection = Vector2.zero,
            distance			= float.PositiveInfinity
        };
    };
}
