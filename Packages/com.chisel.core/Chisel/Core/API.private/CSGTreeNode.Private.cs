using System;
using UnityEngine;

namespace Chisel.Core
{
	partial struct CSGTreeNode
	{
		internal static Matrix4x4 GetTreeToNodeSpaceMatrix(Int32 nodeID)
		{
			Matrix4x4 result = Matrix4x4.identity;
			if (GetTreeToNodeSpaceMatrix(nodeID, out result))
				return result;
			return Matrix4x4.identity;
		}		

		internal static Matrix4x4 GetNodeToTreeSpaceMatrix(Int32 nodeID)
		{
			Matrix4x4 result = Matrix4x4.identity;
			if (GetNodeToTreeSpaceMatrix(nodeID, out result))
				return result;
			return Matrix4x4.identity;
		}

		internal static Matrix4x4 GetNodeLocalTransformation(Int32 nodeID)
		{
			Matrix4x4 result = Matrix4x4.identity;
			if (GetNodeLocalTransformation(nodeID, out result))
				return result;
			return Matrix4x4.identity;
		}

		internal static int CopyTo(Int32 nodeID, CSGTreeNode[] children, int arrayIndex)
		{
			if (children == null)
				throw new ArgumentNullException("children");

			var childCount = GetChildNodeCount(nodeID);
			if (childCount <= 0)
				return 0;

			if (children.Length + arrayIndex < childCount)
				throw new ArgumentException(string.Format("The array does not have enough elements, its length is {0} and needs at least {1}", children.Length, childCount), "children");

			return CopyToUnsafe(nodeID, childCount, children, arrayIndex);
		}
	}
}