using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Serializable]
    public sealed partial class ChiselWireframe
    {
        internal float3[]	    vertices                = null;
        internal Int32[]	    visibleOuterLines       = null;
        
        // TODO: use hash values instead of 'generation'
        internal uint		    outlineHash		        = 0;
        internal CSGTreeBrush   originBrush			    = default;
        internal int		    originSurfaceID			= 0;
        
        public float3[]	    Vertices                { get { return vertices; } }
        public Int32[]		VisibleOuterLines       { get { return visibleOuterLines; } }

        public bool	Dirty { get { return outlineHash != GetBrushOutlineHash(originBrush); }  }

        public static ChiselWireframe CreateWireframe(CSGTreeBrush brush) { if (!brush.Valid) return null; return CreateBrushWireframe(brush); }
        public static ChiselWireframe CreateWireframe(CSGTreeBrush brush, int surfaceID) { if (!brush.Valid) return null; return CreateSurfaceWireframe(brush, surfaceID); }
        public bool UpdateWireframe() { if (originSurfaceID == 0) return UpdateBrushWireframe(this); else  return UpdateSurfaceWireframe(this);  }
    }
}
