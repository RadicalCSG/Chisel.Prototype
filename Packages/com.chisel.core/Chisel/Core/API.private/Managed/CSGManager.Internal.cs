using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    // TODO: review flags, might not make sense any more
    public enum NodeStatusFlags : UInt16
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

    // TODO: clean up
    static partial class CSGManager
    {
        internal const int kDefaultUserID = 0;

        struct NodeFlags
        {
            public NodeStatusFlags	status;
            public CSGOperationType operationType;
            public CSGNodeType		nodeType;

            public void SetOperation	(CSGOperationType operation)	{ this.operationType = operation; }

            public void ClearAllStatusFlags()			                { status = NodeStatusFlags.None; }
            public bool IsAnyStatusFlagSet()			                { return status != NodeStatusFlags.None; }
            public bool IsAnyStatusFlagSet(NodeStatusFlags flag)		{ return (status & flag) != NodeStatusFlags.None; }
            public bool IsStatusFlagSet	(NodeStatusFlags flag)			{ return (status & flag) == flag; }
            public void ClearStatusFlag	(NodeStatusFlags flag)			{ status &= ~flag; }
            public void SetStatusFlag	(NodeStatusFlags flag)			{ status |= flag; }

            internal static void Reset(ref NodeFlags data)
            {
                data.status         = NodeStatusFlags.None;
                data.operationType	= CSGOperationType.Invalid;
                data.nodeType		= CSGNodeType.None;
            }
        };

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
            public List<CompactNodeID>	children;
            public CompactNodeID        treeNodeID;
            public CompactNodeID        parentNodeID;

            internal bool RemoveChild(CompactNodeID childNodeID)
            {
                if (children == null)
                    return false;
                return children.Remove(childNodeID);
            }

            internal bool AddChild(CompactNodeID childNodeID)
            {
                if (children == null)
                    children = new List<CompactNodeID>();
                else
                if (children.Contains(childNodeID))
                    return false;
                children.Add(childNodeID);
                return true;
            }


            internal static void Reset(ref NodeHierarchy data)
            {
                data.children       = null;
                data.treeNodeID     = CompactNodeID.Invalid;
                data.parentNodeID   = CompactNodeID.Invalid;
            }

            internal void SetAncestors(CompactNodeID parentNodeID, CompactNodeID treeNodeID)
            {
                this.parentNodeID = parentNodeID;
                this.treeNodeID = treeNodeID;
            }

            internal void SetTreeNodeID(CompactNodeID treeNodeID)
            {
                this.treeNodeID = treeNodeID;
            }
        }

        // Temporary hack
        public static void ClearOutlines()
        {
            for (var i = 0; i < brushOutlineStates.Count; i++)
            {
                if (brushOutlineStates[i] != null)
                    brushOutlineStates[i].Dispose();
                brushOutlineStates[i] = null;
            }
            brushOutlineStates.Clear();
        }

        internal sealed class BrushOutlineState : IDisposable
        {
            public int			brushMeshInstanceID;

            public BrushOutline brushOutline;

            public void Dispose()
            { 
                if (brushOutline.IsCreated) 
                    brushOutline.Dispose();
                brushOutline = default;
            }
        }

        private static readonly List<int>					nodeUserIDs			= new List<int>();
        private static readonly List<NodeFlags>				statusFlags			= new List<NodeFlags>();
        private static readonly List<NodeTransform>			nodeTransforms		= new List<NodeTransform>();
        private static readonly List<NodeLocalTransform>	nodeLocalTransforms	= new List<NodeLocalTransform>();
        private static readonly List<NodeHierarchy>			nodeHierarchies		= new List<NodeHierarchy>();
        private static readonly List<BrushOutlineState>		brushOutlineStates	= new List<BrushOutlineState>();

        private static readonly List<CompactNodeID>	freeNodeIDs	= new List<CompactNodeID>();
        private static readonly List<CompactNodeID>	trees	    = new List<CompactNodeID>();// TODO: could be CSGTrees
        private static readonly List<CompactNodeID>	branches	= new List<CompactNodeID>();// TODO: could be CSGTreeBranches
        private static readonly List<CompactNodeID>	brushes	    = new List<CompactNodeID>();// TODO: could be CSGTreeBrushes

        internal static void ClearAllNodes()
        {
            for (var i = 0; i < brushOutlineStates.Count; i++)
            {
                if (brushOutlineStates[i] != null)
                    brushOutlineStates[i].Dispose();
                brushOutlineStates[i] = null;
            }

            nodeUserIDs		    .Clear();
            statusFlags		    .Clear();	nodeTransforms		.Clear();
            nodeHierarchies	    .Clear();	nodeLocalTransforms	.Clear();
            brushOutlineStates  .Clear();   

            freeNodeIDs		.Clear();	trees	.Clear();
            branches		.Clear();	brushes	.Clear();

            ChiselTreeLookup.Value.Clear(); 
        }

        internal static bool DestroyNode(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false; 

            var nodeIndex = nodeID.ID - 1; // NOTE: converting ID to index

            var nodeType = statusFlags[nodeIndex].nodeType;

            Debug.Assert(nodeUserIDs.Count == nodeHierarchies.Count);
            Debug.Assert(statusFlags.Count == nodeHierarchies.Count);
            Debug.Assert(nodeTransforms.Count == nodeHierarchies.Count);
            Debug.Assert(nodeLocalTransforms.Count == nodeHierarchies.Count);

            if (nodeType == CSGNodeType.Branch ||
                nodeType == CSGNodeType.Tree)
                SetChildrenTree(nodeID, CompactNodeID.Invalid);

            if (nodeType == CSGNodeType.Branch ||
                nodeType == CSGNodeType.Brush)
            {
                var oldParentNodeID = nodeHierarchies[nodeIndex].parentNodeID;
                var oldTreeNodeID = nodeHierarchies[nodeIndex].treeNodeID;
                if (IsValidNodeID(oldParentNodeID))
                {
                    var oldParentNodeIndex = oldParentNodeID.ID - 1;
                    var oldParentNodeHierarchy = nodeHierarchies[oldParentNodeIndex];
                    oldParentNodeHierarchy.RemoveChild(nodeID);
                    nodeHierarchies[oldParentNodeIndex] = oldParentNodeHierarchy;
                    SetDirtyWithFlag(oldParentNodeID);
                }
                if (IsValidNodeID(oldTreeNodeID))
                {
                    var oldTreeNodeIndex = oldTreeNodeID.ID - 1;
                    if (!IsValidNodeID(oldParentNodeID))
                    {
                        var treeNodeHierarchy = nodeHierarchies[oldTreeNodeIndex];
                        treeNodeHierarchy.RemoveChild(nodeID);
                        nodeHierarchies[oldTreeNodeIndex] = treeNodeHierarchy;
                    }
                    SetTreeDirtyWithFlag(oldTreeNodeID);
                }
            }

            if (nodeType == CSGNodeType.Tree)
                ChiselTreeLookup.Value.Remove(nodeID);
            

            nodeUserIDs		[nodeIndex] = kDefaultUserID;
            
            var flags = statusFlags[nodeIndex];
            NodeFlags.Reset(ref flags);
            statusFlags[nodeIndex] = flags;

            var nodeTransform = nodeTransforms[nodeIndex];
            NodeTransform.Reset(ref nodeTransform);
            nodeTransforms[nodeIndex] = nodeTransform;

            var nodeLocalTransform = nodeLocalTransforms[nodeIndex];
            NodeLocalTransform.Reset(ref nodeLocalTransform);
            nodeLocalTransforms[nodeIndex] = nodeLocalTransform;


            if (brushOutlineStates[nodeIndex] != null)
                brushOutlineStates[nodeIndex].Dispose();
            brushOutlineStates[nodeIndex] = null; 
            
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

            if (nodeID.ID == nodeHierarchies.Count)
            {
                var nodeIDValue = nodeID.ID;
                while (nodeHierarchies.Count > 0 &&
                        !IsValidNodeID(nodeID))
                { 
                    freeNodeIDs			.Remove(new CompactNodeID { ID = nodeIDValue });
                    nodeIndex = nodeIDValue - 1;

                    nodeUserIDs			.RemoveAt(nodeIndex);
                    statusFlags			.RemoveAt(nodeIndex);
                    nodeTransforms		.RemoveAt(nodeIndex);
                    nodeLocalTransforms	.RemoveAt(nodeIndex);
                    brushOutlineStates  .RemoveAt(nodeIndex);
                    nodeHierarchies		.RemoveAt(nodeIndex);
                    nodeIDValue--;
                }
            }			
            return true;
        }

        #region Generate
        static CompactNodeID GenerateValidNodeIndex(Int32 userID, CSGNodeType type)
        {
            if (freeNodeIDs.Count > 0)
            {
                if (freeNodeIDs.Count == 1)
                {
                    var nodeID = freeNodeIDs[0];
                    var nodeIndex = nodeID.ID - 1;
                    freeNodeIDs.Clear();
                    nodeUserIDs[nodeIndex] = userID;
                    return nodeID;
                } else
                { 
                    freeNodeIDs.Sort(); // I'm sorry!
                    var nodeID = freeNodeIDs[0];
                    var nodeIndex = nodeID.ID - 1;
                    freeNodeIDs.RemoveAt(0);// I'm sorry again!
                    nodeUserIDs[nodeIndex] = userID;
                    return nodeID;
                }
            } else
            {
                var nodeIndex = nodeHierarchies.Count; // NOTE: Index, not ID

                nodeUserIDs			.Add(userID); // <- setting userID here
                statusFlags			.Add(new NodeFlags());
                nodeTransforms		.Add(new NodeTransform());
                nodeLocalTransforms	.Add(new NodeLocalTransform());
                brushOutlineStates  .Add(null);
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

                var flags = statusFlags[nodeIndex];
                NodeFlags.Reset(ref flags);
                statusFlags[nodeIndex] = flags;

                var nodeID = nodeIndex + 1;
                return new CompactNodeID { ID = nodeID }; // NOTE: converting index to ID
            }
        }

        internal static bool		GenerateBrush	(Int32 userID, out CompactNodeID generatedNodeID)
        {
            generatedNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Brush);

            var nodeIndex = generatedNodeID.ID - 1; // NOTE: converting ID to index

            var flags = statusFlags[nodeIndex];
            flags.operationType = CSGOperationType.Additive;
            flags.nodeType      = CSGNodeType.Brush;
            statusFlags[nodeIndex] = flags;

            if (brushOutlineStates[nodeIndex] != null)
                brushOutlineStates[nodeIndex].Dispose();
            brushOutlineStates[nodeIndex] = new BrushOutlineState();

            brushes.Add(generatedNodeID);

            SetDirty(generatedNodeID);
            return true;
        }

        internal static bool		GenerateBranch	(Int32 userID, out CompactNodeID generatedBranchNodeID)
        {
            generatedBranchNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Branch);

            var nodeIndex = generatedBranchNodeID.ID - 1; // NOTE: converting ID to index

            var flags = statusFlags[nodeIndex];
            flags.operationType	= CSGOperationType.Additive;
            flags.nodeType		= CSGNodeType.Branch;
            statusFlags[nodeIndex] = flags;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.children = new List<CompactNodeID>();
            nodeHierarchies[nodeIndex] = nodeHierarchy;
            
            branches.Add(generatedBranchNodeID);

            SetDirty(generatedBranchNodeID);
            return true;
        }

        internal static bool		GenerateTree	(Int32 userID, out CompactNodeID generatedTreeNodeID)
        {
            generatedTreeNodeID = GenerateValidNodeIndex(userID, CSGNodeType.Tree);

            var nodeIndex = generatedTreeNodeID.ID - 1;

            var flags = statusFlags[nodeIndex];
            flags.nodeType = CSGNodeType.Tree;
            statusFlags[nodeIndex] = flags;

            var nodeHierarchy = nodeHierarchies[nodeIndex];
            nodeHierarchy.children		= new List<CompactNodeID>();
            nodeHierarchies[nodeIndex]	= nodeHierarchy;
            
            trees.Add(generatedTreeNodeID);

            SetDirty(generatedTreeNodeID);
            return true;
        }
        #endregion
        

        internal static NodeTransformations GetNodeTransformation(CompactNodeID nodeID)
        {
            int nodeIndex = nodeID.ID - 1;
            // TODO: clean this up and make this sensible

            // Note: Currently "localTransformation" is actually nodeToTree, but only for all the brushes. 
            //       Branches do not have a transformation set at the moment.

            // TODO: should be transformations the way up to the tree (but not above), not just tree vs brush
            var brushLocalTransformation     = CSGManager.nodeLocalTransforms[nodeIndex].localTransformation;
            var brushLocalInvTransformation  = CSGManager.nodeLocalTransforms[nodeIndex].invLocalTransformation;

            var nodeTransform                = CSGManager.nodeTransforms[nodeIndex];
            nodeTransform.nodeToTree = brushLocalTransformation;
            nodeTransform.treeToNode = brushLocalInvTransformation;
            CSGManager.nodeTransforms[nodeIndex] = nodeTransform;

            return new NodeTransformations { nodeToTree = nodeTransform.nodeToTree, treeToNode = nodeTransform.treeToNode };
        }

        #region Validation
        internal static bool		IsValidNodeID					(CompactNodeID nodeID)	
        { 
            var nodeIndex = nodeID.ID - 1;
            return (nodeIndex >= 0 && nodeIndex < nodeHierarchies.Count) && statusFlags[nodeIndex].nodeType != CSGNodeType.None; 
        }

        internal static bool        AssertNodeIDValid(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
            {
                if (nodeID == CompactNodeID.Invalid)
                {
                    Debug.LogError("Invalid ID {nodeID}, 'empty'");
                    return false;
                }
                var nodeIndex = nodeID.ID - 1;
                if (nodeIndex >= 0 && nodeIndex < nodeHierarchies.Count)
                    Debug.LogError($"Invalid ID {nodeID} with type {statusFlags[nodeIndex].nodeType}");
                else
                    Debug.LogError($"Invalid ID {nodeID}, outside of bounds");
                return false;
            }
            return true;
        }
        private static bool			AssertNodeType					(CompactNodeID nodeID, CSGNodeType type)
        {
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType == type; 
        }
        private static bool			AssertNodeTypeHasChildren		(CompactNodeID nodeID) 
        {
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType == CSGNodeType.Branch || statusFlags[nodeIndex].nodeType == CSGNodeType.Tree; 
        }
        private static bool			AssertNodeTypeHasParent		    (CompactNodeID nodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType == CSGNodeType.Branch || statusFlags[nodeIndex].nodeType == CSGNodeType.Brush; 
        }
        private static bool			AssertNodeTypeHasOperation		(CompactNodeID nodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType == CSGNodeType.Branch || statusFlags[nodeIndex].nodeType == CSGNodeType.Brush; 
        }
        private static bool			AssertNodeTypeHasTransformation	(CompactNodeID nodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType != CSGNodeType.None; 
        }
        #endregion

        #region Set Dirty
        static bool SetTreeDirtyWithFlag(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            var nodeIndex = nodeID.ID - 1;
            var treeNodeFlags = statusFlags[nodeIndex];
            treeNodeFlags.SetStatusFlag(NodeStatusFlags.TreeNeedsUpdate);
            statusFlags[nodeIndex] = treeNodeFlags;
            return true;
        }

        static bool SetOperationDirtyWithFlag(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return false;
            var treeNodeID = GetTreeOfNode(nodeID);
            var nodeIndex = nodeID.ID - 1;
            var flags = statusFlags[nodeIndex];
            flags.SetStatusFlag(NodeStatusFlags.BranchNeedsUpdate);
            statusFlags[nodeIndex] = flags;
            SetTreeDirtyWithFlag(treeNodeID);
            return true;
        }

        static bool SetBrushDirtyWithFlag(CompactNodeID nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            if (!IsValidNodeID(nodeID))
                return false;

            var treeNodeID = GetTreeOfNode(nodeID);
            var nodeIndex = nodeID.ID - 1;
            var flags = statusFlags[nodeIndex];
            flags.SetStatusFlag(brushNodeFlags);
            statusFlags[nodeIndex] = flags;
            SetTreeDirtyWithFlag(treeNodeID);
            return true;
        }

        static bool SetDirtyWithFlag(CompactNodeID nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            if (!AssertNodeIDValid(nodeID))
                return false;
            var nodeIndex = nodeID.ID - 1;
            switch (statusFlags[nodeIndex].nodeType)
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

        internal static bool SetDirty(CompactNodeID nodeID)
        {
            return SetDirtyWithFlag(nodeID, NodeStatusFlags.NeedFullUpdate);
        }

        static void DirtySelf(CompactNodeID nodeID)
        {
            SetDirtyWithFlag(nodeID);
        }

        static void DirtySelfAndChildren(CompactNodeID nodeID, NodeStatusFlags brushNodeFlags = NodeStatusFlags.NeedFullUpdate)
        {
            SetDirtyWithFlag(nodeID, brushNodeFlags);
            var nodeIndex = nodeID.ID - 1;
            var children = nodeHierarchies[nodeIndex].children;
            if (children == null)
                return;

            // TODO: make this non recursive
            for (var i = 0; i < children.Count; i++)
                DirtySelfAndChildren(children[i], brushNodeFlags);
        }
        #endregion
        
        internal static bool		IsNodeDirty(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID)) return false;
            var nodeIndex = nodeID.ID - 1;
            switch (statusFlags[nodeIndex].nodeType)
            {
                case CSGNodeType.Brush:		return statusFlags[nodeIndex].IsAnyStatusFlagSet(NodeStatusFlags.NeedCSGUpdate);
                case CSGNodeType.Branch:	return statusFlags[nodeIndex].IsAnyStatusFlagSet(NodeStatusFlags.BranchNeedsUpdate | NodeStatusFlags.NeedPreviousSiblingsUpdate);
                case CSGNodeType.Tree:		return statusFlags[nodeIndex].IsAnyStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate | NodeStatusFlags.TreeMeshNeedsUpdate);
            }
            return false;
        }

        internal static CSGNodeType GetTypeOfNode(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID))
                return CSGNodeType.None;
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].nodeType;
        }

        internal static Int32		GetUserIDOfNode	(CompactNodeID nodeID)	
        { 
            if (!IsValidNodeID(nodeID)) 
                return kDefaultUserID;
            var nodeIndex = nodeID.ID - 1;
            return nodeUserIDs[nodeIndex]; 
        }

        internal static Bounds      GetBrushBounds	(CompactNodeID brushNodeID)
        {
            if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) 
                return default;

            var brushNodeIndex      = brushNodeID.ID - 1;
            var treeNodeID          = nodeHierarchies[brushNodeIndex].treeNodeID;
            var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeID];

            if (!chiselLookupValues.brushTreeSpaceBoundLookup.TryGetValue(brushNodeID, out MinMaxAABB result))
                return default;

            if (float.IsInfinity(result.Min.x))
                return default;

            var bounds = new Bounds();
            bounds.SetMinMax(result.Min, result.Max);
            return bounds;
        }

        #region Matrices
        internal static bool		GetNodeLocalTransformation(CompactNodeID nodeID, out Matrix4x4 localTransformation)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID))
            {
                localTransformation = Matrix4x4.identity;
                return false;
            }
            var nodeIndex = nodeID.ID - 1;
            localTransformation = nodeLocalTransforms[nodeIndex].localTransformation;
            return true;
        }

        internal static bool		SetNodeLocalTransformation(CompactNodeID nodeID, ref Matrix4x4 localTransformation)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID))
                return false;

            var nodeIndex = nodeID.ID - 1;
            var nodeLocalTransform = nodeLocalTransforms[nodeIndex];
            NodeLocalTransform.SetLocalTransformation(ref nodeLocalTransform, localTransformation);
            nodeLocalTransforms[nodeIndex] = nodeLocalTransform;

            DirtySelfAndChildren(nodeID, NodeStatusFlags.TransformationModified);
            SetDirtyWithFlag(nodeID, NodeStatusFlags.TransformationModified);
            return true;
        }

        internal static bool GetTreeToNodeSpaceMatrix(CompactNodeID nodeID, out Matrix4x4 treeToNodeMatrix)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { treeToNodeMatrix = Matrix4x4.identity; return false; }
            var nodeIndex = nodeID.ID - 1;
            treeToNodeMatrix = nodeTransforms[nodeIndex].treeToNode; 
            return true;
        }

        internal static bool GetNodeToTreeSpaceMatrix(CompactNodeID nodeID, out Matrix4x4 nodeToTreeMatrix)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasTransformation(nodeID)) { nodeToTreeMatrix = Matrix4x4.identity; return false; }
            var nodeIndex = nodeID.ID - 1;
            nodeToTreeMatrix = nodeTransforms[nodeIndex].nodeToTree;
            return true;
        }
        #endregion

        #region Operations
        internal static CSGOperationType GetNodeOperationType(CompactNodeID nodeID)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasOperation(nodeID)) return CSGOperationType.Invalid;
            var nodeIndex = nodeID.ID - 1;
            return statusFlags[nodeIndex].operationType;
        }

        internal static bool		SetNodeOperationType(CompactNodeID nodeID, CSGOperationType operation)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasOperation(nodeID) || 
                operation == CSGOperationType.Invalid)
                return false;
            var nodeIndex = nodeID.ID - 1;
            var flags = statusFlags[nodeIndex];
            if (flags.operationType == operation)
                return true;
            flags.SetOperation(operation);
            statusFlags[nodeIndex] = flags;
            DirtySelfAndChildren(nodeID, NodeStatusFlags.TransformationModified | NodeStatusFlags.HierarchyModified);
            return true;
        }
        #endregion


        internal static ref BrushOutline GetBrushOutline(CSGTreeBrush brush)
        {
            if (!AssertNodeIDValid(brush.brushNodeID) || !AssertNodeType(brush.brushNodeID, CSGNodeType.Brush))
                throw new ArgumentNullException(nameof(brush));
            var brushNodeIndex = brush.brushNodeID.ID - 1;
            if (!brushOutlineStates[brushNodeIndex].brushOutline.IsCreated)
                brushOutlineStates[brushNodeIndex].brushOutline = BrushOutline.Create();
            return ref brushOutlineStates[brushNodeIndex].brushOutline; 
        }

        internal static Int32 GetBrushMeshID(CompactNodeID brushNodeID)
        {
            if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return BrushMeshInstance.InvalidInstanceID;
            var brushNodeIndex = brushNodeID.ID - 1;
            return brushOutlineStates[brushNodeIndex].brushMeshInstanceID;
        }

        internal static bool SetBrushMeshID(CompactNodeID brushNodeID, Int32 brushMeshID)
        {
            if (!AssertNodeIDValid(brushNodeID) || !AssertNodeType(brushNodeID, CSGNodeType.Brush)) return false;
            var brushNodeIndex = brushNodeID.ID - 1;
            brushOutlineStates[brushNodeIndex].brushMeshInstanceID = brushMeshID; 
            DirtySelf(brushNodeID);
            return true;
        }

        internal static void GetBrushesInOrder(CSGTree tree, List<CSGTreeBrush> brushes)
        {
            var treeNodeID = tree.NodeID;
            if (!AssertNodeIDValid(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree))
                return;

            if (brushes == null) 
                return;
            UpdateTreeNodeList(treeNodeID, null, brushes);
        }

        static void                 UpdateTreeNodeList(CompactNodeID treeNodeID, List<CompactNodeID> nodes, List<CSGTreeBrush> brushes)
        {
            if (nodes   != null) nodes.Clear();
            if (brushes != null) brushes.Clear();
            if (nodes == null && brushes == null)
                return;

            var treeNodeIndex = treeNodeID.ID - 1;
            if (!IsValidNodeID(treeNodeID))
                return;

            var nodeHierarchy = nodeHierarchies[treeNodeIndex];
            if (nodes != null) nodes.Add(treeNodeID);
            if (nodeHierarchy.children != null)
                RecursiveAddTreeChildren(in nodeHierarchy, nodes, brushes);
        }

        static void RecursiveAddTreeChildren(in NodeHierarchy       parent, 
                                             List<CompactNodeID>    nodes, 
                                             List<CSGTreeBrush>     brushes)
        {
            var children = parent.children;
            for (var i = 0; i < children.Count; i++)
            {
                var childID     = children[i];
                if (!IsValidNodeID(childID))
                    continue;
                var childIndex  = childID.ID - 1;
                if (nodes != null) 
                    nodes.Add(childID);
                if (statusFlags[childIndex].nodeType == CSGNodeType.Brush &&
                    brushes != null)
                    brushes.Add(new CSGTreeBrush { brushNodeID = childID });
                var childHierarchy = nodeHierarchies[childIndex];
                if (childHierarchy.children != null)
                    RecursiveAddTreeChildren(in childHierarchy, 
                                             nodes, 
                                             brushes);
            }
        }


        internal static CompactNodeID GetParentOfNode(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID) || 
                !AssertNodeTypeHasParent(nodeID))
                return CompactNodeID.Invalid;
            var nodeIndex = nodeID.ID - 1;
            return nodeHierarchies[nodeIndex].parentNodeID;
        }

        internal static CompactNodeID GetTreeOfNode(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID) || 
                !AssertNodeTypeHasParent(nodeID))
                return CompactNodeID.Invalid;
            var nodeIndex = nodeID.ID - 1;
            return nodeHierarchies[nodeIndex].treeNodeID;
        }

        internal static Int32	    GetChildNodeCount(CompactNodeID nodeID)
        {
            if (!IsValidNodeID(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID))
                return 0;
            var nodeIndex = nodeID.ID - 1;
            return (nodeHierarchies[nodeIndex].children == null) ? 0 : nodeHierarchies[nodeIndex].children.Count;
        }

        internal static CompactNodeID GetChildNodeAtIndex(CompactNodeID nodeID, Int32 index)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID) || nodeHierarchies[nodeIndex].children == null || index < 0 || index >= nodeHierarchies[nodeIndex].children.Count)
                return CompactNodeID.Invalid;
            return nodeHierarchies[nodeIndex].children[index];
        }

        static bool                 SetChildrenTree(CompactNodeID parentNodeID, CompactNodeID treeNodeID, HashSet<CompactNodeID> passed)
        {
            var parentNodeIndex = parentNodeID.ID - 1;
            var children = nodeHierarchies[parentNodeIndex].children;
            if (children == null)
                return false;
            

            // TODO: make this non recursive
            for (var i = 0; i < children.Count; i++)
            {
                var childNodeID     = children[i];
                var childNodeIndex  = childNodeID.ID - 1;

                var nodeHierarchy	= nodeHierarchies[childNodeIndex]; 
                nodeHierarchy.SetTreeNodeID(treeNodeID);
                nodeHierarchies[childNodeIndex] = nodeHierarchy;

                if (!AssertNodeTypeHasChildren(childNodeID))
                    continue;
                if (!passed.Add(childNodeID))
                    return false;
                SetChildrenTree(childNodeID, treeNodeID, passed);
            }
            return true;
        }

        private static void         SetChildrenTree(CompactNodeID parentNodeID, CompactNodeID treeNodeID)
        {
            var parentNodeIndex = parentNodeID.ID - 1;
            var children		= nodeHierarchies[parentNodeIndex].children;
            if (children == null)
                return;

            s_FoundNodes.Clear();
            s_FoundNodes.Add(parentNodeID);

            SetChildrenTree(parentNodeID, treeNodeID, s_FoundNodes);
        }

        // Note: assumes both newParentNodeID and childNodeID are VALID
        private static void         AddToParent(CompactNodeID newParentNodeID, CompactNodeID childNodeID)
        {
            CompactNodeID newTreeNodeID;
            var newParentNodeIndex = newParentNodeID.ID - 1;
            var nodeParentType = statusFlags[newParentNodeIndex].nodeType;
            if (nodeParentType == CSGNodeType.Tree)
            {
                newTreeNodeID = newParentNodeID;
                newParentNodeID = CompactNodeID.Invalid;
            } else
            {
                Debug.Assert(nodeParentType == CSGNodeType.Branch);
                newTreeNodeID = nodeHierarchies[newParentNodeIndex].treeNodeID;
            }

            var childNodeIDIndex    = childNodeID.ID - 1;
            var oldTreeNodeID		= nodeHierarchies[childNodeIDIndex].treeNodeID;
            var oldParentNodeID		= nodeHierarchies[childNodeIDIndex].parentNodeID;
            var oldTreeNodeIndex    = oldTreeNodeID.ID - 1;
            var oldParentNodeIndex  = oldParentNodeID.ID - 1;

            // Could be possible if we move position in a branch
            if (newParentNodeID == oldParentNodeID && 
                newTreeNodeID == oldTreeNodeID)
                return;
            
            if (oldTreeNodeID != newTreeNodeID)
            {
                var nodeType = statusFlags[childNodeIDIndex].nodeType;
                if (nodeType == CSGNodeType.Branch)
                    SetChildrenTree(childNodeID, newTreeNodeID);
                if (IsValidNodeID(oldTreeNodeID))
                    SetTreeDirtyWithFlag(oldTreeNodeID);
                if (IsValidNodeID(newTreeNodeID))
                {
                    var newTreeNodeIndex = newTreeNodeID.ID - 1;
                    SetTreeDirtyWithFlag(newTreeNodeID);
                }
            }

            if (oldParentNodeID != newParentNodeID)
            {
                if (IsValidNodeID(oldParentNodeID))
                {
                    var parentNodeHierarchy = nodeHierarchies[oldParentNodeIndex];
                    parentNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldParentNodeIndex] = parentNodeHierarchy;
                    SetDirtyWithFlag(oldParentNodeID);
                }
                // it is assumed that adding the child is done outside this method since there is more context there
            }

            var nodeHierarchy = nodeHierarchies[childNodeIDIndex];
            nodeHierarchy.SetAncestors(newParentNodeID, newTreeNodeID);
            nodeHierarchies[childNodeIDIndex] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
        }

        // Note: assumes childNodeID is VALID
        private static void         RemoveFromParent(CompactNodeID childNodeID)
        {
            var childNodeIDIndex    = childNodeID.ID - 1;
            var oldTreeNodeID	    = nodeHierarchies[childNodeIDIndex].treeNodeID;
            var oldParentNodeID	    = nodeHierarchies[childNodeIDIndex].parentNodeID;
            var oldParentNodeIndex  = oldParentNodeID.ID - 1;
            var oldTreeNodeIndex    = oldTreeNodeID.ID - 1;

            var nodeType = statusFlags[childNodeIDIndex].nodeType;
            if (nodeType == CSGNodeType.Branch)
                SetChildrenTree(childNodeID, CompactNodeID.Invalid);
            if (IsValidNodeID(oldTreeNodeID))
                SetTreeDirtyWithFlag(oldTreeNodeID);

            if (IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeIndex];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeIndex] = parentNodeHierarchy;
                SetDirtyWithFlag(oldParentNodeID);
                if (IsValidNodeID(oldTreeNodeID))
                    SetTreeDirtyWithFlag(oldTreeNodeID);
            } else
            if (IsValidNodeID(oldTreeNodeID))
            {
                var treeNodeHierarchy = nodeHierarchies[oldTreeNodeIndex];
                treeNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldTreeNodeIndex] = treeNodeHierarchy;
                SetTreeDirtyWithFlag(oldTreeNodeID);
            }

            var nodeHierarchy = nodeHierarchies[childNodeIDIndex];
            nodeHierarchy.SetAncestors(CompactNodeID.Invalid, CompactNodeID.Invalid);
            nodeHierarchies[childNodeIDIndex] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
        }
        
            
        internal static bool        IsAnyStatusFlagSet(CompactNodeID nodeID)
        {
            if (!AssertNodeIDValid(nodeID))
                return false;
            var nodeIndex   = nodeID.ID - 1;

            var statusFlags = CSGManager.statusFlags[nodeIndex];
            return statusFlags.IsAnyStatusFlagSet();
        }

        internal static bool        IsStatusFlagSet(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID))
                return false;
            var statusFlags = CSGManager.statusFlags[nodeIndex];
            return statusFlags.IsStatusFlagSet(flag);
        }

        internal static void SetStatusFlag(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID))
                return;
            var statusFlags = CSGManager.statusFlags[nodeIndex];
            statusFlags.SetStatusFlag(flag);
            CSGManager.statusFlags[nodeIndex] = statusFlags;
        }

        internal static void ClearAllStatusFlags(CompactNodeID nodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID))
                return;
            var statusFlags = CSGManager.statusFlags[nodeIndex];
            statusFlags.ClearAllStatusFlags();
            CSGManager.statusFlags[nodeIndex] = statusFlags;
        }

        internal static void ClearStatusFlag(CompactNodeID nodeID, NodeStatusFlags flag)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID))
                return;
            var statusFlags = CSGManager.statusFlags[nodeIndex];
            statusFlags.ClearStatusFlag(flag);
            CSGManager.statusFlags[nodeIndex] = statusFlags;
        }

        internal static Int32       IndexOfChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                nodeHierarchies[nodeIndex].children == null)
                return -1;
            return nodeHierarchies[nodeIndex].children.IndexOf(childNodeID);
        }

        internal static bool	    ClearChildNodes(CompactNodeID nodeID)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID))
                return false;
            var nodeIndex	= nodeID.ID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || children.Count == 0)
                return true;

            return RemoveChildNodeRange(nodeID, 0, children.Count);
        }

        internal static bool	    RemoveChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)
        {
            var nodeIndex = nodeID.ID - 1;
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID) || 
                !IsValidNodeID(childNodeID) || 
                !AssertNodeTypeHasParent(childNodeID) || 
                nodeHierarchies[nodeIndex].children == null)
                return false;
            var childIndex = nodeHierarchies[nodeIndex].children.IndexOf(childNodeID);
            if (childIndex == -1)
                return false;
            return RemoveChildNodeAt(nodeID, childIndex);
        }

        internal static bool	    RemoveChildNodeAt(CompactNodeID nodeID, Int32 index)
        {
            if (!AssertNodeIDValid(nodeID) || 
                !AssertNodeTypeHasChildren(nodeID))
                return false;
            var nodeIndex	= nodeID.ID - 1;
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

        internal static bool        RemoveChildNodeRange(CompactNodeID nodeID, Int32 index, Int32 count)
        {
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID))
                return false;

            var nodeIndex	= nodeID.ID - 1;
            var children	= nodeHierarchies[nodeIndex].children;
            if (children == null || children.Count == 0 ||
                index < 0 || index >= children.Count || (index + count) > children.Count)
                return false;
            for (var i = 0; i < count; i++)
            {
                if (!IsValidNodeID(children[index + i]))
                    return false;
            }
            for (var i = count - 1; i >= 0; i--)
            { 
                var childNodeID = children[index + i]; 
                RemoveFromParent(childNodeID);
                SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            }
            SetDirtyWithFlag(nodeID);
            return true;
        }

        static bool                 IsAncestor(CompactNodeID nodeID, CompactNodeID ancestorNodeID)
        {
            if (nodeID == ancestorNodeID)
                return true;

            var parentNodeID = GetParentOfNode(nodeID);
            while (parentNodeID != CompactNodeID.Invalid)
            {
                if (parentNodeID == ancestorNodeID)
                    return true;
                parentNodeID = GetParentOfNode(parentNodeID);
            }
            return false;
        }

        internal static bool	    AddChildNode(CompactNodeID nodeID, CompactNodeID childNodeID)
        {
            if (nodeID == CompactNodeID.Invalid ||
                childNodeID == CompactNodeID.Invalid)
                return false;
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                !AssertNodeIDValid(childNodeID) ||
                !AssertNodeTypeHasParent(childNodeID))
                return false;
            if (IsAncestor(nodeID, childNodeID))
                return false;

            var childNodeIndex      = childNodeID.ID - 1;
            var childNode		    = nodeHierarchies[childNodeIndex];
            var oldParentNodeID     = childNode.parentNodeID;
            var oldTreeNodeID	    = childNode.treeNodeID;
            var oldParentNodeIndex  = oldParentNodeID.ID - 1;
            var oldTreeNodeIndex    = oldTreeNodeID.ID - 1;

            var nodeIndex       = nodeID.ID - 1;
            var nodeIsTree		= statusFlags[nodeIndex].nodeType == CSGNodeType.Tree;
            var nodeHierarchy	= nodeHierarchies[nodeIndex];
            var newParentNodeID = nodeIsTree ? CompactNodeID.Invalid : nodeID;
            var newTreeNodeID	= nodeIsTree ? nodeID : nodeHierarchy.treeNodeID;

            if (oldParentNodeID == newParentNodeID &&
                oldTreeNodeID == newTreeNodeID)
                return false;
            
            if (oldParentNodeID != newParentNodeID &&
                IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeIndex];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeIndex] = parentNodeHierarchy;
                SetDirtyWithFlag(oldParentNodeID);
            }
            
            if (IsValidNodeID(oldTreeNodeID))
            {
                if (!IsValidNodeID(oldParentNodeID))
                {
                    var treeNodeHierarchy = nodeHierarchies[oldTreeNodeIndex];
                    treeNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldTreeNodeIndex] = treeNodeHierarchy;
                }
                SetTreeDirtyWithFlag(oldTreeNodeID);
            }

            if (!nodeHierarchy.AddChild(childNodeID))
                return false;
            nodeHierarchies[nodeIndex] = nodeHierarchy;
            SetDirtyWithFlag(childNodeID, NodeStatusFlags.HierarchyModified);
            AddToParent(nodeID, childNodeID);
            SetDirtyWithFlag(nodeID);
            return true;
        }

        internal static bool	    InsertChildNode(CompactNodeID nodeID, Int32 index, CompactNodeID childNodeID)
        {
            if (nodeID == CompactNodeID.Invalid ||
                childNodeID == CompactNodeID.Invalid)
                return false;
            if (!AssertNodeIDValid(nodeID) ||
                !AssertNodeTypeHasChildren(nodeID) ||
                !AssertNodeIDValid(childNodeID) ||
                !AssertNodeTypeHasParent(childNodeID))
                return false;
            if (IsAncestor(nodeID, childNodeID))
                return false;

            var childNodeIndex      = nodeID.ID - 1;
            var childNode		    = nodeHierarchies[childNodeIndex];
            var oldParentNodeID     = childNode.parentNodeID;
            var oldTreeNodeID	    = childNode.treeNodeID;
            var oldTreeNodeIndex    = oldTreeNodeID.ID - 1;
            var oldParentNodeIndex  = oldParentNodeID.ID - 1;



            var nodeIndex       = nodeID.ID - 1;
            var nodeIsTree		= statusFlags[nodeIndex].nodeType == CSGNodeType.Tree;
            var nodeHierarchy	= nodeHierarchies[nodeIndex];
            var newParentNodeID = nodeIsTree ? CompactNodeID.Invalid : nodeID;
            var newTreeNodeID	= nodeIsTree ? nodeID : nodeHierarchy.treeNodeID;
            
            if (oldParentNodeID == newParentNodeID &&
                oldTreeNodeID == newTreeNodeID)
                return false;
            
            if (oldParentNodeID != newParentNodeID &&
                IsValidNodeID(oldParentNodeID))
            {
                var parentNodeHierarchy = nodeHierarchies[oldParentNodeIndex];
                parentNodeHierarchy.RemoveChild(childNodeID);
                nodeHierarchies[oldParentNodeIndex] = parentNodeHierarchy;
                SetDirtyWithFlag(oldParentNodeID);
            }
            
            if (IsValidNodeID(oldTreeNodeID))
            {
                if (!IsValidNodeID(oldParentNodeID))
                {
                    var treeNodeHierarchy = nodeHierarchies[oldTreeNodeIndex];
                    treeNodeHierarchy.RemoveChild(childNodeID);
                    nodeHierarchies[oldTreeNodeIndex] = treeNodeHierarchy;
                }
                SetTreeDirtyWithFlag(oldTreeNodeID);
            }

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

        internal static bool        InsertChildNodeRange(CompactNodeID nodeID, Int32 index, CSGTreeNode[] srcChildren)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID) ||
                srcChildren == null)
                return false;
            var nodeIndex = nodeID.ID - 1;
            var dstChildren = nodeHierarchies[nodeIndex].children;
            if (dstChildren == null)
            {
                if (index > 0)
                    return false;
                var nodeHierarchy = nodeHierarchies[nodeIndex];
                nodeHierarchy.children = new List<CompactNodeID>();
                nodeHierarchies[nodeIndex] = nodeHierarchy;
            } else
            if (index < 0 || index > dstChildren.Count)
                return false;

            for (var i = srcChildren.Length - 1; i >= 0; i--)
            {
                if (nodeID == srcChildren[i].NodeID)
                    return false;
                if (!IsValidNodeID(srcChildren[i].nodeID))
                    return false;
                if (Array.IndexOf(srcChildren, new CSGTreeNode { nodeID = srcChildren[i].nodeID }) != -1)
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

            for (var i = srcChildren.Length - 1; i >= 0; i--)
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

        static readonly HashSet<CompactNodeID> s_FoundNodes = new HashSet<CompactNodeID>();

        internal static bool        SetChildNodes(CompactNodeID nodeID, CSGTreeNode[] children)
        {
            if (!AssertNodeIDValid(nodeID) || !AssertNodeTypeHasChildren(nodeID)) return false;
            if (children == null)
                return false;
            if (nodeID == CompactNodeID.Invalid)
                throw new ArgumentException("nodeID equals " + CSGTreeNode.InvalidNode);

            var foundNodes = new HashSet<CompactNodeID>();
            for (var i = 0; i < children.Length; i++)
            {
                if (nodeID == children[i].NodeID)
                    return false;
                if (!foundNodes.Add(children[i].NodeID))
                {
                    Debug.LogError("Have duplicate child");
                    return false;
                }
            }
            
            for (var i = 0; i < children.Length; i++)
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

        internal static void NotifyBrushMeshRemoved(int brushMeshID)
        {
            // TODO: have some way to lookup this directly instead of going through list
            for (var i = 0; i < nodeHierarchies.Count; i++)
            {
                var brushInfo = brushOutlineStates[i];
                if (brushInfo == null)
                    continue;

                var nodeHierarchy   = nodeHierarchies[i];
                var treeNodeID      = nodeHierarchy.treeNodeID;
                if (treeNodeID == CompactNodeID.Invalid)
                    continue;

                if (brushInfo.brushMeshInstanceID != brushMeshID)
                    continue;

                if (IsValidNodeID(treeNodeID))
                    SetBrushMeshID(treeNodeID, BrushMeshInstance.InvalidInstance.BrushMeshID);
            }
        }
    }
}