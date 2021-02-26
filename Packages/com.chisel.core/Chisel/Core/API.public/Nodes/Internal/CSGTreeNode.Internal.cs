using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeNode
    {
        internal static bool	IsNodeDirty(Int32 nodeID)		{ return CSGManager.IsNodeDirty(nodeID); }
        internal static bool	SetDirty(Int32 nodeID)			{ return CSGManager.SetDirty(nodeID); }

        internal static CSGNodeType GetTypeOfNode(Int32 nodeID) { return CSGManager.GetTypeOfNode(nodeID); }
        internal static bool	IsNodeIDValid(Int32 nodeID)		{ return CSGManager.IsValidNodeID(nodeID); }
        internal static Int32	GetUserIDOfNode(Int32 nodeID)	{ return CSGManager.GetUserIDOfNode(nodeID); }

        internal static Int32	GetParentOfNode(Int32 nodeID)	{ return CSGManager.GetParentOfNode(nodeID); }
        internal static Int32	GetTreeOfNode(Int32 nodeID)		{ return CSGManager.GetTreeOfNode(nodeID); }

        internal static Int32	GetChildNodeCount(Int32 nodeID) { return CSGManager.GetChildNodeCount(nodeID); }

        internal static bool	RemoveChildNode(Int32 nodeID, Int32 childNodeID)				{ return CSGManager.RemoveChildNode(nodeID, childNodeID); }
        internal static bool	AddChildNode(Int32 nodeID, Int32 childNodeID)					{ return CSGManager.AddChildNode(nodeID, childNodeID); }
        internal static bool	ClearChildNodes(Int32 nodeID)									{ return CSGManager.ClearChildNodes(nodeID); }
        internal static Int32	GetChildNodeAtIndex(Int32 nodeID, Int32 index)					{ return CSGManager.GetChildNodeAtIndex(nodeID, index); }
        internal static bool	RemoveChildNodeAt(Int32 nodeID, Int32 index)					{ return CSGManager.RemoveChildNodeAt(nodeID, index); }
        internal static bool	InsertChildNode(Int32 nodeID, Int32 index, Int32 childNodeID)	{ return CSGManager.InsertChildNode(nodeID, index, childNodeID); }
        internal static Int32	IndexOfChildNode(Int32 nodeID, Int32 childNodeID)				{ return CSGManager.IndexOfChildNode(nodeID, childNodeID); }
        internal static bool	RemoveChildNodeRange(Int32 nodeID, Int32 index, Int32 count)	{ return CSGManager.RemoveChildNodeRange(nodeID, index, count); }

        internal static bool	DestroyNode(Int32 nodeID)										{ return CSGManager.DestroyNode(nodeID); }

        internal static bool	GetNodeLocalTransformation(Int32 nodeID, out Matrix4x4 localTransformation)		{ return CSGManager.GetNodeLocalTransformation(nodeID, out localTransformation); }
        internal static bool	SetNodeLocalTransformation(Int32 nodeID, ref Matrix4x4 localTransformation)		{ return CSGManager.SetNodeLocalTransformation(nodeID, ref localTransformation); }
        internal static bool	GetTreeToNodeSpaceMatrix(Int32 nodeID, out Matrix4x4 treeToNodeMatrix)			{ return CSGManager.GetTreeToNodeSpaceMatrix(nodeID, out treeToNodeMatrix); }
        internal static bool	GetNodeToTreeSpaceMatrix(Int32 nodeID, out Matrix4x4 nodeToTreeMatrix)			{ return CSGManager.GetNodeToTreeSpaceMatrix(nodeID, out nodeToTreeMatrix); }

        internal static Int32	GetNodeOperationType(Int32 nodeID)												{ return (int)CSGManager.GetNodeOperationType(nodeID); }
        internal static bool	SetNodeOperationType(Int32 nodeID, CSGOperationType operation)					{ return CSGManager.SetNodeOperationType(nodeID, operation); }


        internal static bool InsertChildNodeRange(Int32 nodeID, Int32 index, CSGTreeNode[] children)
        {
            return CSGManager.InsertChildNodeRange(nodeID, index, children);
        }
    }
}