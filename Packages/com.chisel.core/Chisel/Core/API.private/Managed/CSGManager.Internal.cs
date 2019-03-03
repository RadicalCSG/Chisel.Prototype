using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: clean up
    static partial class CSGManager
    {
#if USE_INTERNAL_IMPLEMENTATION
        internal const int kDefaultUserID = 0;

        struct NodeFlags
        {
            public NodeStatusFlags	status;
            public CSGOperationType operationType;
            public CSGNodeType		nodeType;

            public void SetOperation	(CSGOperationType operation)	{ this.operationType = operation; }
            public void SetStatus		(NodeStatusFlags status)		{ this.status = status; }
            public void SetNodeType		(CSGNodeType nodeType)			{ Debug.Log("SetNodeType " + this.nodeType + " -> " + nodeType);  this.nodeType = nodeType; Debug.Log("after " + this.nodeType); }
            public bool IsAnyNodeFlagSet(NodeStatusFlags flag)			{ return (status & flag) != NodeStatusFlags.None; }
            public bool IsNodeFlagSet	(NodeStatusFlags flag)			{ return (status & flag) == flag; }
            public void UnSetNodeFlag	(NodeStatusFlags flag)			{ status &= ~flag; }
            public void SetNodeFlag		(NodeStatusFlags flag)			{ status |= flag; }

            internal void Reset()
            {
                status			= NodeStatusFlags.None;
                operationType	= CSGOperationType.Invalid;
                nodeType		= CSGNodeType.None;
            }
        };

        struct NodeTransform
        {
            public Matrix4x4 nodeToTree;
            public Matrix4x4 treeToNode;
            public void Reset()
            {
                nodeToTree = Matrix4x4.identity;
                treeToNode = Matrix4x4.identity;
            }
        };

        struct NodeLocalTransform
        {
            public Matrix4x4 localTransformation;
            public Matrix4x4 invLocalTransformation;
            public bool transformDirty;
            public bool inverted;
            public void Reset()
            {
                localTransformation = Matrix4x4.identity;
                invLocalTransformation = Matrix4x4.identity;
                transformDirty = true;
                inverted = false;
            }

            internal void SetLocalTransformation(Matrix4x4 localTransformation)
            {
                this.localTransformation = localTransformation;
                this.invLocalTransformation = localTransformation.inverse;
                this.transformDirty = true;
            }
        };

        internal class TreeInfo
        {
            public readonly List<int>						treeBrushes			= new List<int>();
            public readonly List<GeneratedMeshDescription>	meshDescriptions	= new List<GeneratedMeshDescription>();
            public readonly List<SubMeshCounts>				subMeshCounts		= new List<SubMeshCounts>();

            public void Reset()
            {
                subMeshCounts.Clear();
            }
        }
        
        struct NodeHierarchy
        {
            public List<int>	children;
            public int			treeNodeID;
            public int			parentNodeID;
            public TreeInfo		treeInfo;
            public BrushOutput	brushOutput;

            internal bool RemoveChild(int childNodeID)
            {
                if (children == null)
                    return false;
                return children.Remove(childNodeID);
            }

            internal bool AddChild(int childNodeID)
            {
                if (children == null)
                    children = new List<int>();
                else
                if (children.Contains(childNodeID))
                    return false;
                children.Add(childNodeID);
                return true;
            }

            internal bool RemoveBrush(int brushNodeID)
            {
                if (treeInfo == null)
                    return false;
                return treeInfo.treeBrushes.Remove(brushNodeID);
            }

            internal bool AddBrush(int brushNodeID)
            {
                if (treeInfo == null)
                    return false;
                if (treeInfo.treeBrushes.Contains(brushNodeID))
                    return false;
                treeInfo.treeBrushes.Add(brushNodeID);
                return true;
            }

            internal void Reset()
            {
                children = null;
                treeNodeID = CSGTreeNode.InvalidNodeID;
                parentNodeID = CSGTreeNode.InvalidNodeID;
                treeInfo = null;
                brushOutput = null;
            }

            internal void SetAncestors(int parentNodeID, int treeNodeID)
            {
                this.parentNodeID = parentNodeID;
                this.treeNodeID = treeNodeID;
            }

            internal void SetTreeNodeID(int treeNodeID)
            {
                this.treeNodeID = treeNodeID;
            }
        }

        private static readonly List<int>					nodeUserIDs			= new List<int>();
        private static readonly List<Bounds>				nodeBounds			= new List<Bounds>();
        private static readonly List<NodeFlags>				nodeFlags			= new List<NodeFlags>();
        private static readonly List<NodeTransform>			nodeTransforms		= new List<NodeTransform>();
        private static readonly List<NodeLocalTransform>	nodeLocalTransforms	= new List<NodeLocalTransform>();
        private static readonly List<NodeHierarchy>			nodeHierarchies		= new List<NodeHierarchy>();

        private static readonly List<int>	freeNodeIDs		= new List<int>();
        private static readonly List<int>	trees			= new List<int>();
        private static readonly List<int>	branches		= new List<int>();
        internal static readonly List<int>	brushes			= new List<int>();
        

        internal static int GetNodeCount()		{ return Mathf.Max(0, nodeHierarchies.Count - freeNodeIDs.Count); }
        internal static int GetBrushCount()		{ return brushes.Count; }
        internal static int GetBranchCount()	{ return branches.Count; }
        internal static int GetTreeCount()		{ return trees.Count; }

        internal static void ClearAllNodes()
        {
            nodeUserIDs		.Clear();	nodeBounds			.Clear();
            nodeFlags		.Clear();	nodeTransforms		.Clear();
            nodeHierarchies	.Clear();	nodeLocalTransforms	.Clear();	

            freeNodeIDs		.Clear();	trees	.Clear();
            branches		.Clear();	brushes	.Clear();
        }

        private static int GenerateValidNodeIndex(Int32 userID, CSGNodeType type)
        {
            if (freeNodeIDs.Count > 0)
            {
                if (freeNodeIDs.Count == 1)
                {
                    var nodeID = freeNodeIDs[0];
                    freeNodeIDs.Clear();
                    nodeUserIDs[nodeID - 1] = userID;
                    return nodeID;
                } else
                { 
                    freeNodeIDs.Sort(); // I'm sorry!
                    var nodeID = freeNodeIDs[0];
                    freeNodeIDs.RemoveAt(0);// I'm sorry again!
                    nodeUserIDs[nodeID - 1] = userID;
                    return nodeID;
                }
            } else
            {
                var nodeIndex = nodeHierarchies.Count; // NOTE: Index, not ID

                nodeUserIDs			.Add(userID); // <- setting userID here
                nodeBounds			.Add(new Bounds());
                nodeFlags			.Add(new NodeFlags());
                nodeTransforms		.Add(new NodeTransform());
                nodeLocalTransforms	.Add(new NodeLocalTransform());
                nodeHierarchies		.Add(new NodeHierarchy());

                nodeTransforms		[nodeIndex].Reset();
                nodeLocalTransforms	[nodeIndex].Reset();
                nodeHierarchies		[nodeIndex].Reset();
                nodeFlags			[nodeIndex].Reset();

                return nodeIndex + 1; // NOTE: converting index to ID
            }
        }

        internal static bool DestroyNode(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false;

            var nodeIndex = nodeID - 1; // NOTE: converting ID to index

            var nodeType = nodeFlags[nodeIndex].nodeType;

            Debug.Assert(nodeUserIDs.Count == nodeHierarchies.Count);
            Debug.Assert(nodeBounds.Count == nodeHierarchies.Count);
            Debug.Assert(nodeFlags.Count == nodeHierarchies.Count);
            Debug.Assert(nodeTransforms.Count == nodeHierarchies.Count);
            Debug.Assert(nodeLocalTransforms.Count == nodeHierarchies.Count);

            if (nodeType == CSGNodeType.Branch ||
                nodeType == CSGNodeType.Tree)
                SetChildrenTree(nodeID, CSGTreeNode.InvalidNodeID);

            if (nodeType == CSGNodeType.Branch ||
                nodeType == CSGNodeType.Brush)
            {
                var oldParentNodeID = nodeHierarchies[nodeIndex].parentNodeID;
                var oldTreeNodeID = nodeHierarchies[nodeIndex].treeNodeID;
                if (IsValidNodeID(oldParentNodeID))
                {
                    var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                    parentNodeHierarchy.RemoveChild(nodeID);
                    nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                    CSGManager.SetDirty(oldParentNodeID);
                }
                if (IsValidNodeID(oldTreeNodeID))
                {
                    if (!IsValidNodeID(oldParentNodeID))
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                        treeNodeHierarchy.RemoveChild(nodeID);
                        nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                    }
                    if (nodeType == CSGNodeType.Brush)
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                        treeNodeHierarchy.RemoveBrush(nodeID);
                        nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                    }
                    CSGManager.SetDirty(oldTreeNodeID);
                }
            }

            

            nodeUserIDs		[nodeIndex] = kDefaultUserID;
            nodeBounds		[nodeIndex] = new Bounds();

            var flags = nodeFlags[nodeIndex];
            flags.Reset();
            nodeFlags[nodeIndex] = flags;

            var nodeTransform = nodeTransforms[nodeIndex];
            nodeTransform.Reset();
            nodeTransforms[nodeIndex] = nodeTransform;

            var nodeLocalTransfom = nodeLocalTransforms[nodeIndex];
            nodeLocalTransfom.Reset();
            nodeLocalTransforms[nodeIndex] = nodeLocalTransfom;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.Reset();
            nodeHierarchies[nodeIndex] = nodeHierarchy;

            
            freeNodeIDs .Add(nodeID);

            switch(nodeType)
            {
                case CSGNodeType.Brush:		brushes.Remove(nodeID); break;
                case CSGNodeType.Branch:	branches.Remove(nodeID); break;
                case CSGNodeType.Tree:		trees.Remove(nodeID); break;
            }

            if (nodeID == nodeHierarchies.Count)
            { 
                while (nodeHierarchies.Count > 0 &&
                        !IsValidNodeID(nodeID))
                { 
                    freeNodeIDs			.Remove(nodeID);
                    nodeIndex = nodeID - 1;

                    nodeUserIDs			.RemoveAt(nodeIndex);
                    nodeBounds			.RemoveAt(nodeIndex);
                    nodeFlags			.RemoveAt(nodeIndex);
                    nodeTransforms		.RemoveAt(nodeIndex);
                    nodeLocalTransforms	.RemoveAt(nodeIndex);
                    nodeHierarchies		.RemoveAt(nodeIndex);
                    nodeID--;
                }
            }			
            return true;
        }


        internal static bool		GenerateBrush	(Int32 userID, out Int32 generatedNodeID)
        {
            generatedNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Brush);

            var nodeIndex = generatedNodeID - 1; // NOTE: converting ID to index

            var flags = nodeFlags[nodeIndex];
            flags.operationType = CSGOperationType.Additive;
            flags.nodeType = CSGNodeType.Brush;
            nodeFlags[nodeIndex] = flags;
            
            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.brushOutput	= new BrushOutput();
            nodeHierarchies[nodeIndex]	= nodeHierarchy;

            brushes.Add(generatedNodeID);

            SetDirty(generatedNodeID);
            return true;
        }

        internal static bool		GenerateBranch	(Int32 userID, out Int32 generatedBranchNodeID)
        {
            generatedBranchNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Branch);

            var nodeIndex = generatedBranchNodeID - 1; // NOTE: converting ID to index

            var flags = nodeFlags[nodeIndex];
            flags.operationType	= CSGOperationType.Additive;
            flags.nodeType		= CSGNodeType.Branch;
            nodeFlags[nodeIndex] = flags;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.children = new List<int>();
            nodeHierarchies[nodeIndex] = nodeHierarchy;
            
            branches.Add(generatedBranchNodeID);

            SetDirty(generatedBranchNodeID);
            return true;
        }

        internal static bool		GenerateTree	(Int32 userID, out Int32 generatedTreeNodeID)
        {
            generatedTreeNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Tree);

            var nodeIndex = generatedTreeNodeID - 1;

            var flags = nodeFlags[nodeIndex];
            flags.nodeType = CSGNodeType.Tree;
            nodeFlags[nodeIndex] = flags;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.children		= new List<int>();
            nodeHierarchy.treeInfo		= new TreeInfo();
            nodeHierarchies[nodeIndex]	= nodeHierarchy;
            
            trees.Add(generatedTreeNodeID);

            SetDirty(generatedTreeNodeID);
            return true;
        }





        internal static bool		IsValidNodeID					(Int32 nodeID)	{ return (nodeID > 0 && nodeID <= nodeHierarchies.Count) && nodeFlags[nodeID - 1].nodeType != CSGNodeType.None; }

        private static bool			AssertNodeIDValid				(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID))
            {
                if (nodeID == CSGTreeNode.InvalidNodeID)
                {
                    Debug.LogError("Invalid ID " + nodeID + ", 'empty'");
                    return false;
                }
                var nodeIndex = nodeID - 1;
                if (nodeIndex >= 0 && nodeIndex < nodeHierarchies.Count)
                    Debug.LogError("Invalid ID " + nodeID + " with type " + nodeFlags[nodeIndex].nodeType);
                else
                    Debug.LogError("Invalid ID " + nodeID + ", outside of bounds");
                return false;
            }
            return true;
        }
        private static bool			AssertNodeType					(Int32 nodeID, CSGNodeType type) { return nodeFlags[nodeID-1].nodeType == type; }
        private static bool			AssertNodeTypeHasChildren		(Int32 nodeID) { return nodeFlags[nodeID-1].nodeType == CSGNodeType.Branch || nodeFlags[nodeID - 1].nodeType == CSGNodeType.Tree; }
        private static bool			AssertNodeTypeHasParent			(Int32 nodeID) { return nodeFlags[nodeID-1].nodeType == CSGNodeType.Branch || nodeFlags[nodeID - 1].nodeType == CSGNodeType.Brush; }
        private static bool			AssertNodeTypeHasOperation		(Int32 nodeID) { return nodeFlags[nodeID-1].nodeType == CSGNodeType.Branch || nodeFlags[nodeID - 1].nodeType == CSGNodeType.Brush; }
        private static bool			AssertNodeTypeHasTransformation	(Int32 nodeID) { return nodeFlags[nodeID-1].nodeType != CSGNodeType.None; }


        internal static CSGNodeType GetTypeOfNode(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return CSGNodeType.None;
            return nodeFlags[nodeID - 1].nodeType;
        }
        internal static Int32		GetUserIDOfNode	(Int32 nodeID)	{ if (!IsValidNodeID(nodeID)) return kDefaultUserID; return nodeUserIDs[nodeID - 1]; }

        internal static bool		GetBrushBounds	(Int32 brushNodeID, ref Bounds bounds) { if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return false; bounds = nodeBounds[brushNodeID - 1]; return true; }


        internal static bool		IsNodeDirty(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false;
            switch (nodeFlags[nodeID - 1].nodeType)
            {
                case CSGNodeType.Brush:		return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.NeedCSGUpdate);
                case CSGNodeType.Branch:	return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.OperationNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate);
                case CSGNodeType.Tree:		return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate);
            }
            return false;
        }

        internal static bool SetDirty(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID))
                return false;
            switch (nodeFlags[nodeID - 1].nodeType)
            {
                case CSGNodeType.Brush:
                {
                    int treeNodeID = GetTreeOfNode(nodeID);
                    var flags = nodeFlags[nodeID - 1];
                    flags.SetNodeFlag(NodeStatusFlags.NeedUpdate);
                    nodeFlags[nodeID - 1] = flags;
                    if (IsValidNodeID(treeNodeID))
                    {
                        var treeNodeFlags = nodeFlags[nodeID - 1];
                        treeNodeFlags.SetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                        nodeFlags[nodeID - 1] = treeNodeFlags;
                    }
                    break;
                }
                case CSGNodeType.Branch:
                {
                    int treeNodeID = GetTreeOfNode(nodeID);
                    var flags = nodeFlags[nodeID - 1];
                    flags.SetNodeFlag(NodeStatusFlags.OperationNeedsUpdate);
                    nodeFlags[nodeID - 1] = flags;
                    if (IsValidNodeID(treeNodeID))
                    {
                        var treeNodeFlags = nodeFlags[nodeID - 1];
                        treeNodeFlags.SetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                        nodeFlags[nodeID - 1] = treeNodeFlags;
                    }
                    break;
                }
                case CSGNodeType.Tree:
                {
                    var treeNodeFlags = nodeFlags[nodeID - 1];
                    treeNodeFlags.SetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate | NodeStatusFlags.TreeNeedsUpdate);
                    nodeFlags[nodeID - 1] = treeNodeFlags;
                    /*
                    CSGTree* tree = (CSGTree*)(node);
                    auto const brushNodeIDs = tree->treeBrushNodeIDs;
                    for (int i = 0, iCount = (int)brushNodeIDs.Count; i < iCount; i++)
                    {
                        const auto treeBrushNodeID = brushNodeIDs[i];
                        CSGNode *const __restrict childNode = manager->GetNodeByIndex(treeBrushNodeID);
                        if (childNode == nullptr)
                            continue;
                        CSGNodeType const childNodeType   = manager->GetNodeTypeUnsafe(treeBrushNodeID);
                        if (childNodeType != CSGNodeType.Brush)
                            continue;
                        CSGBrush *const __restrict brush = (CSGBrush*)childNode;
                        brush->ClearRenderBuffers();
                    }
                    */
                    break;
                }
                default:
                {
                    Debug.LogError("Unknown node type");
                    return false;
                }
            }
            return true;
        }

        internal static bool ClearDirty(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID)) return false;
            var flags = nodeFlags[nodeID - 1];
            switch (flags.nodeType)
            {
                case CSGNodeType.Brush:		flags.UnSetNodeFlag(NodeStatusFlags.NeedFullUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Branch:	flags.UnSetNodeFlag(NodeStatusFlags.OperationNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Tree:		flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
            }
            return false;
        }

        private static void DirtySelf(Int32 nodeID)
        {
            SetDirty(nodeID);
        }

        private static void DirtySelfAndChildren(Int32 nodeID)
        {
            SetDirty(nodeID);
            var children = nodeHierarchies[nodeID - 1].children;
            if (children == null)
                return;

            // TODO: make this non recursive
            for (int i = 0; i < children.Count; i++)
                DirtySelfAndChildren(children[i]);
        }


        internal static bool		GetNodeLocalTransformation(Int32 nodeID, out Matrix4x4 localTransformation)		{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { localTransformation = Matrix4x4.identity; return false; } localTransformation = nodeLocalTransforms[nodeID - 1].localTransformation; return true; }
        internal static bool		SetNodeLocalTransformation(Int32 nodeID, ref Matrix4x4 localTransformation)		{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) {                                           return false; } var nodeLocalTransfom = nodeLocalTransforms[nodeID - 1]; nodeLocalTransfom.SetLocalTransformation(localTransformation); nodeLocalTransforms[nodeID - 1] = nodeLocalTransfom; DirtySelfAndChildren(nodeID); return true; }
        internal static bool		GetTreeToNodeSpaceMatrix(Int32 nodeID, out Matrix4x4 treeToNodeMatrix)			{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { treeToNodeMatrix    = Matrix4x4.identity; return false; } treeToNodeMatrix = nodeTransforms[nodeID - 1].treeToNode; return true; }
        internal static bool		GetNodeToTreeSpaceMatrix(Int32 nodeID, out Matrix4x4 nodeToTreeMatrix)			{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { nodeToTreeMatrix    = Matrix4x4.identity; return false; } nodeToTreeMatrix = nodeTransforms[nodeID - 1].nodeToTree; return true; }


        internal static CSGOperationType	GetNodeOperationType(Int32 nodeID)								{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasOperation(nodeID)) return CSGOperationType.Invalid; return nodeFlags[nodeID - 1].operationType; }
        internal static bool SetNodeOperationType(Int32 nodeID, CSGOperationType operation)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasOperation(nodeID) || 
                operation == CSGOperationType.Invalid)
                return false;
            var flags = nodeFlags[nodeID - 1];
            if (flags.operationType == operation)
                return true;
            flags.SetOperation(operation);
            nodeFlags[nodeID - 1] = flags;
            DirtySelfAndChildren(nodeID);
            return true;
        }


        internal static BrushOutput GetBrushOutput(Int32 brushNodeID)									{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return null; return nodeHierarchies[brushNodeID - 1].brushOutput; }
        

        internal static Int32		GetBrushMeshID(Int32 brushNodeID)									{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return BrushMeshInstance.InvalidInstanceID; return nodeHierarchies[brushNodeID-1].brushOutput.brushMeshInstanceID; }
        internal static bool		SetBrushMeshID(Int32 brushNodeID, Int32 brushMeshID)				{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return false; nodeHierarchies[brushNodeID - 1].brushOutput.brushMeshInstanceID = brushMeshID; DirtySelf(brushNodeID); return true; }


        internal static Int32		GetNumberOfBrushesInTree(Int32 treeNodeID)							{ if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return 0; if (nodeHierarchies[treeNodeID - 1].treeInfo == null) return 0; return nodeHierarchies[treeNodeID - 1].treeInfo.treeBrushes.Count; }
        internal static bool	    DoesTreeContainBrush(Int32 treeNodeID, Int32 brushNodeID)
        {
            if (!AssertNodeIDValid(treeNodeID) || 
                !AssertNodeIDValid(brushNodeID) || 
                !AssertNodeType(treeNodeID, CSGNodeType.Tree) || 
                !AssertNodeType(brushNodeID, CSGNodeType.Brush))
                return false;
            if (nodeHierarchies[treeNodeID - 1].treeInfo == null)
                return false;
            return nodeHierarchies[treeNodeID - 1].treeInfo.treeBrushes.Contains(brushNodeID);
        }

        internal static Int32		FindTreeByUserID(Int32 userID)
        {
            if (userID == kDefaultUserID)
                return CSGTreeNode.InvalidNodeID;
            for (int i = 0; i < trees.Count; i++)
            {
                var treeNodeID = trees[i];
                if (nodeUserIDs[treeNodeID - 1] == userID)
                    return treeNodeID;
            }
            return CSGTreeNode.InvalidNodeID;
        }


        internal static Int32 GetParentOfNode(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID) || 
                !AssertNodeTypeHasParent(nodeID))
                return CSGTreeNode.InvalidNodeID;
            return nodeHierarchies[nodeID - 1].parentNodeID;
        }
        internal static Int32 GetTreeOfNode(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID) || 
                !AssertNodeTypeHasParent(nodeID))
                return CSGTreeNode.InvalidNodeID;
            return nodeHierarchies[nodeID - 1].treeNodeID;
        }
        internal static Int32	GetChildNodeCount(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID))
                return 0;
            return (nodeHierarchies[nodeID - 1].children == null) ? 0 : nodeHierarchies[nodeID - 1].children.Count;
        }
        internal static Int32 GetChildNodeAtIndex(Int32 nodeID, Int32 index)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID) || nodeHierarchies[nodeID - 1].children == null || index < 0 || index >= nodeHierarchies[nodeID - 1].children.Count)
                return 0;
            return nodeHierarchies[nodeID - 1].children[index];
        }

        static bool SetChildrenTree(Int32 parentNodeID, Int32 treeNodeID, HashSet<Int32> passed)
        {
            var parentNodeIndex = parentNodeID - 1;
            var children = nodeHierarchies[parentNodeIndex].children;
            if (children == null)
                return false;
            

            // TODO: make this non recursive
            for (int i = 0; i < children.Count; i++)
            {
                var childID = children[i];

                var isBrush			= nodeFlags[childID - 1].nodeType == CSGNodeType.Brush;
                var nodeHierarchy	= nodeHierarchies[childID - 1]; 
                if (isBrush && IsValidNodeID(nodeHierarchy.treeNodeID))
                    nodeHierarchies[nodeHierarchy.treeNodeID - 1].RemoveBrush(childID);
                nodeHierarchy.SetTreeNodeID(treeNodeID);
                nodeHierarchies[childID - 1] = nodeHierarchy;
                if (isBrush && IsValidNodeID(treeNodeID))
                    nodeHierarchies[treeNodeID - 1].AddBrush(childID);
                if (!AssertNodeTypeHasChildren(childID))
                    continue;
                if (!passed.Add(childID))
                    return false;
                SetChildrenTree(childID, treeNodeID, passed);
            }
            return true;
        }

        private static void SetChildrenTree(Int32 parentNodeID, Int32 treeNodeID)
        {
            var parentNodeIndex = parentNodeID - 1;
            var children		= nodeHierarchies[parentNodeIndex].children;
            if (children == null)
                return;

            HashSet<Int32> passed = new HashSet<Int32>(); 
            passed.Add(parentNodeID);

            SetChildrenTree(parentNodeID, treeNodeID, passed);
        }

        // Note: assumes both newParentNodeID and childNodeID are VALID
        private static void AddToParent(Int32 newParentNodeID, Int32 childNodeID)
        {
            int newTreeNodeID;
            var nodeParentType = nodeFlags[newParentNodeID - 1].nodeType;
            if (nodeParentType == CSGNodeType.Tree)
            {
                newTreeNodeID = newParentNodeID;
                newParentNodeID = CSGTreeNode.InvalidNodeID;
            } else
            {
                Debug.Assert(nodeParentType == CSGNodeType.Branch);
                newTreeNodeID = nodeHierarchies[newParentNodeID - 1].treeNodeID;
            }
            
            var oldTreeNodeID		= nodeHierarchies[childNodeID - 1].treeNodeID;
            var oldParentNodeID		= nodeHierarchies[childNodeID - 1].parentNodeID;

            // Could be possible if we move position in a branch
            if (newParentNodeID == oldParentNodeID && 
                newTreeNodeID == oldTreeNodeID)
                return;
            
            if (oldTreeNodeID != newTreeNodeID)
            {
                var nodeType = nodeFlags[childNodeID - 1].nodeType;
                if (nodeType == CSGNodeType.Brush)
                {
                    if (IsValidNodeID(oldTreeNodeID))
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                        treeNodeHierarchy.RemoveBrush(childNodeID);
                        nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                        CSGManager.SetDirty(oldTreeNodeID);
                    }
                    if (IsValidNodeID(newTreeNodeID))
                    {
                        var treeNodeHierarchy = nodeHierarchies[newTreeNodeID - 1];
                        treeNodeHierarchy.AddBrush(childNodeID);
                        nodeHierarchies[newTreeNodeID - 1] = treeNodeHierarchy;
                        CSGManager.SetDirty(newTreeNodeID);
                    }
                } else
                if (nodeType == CSGNodeType.Branch)
                    SetChildrenTree(childNodeID, newTreeNodeID);
            }

            if (oldParentNodeID != newParentNodeID)
            {
                if (IsValidNodeID(oldParentNodeID))
                {
                    var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                    parentNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                    CSGManager.SetDirty(oldParentNodeID);
                }
                // it is assumed that adding the child is done outside this method since there is more context there
            }

            var nodeHierarchy = nodeHierarchies[childNodeID - 1];
            nodeHierarchy.SetAncestors(newParentNodeID, newTreeNodeID);
            nodeHierarchies[childNodeID - 1] = nodeHierarchy;
            CSGManager.SetDirty(childNodeID);
        }

        // Note: assumes childNodeID is VALID
        private static void RemoveFromParent(Int32 childNodeID)
        {
            var oldTreeNodeID	= nodeHierarchies[childNodeID - 1].treeNodeID;
            var oldParentNodeID	= nodeHierarchies[childNodeID - 1].parentNodeID;

            var nodeType = nodeFlags[childNodeID - 1].nodeType;
            if (nodeType == CSGNodeType.Brush)
            {
                if (IsValidNodeID(oldTreeNodeID))
                {
                    var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                    treeNodeHierarchy.RemoveBrush(childNodeID);
                    nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                    CSGManager.SetDirty(oldTreeNodeID);
                }
            } else
            if (nodeType == CSGNodeType.Branch)
                SetChildrenTree(childNodeID, CSGTreeNode.InvalidNodeID);

            if (IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                CSGManager.SetDirty(oldParentNodeID);
                if (IsValidNodeID(oldTreeNodeID))
                    CSGManager.SetDirty(oldTreeNodeID);
            } else
            if (IsValidNodeID(oldTreeNodeID))
            {
                var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                treeNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                CSGManager.SetDirty(oldTreeNodeID);
            }

            var nodeHierarchy = nodeHierarchies[childNodeID - 1];
            nodeHierarchy.SetAncestors(CSGTreeNode.InvalidNodeID, CSGTreeNode.InvalidNodeID);
            nodeHierarchies[childNodeID - 1] = nodeHierarchy;
            CSGManager.SetDirty(childNodeID);
        }

        internal static Int32 IndexOfChildNode(Int32 nodeID, Int32 childNodeID)
        {
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                nodeHierarchies[nodeID - 1].children == null)
                return -1;
            return nodeHierarchies[nodeID - 1].children.IndexOf(childNodeID);
        }

        internal static bool	ClearChildNodes(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID))
                return false;
            var nodeIndex	= nodeID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || children.Count == 0)
                return true;

            return RemoveChildNodeRange(nodeID, 0, children.Count);
        }

        internal static bool	RemoveChildNode(Int32 nodeID, Int32 childNodeID)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID) || 
                !IsValidNodeID(childNodeID) || 
                !AssertNodeTypeHasParent(childNodeID) || 
                nodeHierarchies[nodeID - 1].children == null)
                return false;
            int childIndex = nodeHierarchies[nodeID - 1].children.IndexOf(childNodeID);
            if (childIndex == -1)
                return false;
            return RemoveChildNodeAt(nodeID, childIndex);
        }

        internal static bool	RemoveChildNodeAt(Int32 nodeID, Int32 index)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID))
                return false;
            var nodeIndex	= nodeID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || children.Count == 0 ||
                index < 0 || index >= children.Count)
                return false;
            var childNodeID = children[index]; 
            RemoveFromParent(children[index]);
            CSGManager.SetDirty(childNodeID);
            CSGManager.SetDirty(nodeID);
            return true;
        }

        internal static bool RemoveChildNodeRange(Int32 nodeID, Int32 index, Int32 count)
        {
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID))
                return false;
            var nodeIndex	= nodeID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || children.Count == 0 ||
                index < 0 || index >= children.Count || (index + count) > children.Count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!IsValidNodeID(children[index + i]))
                    return false;
            }
            for (int i = count - 1; i >= 0; i--)
            { 
                var childNodeID = children[index + i]; 
                RemoveFromParent(childNodeID);
                CSGManager.SetDirty(childNodeID);
            }
            CSGManager.SetDirty(nodeID);
            return true;
        }

        static bool IsAncestor(Int32 nodeID, Int32 ancestorNodeID)
        {
            if (nodeID == ancestorNodeID)
                return true;

            Int32 parentNodeID = GetParentOfNode(nodeID);
            while (parentNodeID != CSGTreeNode.InvalidNodeID)
            {
                if (parentNodeID == ancestorNodeID)
                    return true;
                parentNodeID = GetParentOfNode(parentNodeID);
            }
            return false;
        }

        internal static bool	AddChildNode(Int32 nodeID, Int32 childNodeID)
        {
            if (nodeID == CSGTreeNode.InvalidNodeID ||
                childNodeID == CSGTreeNode.InvalidNodeID)
                return false;
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                !AssertNodeIDValid(childNodeID) ||
                !AssertNodeTypeHasParent(childNodeID))
                return false;
            if (IsAncestor(nodeID, childNodeID))
                return false;
            var childNode		= nodeHierarchies[childNodeID - 1];
            var oldParentNodeID = childNode.parentNodeID;
            var oldTreeNodeID	= childNode.treeNodeID;
            
            var nodeIsTree		= nodeFlags[nodeID - 1].nodeType == CSGNodeType.Tree;
            var nodeHierarchy	= nodeHierarchies[nodeID - 1];
            var newParentNodeID = nodeIsTree ? 0 : nodeID;
            var newTreeNodeID	= nodeIsTree ? nodeID : nodeHierarchy.treeNodeID;

            if (oldParentNodeID == newParentNodeID &&
                oldTreeNodeID == newTreeNodeID)
                return false;
            
            if (oldParentNodeID != newParentNodeID &&
                IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                CSGManager.SetDirty(oldParentNodeID);
            }
            
            if (IsValidNodeID(oldTreeNodeID))
            {
                if (!IsValidNodeID(oldParentNodeID))
                {
                    var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                    treeNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                }
                if (oldTreeNodeID != newTreeNodeID)
                {
                    if (nodeFlags[oldTreeNodeID - 1].nodeType == CSGNodeType.Brush)
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                        treeNodeHierarchy.RemoveBrush(childNodeID);
                        nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                    }
                }
                CSGManager.SetDirty(oldTreeNodeID);
            }

            if (!nodeHierarchy.AddChild(childNodeID))
                return false;
            nodeHierarchies[nodeID - 1] = nodeHierarchy;
            CSGManager.SetDirty(childNodeID);
            AddToParent(nodeID, childNodeID);
            CSGManager.SetDirty(nodeID);
            return true;
        }

        internal static bool	InsertChildNode(Int32 nodeID, Int32 index, Int32 childNodeID)
        {
            if (nodeID == CSGTreeNode.InvalidNodeID ||
                childNodeID == CSGTreeNode.InvalidNodeID)
                return false;
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                !AssertNodeIDValid(childNodeID) ||
                !AssertNodeTypeHasParent(childNodeID))
                return false;
            if (IsAncestor(nodeID, childNodeID))
                return false;
            
            var childNode		= nodeHierarchies[childNodeID - 1];
            var oldParentNodeID = childNode.parentNodeID;
            var oldTreeNodeID	= childNode.treeNodeID;
            
            var nodeIsTree		= nodeFlags[nodeID - 1].nodeType == CSGNodeType.Tree;
            var nodeHierarchy	= nodeHierarchies[nodeID - 1];
            var newParentNodeID = nodeIsTree ? 0 : nodeID;
            var newTreeNodeID	= nodeIsTree ? nodeID : nodeHierarchy.treeNodeID;
            
            if (oldParentNodeID == newParentNodeID &&
                oldTreeNodeID == newTreeNodeID)
                return false;
            
            if (oldParentNodeID != newParentNodeID &&
                IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                CSGManager.SetDirty(oldParentNodeID);
            }
            
            if (IsValidNodeID(oldTreeNodeID))
            {
                if (!IsValidNodeID(oldParentNodeID))
                {
                    var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                    treeNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                }
                if (oldTreeNodeID != newTreeNodeID)
                {
                    if (nodeFlags[oldTreeNodeID - 1].nodeType == CSGNodeType.Brush)
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                        treeNodeHierarchy.RemoveBrush(childNodeID);
                        nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                    }
                }
                CSGManager.SetDirty(oldTreeNodeID);
            }

            var nodeIndex	= nodeID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || 
                index < 0 || index > children.Count)
                return false;
            var oldIndex = children.IndexOf(childNodeID);
            if (oldIndex != -1)
            {
                // NOTE: same parent, same tree
                if (oldIndex > index)
                {
                    children.RemoveAt(oldIndex);
                    children.Insert(index, childNodeID);
                } else
                if (oldIndex < index)
                {
                    children.Insert(index, childNodeID);
                    children.RemoveAt(oldIndex);
                }// else: same position, so already good
                return true; 
            }
            children.Insert(index, childNodeID);
            CSGManager.SetDirty(childNodeID);
            AddToParent(nodeID, childNodeID);
            CSGManager.SetDirty(nodeID);
            return true;
        }

        internal static bool InsertChildNodeRange(Int32 nodeID, Int32 index, CSGTreeNode[] srcChildren)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID) ||
                srcChildren == null)
                return false;
            var nodeIndex = nodeID - 1;
            var dstChildren = nodeHierarchies[nodeIndex].children;
            if (dstChildren == null)
            {
                if (index > 0)
                    return false;
                var nodeHierarchy = nodeHierarchies[nodeID - 1];
                nodeHierarchy.children = new List<int>();
                nodeHierarchies[nodeID - 1] = nodeHierarchy;
            } else
            if (index < 0 || index > dstChildren.Count)
                return false;

            for (int i = srcChildren.Length - 1; i >= 0; i--)
            {
                if (nodeID == srcChildren[i].NodeID)
                    return false;
                if (!IsValidNodeID(srcChildren[i].nodeID))
                    return false;
                if (Array.IndexOf(srcChildren, srcChildren[i].nodeID) != -1)
                    return false;
                // TODO: handle adding nodes already part of this particular parent (move)
                if (dstChildren.Contains(srcChildren[i].nodeID))
                    return false;
                var childNodeType = GetTypeOfNode(srcChildren[i].nodeID);
                if (childNodeType != CSGNodeType.Brush &&
                    childNodeType != CSGNodeType.Branch)
                    return false;
                if (IsAncestor(nodeID, srcChildren[i].NodeID))
                    return false;
            }

            for (int i = srcChildren.Length - 1; i >= 0; i--)
            {
                if (!IsValidNodeID(srcChildren[i].nodeID))
                    continue;
                dstChildren.Insert(index, srcChildren[i].nodeID);
                CSGManager.SetDirty(srcChildren[i].nodeID);
                AddToParent(nodeID, srcChildren[i].nodeID);
            }
            CSGManager.SetDirty(nodeID);
            return true;
        }


        internal static bool SetChildNodes(Int32 nodeID, CSGTreeNode[] children)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID)) return false;
            if (children == null)
                return false;
            if (nodeID == CSGTreeNode.InvalidNodeID)
                throw new ArgumentException("nodeID equals " + CSGTreeNode.InvalidNode);

            var foundNodes = new HashSet<int>();
            for (int i = 0; i < children.Length; i++)
            {
                if (nodeID == children[i].NodeID)
                    return false;
                if (!foundNodes.Add(children[i].NodeID))
                {
                    Debug.LogError("Have duplicate child");
                    return false;
                }
            }
            
            for (int i = 0; i < children.Length; i++)
            {
                if (IsAncestor(nodeID, children[i].NodeID))
                {
                    Debug.LogError("Trying to set ancestor of node as child");
                    return false;
                }
            }

            if (!ClearChildNodes(nodeID))
                return false;

            foreach(var child in children)
            {
                if (!AddChildNode(nodeID, child.nodeID))
                    return false;
                CSGManager.SetDirty(child.nodeID);
            }
            CSGManager.SetDirty(nodeID);
            return true;
        }

        internal static CSGTreeNode[] GetChildNodes(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID)) return null;

            var srcChildren = nodeHierarchies[nodeID - 1].children;
            if (srcChildren == null ||
                srcChildren.Count == 0)
                return new CSGTreeNode[0];

            var dstChildren	= new CSGTreeNode[srcChildren.Count];
            for (int i = 0; i < srcChildren.Count; i++)
                dstChildren[i] = new CSGTreeNode() { nodeID = srcChildren[i] };

            return dstChildren;
        }

        internal static int CopyToUnsafe(Int32 nodeID, int childCount, CSGTreeNode[] children, int arrayIndex)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID)) return 0;

            if (children == null)
                return 0;

            var srcChildren = nodeHierarchies[nodeID - 1].children;
            if (srcChildren == null ||
                srcChildren.Count == 0 ||
                childCount != srcChildren.Count)
                return 0;

            if (children.Length + arrayIndex < childCount)
                return 0;

            for (int i = 0; i < childCount; i++)
                children[arrayIndex + i].nodeID = srcChildren[i];

            return childCount;
        }

        internal static bool DestroyAllNodesWithUserID(Int32 userID)
        {
            bool found = false;
            for (int i = 0; i < nodeUserIDs.Count; i++)
            {
                if (!AssertNodeIDValid(i))
                    continue;
                if (nodeUserIDs[i] == userID)
                {
                    found = true;
                    DestroyNode(nodeUserIDs[i]);
                }
            }
            return found;
        }

        internal static bool DestroyNodes(CSGTreeNode[] nodeIDs)
        {
            if (nodeIDs == null)
                return false;

            var destroyed = new HashSet<int>();

            bool fail = false;
            for (int i = 0; i < nodeIDs.Length; i++)
            {
                var nodeID = nodeIDs[i].nodeID;
                if (!destroyed.Add(nodeID))
                    continue;
                if (!DestroyNode(nodeID))
                    fail = true;
            }
            return !fail;
        }


        private static CSGTreeNode[] GetAllTreeNodes()
        {
            var nodeCount = GetNodeCount();
            var allTreeNodeIDs = new CSGTreeNode[nodeCount];
            if (nodeCount == 0)
                return allTreeNodeIDs;

            int n = 0;
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var nodeID = i;
                if (!AssertNodeIDValid(nodeID))
                    continue;
                allTreeNodeIDs[n].nodeID = nodeID;
                n++;
            }
            Debug.Assert(n == nodeCount);

            return allTreeNodeIDs;
        }

        private static CSGTree[] GetAllTrees()
        {
            var nodeCount = GetTreeCount();
            var allTrees = new CSGTree[nodeCount];
            if (nodeCount == 0)
                return allTrees;

            for (int i = 0; i < trees.Count; i++)
                allTrees[i].treeNodeID = trees[i];

            return allTrees;
        }

        internal static void NotifyBrushMeshRemoved(int brushMeshID)
        {
            for (int i = 0; i < brushes.Count; i++)
            {
                var brush = new CSGTreeBrush() { brushNodeID = brushes[i] };
                if (brush.BrushMesh.brushMeshID != brushMeshID)
                    continue;
                brush.BrushMesh = BrushMeshInstance.InvalidInstance;
            }
        }

        internal static void NotifyBrushMeshModified(int brushMeshID)
        {
            for (int i = 0; i < brushes.Count; i++)
            {
                var brush = new CSGTreeBrush() { brushNodeID = brushes[i] };
                if (brush.BrushMesh.brushMeshID != brushMeshID)
                    continue;
                brush.SetDirty();
            }
        }


        internal static int GetBrushMeshCount() { return BrushMeshManager.GetBrushMeshCount(); }
        
        private static BrushMeshInstance[] GetAllBrushMeshInstances() { return BrushMeshManager.GetAllBrushMeshInstances(); }

        internal static CSGTreeBrushFlags GetBrushFlags(Int32 brushNodeID) { return CSGTreeBrushFlags.Default; } // TODO: implement
        internal static bool SetBrushFlags(Int32 brushNodeID, CSGTreeBrushFlags flags) { return false; } // TODO: implement
#endif
    }
}