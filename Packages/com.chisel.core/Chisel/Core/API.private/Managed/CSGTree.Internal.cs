using System;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTree
    {
        private static bool	    GenerateTree(Int32 userID, out Int32 generatedTreeNodeID)
        {
            return CSGManager.GenerateTree(userID, out generatedTreeNodeID);
        }

        private static Int32	FindTreeByUserID(Int32 userID)
        {
            return CSGManager.FindTreeByUserID(userID);
        }

        private static GeneratedMeshDescription[] GetMeshDescriptions(Int32				 treeNodeID,
                                                                      MeshQuery[]		 meshQueries,
                                                                      VertexChannelFlags vertexChannelMask)
        {
            return CSGManager.GetMeshDescriptions(treeNodeID, meshQueries, vertexChannelMask);
        }

        private static GeneratedMeshContents GetGeneratedMesh(int treeNodeID, GeneratedMeshDescription meshDescription)
        {
            return CSGManager.GetGeneratedMesh(treeNodeID, meshDescription);
        }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int		CountOfBrushesInTree			{ get { return CSGManager.GetNumberOfBrushesInTree(treeNodeID); } }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int		GetChildBrushNodeIDAtIndex(int index) { return CSGManager.GetChildBrushNodeIDAtIndex(treeNodeID, index); }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool		IsInTree(CSGTreeBrush brush)	{ return CSGManager.DoesTreeContainBrush(treeNodeID, brush.NodeID); }

    }
}