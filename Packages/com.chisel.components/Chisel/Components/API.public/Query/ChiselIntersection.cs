using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using GameObject = UnityEngine.GameObject;
using Camera = UnityEngine.Camera;
using Plane = UnityEngine.Plane;
using System.Runtime.InteropServices;

namespace Chisel.Components
{
    /// <summary>
    /// This class defines an intersection into a specific brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChiselIntersection
    {
        public ChiselModel	model;
        public ChiselNode	treeNode;

        public Plane        worldPlane;
        public Vector3      worldPlaneIntersection;

        public CSGTreeBrushIntersection brushIntersection;

        public readonly static ChiselIntersection None = new ChiselIntersection()
        {
            model                   = null,
            treeNode                    = null,
            worldPlane			    = new Plane(Vector3.zero, 0),
            worldPlaneIntersection	= Vector3.zero,
            brushIntersection       = CSGTreeBrushIntersection.None
        };
    };
}
