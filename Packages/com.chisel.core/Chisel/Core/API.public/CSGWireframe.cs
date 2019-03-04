using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
	[Serializable]
	public sealed partial class CSGWireframe
	{
		internal Vector3[]	vertices                = null;
		internal Int32[]	visibleOuterLines       = null;
		internal Int32[]	visibleInnerLines       = null;
		internal Int32[]	visibleTriangles		= null;
		internal Int32[]	invisibleOuterLines     = null;
		internal Int32[]	invisibleInnerLines     = null;
		internal Int32[]	invalidLines            = null;
		
		// TODO: use hash values instead of 'generation'
		internal UInt64		outlineGeneration		= 0;
		internal int		originBrushID			= 0;
		internal int		originSurfaceID			= 0;
		
		public Vector3[]	Vertices                { get { return vertices; } }
		public Int32[]		VisibleOuterLines       { get { return visibleOuterLines; } }
		public Int32[]		VisibleInnerLines       { get { return visibleInnerLines; } }
		public Int32[]		VisibleTriangles		{ get { return visibleTriangles; } }
		public Int32[]		InvisibleOuterLines     { get { return invisibleOuterLines; } }
		public Int32[]		InvisibleInnerLines     { get { return invisibleInnerLines; } }
		public Int32[]		InvalidLines            { get { return invalidLines; } }

		public bool	Dirty { get { return outlineGeneration != GetBrushOutlineGeneration(originBrushID); }  }

		public static CSGWireframe CreateWireframe(CSGTreeBrush brush) { if (!brush.Valid) return null; return CreateBrushWireframe(brush.NodeID); }
		public static CSGWireframe CreateWireframe(CSGTreeBrush brush, int surfaceID) { if (!brush.Valid) return null; return CreateSurfaceWireframe(brush.NodeID, surfaceID); }
		public bool UpdateWireframe() { if (originSurfaceID == 0) return UpdateBrushWireframe(this); else  return UpdateSurfaceWireframe(this);  }
	}
}
