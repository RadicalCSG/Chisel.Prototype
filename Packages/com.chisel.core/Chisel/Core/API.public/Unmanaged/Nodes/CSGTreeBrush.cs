using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

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
    [StructLayout(LayoutKind.Sequential), GenerateTestsForBurstCompatibility, Serializable]
    [System.Diagnostics.DebuggerDisplay("Brush ({nodeID})")]
    public struct CSGTreeBrush : IEquatable<CSGTreeBrush>
    {
        #region Create
        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular brush. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="localTransformation">The transformation of the brush relative to the tree root</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGTreeBrush Create(Int32 userID, float4x4 localTransformation, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            var brushNodeID = CompactHierarchyManager.CreateBrush(brushMesh, localTransformation, operation, userID);
            Debug.Assert(CompactHierarchyManager.IsValidNodeID(brushNodeID));
            CompactHierarchyManager.SetDirty(brushNodeID);
            return CSGTreeBrush.Find(brushNodeID);
        }
        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="localTransformation">The transformation of the brush relative to the tree root</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTreeBrush Create(Matrix4x4 localTransformation, Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return Create(userID, localTransformation, brushMesh, operation);
        }

        /// <summary>Generates a brush and returns a <see cref="Chisel.Core.CSGTreeBrush"/> struct that contains a reference to it.</summary>
        /// <param name="userID">A unique id to help identify this particular brush. For instance, this could be an InstanceID to a [UnityEngine.Object](https://docs.unity3d.com/ScriptReference/Object.html)</param>
        /// <param name="brushMesh">A <see cref="Chisel.Core.BrushMeshInstance"/>, which is a reference to a <see cref="Chisel.Core.BrushMesh"/>.</param>
        /// <param name="operation">The <see cref="Chisel.Core.CSGOperationType"/> that needs to be performed with this <see cref="Chisel.Core.CSGTreeBrush"/>.</param>
        /// <param name="flags"><see cref="Chisel.Core.CSGTreeBrush"/> specific flags</param>
        /// <returns>A new <see cref="Chisel.Core.CSGTreeBrush"/>. May be an invalid node if it failed to create it.</returns>
        public static CSGTreeBrush Create(Int32 userID = 0, BrushMeshInstance brushMesh = default(BrushMeshInstance), CSGOperationType operation = CSGOperationType.Additive)
        {
            return Create(userID, float4x4.identity, brushMesh, operation);
        }
        #endregion

        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBrush Find(NodeID nodeID)
        {
            if (nodeID == NodeID.Invalid)
                return CSGTreeBrush.Invalid;
            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(nodeID);
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBrush.Invalid;
            //var compactHierarchyID = CompactHierarchyManager.GetHierarchyID(nodeID);
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBrush Find(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBrush.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeID(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeBrush.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBrush FindNoErrors(CompactNodeID compactNodeID)
        {
            if (compactNodeID == CompactNodeID.Invalid)
                return CSGTreeBrush.Invalid;
            ref var hierarchy = ref CompactHierarchyManager.GetHierarchy(compactNodeID);
            var nodeID = hierarchy.GetNodeIDNoErrors(compactNodeID);
            if (nodeID == NodeID.Invalid)
                return CSGTreeBrush.Invalid;
            //var compactHierarchyID = hierarchy.HierarchyID;
            return Encapsulate(nodeID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CSGTreeBrush Encapsulate(NodeID nodeID)
        {
            return new CSGTreeBrush() { nodeID = nodeID };
        }


        #region Node
        /// <value>Returns if the current <see cref="Chisel.Core.CSGTreeBrush"/> is valid or not.</value>
        /// <remarks><note>If <paramref name="Valid"/> is <b>false</b> that could mean that this node has been destroyed.</note></remarks>
        public bool				Valid			{ get { return nodeID != NodeID.Invalid && CompactHierarchyManager.IsValidNodeID(nodeID); } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBrush.NodeID"/> of the <see cref="Chisel.Core.CSGTreeBrush"/>, which is a unique ID of this node.</value>
        /// <remarks><note>NodeIDs are eventually recycled, so be careful holding on to Nodes that have been destroyed.</note></remarks>
        public NodeID           NodeID			{ get { return nodeID; } }

        /// <value>Gets the <see cref="Chisel.Core.CSGTreeBrush.UserID"/> set to the <see cref="Chisel.Core.CSGTreeBrush"/> at creation time.</value>
        public Int32			UserID			{ get { return CompactHierarchyManager.GetUserIDOfNode(nodeID); } }

        /// <value>Returns the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>. When the it's dirty, then it means (some of) its generated meshes have been modified.</value>
        public bool				Dirty			{ get { return CompactHierarchyManager.IsNodeDirty(nodeID); } }

        /// <summary>Force set the dirty flag of the <see cref="Chisel.Core.CSGTreeBrush"/>.</summary>
        public void SetDirty	()				{ CompactHierarchyManager.SetDirty(nodeID); }

        /// <summary>Destroy this <see cref="Chisel.Core.CSGTreeBrush"/>. Sets the state to invalid.</summary>
        /// <returns><b>true</b> on success, <b>false</b> on failure</returns>
        public bool Destroy		()				{ var prevBrushNodeID = nodeID; this = CSGTreeBrush.Invalid; return CompactHierarchyManager.DestroyNode(prevBrushNodeID); }

        /// <summary>Sets the state of this struct to invalid.</summary>
        public void SetInvalid	()				{ this = CSGTreeBrush.Invalid; }
        #endregion

        #region ChildNode
        /// <value>Returns the parent <see cref="Chisel.Core.CSGTreeBranch"/> this <see cref="Chisel.Core.CSGTreeBrush"/> is a child of. Returns an invalid node if it's not a child of any <see cref="Chisel.Core.CSGTreeBranch"/>.</value>
        public CSGTreeBranch	Parent			{ get { return CSGTreeBranch.Find(Hierarchy.ParentOf(CompactNodeID)); } }
        
        /// <value>Returns tree this <see cref="Chisel.Core.CSGTreeBrush"/> belongs to.</value>
        public CSGTree			Tree			{ get { return CSGTree.Find(Hierarchy.GetRootOfNode(CompactNodeID)); } }

        /// <value>The CSG operation that this <see cref="Chisel.Core.CSGTreeBrush"/> will use.</value>
        public CSGOperationType Operation		{ get { return (CSGOperationType)CompactHierarchyManager.GetNodeOperationType(nodeID); } set { CompactHierarchyManager.SetNodeOperationType(nodeID, value); } }
        #endregion

        #region TreeBrush specific
        /// <value>Sets or gets a <see cref="Chisel.Core.BrushMeshInstance"/></value>
        /// <remarks>By modifying the <see cref="Chisel.Core.BrushMeshInstance"/> you can change the shape of the <see cref="Chisel.Core.CSGTreeBrush"/>
        /// <note><see cref="Chisel.Core.BrushMeshInstance"/>s can be shared between <see cref="Chisel.Core.CSGTreeBrush"/>es.</note></remarks>
        /// <seealso cref="Chisel.Core.BrushMesh" />
        public BrushMeshInstance    BrushMesh		
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { CompactHierarchyManager.SetBrushMeshID(nodeID, value.brushMeshHash); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new BrushMeshInstance { brushMeshHash = CompactHierarchyManager.GetBrushMeshID(nodeID) }; } 
        }
        
        public ref BrushOutline     Outline         { get { return ref CompactHierarchyManager.GetBrushOutline(this.nodeID); } }
        #endregion

        #region TreeBrush specific
        /// <value>Gets the bounds of this <see cref="Chisel.Core.CSGTreeBrush"/>.</value>
        public AABB Bounds { get { return Hierarchy.GetBrushBounds(CompactNodeID); } }

        public AABB GetBounds(float4x4 transformation) { return Hierarchy.GetBrushBounds(CompactNodeID, transformation); }
        #endregion

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
        public static bool operator == (CSGTreeBrush left, CSGTreeBrush right) { return left.nodeID == right.nodeID; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator != (CSGTreeBrush left, CSGTreeBrush right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeBrush left, CSGTreeNode right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeBrush left, CSGTreeNode right) { return left.nodeID != right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator ==(CSGTreeNode left, CSGTreeBrush right) { return left.nodeID == right.nodeID; }
		[EditorBrowsable(EditorBrowsableState.Never)]
        public static bool operator !=(CSGTreeNode left, CSGTreeBrush right) { return left.nodeID != right.nodeID; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
		{
			if (obj is CSGTreeBrush) return this == ((CSGTreeBrush)obj);
			if (obj is CSGTreeNode) return this == ((CSGTreeNode)obj);
			return false;
        }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool Equals(CSGTreeBrush other) { return this == other; }
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() { return nodeID.GetHashCode(); }
        #endregion

        /// <value>An invalid node</value>
        public static readonly CSGTreeBrush Invalid = new CSGTreeBrush { nodeID = NodeID.Invalid };

        // Temporary workaround until we can switch to hashes
        internal bool IsAnyStatusFlagSet()                  { return Hierarchy.IsAnyStatusFlagSet(CompactNodeID); }
        internal bool IsStatusFlagSet(NodeStatusFlags flag) { return Hierarchy.IsStatusFlagSet(CompactNodeID, flag); }
        internal void SetStatusFlag(NodeStatusFlags flag)   { Hierarchy.SetStatusFlag(CompactNodeID, flag); }
        internal void ClearStatusFlag(NodeStatusFlags flag) { Hierarchy.ClearStatusFlag(CompactNodeID, flag); }
        internal void ClearAllStatusFlags()                 { Hierarchy.ClearAllStatusFlags(CompactNodeID); }


        [SerializeField] internal NodeID nodeID;


        internal CompactNodeID      CompactNodeID       { get { return CompactHierarchyManager.GetCompactNodeID(nodeID); } }
        internal CompactHierarchyID CompactHierarchyID  { get { return CompactNodeID.hierarchyID; } }
        ref CompactHierarchy Hierarchy
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