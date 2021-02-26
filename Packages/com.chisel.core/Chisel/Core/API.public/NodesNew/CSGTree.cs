using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core.New
{
    /// <summary>The root of a CSG tree which is used to generate meshes with. All other types of nodes are children of this node.</summary>
    /// <remarks><note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTree"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) and [Create Unity Meshes](~/documentation/createUnityMesh.md) documentation for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential), BurstCompatible, Serializable]
    public partial struct CSGTree
    {
        #region Create
        /// <summary>Generates a tree returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular tree. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html).</param>
        /// <param name="childrenArray">A pointer to an array of child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <param name="childrenArrayLength">The length of the array of child nodes that are children of this tree.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static unsafe CSGTree Create(Int32 userID, CSGTreeNode* childrenArray, int childrenArrayLength)
        {
            if (!CompactHierarchyManager.GenerateTree(userID, out var treeNodeID))
                return new CSGTree() { treeNodeID = CompactNodeID.Invalid };
            if (childrenArray != null && childrenArrayLength > 0)
            {
                if (!CompactHierarchyManager.SetChildNodes(treeNodeID, childrenArray, childrenArrayLength))
                {
                    CompactHierarchyManager.DestroyNode(treeNodeID);
                    return new CSGTree() { treeNodeID = CompactNodeID.Invalid };
                }
            }
            return new CSGTree() { treeNodeID = treeNodeID };
        }

        /// <summary>Generates a tree returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular tree. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html).</param>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard]
        public static unsafe CSGTree Create(Int32 userID, params CSGTreeNode[] children)
        {
            if (children == null || children.Length == 0)
                return Create(userID, null, 0);
            
            var length = children.Length;
            var arrayPtr = (CSGTreeNode*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.PinGCArrayAndGetDataAddress(children, out var handle);
            try
            {
                return Create(userID, arrayPtr, length);
            }
            finally { Unity.Collections.LowLevel.Unsafe.UnsafeUtility.ReleaseGCObject(handle); }
        }

        /// <summary>Generates a tree and returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard]
        public static CSGTree Create(params CSGTreeNode[] children) { return Create(0, children); }
        #endregion


        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTree"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return treeNodeID != CompactNodeID.Invalid && CompactHierarchyManager.IsValidNodeID(treeNodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.NodeID"/> of the <see cref="Chisel.Core.CSGTree"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public CompactNodeID	NodeID			{ get { return treeNodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.UserID"/> set to the <see cref="Chisel.Core.CSGTree"/> at creation time.</value>
        public Int32			UserID			{ get { return CompactHierarchyManager.GetUserIDOfNode(treeNodeID); } }
        
        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTree"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(treeNodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(treeNodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevTreeNodeID = treeNodeID; treeNodeID = CompactNodeID.Invalid; return CompactHierarchyManager.DestroyNode(prevTreeNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ treeNodeID = CompactNodeID.Invalid; }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTree"/>.</value>
        public Int32			Count								{ get { return CompactHierarchyManager.GetChildNodeCount(treeNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]						{ get { return new CSGTreeNode { nodeID = CompactHierarchyManager.GetChildNodeAtIndex(treeNodeID, index) }; } }

        
        /// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Add			(CSGTreeNode item)					{ return CompactHierarchyManager.AddChildNode(treeNodeID, item.nodeID); }


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public unsafe bool AddRange(CSGTreeNode* arrayPtr, int length)
        {
            if (arrayPtr == null)
                throw new ArgumentNullException(nameof(arrayPtr));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(treeNodeID, Count, arrayPtr, length);
        }

        /// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Insert		(int index, CSGTreeNode item)		{ return CompactHierarchyManager.InsertChildNode(treeNodeID, index, item.nodeID); }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTree"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. </param>
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
            return CompactHierarchyManager.InsertChildNodeRange(treeNodeID, index, arrayPtr, length); 
        }

        /// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Remove		(CSGTreeNode item)					{ return CompactHierarchyManager.RemoveChildNode(treeNodeID, item.nodeID); }

        /// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based index of the child to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveAt	(int index)							{ return CompactHierarchyManager.RemoveChildNodeAt(treeNodeID, index); }

        /// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based starting index of the range of children to remove.</param>
        /// <param name="count">The number of children to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveRange	(int index, int count)				{ return CompactHierarchyManager.RemoveChildNodeRange(treeNodeID, index, count); }

        /// <summary>Removes all children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        public void Clear		()									{ CompactHierarchyManager.ClearChildNodes(treeNodeID); }
        
        
        /// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, –1.</returns>
        public int  IndexOf		(CSGTreeNode item)					{ return CompactHierarchyManager.IndexOfChildNode(treeNodeID, item.nodeID); }

        /// <summary>Determines whether the <see cref="Chisel.Core.CSGTree"/> contains a specific value.</summary>
        /// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, <b>false</b>.</returns>
        public bool Contains	(CSGTreeNode item)					{ return CompactHierarchyManager.IndexOfChildNode(treeNodeID, item.nodeID) != -1; }
        #endregion

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
}