using System;
using System.Runtime.InteropServices;
using Chisel.Core;
using Vector3 = UnityEngine.Vector3;
using Plane = UnityEngine.Plane;

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
