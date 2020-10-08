using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    // TODO: clean up

    static partial class CSGManager
    {
        internal const int kDefaultUserID = 0;


        // TODO: review flags, might not make sense any more
        enum NodeStatusFlags : UInt16
        {
            None                        = 0,
            //NeedChildUpdate		    = 1,
            NeedPreviousSiblingsUpdate  = 2,

            BranchNeedsUpdate           = 4,
            
            TreeIsDisabled              = 1024,// TODO: remove, or make more useful
            TreeNeedsUpdate             = 8,
            TreeMeshNeedsUpdate         = 16,

            
            ShapeModified               = 32,
            TransformationModified      = 64,
            HierarchyModified           = 128,
            OutlineModified             = 256,
            NeedAllTouchingUpdated      = 512,	// all brushes that touch this brush need to be updated,
            NeedFullUpdate              = ShapeModified | TransformationModified | OutlineModified | HierarchyModified,
            NeedCSGUpdate               = ShapeModified | TransformationModified | HierarchyModified,
            NeedUpdateDirectOnly        = TransformationModified | OutlineModified,
        };

        struct NodeFlags
        {
            public NodeStatusFlags	status;
            public CSGOperationType operationType;
            public CSGNodeType		nodeType;

            public void SetOperation	(CSGOperationType operation)	{ this.operationType = operation; }
            public void SetStatus		(NodeStatusFlags status)		{ this.status = status; }
            public void SetNodeType		(CSGNodeType nodeType)			{ this.nodeType = nodeType; }
            public bool IsAnyNodeFlagSet(NodeStatusFlags flag)			{ return (status & flag) != NodeStatusFlags.None; }
            public bool IsNodeFlagSet	(NodeStatusFlags flag)			{ return (status & flag) == flag; }
            public void UnSetNodeFlag	(NodeStatusFlags flag)			{ status &= ~flag; }
            public void SetNodeFlag		(NodeStatusFlags flag)			{ status |= flag; }

            internal static void Reset(ref NodeFlags data)
            {
                data.status         = NodeStatusFlags.None;
                data.operationType	= CSGOperationType.Invalid;
                data.nodeType		= CSGNodeType.None;
            }
        };

        internal sealed class BrushSet
        {
            public readonly List<int> items = new List<int>();
        }

        internal sealed class TreeInfo
        {
            public readonly BrushSet        allTreeBrushes      = new BrushSet();
        }

        struct NodeTransform
        {
            public Matrix4x4 nodeToTree;
            public Matrix4x4 treeToNode;

            public static void Reset(ref NodeTransform data)
            {
                data.nodeToTree = Matrix4x4.identity;
                data.treeToNode = Matrix4x4.identity;
            }
        };

        struct NodeLocalTransform
        {
            public Matrix4x4 localTransformation;
            public Matrix4x4 invLocalTransformation;
            public bool transformDirty;
            public bool inverted;

            public static void Reset(ref NodeLocalTransform data)
            {
                data.localTransformation    = Matrix4x4.identity;
                data.invLocalTransformation = Matrix4x4.identity;
                data.transformDirty         = true;
                data.inverted               = false;
            }

            internal static void SetLocalTransformation(ref NodeLocalTransform data, Matrix4x4 localTransformation)
            {
                data.localTransformation    = localTransformation;
                data.invLocalTransformation = localTransformation.inverse;
                data.transformDirty         = true;
            }
        };
        
        internal struct NodeHierarchy
        {
            public List<int>	children;
            public int			treeNodeID;
            public int			parentNodeID;
            public TreeInfo		treeInfo;
            public BrushInfo	brushInfo;

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

                var brushIndex = treeInfo.allTreeBrushes.items.IndexOf(brushNodeID);
                if (brushIndex == -1)
                    return false;
                treeInfo.allTreeBrushes.items.RemoveAt(brushIndex);
                return true;
            }

            internal bool AddBrush(int brushNodeID)
            {
                if (treeInfo == null)
                    return false;
                if (treeInfo.allTreeBrushes.items.Contains(brushNodeID))
                    return false;
                treeInfo.allTreeBrushes.items.Add(brushNodeID);
                return true;
            }

            internal static void Reset(ref NodeHierarchy data)
            {
                data.children       = null;
                data.treeNodeID     = CSGTreeNode.InvalidNodeID;
                data.parentNodeID   = CSGTreeNode.InvalidNodeID;
                data.treeInfo       = null;
                data.brushInfo      = null;
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
        private static readonly List<NodeFlags>				nodeFlags			= new List<NodeFlags>();
        private static readonly List<NodeTransform>			nodeTransforms		= new List<NodeTransform>();
        private static readonly List<NodeLocalTransform>	nodeLocalTransforms	= new List<NodeLocalTransform>();
        private static readonly List<NodeHierarchy>			nodeHierarchies		= new List<NodeHierarchy>();

        private static readonly List<int>	freeNodeIDs		= new List<int>();
        private static readonly List<int>	trees			= new List<int>();// TODO: could be CSGTrees
        private static readonly List<int>	branches		= new List<int>();// TODO: could be CSGTreeBranches
        internal static readonly List<int>	brushes			= new List<int>();// TODO: could be CSGTreeBrushes

        internal static int GetMaxNodeIndex() { return nodeHierarchies.Count; }

        internal static int GetNodeCount()		{ return Mathf.Max(0, nodeHierarchies.Count - freeNodeIDs.Count); }
        internal static int GetBrushCount()		{ return brushes.Count; }
        internal static int GetBranchCount()	{ return branches.Count; }
        internal static int GetTreeCount()		{ return trees.Count; }

        internal static void ClearAllNodes()
        {
            nodeUserIDs		.Clear();	
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
                nodeFlags			.Add(new NodeFlags());
                nodeTransforms		.Add(new NodeTransform());
                nodeLocalTransforms	.Add(new NodeLocalTransform());
                nodeHierarchies		.Add(new NodeHierarchy());

                var nodeTransform = nodeTransforms[nodeIndex];
                NodeTransform.Reset(ref nodeTransform);
                nodeTransforms[nodeIndex] = nodeTransform;

                var nodeLocalTransform = nodeLocalTransforms[nodeIndex];
                NodeLocalTransform.Reset(ref nodeLocalTransform);
                nodeLocalTransforms[nodeIndex] = nodeLocalTransform;

                var nodeHierarchy = nodeHierarchies[nodeIndex];
                NodeHierarchy.Reset(ref nodeHierarchy);
                nodeHierarchies[nodeIndex] = nodeHierarchy;

                var flags = nodeFlags[nodeIndex];
                NodeFlags.Reset(ref flags);
                nodeFlags[nodeIndex] = flags;

                return nodeIndex + 1; // NOTE: converting index to ID
            }
        }

        internal static bool DestroyNode(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false;

            var nodeIndex = nodeID - 1; // NOTE: converting ID to index

            var nodeType = nodeFlags[nodeIndex].nodeType;

            Debug.Assert(nodeUserIDs.Count == nodeHierarchies.Count);
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
                    SetDirtyWithFlag(oldParentNodeID);
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
                    SetTreeDirtyWithFlag(oldTreeNodeID);
                }
            }

            

            nodeUserIDs		[nodeIndex] = kDefaultUserID;
            
            var flags = nodeFlags[nodeIndex];
            NodeFlags.Reset(ref flags);
            nodeFlags[nodeIndex] = flags;

            var nodeTransform = nodeTransforms[nodeIndex];
            NodeTransform.Reset(ref nodeTransform);
            nodeTransforms[nodeIndex] = nodeTransform;

            var nodeLocalTransform = nodeLocalTransforms[nodeIndex];
            NodeLocalTransform.Reset(ref nodeLocalTransform);
            nodeLocalTransforms[nodeIndex] = nodeLocalTransform;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            NodeHierarchy.Reset(ref nodeHierarchy);
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
            nodeHierarchy.brushInfo = new BrushInfo();
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
            nodeHierarchy.treeInfo		= new TreeInfo();
            nodeHierarchy.children		= new List<int>();
            nodeHierarchies[nodeIndex]	= nodeHierarchy;
            
            trees.Add(generatedTreeNodeID);

            SetDirty(generatedTreeNodeID);
            return true;
        }

        internal static bool		IsValidNodeID					(Int32 nodeID)	{ return (nodeID > 0 && nodeID <= nodeHierarchies.Count) && nodeFlags[nodeID - 1].nodeType != CSGNodeType.None; }

        internal static bool	    AssertNodeIDValid				(Int32 nodeID)
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
                    Debug.LogError($"Invalid ID {nodeID} with type {nodeFlags[nodeIndex].nodeType}");
                else
                    Debug.LogError($"Invalid ID {nodeID}, outside of bounds");
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
            else
                return nodeFlags[nodeID - 1].nodeType;
        }


        internal static Int32		GetUserIDOfNode	(Int32 nodeID)	{ if (!IsValidNodeID(nodeID)) return kDefaultUserID; return nodeUserIDs[nodeID - 1]; }

        internal static Bounds      GetBrushBounds	(Int32 brushNodeID)
        {
            if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) 
                return default;
            var brushNodeIndex  = brushNodeID - 1;
            var treeNodeID      = nodeHierarchies[brushNodeIndex].treeNodeID;
            var treeNodeIndex   = treeNodeID - 1;
            var chiselLookupValues = ChiselTreeLookup.Value[treeNodeIndex];

            if (!chiselLookupValues.brushTreeSpaceBoundLookup.TryGetValue(brushNodeIndex, out MinMaxAABB result))
                return default;

            if (float.IsInfinity(result.Min.x))
                return default;

            var bounds = new Bounds();
            bounds.SetMinMax(result.Min, result.Max);
            return bounds;
        }


        internal static bool		IsNodeDirty(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false;
            switch (nodeFlags[nodeID - 1].nodeType)
            {
                case CSGNodeType.Brush:		return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.NeedCSGUpdate);
                case CSGNodeType.Branch:	return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.BranchNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate);
                case CSGNodeType.Tree:		return nodeFlags[nodeID - 1].IsAnyNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate);
            }
            return false;
        }

        static bool SetTreeDirtyWithFlag(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            var treeNodeFlags = nodeFlags[nodeID - 1];
            treeNodeFlags.SetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
            nodeFlags[nodeID - 1] = treeNodeFlags;
            return true;
        }

        static bool SetOperationDirtyWithFlag(Int32 nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            int treeNodeID = GetTreeOfNode(nodeID);
            var flags = nodeFlags[nodeID - 1];
            flags.SetNodeFlag(NodeStatusFlags.BranchNeedsUpdate);
            nodeFlags[nodeID - 1] = flags;
            SetTreeDirtyWithFlag(treeNodeID);
            return true;
        }

        static bool SetBrushDirtyWithFlag(Int32 nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            if (!IsValidNodeID(nodeID))
                return false;

            int treeNodeID = GetTreeOfNode(nodeID);
            var flags = nodeFlags[nodeID - 1];
            flags.SetNodeFlag(brushNodeFlags);
            nodeFlags[nodeID - 1] = flags;
            SetTreeDirtyWithFlag(treeNodeID);
            return true;
        }

        static bool SetDirtyWithFlag(Int32 nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            if (!AssertNodeIDValid(nodeID))
                return false;
            switch (nodeFlags[nodeID - 1].nodeType)
            {
                case CSGNodeType.Brush:     { return SetBrushDirtyWithFlag(nodeID, brushNodeFlags); }
                case CSGNodeType.Branch:    { return SetOperationDirtyWithFlag(nodeID); }
                case CSGNodeType.Tree:      { return SetTreeDirtyWithFlag(nodeID); }
                default:
                {
                    Debug.LogError("Unknown node type");
                    return false;
                }
            }
        }

        internal static bool SetDirty(Int32 nodeID)
        {
            return SetDirtyWithFlag(nodeID, NodeStatusFlags.NeedFullUpdate);
        }

        internal static bool ClearDirty(Int32 nodeID)
        {
            if (!AssertNodeIDValid(nodeID)) return false;
            var flags = nodeFlags[nodeID - 1];
            switch (flags.nodeType)
            {
                case CSGNodeType.Brush:		flags.UnSetNodeFlag(NodeStatusFlags.NeedFullUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Branch:	flags.UnSetNodeFlag(NodeStatusFlags.BranchNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
                case CSGNodeType.Tree:		flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate); nodeFlags[nodeID - 1] = flags; return true;
            }
            return false;
        }

        private static void DirtySelf(Int32 nodeID)
        {
            SetDirtyWithFlag(nodeID);
        }

        private static void DirtySelfAndChildren(Int32 nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            SetDirtyWithFlag(nodeID, brushNodeFlags);
            var children = nodeHierarchies[nodeID - 1].children;
            if (children == null)
                return;

            // TODO: make this non recursive
            for (int i = 0; i < children.Count; i++)
                DirtySelfAndChildren(children[i], brushNodeFlags);
        }


        internal static bool		GetNodeLocalTransformation(Int32 nodeID, out Matrix4x4 localTransformation)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID))
            {
                localTransformation = Matrix4x4.identity;
                return false;
            }
            localTransformation = nodeLocalTransforms[nodeID - 1].localTransformation;
            return true;
        }

        internal static bool		SetNodeLocalTransformation(Int32 nodeID, ref Matrix4x4 localTransformation)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID))
                return false;

            var nodeIndex = nodeID - 1;

            var treeNodeID          = nodeHierarchies[nodeIndex].treeNodeID;
            var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeID - 1];

            var nodeLocalTransform = nodeLocalTransforms[nodeIndex];
            NodeLocalTransform.SetLocalTransformation(ref nodeLocalTransform, localTransformation);
            nodeLocalTransforms[nodeIndex] = nodeLocalTransform;

            DirtySelfAndChildren(nodeID, NodeStatusFlags.TransformationModified);
            SetDirtyWithFlag(nodeID, NodeStatusFlags.TransformationModified);
            return true;
        }

        internal static bool		GetTreeToNodeSpaceMatrix(Int32 nodeID, out Matrix4x4 treeToNodeMatrix)			{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { treeToNodeMatrix = Matrix4x4.identity; return false; } treeToNodeMatrix = nodeTransforms[nodeID - 1].treeToNode; return true; }

        internal static bool		GetNodeToTreeSpaceMatrix(Int32 nodeID, out Matrix4x4 nodeToTreeMatrix)			{ if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { nodeToTreeMatrix = Matrix4x4.identity; return false; } nodeToTreeMatrix = nodeTransforms[nodeID - 1].nodeToTree; return true; }


        internal static CSGOperationType GetNodeOperationType(Int32 nodeID) { if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasOperation(nodeID)) return CSGOperationType.Invalid; return nodeFlags[nodeID - 1].operationType; }

        internal static bool		SetNodeOperationType(Int32 nodeID, CSGOperationType operation)
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
            DirtySelfAndChildren(nodeID, NodeStatusFlags.TransformationModified | NodeStatusFlags.HierarchyModified);
            return true;
        }


        internal static BrushInfo	GetBrushInfo(Int32 brushNodeID)										{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return null; return nodeHierarchies[brushNodeID - 1].brushInfo; }
        

        internal static BrushInfo	GetBrushInfoUnsafe(Int32 brushNodeID)								{ return nodeHierarchies[brushNodeID - 1].brushInfo; }


        internal static Int32		GetBrushMeshID(Int32 brushNodeID)									{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return BrushMeshInstance.InvalidInstanceID; return nodeHierarchies[brushNodeID-1].brushInfo.brushMeshInstanceID; }
        internal static bool		SetBrushMeshID(Int32 brushNodeID, Int32 brushMeshID)				{ if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return false; nodeHierarchies[brushNodeID - 1].brushInfo.brushMeshInstanceID = brushMeshID; DirtySelf(brushNodeID); return true; }


        internal static Int32		GetNumberOfBrushesInTree(Int32 treeNodeID)							{ if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) return 0; if (nodeHierarchies[treeNodeID - 1].treeInfo == null) return 0; return nodeHierarchies[treeNodeID - 1].treeInfo.allTreeBrushes.items.Count; }
        internal static bool	    DoesTreeContainBrush(Int32 treeNodeID, Int32 brushNodeID)
        {
            if (!AssertNodeIDValid(treeNodeID) || 
                !AssertNodeIDValid(brushNodeID) || 
                !AssertNodeType(treeNodeID, CSGNodeType.Tree) || 
                !AssertNodeType(brushNodeID, CSGNodeType.Brush))
                return false;
            if (nodeHierarchies[treeNodeID - 1].treeInfo == null)
                return false;
            return nodeHierarchies[treeNodeID - 1].treeInfo.allTreeBrushes.items.Contains(brushNodeID);
        }
        
        internal static Int32		GetChildBrushNodeIDAtIndex(Int32 treeNodeID, Int32 index)			
        { 
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree)) 
                return 0; 
            if (nodeHierarchies[treeNodeID - 1].treeInfo == null) 
                return 0;
            if (index < 0 || index > nodeHierarchies[treeNodeID - 1].treeInfo.allTreeBrushes.items.Count)
                return 0;
            return nodeHierarchies[treeNodeID - 1].treeInfo.allTreeBrushes.items[index];
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
                var childNodeID     = children[i];

                var isBrush			= nodeFlags[childNodeID - 1].nodeType == CSGNodeType.Brush;
                var nodeHierarchy	= nodeHierarchies[childNodeID - 1]; 
                if (isBrush && IsValidNodeID(nodeHierarchy.treeNodeID))
                    nodeHierarchies[nodeHierarchy.treeNodeID - 1].RemoveBrush(childNodeID);
                nodeHierarchy.SetTreeNodeID(treeNodeID);
                nodeHierarchies[childNodeID - 1] = nodeHierarchy;
                if (isBrush && IsValidNodeID(treeNodeID))
                    nodeHierarchies[treeNodeID - 1].AddBrush(childNodeID);
                if (!AssertNodeTypeHasChildren(childNodeID))
                    continue;
                if (!passed.Add(childNodeID))
                    return false;
                SetChildrenTree(childNodeID, treeNodeID, passed);
            }
            return true;
        }

        private static void SetChildrenTree(Int32 parentNodeID, Int32 treeNodeID)
        {
            var parentNodeIndex = parentNodeID - 1;
            var children		= nodeHierarchies[parentNodeIndex].children;
            if (children == null)
                return;

            foundNodes.Clear();
            foundNodes.Add(parentNodeID);

            SetChildrenTree(parentNodeID, treeNodeID, foundNodes);
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
                        SetTreeDirtyWithFlag(oldTreeNodeID);
                    }
                    if (IsValidNodeID(newTreeNodeID))
                    {
                        var treeNodeHierarchy = nodeHierarchies[newTreeNodeID - 1];
                        treeNodeHierarchy.AddBrush(childNodeID);
                        nodeHierarchies[newTreeNodeID - 1] = treeNodeHierarchy;
                        SetTreeDirtyWithFlag(newTreeNodeID);
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
                    SetDirtyWithFlag(oldParentNodeID);
                }
                // it is assumed that adding the child is done outside this method since there is more context there
            }

            var nodeHierarchy = nodeHierarchies[childNodeID - 1];
            nodeHierarchy.SetAncestors(newParentNodeID, newTreeNodeID);
            nodeHierarchies[childNodeID - 1] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
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
                    SetTreeDirtyWithFlag(oldTreeNodeID);
                }
            } else
            if (nodeType == CSGNodeType.Branch)
                SetChildrenTree(childNodeID, CSGTreeNode.InvalidNodeID);

            if (IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeID - 1];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeID - 1] = parentNodeHierarchy;
                SetDirtyWithFlag(oldParentNodeID);
                if (IsValidNodeID(oldTreeNodeID))
                    SetTreeDirtyWithFlag(oldTreeNodeID);
            } else
            if (IsValidNodeID(oldTreeNodeID))
            {
                var treeNodeHierarchy = nodeHierarchies[oldTreeNodeID - 1];
                treeNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldTreeNodeID - 1] = treeNodeHierarchy;
                SetTreeDirtyWithFlag(oldTreeNodeID);
            }

            var nodeHierarchy = nodeHierarchies[childNodeID - 1];
            nodeHierarchy.SetAncestors(CSGTreeNode.InvalidNodeID, CSGTreeNode.InvalidNodeID);
            nodeHierarchies[childNodeID - 1] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
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
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            SetDirtyWithFlag(nodeID);
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
                SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            }
            SetDirtyWithFlag(nodeID);
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
                SetDirtyWithFlag(oldParentNodeID);
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
                SetTreeDirtyWithFlag(oldTreeNodeID);
            }

            if (!nodeHierarchy.AddChild(childNodeID))
                return false;
            nodeHierarchies[nodeID - 1] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            AddToParent(nodeID, childNodeID);
            SetDirtyWithFlag(nodeID);
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
                SetDirtyWithFlag(oldParentNodeID);
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
                SetTreeDirtyWithFlag(oldTreeNodeID);
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
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            AddToParent(nodeID, childNodeID);
            SetDirtyWithFlag(nodeID);
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
                SetDirtyWithFlag(srcChildren[i].nodeID, NodeStatusFlags.HierarchyModified);
                AddToParent(nodeID, srcChildren[i].nodeID);
            }
            SetDirtyWithFlag(nodeID);
            return true;
        }

        static readonly HashSet<int> foundNodes = new HashSet<int>();

        internal static bool SetChildNodes(Int32 nodeID, List<CSGTreeNode> children)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID)) return false;
            if (children == null)
                throw new ArgumentNullException(nameof(children));
            if (nodeID == CSGTreeNode.InvalidNodeID)
                throw new ArgumentException("nodeID equals " + CSGTreeNode.InvalidNode);
            if (children.Count == 0)
                return true;

            foundNodes.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                if (nodeID == children[i].NodeID)
                    return false;
                if (!foundNodes.Add(children[i].NodeID))
                {
                    Debug.LogError("Have duplicate child");
                    return false;
                }
            }
            foundNodes.Clear();
            
            for (int i = 0; i < children.Count; i++)
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
                SetDirtyWithFlag(child.nodeID, NodeStatusFlags.HierarchyModified);
            }
            SetDirtyWithFlag(nodeID);
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
                SetDirtyWithFlag(child.nodeID, NodeStatusFlags.HierarchyModified);
            }
            SetDirtyWithFlag(nodeID);
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

        static readonly HashSet<int> destroyed = new HashSet<int>();

        internal static bool DestroyNodes(CSGTreeNode[] nodeIDs)
        {
            if (nodeIDs == null)
                return false;

            destroyed.Clear();
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

        internal static bool DestroyNodes(HashSet<CSGTreeNode> nodeIDs)
        {
            if (nodeIDs == null)
                return false;

            destroyed.Clear();
            bool fail = false;
            foreach(var item in nodeIDs)
            {
                var nodeID = item.nodeID;
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

            Debug.Assert(nodeUserIDs.Count == nodeHierarchies.Count);
            Debug.Assert(nodeFlags.Count == nodeHierarchies.Count);
            Debug.Assert(nodeTransforms.Count == nodeHierarchies.Count);
            Debug.Assert(nodeLocalTransforms.Count == nodeHierarchies.Count);

            int n = 0;
            for (int nodeIndex = 0; nodeIndex < nodeHierarchies.Count; nodeIndex++)
            {
                var nodeID = nodeIndex + 1;
                if (!IsValidNodeID(nodeID))
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
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var nodeHierarchy = nodeHierarchies[i];
                if (nodeHierarchy.brushInfo == null ||
                    nodeHierarchy.treeNodeID == CSGTreeNode.InvalidNodeID)
                    continue;

                if (nodeHierarchy.brushInfo.brushMeshInstanceID != brushMeshID)
                    continue;

                if (CSGTreeNode.IsNodeIDValid(nodeHierarchy.treeNodeID))
                    CSGManager.SetBrushMeshID(nodeHierarchy.treeNodeID, BrushMeshInstance.InvalidInstance.BrushMeshID);
            }
        }

        public static void NotifyBrushMeshModified(int brushMeshID)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var nodeHierarchy = nodeHierarchies[i];
                if (nodeHierarchy.brushInfo == null ||
                    nodeHierarchy.treeNodeID == CSGTreeNode.InvalidNodeID)
                    continue;

                if (nodeHierarchy.brushInfo.brushMeshInstanceID != brushMeshID)
                    continue;

                if (CSGTreeNode.IsNodeIDValid(nodeHierarchy.treeNodeID))
                    CSGTreeNode.SetDirty(nodeHierarchy.treeNodeID);
            }
        }
        public static void NotifyBrushMeshModified(HashSet<int> modifiedBrushMeshes)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (int i = 0; i < nodeHierarchies.Count; i++)
            {
                var nodeHierarchy = nodeHierarchies[i];
                if (nodeHierarchy.brushInfo == null ||
                    nodeHierarchy.treeNodeID == CSGTreeNode.InvalidNodeID)
                    continue;

                if (!modifiedBrushMeshes.Contains(nodeHierarchy.brushInfo.brushMeshInstanceID))
                    continue;

                if (CSGTreeNode.IsNodeIDValid(nodeHierarchy.treeNodeID))
                    CSGTreeNode.SetDirty(nodeHierarchy.treeNodeID);
            }
        }


        internal static int GetBrushMeshCount() { return BrushMeshManager.GetBrushMeshCount(); }

        private static BrushMeshInstance[] GetAllBrushMeshInstances() { return BrushMeshManager.GetAllBrushMeshInstances(); }

        internal static CSGTreeBrushFlags GetBrushFlags(Int32 brushNodeID) { return CSGTreeBrushFlags.Default; } // TODO: implement
        internal static bool SetBrushFlags(Int32 brushNodeID, CSGTreeBrushFlags flags) { return false; } // TODO: implement
    }
}