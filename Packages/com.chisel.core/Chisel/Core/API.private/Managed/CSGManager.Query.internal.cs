using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
	// TODO: clean up
	static partial class CSGManager
	{
#if USE_MANAGED_CSG_IMPLEMENTATION

        internal static bool RayCastMulti(MeshQuery[] meshQuery, // TODO: add meshquery support here
										  Vector3 worldRayStart,
										  Vector3 worldRayEnd,
										  int filterLayerParameter0,
										  out CSGTreeBrushIntersection[] intersections,
										  CSGTreeNode[] ignoreNodes = null)
		{
			// TODO: implement
			intersections = null;
			return false;
		}

		internal static bool GetNodesInFrustum(Plane[] planes,
											   out CSGTreeNode[] nodes)
		{
			nodes = null;

			if (planes == null ||
				planes.Length != 6)
			{
				return false;
			}

			// TODO: implement
			return false;
		}

		internal static bool GetUserIDsInFrustum(Plane[] planes,
												 out Int32[] userIDs)
		{
			userIDs = null;

			if (planes == null ||
				planes.Length != 6)
			{
				return false;
			}

			// TODO: implement
			return false;
		}

#endif
	}
}
