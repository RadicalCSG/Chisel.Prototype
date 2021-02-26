using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;

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
    [StructLayout(LayoutKind.Sequential)]
    public partial struct CSGTreeNode 
    {
        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeNode"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is *false* that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return nodeID != CSGTreeNode.InvalidNodeID && CSGTreeNode.IsNodeIDValid(nodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeNode.NodeID"/> of the <see cref="Chisel.Core.CSGTreeNode"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public Int32			NodeID			{ get { return nodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeNode.UserID"/> set to the <see cref="Chisel.Core.CSGTreeNode"/> at creation time.</value>
        public Int32			UserID			{ get { return CSGTreeNode.GetUserIDOfNode(nodeID); } }

        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</value>
        public bool				Dirty			{ get { return CSGTreeNode.IsNodeDirty(nodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        public void SetDirty	()				{ CSGTreeNode.SetDirty(nodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool	Destroy		()				{ var prevNodeID = nodeID; nodeID = CSGTreeNode.InvalidNodeID; return CSGTreeNode.DestroyNode(prevNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ nodeID = CSGTreeNode.InvalidNodeID; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeNode"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent			{ get { return new CSGTreeBranch { branchNodeID = CSGTreeNode.GetParentOfNode(nodeID) }; } }

        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeNode"/> belongs to.</value>
        public CSGTree			Tree			{ get { return new CSGTree		 { treeNodeID   = CSGTreeNode.GetTreeOfNode(nodeID)  }; } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeNode"/> will use. Will not do anything if the <see cref="Chisel.Core.CSGTreeNode"/> is a <see cref="Chisel.Core.CSGTree"/>.</value>
        public CSGOperationType Operation		{ get { return (CSGOperationType)GetNodeOperationType(nodeID); } set { SetNodeOperationType(nodeID, value); } }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public Int32			Count			{ get { return CSGTreeNode.GetChildNodeCount(nodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]	{ get { return new CSGTreeNode { nodeID = CSGTreeNode.GetChildNodeAtIndex(nodeID, index) }; } }
        #endregion

        #region TreeNode specific
        /// <value>Gets the node-type of this <see cref="Chisel.Core.CSGTreeNode"/>.</value>
        public CSGNodeType		Type				  { get { return CSGTreeNode.GetTypeOfNode(nodeID); } }

        /// <summary>Creates a <see cref="Chisel.Core.CSGTreeNode"/> from a given nodeID.</summary>
        /// <param name="id">ID of the node to create a <see cref="Chisel.Core.CSGTreeNode"/> from</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> with the givenID. If the ID is not known, this will result in an invalid <see cref="Chisel.Core.CSGTreeNode"/>.</returns>
        public static CSGTreeNode Encapsulate(int id) { return new CSGTreeNode { nodeID = id }; }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTree"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="tree">The <see cref="Chisel.Core.CSGTree"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="tree"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTree       tree  ) { return new CSGTreeNode { nodeID = tree.treeNodeID     }; }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTreeBranch"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="branch">The <see cref="Chisel.Core.CSGTreeBranch"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="branch"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTreeBranch branch) { return new CSGTreeNode { nodeID = branch.branchNodeID }; }

        /// <summary>Operator to implicitly convert a <see cref="Chisel.Core.CSGTreeBrush"/> into a <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        /// <param name="brush">The <see cref="Chisel.Core.CSGTreeBrush"/> to convert into a <see cref="Chisel.Core.CSGTreeNode"/>.</param>
        /// <returns>A <see cref="Chisel.Core.CSGTreeNode"/> containing the same NodeID as <paramref name="brush"/></returns>
        /// <remarks>This can be used to build arrays of <see cref="Chisel.Core.CSGTreeNode"/>'s that contain a mix of type of nodes.</remarks>
        public static implicit operator CSGTreeNode   (CSGTreeBrush  brush ) { return new CSGTreeNode { nodeID = brush.brushNodeID   }; }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTree"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTree"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTree       (CSGTreeNode   node  ) { if (node.Type != CSGNodeType.Tree) return new CSGTree       { treeNodeID   = InvalidNodeID }; else return new CSGTree       { treeNodeID   = node.nodeID }; }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTreeBranch"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTreeBranch"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTreeBranch (CSGTreeNode   node  ) { if (node.Type != CSGNodeType.Branch) return new CSGTreeBranch { branchNodeID = InvalidNodeID }; else return new CSGTreeBranch { branchNodeID = node.nodeID }; }

        /// <summary>Operator to allow a <see cref="Chisel.Core.CSGTreeNode"/> to be explicitly converted into a <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        /// <param name="node">The <see cref="Chisel.Core.CSGTreeNode"/> to be convert into a <see cref="Chisel.Core.CSGTreeBrush"/></param>
        /// <returns>A valid <see cref="Chisel.Core.CSGTreeBrush"/> if <paramref name="node"/> actually was one, otherwise an invalid node.</returns>
        public static explicit operator CSGTreeBrush  (CSGTreeNode   node  ) { if (node.Type != CSGNodeType.Brush) return new CSGTreeBrush  { brushNodeID  = InvalidNodeID }; else return new CSGTreeBrush  { brushNodeID  = node.nodeID }; }
        
        /// <value>An invalid node</value>
        public static readonly CSGTreeNode InvalidNode = new CSGTreeNode { nodeID = CSGTreeNode.InvalidNodeID };
        internal const Int32 InvalidNodeID = 0;
        #endregion
          
#if UNITY_EDITOR
        #region Inspector State

        public bool Visible         { get { return CSGManager.IsBrushVisible(nodeID); } set { CSGManager.SetVisibility(nodeID, value); } }
        public bool PickingEnabled  { get { return CSGManager.IsBrushPickingEnabled(nodeID); } set { CSGManager.SetPickingEnabled(nodeID, value); } }
        public bool IsSelectable    { get { return CSGManager.IsBrushSelectable(nodeID); } }

        #endregion
#endif

        #region Transformation		
        // TODO: add description
        public Matrix4x4			LocalTransformation		{ get { return GetNodeLocalTransformation(nodeID); } set { SetNodeLocalTransformation(nodeID, ref value); } }		
        // TODO: add description
        //public Matrix4x4			TreeToNodeSpaceMatrix   { get { if (!CSGManager.GetTreeToNodeSpaceMatrix(nodeID, out Matrix4x4 result)) return Matrix4x4.identity; return result; } }
        // TODO: add description
        //public Matrix4x4			NodeToTreeSpaceMatrix	{ get { if (!CSGManager.GetNodeToTreeSpaceMatrix(nodeID, out Matrix4x4 result)) return Matrix4x4.identity; return result; } }
        #endregion

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator >(CSGTreeNode left, CSGTreeNode right) { return left.nodeID > right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator <(CSGTreeNode left, CSGTreeNode right) { return left.nodeID < right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator >=(CSGTreeNode left, CSGTreeNode right) { return left.nodeID >= right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator <=(CSGTreeNode left, CSGTreeNode right) { return left.nodeID <= right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTreeNode right) { return left.nodeID == right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTreeNode right) { return left.nodeID != right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is CSGTreeNode) return nodeID == ((CSGTreeNode)obj).nodeID;
            if (obj is CSGTreeBrush) return nodeID == ((CSGTreeBrush)obj).brushNodeID;
            if (obj is CSGTreeBranch) return nodeID == ((CSGTreeBranch)obj).branchNodeID;
            if (obj is CSGTree) return nodeID == ((CSGTree)obj).treeNodeID;
            return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return nodeID.GetHashCode(); }
        #endregion
        
        [SerializeField] // Useful to be able to handle selection in history
        internal Int32 nodeID;

        public override string ToString() { return $"{Type} ({nodeID})"; }
    }
}