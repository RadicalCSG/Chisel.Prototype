using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeNode
    {/*
        internal static bool	    IsNodeDirty(CompactNodeID nodeID)		        { return CSGManager.IsNodeDirty(nodeID); }
        internal static bool	    SetDirty(CompactNodeID nodeID)			        { return CSGManager.SetDirty(nodeID); }

        internal static CSGNodeType GetTypeOfNode(CompactNodeID nodeID)         { return CSGManager.GetTypeOfNode(nodeID); }
        internal static bool	    IsValidNodeID(CompactNodeID nodeID)		    { return CSGManager.IsValidNodeID(nodeID); }
        internal static Int32	    GetUserIDOfNode(CompactNodeID nodeID)	    { return CSGManager.GetUserIDOfNode(nodeID); }

        internal static CompactNodeID GetParentOfNode(CompactNodeID nodeID)	    { return CSGManager.GetParentOfNode(nodeID); }
        internal static CompactNodeID GetTreeOfNode(CompactNodeID nodeID)		{ return CSGManager.GetTreeOfNode(nodeID); }

        internal static Int32	GetChildNodeCount(CompactNodeID nodeID)         { return CSGManager.GetChildNodeCount(nodeID); }

        internal static bool	RemoveChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)				        { return CSGManager.RemoveChildNode(nodeID, childNodeID); }
        internal static bool	AddChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)					        { return CSGManager.AddChildNode(nodeID, childNodeID); }
        internal static bool	ClearChildNodes(CompactNodeID nodeID)									                { return CSGManager.ClearChildNodes(nodeID); }
        internal static CompactNodeID GetChildNodeAtIndex(CompactNodeID nodeID, Int32 index)					        { return CSGManager.GetChildNodeAtIndex(nodeID, index); }
        internal static bool	RemoveChildNodeAt(CompactNodeID nodeID, Int32 index)					                { return CSGManager.RemoveChildNodeAt(nodeID, index); }
        internal static bool	InsertChildNode(CompactNodeID nodeID, Int32 index, CompactNodeID childNodeID)	        { return CSGManager.InsertChildNode(nodeID, index, childNodeID); }
        internal static Int32	IndexOfChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)				        { return CSGManager.IndexOfChildNode(nodeID, childNodeID); }
        internal static bool	RemoveChildNodeRange(CompactNodeID nodeID, Int32 index, Int32 count)	                { return CSGManager.RemoveChildNodeRange(nodeID, index, count); }

        internal static bool	DestroyNode(CompactNodeID nodeID)								                        { return CSGManager.DestroyNode(nodeID); }

        internal static bool	GetNodeLocalTransformation(CompactNodeID nodeID, out Matrix4x4 localTransformation)		{ return CSGManager.GetNodeLocalTransformation(nodeID, out localTransformation); }
        internal static bool	SetNodeLocalTransformation(CompactNodeID nodeID, ref Matrix4x4 localTransformation)		{ return CSGManager.SetNodeLocalTransformation(nodeID, ref localTransformation); }
        internal static bool	GetTreeToNodeSpaceMatrix(CompactNodeID nodeID, out Matrix4x4 treeToNodeMatrix)			{ return CSGManager.GetTreeToNodeSpaceMatrix(nodeID, out treeToNodeMatrix); }
        internal static bool	GetNodeToTreeSpaceMatrix(CompactNodeID nodeID, out Matrix4x4 nodeToTreeMatrix)			{ return CSGManager.GetNodeToTreeSpaceMatrix(nodeID, out nodeToTreeMatrix); }

        internal static CSGOperationType GetNodeOperationType(CompactNodeID nodeID)										{ return CSGManager.GetNodeOperationType(nodeID); }
        internal static bool	SetNodeOperationType(CompactNodeID nodeID, CSGOperationType operation)					{ return CSGManager.SetNodeOperationType(nodeID, operation); }


        internal static bool InsertChildNodeRange(CompactNodeID nodeID, Int32 index, CSGTreeNode[] children)
        {
            return CSGManager.InsertChildNodeRange(nodeID, index, children);
        }*/
    }
}