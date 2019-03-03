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

		public Int32        brushUserID;
		public Int32        surfaceID;
		public Int32        surfaceIndex;

		public CSGSurfaceIntersection surfaceIntersection;
	};
}
