using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeNode
    {

        internal static Matrix4x4 GetNodeLocalTransformation(Int32 nodeID)
        {
            if (GetNodeLocalTransformation(nodeID, out Matrix4x4 result))
                return result;
            return Matrix4x4.identity;
        }
    }
}