using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;

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
    [StructLayout(LayoutKind.Sequential), BurstCompatible, Serializable]
    [System.Diagnostics.DebuggerDisplay("Branch ({branchNodeID})")]
    public partial struct CSGTreeBranch
    {
        #region Create
        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="childrenArray">A pointer to an array of child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <param name="childrenArray">The length of an array of child nodes that are children of this branch. </param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static unsafe CSGTreeBranch CreateUnsafe(Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive, CSGTreeNode* childrenArray = null, int childrenArrayLength = 0)
        {
            var branchNodeID = CompactHierarchyManager.CreateBranch(operation, userID);
            Debug.Assert(CompactHierarchyManager.IsValidNodeID(branchNodeID));
            if (childrenArray != null && childrenArrayLength > 0)
            {
                if (!CompactHierarchyManager.SetChildNodes(branchNodeID, childrenArray, childrenArrayLength))
                {
                    CompactHierarchyManager.DestroyNode(branchNodeID);
                    return new CSGTreeBranch() { branchNodeID = NodeID.Invalid };
                }
            }
            CompactHierarchyManager.SetDirty(branchNodeID);
            return new CSGTreeBranch() { branchNodeID = branchNodeID };
        }

        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard]
        public static unsafe CSGTreeBranch Create(Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive, params CSGTreeNode[] children)
        {
            if (children == null || children.Length == 0)
                return CreateUnsafe(userID, operation, null, 0);

            var length = children.Length;
            var arrayPtr = (CSGTreeNode*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.PinGCArrayAndGetDataAddress(children, out var handle);
            try
            {
                return CreateUnsafe(userID, operation, arrayPtr, length);
            }
            finally { Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ReleaseGCObject(handle); }
        }

        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular branch. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard]
        public static CSGTreeBranch Create(Int32 userID, params CSGTreeNode[] children) { return Create(userID: userID, CSGOperationType.Additive, children); }

        /// <summary>Generates a branch and returns a <see cref="Chisel.Core.CSGTreeBranch"/> struct that contains a reference to it.</summary>
        /// <param name="children">The child nodes that are children of this branch. A branch may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBranch"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard]
        public static CSGTreeBranch Create(params CSGTreeNode[] children) { return Create(0, children); }
        #endregion


        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeBranch"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return branchNodeID != NodeID.Invalid && CompactHierarchyManager.IsValidNodeID(branchNodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.NodeID"/> of the <see cref="Chisel.Core.CSGTreeBranch"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public NodeID           NodeID			{ get { return branchNodeID; } }
        
        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBranch.UserID"/> set to the <see cref="Chisel.Core.CSGTreeBranch"/> at creation time.</value>
        public Int32			UserID			{ get { return CompactHierarchyManager.GetUserIDOfNode(branchNodeID); } }
        
        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeBranch"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(branchNodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(branchNodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevBranchNodeID = branchNodeID; branchNodeID = NodeID.Invalid; return CompactHierarchyManager.DestroyNode(prevBranchNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ branchNodeID = NodeID.Invalid; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeBranch"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent			{ get { return new CSGTreeBranch { branchNodeID = CompactHierarchyManager.GetParentOfNode(branchNodeID) }; } }

        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeBranch"/> belongs to.</value>
        public CSGTree			Tree			{ get { return new CSGTree       { treeNodeID   = CompactHierarchyManager.GetRootOfNode(branchNodeID) }; } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeBranch"/> will use.</value>
        public CSGOperationType Operation		{ get { return (CSGOperationType)CompactHierarchyManager.GetNodeOperationType(branchNodeID); } set { CompactHierarchyManager.SetNodeOperationType(branchNodeID, value); } }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public Int32			Count			{ get { return CompactHierarchyManager.GetChildNodeCount(branchNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]	{ get { return new CSGTreeNode { nodeID = CompactHierarchyManager.GetChildNodeAtIndex(branchNodeID, index) }; } }


        /// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Add			(CSGTreeNode item)				        { return CompactHierarchyManager.AddChildNode(branchNodeID, item.nodeID); }

        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTreeBranch"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public unsafe bool AddRange	(CSGTreeNode* arrayPtr, int length) 
        { 
            if (arrayPtr == null) 
                throw new ArgumentNullException(nameof(arrayPtr));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(branchNodeID, Count, arrayPtr, length); 
        }

        /// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Insert		(int index, CSGTreeNode item)	        { return CompactHierarchyManager.InsertChildNode(branchNodeID, index, item.nodeID); }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTreeBranch"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTreeBranch"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public unsafe bool InsertRange(int index, CSGTreeNode* arrayPtr, int length)
        {
            if (arrayPtr == null)
                throw new ArgumentNullException(nameof(arrayPtr));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(branchNodeID, index, arrayPtr, length);
        }

        /// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Remove		(CSGTreeNode item)				{ return CompactHierarchyManager.RemoveChildNode(branchNodeID, item.nodeID); }

        /// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="index">The zero-based index of the child to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveAt	(int index)						{ return CompactHierarchyManager.RemoveChildNodeAt(branchNodeID, index); }

        /// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="index">The zero-based starting index of the range of children to remove.</param>
        /// <param name="count">The number of children to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveRange	(int index, int count)			{ return CompactHierarchyManager.RemoveChildNodeRange(branchNodeID, index, count); }

        /// <summary>Removes all children from the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        public void Clear		()								{ CompactHierarchyManager.ClearChildNodes(branchNodeID); }

        /// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTreeBranch"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, –1.</returns>
        public int  IndexOf		(CSGTreeNode item)				{ return CompactHierarchyManager.SiblingIndexOf(branchNodeID, item.nodeID); }

        /// <summary>Determines whether the <see cref="Chisel.Core.CSGTreeBranch"/> contains a specific value.</summary>
        /// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTreeBranch"/>.</param>
        /// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTreeBranch"/>; otherwise, <b>false</b>.</returns>
        public bool Contains	(CSGTreeNode item)				{ return CompactHierarchyManager.SiblingIndexOf(branchNodeID, item.nodeID) != -1; }
        #endregion
        
#if UNITY_EDITOR
        #region Inspector State

        public bool Visible         { get { return CompactHierarchyManager.IsBrushVisible(branchNodeID); } set { CompactHierarchyManager.SetVisibility(branchNodeID, value); } }
        public bool PickingEnabled  { get { return CompactHierarchyManager.IsBrushPickingEnabled(branchNodeID); } set { CompactHierarchyManager.SetPickingEnabled(branchNodeID, value); } }
        public bool IsSelectable    { get { return CompactHierarchyManager.IsBrushSelectable(branchNodeID); } }

        #endregion
#endif

        #region Transformation
        // TODO: add description
        public float4x4			    LocalTransformation		{ get { return CompactHierarchyManager.GetNodeLocalTransformation(branchNodeID); } set { CompactHierarchyManager.SetNodeLocalTransformation(branchNodeID, in value); } }		
        // TODO: add description
        //public float4x4           TreeToNodeSpaceMatrix   { get { return CompactHierarchyManager.GetTreeToNodeSpaceMatrix(branchNodeID, out var result) ? result : float4x4.identity; } }
        // TODO: add description
        //public float4x4           NodeToTreeSpaceMatrix	{ get { return CompactHierarchyManager.GetNodeToTreeSpaceMatrix(branchNodeID, out var result) ? result : float4x4.identity; } }
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


        // Temporary workaround until we can switch to hashes
        internal bool IsAnyStatusFlagSet()                  { return CompactHierarchyManager.IsAnyStatusFlagSet(branchNodeID); }
        internal bool IsStatusFlagSet(NodeStatusFlags flag) { return CompactHierarchyManager.IsStatusFlagSet(branchNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { CompactHierarchyManager.SetStatusFlag(branchNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { CompactHierarchyManager.ClearStatusFlag(branchNodeID, flag); }
        internal void ClearAllStatusFlags()                 { CompactHierarchyManager.ClearAllStatusFlags(branchNodeID); }
        

        [SerializeField] // Useful to be able to handle selection in history
        internal NodeID branchNodeID;
    }
}