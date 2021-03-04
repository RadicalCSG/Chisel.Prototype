using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Bounds = UnityEngine.Bounds;
using UnityEngine;
using Unity.Burst;

namespace Chisel.Core
{
    /// <summary>A leaf node in a CSG tree that has a shape (a <see cref="Chisel.Core.BrushMesh"/>) with which CSG operations can be performed.</summary>
    /// <remarks><note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTreeBrush"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) and [Brush Meshes](~/documentation/brushMesh.md) articles for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTree"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    /// <seealso cref="Chisel.Core.BrushMesh"/>
    /// <seealso cref="Chisel.Core.BrushMeshInstance"/>
    [StructLayout(LayoutKind.Sequential)]	
    public partial struct CSGTreeBrush 
    {
        #region Create
        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular brush. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="localTransformation">The transformation of the brush relative to the tree root</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTreeBrush Create(Int32 userID, Matrix4x4 localTransformation, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            int brushNodeID;
            if (CSGManager.GenerateBrush(userID, out brushNodeID))
            { 
                if (localTransformation != default(Matrix4x4)) CSGTreeNode.SetNodeLocalTransformation(brushNodeID, ref localTransformation);
                if (operation != CSGOperationType.Additive) CSGTreeNode.SetNodeOperationType(brushNodeID, operation);
                if (brushMesh.Valid) CSGManager.SetBrushMeshID(brushNodeID, brushMesh.brushMeshID);
            } else
                brushNodeID = 0;
            return new CSGTreeBrush() { brushNodeID = brushNodeID };
        }

        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="localTransformation">The transformation of the brush relative to the tree root</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTreeBrush Create(Matrix4x4 localTransformation, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return Create(0, localTransformation, brushMesh, operation);
        }

        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular brush. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTreeBrush Create(Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return Create(userID, default(Matrix4x4), brushMesh, operation);
        }
        #endregion

        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeBrush"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return brushNodeID != CSGTreeNode.InvalidNodeID && CSGTreeNode.IsNodeIDValid(brushNodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBrush.NodeID"/> of the <see cref="Chisel.Core.CSGTreeBrush"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public Int32			NodeID			{ get { return brushNodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBrush.UserID"/> set to the <see cref="Chisel.Core.CSGTreeBrush"/> at creation time.</value>
        public Int32			UserID			{ get { return CSGTreeNode.GetUserIDOfNode(brushNodeID); } }

        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CSGTreeNode.IsNodeDirty(brushNodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        public void SetDirty	()				{ CSGTreeNode.SetDirty(brushNodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeBrush"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevBrushNodeID = brushNodeID; brushNodeID = CSGTreeNode.InvalidNodeID; return CSGTreeNode.DestroyNode(prevBrushNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ brushNodeID = CSGTreeNode.InvalidNodeID; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeBrush"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent				{ get { return new CSGTreeBranch { branchNodeID = CSGTreeNode.GetParentOfNode(brushNodeID) }; } }
        
        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeBrush"/> belongs to.</value>
        public CSGTree			Tree				{ get { return new CSGTree       { treeNodeID   = CSGTreeNode.GetTreeOfNode(brushNodeID) }; } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeBrush"/> will use.</value>
        public CSGOperationType Operation			{ get { return (CSGOperationType)CSGTreeNode.GetNodeOperationType(brushNodeID); } set { CSGTreeNode.SetNodeOperationType(brushNodeID, value); } }
        #endregion

        #region TreeBrush specific
        /// <value>Sets or gets a <see cref="Chisel.Core.BrushMeshInstance"/></value>
        /// <remarks>By modifying the <see cref="Chisel.Core.BrushMeshInstance"/> you can change the shape of the <see cref="Chisel.Core.CSGTreeBrush"/>
        /// <note><see cref="Chisel.Core.BrushMeshInstance"/>s can be shared between <see cref="Chisel.Core.CSGTreeBrush"/>es.</note></remarks>
        /// <seealso cref="Chisel.Core.BrushMesh" />
        public BrushMeshInstance    BrushMesh		{ set { CSGManager.SetBrushMeshID(brushNodeID, value.brushMeshID); } get { return new BrushMeshInstance { brushMeshID = CSGManager.GetBrushMeshID(brushNodeID) }; } }

        /// <value>Gets the bounds of this <see cref="Chisel.Core.CSGTreeBrush"/>.</value>
        public Bounds			    Bounds			{ get { return CSGManager.GetBrushBounds(brushNodeID); } }

        public ref BrushOutline     Outline         { get { return ref CSGManager.GetBrushOutline(this); } }
        #endregion
        
#if UNITY_EDITOR
        #region Inspector State

        public bool Visible         { get { return CSGManager.IsBrushVisible(brushNodeID); } set { CSGManager.SetVisibility(brushNodeID, value); } }
        public bool PickingEnabled  { get { return CSGManager.IsBrushPickingEnabled(brushNodeID); } set { CSGManager.SetPickingEnabled(brushNodeID, value); } }
        public bool IsSelectable    { get { return CSGManager.IsBrushSelectable(brushNodeID); } }

        #endregion
#endif

        #region Transformation
        // TODO: add description
		public Matrix4x4		LocalTransformation		{ get { return CSGTreeNode.GetNodeLocalTransformation(brushNodeID); } [BurstDiscard] set { CSGTreeNode.SetNodeLocalTransformation(brushNodeID, ref value); } }		
        // TODO: add description
		public Matrix4x4		TreeToNodeSpaceMatrix	{ get { if (!CSGManager.GetTreeToNodeSpaceMatrix(brushNodeID, out Matrix4x4 result)) return Matrix4x4.identity; return result; } }
        // TODO: add description
		public Matrix4x4		NodeToTreeSpaceMatrix	{ get { if (!CSGManager.GetNodeToTreeSpaceMatrix(brushNodeID, out Matrix4x4 result)) return Matrix4x4.identity; return result; } }
        #endregion
        
        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (CSGTreeBrush left, CSGTreeBrush right) { return left.brushNodeID == right.brushNodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (CSGTreeBrush left, CSGTreeBrush right) { return left.brushNodeID != right.brushNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeBrush left, CSGTreeNode right) { return left.brushNodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeBrush left, CSGTreeNode right) { return left.brushNodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTreeBrush right) { return left.nodeID == right.brushNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTreeBrush right) { return left.nodeID != right.brushNodeID; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
		{
			if (obj is CSGTreeBrush) return brushNodeID == ((CSGTreeBrush)obj).brushNodeID;
			if (obj is CSGTreeNode) return brushNodeID == ((CSGTreeNode)obj).nodeID;
			return false;
		}
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return brushNodeID.GetHashCode(); }
        #endregion

        [SerializeField] // Useful to be able to handle selection in history
        internal Int32 brushNodeID;

        public override string ToString() { return $"({NodeID})"; }

    }
}