using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;

namespace Chisel.Core
{
    /// <summary>A branch in a CSG tree, used to encapsulate other <see cref="Chisel.Core.CSGTreeBranch"/>es and <see cref="Chisel.Core.CSGTreeBrush"/>es and perform operations with them as a whole.</summary>
    /// <remarks>A branch can be used to combine multiple branches and/or brushes, each with different <see cref="Chisel.Core.CSGOperationType"/>s, 
    /// and perform a CSG operation with the shape that's defined by all those branches and brushes on other parts of the CSG tree.
    /// <note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTreeBranch"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) article for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTree"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]	
    public partial struct CSGTreeBranch
	{
		#region Create
		/// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
		/// <param name="userID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
		/// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
		/// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
		public static CSGTreeBranch Create(Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive, params CSGTreeNode[] children)
		{
			int branchNodeID;
			if (!GenerateBranch(userID, out branchNodeID))
				return new CSGTreeBranch() { branchNodeID = 0 };
			if (children != null && children.Length > 0)
			{
				if (operation != CSGOperationType.Additive) CSGTreeNode.SetNodeOperationType(userID, operation);
				if (!CSGTreeNode.SetChildNodes(branchNodeID, children))
				{
					CSGTreeNode.DestroyNode(branchNodeID);
					return new CSGTreeBranch() { branchNodeID = 0 };
				}
			}
			return new CSGTreeBranch() { branchNodeID = branchNodeID };
		}

		/// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
		/// <param name="userID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
		/// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
		/// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
		public static CSGTreeBranch Create(Int32 userID, params CSGTreeNode[] children) 
		{
			int branchNodeID;
			if (!GenerateBranch(userID, out branchNodeID))
				return new CSGTreeBranch() { branchNodeID = 0 };
			if (children != null && children.Length > 0)
			{
				if (!CSGTreeNode.SetChildNodes(branchNodeID, children))
				{
					CSGTreeNode.DestroyNode(branchNodeID);
					return new CSGTreeBranch() { branchNodeID = 0 };
				}
			}
			return new CSGTreeBranch() { branchNodeID = branchNodeID };
		}
		
		/// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
		/// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
		/// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
		public static CSGTreeBranch Create(params CSGTreeNode[] children) { return Create(0, children); }
		#endregion


		#region Node
		/// <value>Returns if the current <see cref="Chisel.Core.CSGTreeBranch"/> is valid or not.</value>
		/// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
		public bool				Valid			{ get { return branchNodeID != CSGTreeNode.InvalidNodeID && CSGTreeNode.IsNodeIDValid(branchNodeID); } }

		/// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.NodeID"/> of the <see cref="Chisel.Core.CSGTreeBranch"/>, which is a unique ID of this node.</value>
		/// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
		public Int32			NodeID			{ get { return branchNodeID; } }
		
		/// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.UserID"/> set to the <see cref="Chisel.Core.CSGTreeBranch"/> at creation time.</value>
		public Int32			UserID			{ get { return CSGTreeNode.GetUserIDOfNode(branchNodeID); } }
		
		/// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeBranch"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
		public bool				Dirty			{ get { return CSGTreeNode.IsNodeDirty(branchNodeID); } }

		/// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
		public void SetDirty	()				{ CSGTreeNode.SetDirty(branchNodeID); }

		/// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool Destroy		()				{ var prevBranchNodeID = branchNodeID; branchNodeID = CSGTreeNode.InvalidNodeID; return CSGTreeNode.DestroyNode(prevBranchNodeID); }
		
		/// <summary>Sets the state of this struct to invalid.</summary>
		public void SetInvalid	()				{ branchNodeID = CSGTreeNode.InvalidNodeID; }
		#endregion

		#region ChildNode
		/// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeBranch"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
		public CSGTreeBranch	Parent			{ get { return new CSGTreeBranch { branchNodeID = CSGTreeNode.GetParentOfNode(branchNodeID) }; } }

		/// <value>Returns tree this <see cref="Chisel.Core.CSGTreeBranch"/> belongs to.</value>
		public CSGTree			Tree			{ get { return new CSGTree       { treeNodeID   = CSGTreeNode.GetTreeOfNode(branchNodeID)  }; } }

		/// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeBranch"/> will use.</value>
		public CSGOperationType Operation		{ get { return (CSGOperationType)CSGTreeNode.GetNodeOperationType(branchNodeID); } set { CSGTreeNode.SetNodeOperationType(branchNodeID, value); } }
		#endregion

		#region ChildNodeContainer
		/// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
		public Int32			Count			{ get { return CSGTreeNode.GetChildNodeCount(branchNodeID); } }

		/// <summary>Gets child at the specified index.</summary>
		/// <param name="index">The zero-based index of the child to get.</param>
		/// <returns>The element at the specified index.</returns>
		public CSGTreeNode		this[int index]	{ get { return new CSGTreeNode { nodeID = CSGTreeNode.GetChildNodeAtIndex(branchNodeID, index) }; } }
		

		/// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool Add			(CSGTreeNode item)				{ return CSGTreeNode.AddChildNode(branchNodeID, item.nodeID); }
		
		/// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool AddRange	(CSGTreeNode[] array)			{ if (array == null) throw new ArgumentNullException("array"); return CSGTreeNode.InsertChildNodeRange(branchNodeID, Count, array); }

		/// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
		/// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool Insert		(int index, CSGTreeNode item)	{ return CSGTreeNode.InsertChildNode(branchNodeID, index, item.nodeID); }

		/// <summary>Inserts the <see cref="Chisel.Core.CSGTreeNode"/>s of an array into the <see cref="Chisel.Core.CSGTreeBranch"/> at the specified index.</summary>
		/// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
		/// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool InsertRange	(int index, CSGTreeNode[] array){ if (array == null) throw new ArgumentNullException("array"); return CSGTreeNode.InsertChildNodeRange(branchNodeID, index, array); }
		

		/// <summary>Sets all the children of this <see cref="Chisel.Core.CSGTreeBranch"/> to the give array of <see cref="Chisel.Core.CSGTreeNode"/>s at the specified index.</summary>
		/// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool SetChildren	(CSGTreeNode[] array)			{ if (array == null) throw new ArgumentNullException("array"); return CSGTreeNode.SetChildNodes(branchNodeID, array); }

		/// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool Remove		(CSGTreeNode item)				{ return CSGTreeNode.RemoveChildNode(branchNodeID, item.nodeID); }

		/// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="index">The zero-based index of the child to remove.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool RemoveAt	(int index)						{ return CSGTreeNode.RemoveChildNodeAt(branchNodeID, index); }
		
		/// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="index">The zero-based starting index of the range of children to remove.</param>
		/// <param name="count">The number of children to remove.</param>
		/// <returns><b>true</b> on success, <b>false</b> on failure</returns>
		public bool RemoveRange	(int index, int count)			{ return CSGTreeNode.RemoveChildNodeRange(branchNodeID, index, count); }

		/// <summary>Removes all children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		public void Clear		()								{ CSGTreeNode.ClearChildNodes(branchNodeID); }

		/// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
		/// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
		/// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, –1.</returns>
		public int  IndexOf		(CSGTreeNode item)				{ return CSGTreeNode.IndexOfChildNode(branchNodeID, item.nodeID); }
		
		/// <summary>Determines whether the <see cref="Chisel.Core.CSGTreeBranch"/> contains a specific value.</summary>
		/// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
		/// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, <b>false</b>.</returns>
		public bool Contains	(CSGTreeNode item)				{ return CSGTreeNode.IndexOfChildNode(branchNodeID, item.nodeID) != -1; }
		
		/// <summary>Copies the immediate children of the <see cref="Chisel.Core.CSGTreeBranch"/> to an Array, starting at a particular Array index.</summary>
		/// <param name="array">The one-dimensional Array that is the destination of the elements copied from <see cref="Chisel.Core.CSGTreeBranch"/>. The Array must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
		/// <returns>The number of children copied into <paramref name="array"/>.</returns>
		public int	CopyChildrenTo(CSGTreeNode[] array, int arrayIndex)	{ return CSGTreeNode.CopyTo(branchNodeID, array, arrayIndex); } 
		
		/// <summary>Copies the <see cref="Chisel.Core.CSGTreeNode"/>s of the <see cref="Chisel.Core.CSGTreeBranch"/> to a new array.</summary>
		/// <returns>An array containing the <see cref="Chisel.Core.CSGTreeNode"/>s of the <see cref="Chisel.Core.CSGTreeBranch"/>.</returns>
		public CSGTreeNode[] ChildrenToArray()					{ return CSGTreeNode.GetChildNodes(branchNodeID); }
		#endregion
		
		#region Transformation
		// TODO: add description
		public Matrix4x4			LocalTransformation		{ get { return CSGTreeNode.GetNodeLocalTransformation(branchNodeID); } set { CSGTreeNode.SetNodeLocalTransformation(branchNodeID, ref value); } }		
		// TODO: add description
		public Matrix4x4			TreeToNodeSpaceMatrix	{ get { return CSGTreeNode.GetTreeToNodeSpaceMatrix(branchNodeID); } }
		// TODO: add description
		public Matrix4x4			NodeToTreeSpaceMatrix	{ get { return CSGTreeNode.GetNodeToTreeSpaceMatrix(branchNodeID); } }
		#endregion
				
		#region Comparison
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator == (CSGTreeBranch left, CSGTreeBranch right) { return left.branchNodeID == right.branchNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator != (CSGTreeBranch left, CSGTreeBranch right) { return left.branchNodeID != right.branchNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator ==(CSGTreeBranch left, CSGTreeNode right) { return left.branchNodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator !=(CSGTreeBranch left, CSGTreeNode right) { return left.branchNodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator ==(CSGTreeNode left, CSGTreeBranch right) { return left.nodeID == right.branchNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool operator !=(CSGTreeNode left, CSGTreeBranch right) { return left.nodeID != right.branchNodeID; }

		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool Equals(object obj)
		{
			if (obj is CSGTreeBranch) return branchNodeID == ((CSGTreeBranch)obj).branchNodeID;
			if (obj is CSGTreeNode) return branchNodeID == ((CSGTreeNode)obj).nodeID;
			return false;
		}
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() { return branchNodeID.GetHashCode(); }
		#endregion
	
		[SerializeField] // Useful to be able to handle selection in history
		internal Int32 branchNodeID;
	}
}