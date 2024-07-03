using System;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    /// <summary>
    /// This class defines an intersection into a specific brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CSGTreeBrushIntersection
    {
        public CSGTree		tree;
        public CSGTreeBrush	brush;
        
        public Int32        surfaceIndex;
        
        public ChiselSurfaceIntersection surfaceIntersection;

        public readonly static CSGTreeBrushIntersection None = new CSGTreeBrushIntersection()
        {
            tree				= (CSGTree)CSGTreeNode.Invalid,
            brush				= (CSGTreeBrush)CSGTreeNode.Invalid,
            surfaceIndex		= -1,
            surfaceIntersection	= ChiselSurfaceIntersection.None
        };
    };
}
