using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    /// <summary>Enum which describes the type of a node</summary>
    public enum CSGNodeType : byte
    {
        /// <summary>
        /// Invalid or unknown node.
        /// </summary>
        None,
        /// <summary>
        /// Node is a <see cref="Chisel.Core.CSGTree"/>.
        /// </summary>
        Tree,
        /// <summary>
        /// Node is a <see cref="Chisel.Core.CSGTreeBranch"/>.
        /// </summary>
        Branch,
        /// <summary>
        /// Node is a <see cref="Chisel.Core.CSGTreeBrush"/>.
        /// </summary>
        Brush
    };

    /// <summary>Represents a generic node in a CSG tree. This is used to be able to store different types of nodes together.</summary>
    /// <remarks><note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeBrush"/>, <see cref="Chisel.Core.CSGTreeBranch"/> or 
    /// <see cref="Chisel.Core.CSGTree"/> depending on what kind of node is stored in the <see cref="Chisel.Core.CSGTreeNode"/>.
    /// The type of node can be queried by using <see cref="Chisel.Core.CSGTreeNode.Type"/>. 
    /// If a <see cref="Chisel.Core.CSGTreeNode"/> is cast to the wrong kind of node, an invalid node is generated.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTreeNode"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note></remarks>	
    /// <seealso cref="Chisel.Core.CSGTree"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential), BurstCompatible, Serializable]
    [System.Diagnostics.DebuggerDisplay("{Type} ({nodeID})")]
    public struct CSGTreeNode : IEquatable<CSGTreeNode>
    {        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeNode Find(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return CSGTreeNode.Invalid;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(nodeID);
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeNode.Invalid;
            var compactHierarchyID = CompactHierarchyManager.GetHierarchyID(nodeID);
            return Encapsulate(nodeID, compactNodeID, compactHierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeNode Find(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeNode.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeID(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeNode.Invalid;
            var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID, compactNodeID, compactHierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeNode FindNoErrors(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeNode.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeIDNoErrors(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeNode.Invalid;
            var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID, compactNodeID, compactHierarchyID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeNode Encapsulate(NodeID nodeID, CompactNodeID compactNodeID, CompactHierarchyID compactHierarchyID)
        {
            return new CSGTreeNode() { nodeID = nodeID, compactNodeID = compactNodeID, compactHierarchyID = compactHierarchyID };
        }


        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeNode"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is *false* that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return nodeID != NodeID.Invalid && compactNodeID != CompactNodeID.Invalid && compactHierarchyID != CompactHierarchyID.Invalid && CompactHierarchyManager.IsValidNodeID(nodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeNode.NodeID"/> of the <see cref="Chisel.Core.CSGTreeNode"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public NodeID           NodeID			{ get { return nodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeNode.UserID"/> set to the <see cref="Chisel.Core.CSGTreeNode"/> at creation time.</value>
        public Int32			UserID			{ get { return CompactHierarchyManager.GetUserIDOfNode(nodeID); } }

        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(nodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(nodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool	Destroy		()				{ var prevNodeID = nodeID; this = Invalid; return CompactHierarchyManager.DestroyNode(prevNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ this = Invalid; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeNode"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent			{ get { return CSGTreeBranch.Find(Hierarchy.ParentOf(CompactNodeID)); } }

        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeNode"/> belongs to.</value>
        public CSGTree			Tree			{ get { return CSGTree.Find(Hierarchy.GetRootOfNode(CompactNodeID)); } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeNode"/> will use. Will not do anything if the <see cref="Chisel.Core.CSGTreeNode"/> is a <see cref="Chisel.Core.CSGTree"/>.</value>
        public CSGOperationType Operation		{ get { return (CSGOperationType)CompactHierarchyManager.GetNodeOperationType(nodeID); } set { CompactHierarchyManager.SetNodeOperationType(nodeID, value); } }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public Int32			Count			    { get { return Hierarchy.ChildCount(CompactNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]	    { get { return Find(Hierarchy.GetChildIDAt(CompactNodeID, index)); } }
        #endregion

        #region TreeNode specific
        /// <value>Gets the node-type of this <see cref="Chisel.Core.CSGTreeNode"/>.</value>
        public CSGNodeType		Type			    { get { return CompactHierarchyManager.GetTypeOfNode(nodeID); } }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTree"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="tree">The <see cref="Chisel.Core.CSGTree"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="tree"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTree       tree  ) { return CSGTreeNode.Encapsulate(tree.nodeID, tree.compactNodeID, tree.compactHierarchyID); }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTreeBranch"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="branch">The <see cref="Chisel.Core.CSGTreeBranch"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="branch"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTreeBranch branch) { return CSGTreeNode.Encapsulate(branch.nodeID, branch.compactNodeID, branch.compactHierarchyID); }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTreeBrush"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="brush">The <see cref="Chisel.Core.CSGTreeBrush"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="brush"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTreeBrush  brush ) { return CSGTreeNode.Encapsulate(brush.nodeID, brush.compactNodeID, brush.compactHierarchyID); }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTree"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTree"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTree       (CSGTreeNode   node  ) { if (!node.Valid || node.Type != CSGNodeType.Tree) return CSGTree.Invalid; else return CSGTree.Encapsulate(node.nodeID, node.compactNodeID, node.compactHierarchyID); }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTreeBranch"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTreeBranch"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTreeBranch (CSGTreeNode   node  ) { if (!node.Valid || node.Type != CSGNodeType.Branch) return CSGTreeBranch.Invalid; else return CSGTreeBranch.Encapsulate(node.nodeID, node.compactNodeID, node.compactHierarchyID); }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTreeBrush"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTreeBrush"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTreeBrush  (CSGTreeNode   node  ) { if (!node.Valid || node.Type != CSGNodeType.Brush) return CSGTreeBrush.Invalid; else return CSGTreeBrush.Encapsulate(node.nodeID, node.compactNodeID, node.compactHierarchyID); }
        
        #endregion
        
        /// <value>An invalid node</value>
        public static readonly CSGTreeNode Invalid = new CSGTreeNode { nodeID = NodeID.Invalid, compactNodeID = CompactNodeID.Invalid, compactHierarchyID = CompactHierarchyID.Invalid };
        
#if UNITY_EDITOR
        #region Inspector State

        public bool Visible         { get { return CompactHierarchyManager.IsBrushVisible(nodeID); } set { CompactHierarchyManager.SetVisibility(nodeID, value); } }
        public bool PickingEnabled  { get { return CompactHierarchyManager.IsBrushPickingEnabled(nodeID); } set { CompactHierarchyManager.SetPickingEnabled(nodeID, value); } }
        public bool IsSelectable    { get { return CompactHierarchyManager.IsBrushSelectable(nodeID); } }

        #endregion
#endif

        #region Transformation
        // TODO: add description
        public float4x4			    LocalTransformation		{ get { return CompactHierarchyManager.GetNodeLocalTransformation(nodeID); } set { CompactHierarchyManager.SetNodeLocalTransformation(nodeID, in value); } }		
        // TODO: add description
        public float4x4             TreeToNodeSpaceMatrix   { get { return CompactHierarchyManager.GetTreeToNodeSpaceMatrix(nodeID, out var result) ? result : float4x4.identity; } }
        // TODO: add description
        public float4x4             NodeToTreeSpaceMatrix	{ get { return CompactHierarchyManager.GetNodeToTreeSpaceMatrix(nodeID, out var result) ? result : float4x4.identity; } }
        #endregion

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTreeNode right) { return //left.compactHierarchyID == right.compactHierarchyID && left.compactNodeID == right.compactNodeID && 
                                                                                     left.nodeID == right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTreeNode right) { return //left.compactHierarchyID != right.compactHierarchyID || left.compactNodeID != right.compactNodeID || 
                                                                                     left.nodeID != right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is CSGTreeNode) return this == ((CSGTreeNode)obj);
            if (obj is CSGTreeBrush) return this == ((CSGTreeNode)obj);
            if (obj is CSGTreeBranch) return this == ((CSGTreeNode)obj);
            if (obj is CSGTree) return this == ((CSGTreeNode)obj);
            return false;
        }
        public bool Equals(CSGTreeNode other) { return this == other; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return nodeID.GetHashCode(); }
        #endregion


        // Temporary workaround until we can switch to hashes
        internal bool IsAnyStatusFlagSet()                  { return Hierarchy.IsAnyStatusFlagSet(CompactNodeID); }
        internal bool IsStatusFlagSet(NodeStatusFlags flag) { return Hierarchy.IsStatusFlagSet(CompactNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { Hierarchy.SetStatusFlag(CompactNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { Hierarchy.ClearStatusFlag(CompactNodeID, flag); }
        internal void ClearAllStatusFlags()                 { Hierarchy.ClearAllStatusFlags(CompactNodeID); }

        [SerializeField] internal NodeID nodeID;
        [SerializeField] internal CompactNodeID compactNodeID;
        [SerializeField] internal CompactHierarchyID compactHierarchyID;


        internal CompactNodeID      CompactNodeID     { get { return CompactHierarchyManager.GetCompactNodeID(nodeID); } }
        internal CompactHierarchyID HierarchyID       { get { return CompactNodeID.hierarchyID; } }
        ref CompactHierarchy Hierarchy
        {
            get
            {
                var hierarchyID = HierarchyID;
                if (hierarchyID == CompactHierarchyID.Invalid)
                    throw new InvalidOperationException($"Invalid NodeID");
                return ref CompactHierarchyManager.GetHierarchy(hierarchyID);
            }
        }
    }
}