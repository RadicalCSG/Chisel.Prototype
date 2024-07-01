using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    /// <summary>The root of a CSG tree which is used to generate meshes with. All other types of nodes are children of this node.</summary>
    /// <remarks><note>The internal ID contained in this struct is generated at runtime and is not persistent.</note>
    /// <note>This struct can be converted into a <see cref="Chisel.Core.CSGTreeNode"/> and back again.</note>
    /// <note>Be careful when keeping track of <see cref="Chisel.Core.CSGTree"/>s because <see cref="Chisel.Core.BrushMeshInstance.BrushMeshID"/>s can be recycled after being Destroyed.</note>
    /// See the [CSG Trees](~/documentation/CSGTrees.md) and [Create Unity Meshes](~/documentation/createUnityMesh.md) documentation for more information.</remarks>
    /// <seealso cref="Chisel.Core.CSGTreeNode"/>
    /// <seealso cref="Chisel.Core.CSGTreeBranch"/>
    /// <seealso cref="Chisel.Core.CSGTreeBrush"/>
    [StructLayout(LayoutKind.Sequential), GenerateTestsForBurstCompatibility, Serializable]
    [System.Diagnostics.DebuggerDisplay("Tree ({nodeID})")]
    public struct CSGTree : IEquatable<CSGTree>, IComparable<CSGTree>
    {
        #region Create
        /// <summary>Generates a tree returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular tree. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html).</param>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTree Create(Int32 userID, params CSGTreeNode[] children)
        {
            var treeNodeID = CompactHierarchyManager.CreateTree(userID);
            Debug.Assert(CompactHierarchyManager.IsValidNodeID(treeNodeID));
            if (children != null && children.Length > 0)
            {
                using (var childrenNativeArray = children.ToNativeArray(Allocator.Temp))
                {
                    if (!CompactHierarchyManager.SetChildNodes(treeNodeID, childrenNativeArray))
                    {
                        CompactHierarchyManager.DestroyNode(treeNodeID);
                        return CSGTree.Invalid;
                    }
                }
            }
            CompactHierarchyManager.SetDirty(treeNodeID);
            return CSGTree.Find(treeNodeID);
        }

        /// <summary>Generates a tree and returns a <see cref="Chisel.Core.CSGTree"/> struct that contains a reference to it.</summary>
        /// <param name="children">The child nodes that are children of this tree. A tree may not have duplicate children, contain itself or contain a <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTree"/>. May be an invalid node if it failed to create it.</returns>
        [BurstDiscard, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTree Create(params CSGTreeNode[] children) { return Create(0, children); }
        #endregion

        public CSGTreeBrush CreateBrush(Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return CSGTreeBrush.Create(userID, brushMesh, operation);
        }

        public CSGTreeBranch CreateBranch(Int32 userID = 0, CSGOperationType operation = CSGOperationType.Additive)
        {
            return CSGTreeBranch.Create(userID, operation);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTree Find(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return CSGTree.Invalid;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(nodeID);
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTree.Invalid;
            //var compactHierarchyID = CompactHierarchyManager.GetHierarchyID(nodeID);
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTree Find(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTree.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeID(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTree.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTree FindNoErrors(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTree.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeIDNoErrors(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTree.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTree Encapsulate(NodeID nodeID)
        {
            return new CSGTree { nodeID = nodeID };
        }

        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTree"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return nodeID != NodeID.Invalid && CompactHierarchyManager.IsValidNodeID(nodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.NodeID"/> of the <see cref="Chisel.Core.CSGTree"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public NodeID           NodeID			{ get { return nodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTree.UserID"/> set to the <see cref="Chisel.Core.CSGTree"/> at creation time.</value>
        public Int32			UserID			{ get { return CompactHierarchyManager.GetUserIDOfNode(nodeID); } }
        
        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTree"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(nodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeNode"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(nodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeNode"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevTreeNodeID = nodeID; this = CSGTree.Invalid; return CompactHierarchyManager.DestroyNode(prevTreeNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ this = CSGTree.Invalid; }
        #endregion

        #region ChildNodeContainer
        /// <value>Gets the number of elements contained in the <see cref="Chisel.Core.CSGTree"/>.</value>
        public Int32			Count				{ get { return Hierarchy.ChildCount(CompactNodeID); } }

        /// <summary>Gets child at the specified index.</summary>
        /// <param name="index">The zero-based index of the child to get.</param>
        /// <returns>The element at the specified index.</returns>
        public CSGTreeNode		this[int index]		{ get { return Find(Hierarchy.GetChildCompactNodeIDAt(CompactNodeID, index)); } }

        
        /// <summary>Adds a <see cref="Chisel.Core.CSGTreeNode"/> to the end of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to be added to the end of the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Add			(CSGTreeNode item)	{ return CompactHierarchyManager.AddChildNode(nodeID, item.nodeID); }


        /// <summary>Adds the <see cref="Chisel.Core.CSGTreeNode"/>s of the specified array to the end of the  <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be added to the end of the <see cref="Chisel.Core.CSGTree"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool AddRange(NativeArray<CSGTreeNode> array)
        {
            if (array.Length == 0)
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(nodeID, Count, array);
        }

        /// <summary>Inserts an element into the <see cref="Chisel.Core.CSGTreeNode"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to insert.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Insert		(int index, CSGTreeNode item)		{ return CompactHierarchyManager.InsertChildNode(nodeID, index, item.nodeID); }

        /// <summary>Inserts an array of <see cref="Chisel.Core.CSGTreeNode"/>s into the <see cref="Chisel.Core.CSGTree"/> at the specified index.</summary>
        /// <param name="index">The zero-based index at which the new <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted.</param>
        /// <param name="arrayPtr">The pointer to the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. The array itself cannot be null.</param>
        /// <param name="length">The length of the array whose <see cref="Chisel.Core.CSGTreeNode"/>s should be inserted into the <see cref="Chisel.Core.CSGTree"/>. </param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool InsertRange(int index, NativeArray<CSGTreeNode> array) 
        { 
            if (array.Length == 0) 
                return true;
            return CompactHierarchyManager.InsertChildNodeRange(nodeID, index, array); 
        }

        /// <summary>Removes a specific <see cref="Chisel.Core.CSGTreeNode"/> from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to remove from the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Remove		(CSGTreeNode item)					{ return CompactHierarchyManager.RemoveChildNode(nodeID, item.nodeID); }

        /// <summary>Removes the child at the specified index of the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based index of the child to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveAt	(int index)							{ return CompactHierarchyManager.RemoveChildNodeAt(nodeID, index); }

        /// <summary>Removes a range of children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="index">The zero-based starting index of the range of children to remove.</param>
        /// <param name="count">The number of children to remove.</param>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool RemoveRange	(int index, int count)				{ return CompactHierarchyManager.RemoveChildNodeRange(nodeID, index, count); }

        /// <summary>Removes all children from the <see cref="Chisel.Core.CSGTree"/>.</summary>
        public void Clear		()									{ CompactHierarchyManager.ClearChildNodes(nodeID); }
        
        
        /// <summary>Determines the index of a specific child in the <see cref="Chisel.Core.CSGTree"/>.</summary>
        /// <param name="item">The <see cref="Chisel.Core.CSGTreeNode"/> to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns>The index of <paramref name="item"/> if found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, –1.</returns>
        public int  IndexOf		(CSGTreeNode item)					{ return Hierarchy.SiblingIndexOf(CompactNodeID, item.CompactNodeID); }

        /// <summary>Determines whether the <see cref="Chisel.Core.CSGTree"/> contains a specific value.</summary>
        /// <param name="item">The Object to locate in the <see cref="Chisel.Core.CSGTree"/>.</param>
        /// <returns><b>true</b> if item is found in the <see cref="Chisel.Core.CSGTree"/>; otherwise, <b>false</b>.</returns>
        public bool Contains	(CSGTreeNode item)					{ return IndexOf(item) != -1; }
        #endregion

        #region Comparison
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator == (CSGTree left, CSGTree right) { return left.nodeID == right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (CSGTree left, CSGTree right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTree left, CSGTreeNode right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTree left, CSGTreeNode right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTree right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTree right) { return left.nodeID != right.nodeID; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is CSGTree) return this == ((CSGTreeNode)obj);
            if (obj is CSGTreeNode) return this == ((CSGTreeNode)obj);
			return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CSGTree other) { return this == other; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return nodeID.GetHashCode(); }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int CompareTo(CSGTree other) { return nodeID.CompareTo(other.nodeID); }
        #endregion

        /// <value>An invalid node</value>
        public static readonly CSGTree Invalid = new CSGTree { nodeID = NodeID.Invalid };

        // Temporary workaround until we can switch to hashes
        internal bool IsAnyStatusFlagSet()                  { return Hierarchy.IsAnyStatusFlagSet(CompactNodeID); }
        internal bool IsStatusFlagSet(NodeStatusFlags flag) { return Hierarchy.IsStatusFlagSet(CompactNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { Hierarchy.SetStatusFlag(CompactNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { Hierarchy.ClearStatusFlag(CompactNodeID, flag); }
        internal void ClearAllStatusFlags()                 { Hierarchy.ClearAllStatusFlags(CompactNodeID); }

        [SerializeField] internal NodeID nodeID;


        internal CompactNodeID      CompactNodeID       { get { return CompactHierarchyManager.GetCompactNodeID(nodeID); } }
        internal CompactHierarchyID CompactHierarchyID  { get { return CompactNodeID.hierarchyID; } }
        ref CompactHierarchy    Hierarchy         
        { 
            get 
            {
                var hierarchyID = CompactHierarchyID;
                if (hierarchyID == CompactHierarchyID.Invalid)
                    throw new InvalidOperationException($"Invalid NodeID");
                return ref CompactHierarchyManager.GetHierarchy(hierarchyID); 
            }
        }

        public override string ToString() => $"{((CSGTreeNode)this).Type} ({nodeID})";
    }
}