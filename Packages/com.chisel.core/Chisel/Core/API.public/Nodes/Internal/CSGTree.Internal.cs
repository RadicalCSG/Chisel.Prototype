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
                
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int		CountOfBrushesInTree			    { get { return CSGManager.GetNumberOfBrushesInTree(treeNodeID); } }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CSGTreeBrush GetChildBrushAtIndex(int index) { return CSGManager.GetChildBrushAtIndex(treeNodeID, index); }
        
        // Do not use. This method might be removed/renamed in the future
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool		IsInTree(CSGTreeBrush brush)	    { return CSGManager.DoesTreeContainBrush(treeNodeID, brush); }
    }
}