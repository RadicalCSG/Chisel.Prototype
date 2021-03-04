using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
/*
namespace Chisel.Core.Old
{
    /// <summary>The root of a CSG tree which is used to generate meshes with. All other types of nodes are children of this node.</summary>
    /// <remarks><note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTree"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) and [Create Unity Meshes](~/documentation/createUnityMesh.md) documentation for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential)]	
    public partial struct CSGTree
    {
        #region Create
        /// <summary>Generates a tree returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular tree. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html).</param>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTree Create(Int32 userID, params CSGTreeNode[] children)
        {
            if (!CSGManager.GenerateTree(userID, out var treeNodeID))
                return new CSGTree() { treeNodeID = CompactNodeID.Invalid };
            if (children != null && children.Length > 0)
            {
                if (!CSGManager.SetChildNodes(treeNodeID, children))
                {
                    CSGManager.DestroyNode(treeNodeID);
                    return new CSGTree() { treeNodeID = CompactNodeID.Invalid };
                }
            }
            return new CSGTree() { treeNodeID = treeNodeID };
        }

        /// <summary>Generates a tree and returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTree Create(params CSGTreeNode[] children) { return Create(0, children); }
        #endregion


        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTree"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return treeNodeID != CompactNodeID.Invalid && CSGManager.IsValidNodeID(treeNodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.NodeID"/> of the <see cref="Chisel.Core.CSGTree"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public CompactNodeID    NodeID          { get { return treeNodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.UserID"/> set to the <see cref="Chisel.Core.CSGTree"/> at creation time.</value>
        public Int32			UserID			{ get { return CSGManager.GetUserIDOfNode(treeNodeID); } }
        
        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTree"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CSGManager.IsNodeDirty(treeNodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        public void SetDirty	()				{ CSGManager.SetDirty(treeNodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevTreeNodeID = treeNodeID; treeNodeID = CompactNodeID.Invalid; return CSGManager.DestroyNode(prevTreeNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ treeNodeID = CompactNodeID.Invalid; }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTree"/>.</value>
        public Int32			Count								{ get { return CSGManager.GetChildNodeCount(treeNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]						{ get { return new CSGTreeNode { nodeID = CSGManager.GetChildNodeAtIndex(treeNodeID, index) }; } }

        
        /// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Add			(CSGTreeNode item)					{ return CSGManager.AddChildNode(treeNodeID, item.nodeID); }

        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool AddRange	(params CSGTreeNode[] array)		{ if (array == null) throw new ArgumentNullException("array"); return CSGManager.InsertChildNodeRange(treeNodeID, Count, array); }

        /// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Insert		(int index, CSGTreeNode item)		{ return CSGManager.InsertChildNode(treeNodeID, index, item.nodeID); }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTree"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="array">The array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool InsertRange	(int index, params CSGTreeNode[] array)	{ if (array == null) throw new ArgumentNullException("array"); return CSGManager.InsertChildNodeRange(treeNodeID, index, array); }

        /// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Remove		(CSGTreeNode item)					{ return CSGManager.RemoveChildNode(treeNodeID, item.nodeID); }

        /// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based index of the child to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveAt	(int index)							{ return CSGManager.RemoveChildNodeAt(treeNodeID, index); }

        /// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based starting index of the range of children to remove.</param>
        /// <param name="count">The number of children to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveRange	(int index, int count)				{ return CSGManager.RemoveChildNodeRange(treeNodeID, index, count); }

        /// <summary>Removes all children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        public void Clear		()									{ CSGManager.ClearChildNodes(treeNodeID); }
        
        
        /// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, –1.</returns>
        public int  IndexOf		(CSGTreeNode item)					{ return CSGManager.IndexOfChildNode(treeNodeID, item.nodeID); }

        /// <summary>Determines whether the <see cref="Chisel.Core.CSGTree"/> contains a specific value.</summary>
        /// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, <b>false</b>.</returns>
        public bool Contains	(CSGTreeNode item)					{ return CSGManager.IndexOfChildNode(treeNodeID, item.nodeID) != -1; }
        #endregion
        
        internal bool IsAnyStatusFlagSet()                  { return CSGManager.IsAnyStatusFlagSet(treeNodeID); }
        internal bool IsStatusFlagSet(NodeStatusFlags flag) { return CSGManager.IsStatusFlagSet(treeNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { CSGManager.SetStatusFlag(treeNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { CSGManager.ClearStatusFlag(treeNodeID, flag); }
        internal void ClearAllStatusFlags()                 { CSGManager.ClearAllStatusFlags(treeNodeID); }

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (CSGTree left, CSGTree right) { return left.treeNodeID == right.treeNodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (CSGTree left, CSGTree right) { return left.treeNodeID != right.treeNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTree left, CSGTreeNode right) { return left.treeNodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTree left, CSGTreeNode right) { return left.treeNodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTree right) { return left.nodeID == right.treeNodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTree right) { return left.nodeID != right.treeNodeID; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
		{
			if (obj is CSGTree) return treeNodeID == ((CSGTree)obj).treeNodeID;
			if (obj is CSGTreeNode) return treeNodeID == ((CSGTreeNode)obj).nodeID;
			return false;
		}
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return treeNodeID.GetHashCode(); }
        #endregion

        
        [SerializeField] // Useful to be able to handle selection in history
        internal CompactNodeID treeNodeID;
    }

}*/