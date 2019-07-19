using System;

namespace Chisel.Core
{
    static partial class CSGManager
    {

        private static bool DeepDestroyNode(CSGTreeNode node)
        {
            if (!node.Valid)
                return false;
            
            switch (node.Type)
            {
                case CSGNodeType.Branch:	if (!DeepDestroyNodes(((CSGTreeBranch)node).ChildrenToArray())) return false; break;
                case CSGNodeType.Tree:		if (!DeepDestroyNodes(((CSGTree)node).ChildrenToArray())) return false; break;
            }
            return CSGTreeNode.DestroyNode(node.nodeID);
        }

        private static bool DeepDestroyNodes(CSGTreeNode[] nodeIDs)
        {
            if (nodeIDs == null)
                return false;

            // TODO: do this without recursion
            // TODO: do this without allocations
            for (int i = 0; i < nodeIDs.Length; i++)
            {
                switch (nodeIDs[i].Type)
                {
                    case CSGNodeType.Branch:	if (!DeepDestroyNodes(((CSGTreeBranch)nodeIDs[i]).ChildrenToArray())) return false; break;
                    case CSGNodeType.Tree:		if (!DeepDestroyNodes(((CSGTree)nodeIDs[i]).ChildrenToArray())) return false; break;
                }
            }
            return DestroyNodes(nodeIDs);
        }



        // Do not use. This method might be removed/renamed in the future
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool	ClearDirty	(CSGTreeNode   node)	{ return ClearDirty(node.NodeID); }



        internal static CSGTreeNode DuplicateInternal(CSGTreeNode node)
        {
            switch (node.Type)
            {
                case CSGNodeType.Brush:
                {
                    var srcTreeBrush = (CSGTreeBrush)node;
                    return CSGTreeBrush.Create(srcTreeBrush.UserID, srcTreeBrush.LocalTransformation, srcTreeBrush.BrushMesh, srcTreeBrush.Operation, srcTreeBrush.Flags);
                }
                case CSGNodeType.Tree:
                {
                    var srcTree = (CSGTree)node;
                    return CSGTree.Create(srcTree.UserID, DuplicateChildNodesInternal(srcTree));
                }
                case CSGNodeType.Branch:
                {
                    var srcTreeBranch = (CSGTreeBranch)node;
                    return CSGTreeBranch.Create(srcTreeBranch.UserID, srcTreeBranch.Operation, DuplicateChildNodesInternal(srcTreeBranch));
                }
                default:
                    throw new NotImplementedException();
            }
        }

        internal static CSGTreeNode[] DuplicateChildNodesInternal(CSGTree tree)
        {
            var childCount = tree.Count;
            if (childCount == 0)
                return new CSGTreeNode[0];
            var duplicateNodes = new CSGTreeNode[childCount];
            for (int i = 0; i < childCount; i++)
                duplicateNodes[i] = DuplicateInternal(tree[i]);
            return duplicateNodes;
        }

        internal static CSGTreeNode[] DuplicateChildNodesInternal(CSGTreeBranch branch)
        {
            var childCount = branch.Count;
            if (childCount == 0)
                return new CSGTreeNode[0];
            var duplicateNodes = new CSGTreeNode[childCount];
            for (int i = 0; i < childCount; i++)
                duplicateNodes[i] = DuplicateInternal(branch[i]);
            return duplicateNodes;
        }

        internal static CSGTreeNode[] DuplicateInternal(CSGTreeNode[] nodes)
        {
            if (nodes == null)
                return null;
            if (nodes.Length == 0)
                return new CSGTreeNode[0];

            var duplicateNodes = new CSGTreeNode[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                duplicateNodes[i] = DuplicateInternal(nodes[i]);
            return duplicateNodes;
        }
    }
}