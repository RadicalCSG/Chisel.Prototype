﻿//#define BE_CHATTY
using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Chisel.Assets;

namespace Chisel.Components
{
	// TODO: put in separate file
	[Serializable]
	public sealed class SurfaceReference : IEquatable<SurfaceReference>, IEqualityComparer<SurfaceReference>
	{
		public CSGNode				node;
		public CSGBrushMeshAsset	brushMeshAsset;
		public int                  subNodeIndex;
		public int                  subMeshIndex;
		public int					surfaceID;
		public int					surfaceIndex;

		public SurfaceReference(CSGNode node, CSGBrushMeshAsset brushMeshAsset, int subNodeIndex, int subMeshIndex, int surfaceIndex, int surfaceID)
		{
			this.node = node;
			this.brushMeshAsset = brushMeshAsset;
			this.subNodeIndex = subNodeIndex;
			this.subMeshIndex = subMeshIndex;
			this.surfaceIndex = surfaceIndex;
			this.surfaceID = surfaceID;
		}

		public CSGBrushSubMesh.Polygon Polygon
		{
			get
			{
				if (!brushMeshAsset)
					return null;
				if (subMeshIndex < 0 || subMeshIndex >= brushMeshAsset.SubMeshCount)
					return null;
				var subMesh = brushMeshAsset.SubMeshes[subMeshIndex];
				if (surfaceIndex < 0 || surfaceIndex >= subMesh.Polygons.Length)
					return null;
				return subMesh.Polygons[surfaceIndex];
			}
		}

		public CSGBrushSubMesh.Orientation Orientation
		{
			get
			{
				if (!brushMeshAsset)
					return null;
				if (subMeshIndex < 0 || subMeshIndex >= brushMeshAsset.SubMeshCount)
					return null;
				var subMesh = brushMeshAsset.SubMeshes[subMeshIndex];
				if (surfaceIndex < 0 || surfaceIndex >= subMesh.Orientations.Length)
					return null;
				return subMesh.Orientations[surfaceIndex];
			}
		}

		public CSGTreeBrush treeBrush
		{
			get
			{
				if (node == null)
					return (CSGTreeBrush)CSGTreeNode.InvalidNode;
				return (CSGTreeBrush)node.GetTreeNodeByIndex(subNodeIndex);
			}
		}

		#region Equals
		public bool Equals(SurfaceReference other)
		{
			return Equals(this, other);
		}

		public bool Equals(SurfaceReference x, SurfaceReference y)
		{
			if (ReferenceEquals(x, y))
				return true;
			if (ReferenceEquals(x, null) ||
				ReferenceEquals(y, null))
				return false;
			return	//x.treeBrush			== y.treeBrush &&
					x.brushMeshAsset	== y.brushMeshAsset &&
					x.subNodeIndex		== y.subNodeIndex &&
					x.subMeshIndex		== y.subMeshIndex &&
					x.surfaceID			== y.surfaceID &&
					x.surfaceIndex		== y.surfaceIndex;
		}
		
		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;
			var other = obj as SurfaceReference;
			if (ReferenceEquals(other, null))
				return false;
			return Equals(this, other);
		}
		#endregion

		#region GetHashCode
		public override int GetHashCode()
		{
			return GetHashCode(this);
		}

		public int GetHashCode(SurfaceReference obj)
		{
			return  //obj.treeBrush.NodeID.GetHashCode() ^
					obj.brushMeshAsset.GetInstanceID() ^
					obj.subNodeIndex ^
					obj.subMeshIndex ^
					obj.surfaceID ^
					obj.surfaceIndex;
		}
		#endregion
	}
	
	// TODO: put in separate file
	[Serializable]
	public sealed class SurfaceIntersection
	{
		public SurfaceReference			surface;
		public CSGSurfaceIntersection	intersection;
	}

	[DisallowMultipleComponent]
	//[ExcludeFromObjectFactoryAttribute]
	public abstract class CSGNode : MonoBehaviour
	{
		public abstract string NodeTypeName { get; }

		public CSGNode()			{ hierarchyItem = new CSGHierarchyItem(this); CSGNodeHierarchyManager.Register(this); }
		protected void OnDestroy()	{ CSGNodeHierarchyManager.Unregister(this); OnCleanup(); }
		public void OnValidate()	{ OnValidateInternal(); }

		protected virtual void OnValidateInternal() { SetDirty(); }

		public void Reset() { OnResetInternal(); }
		protected virtual void OnResetInternal() { OnInitialize(); }

		public virtual void OnInitialize() { }
		protected virtual void OnCleanup() {  }

		[NonSerialized][HideInInspector] public readonly CSGHierarchyItem hierarchyItem;

		protected void OnEnable()
		{
#if BE_CHATTY
			Debug.Log("OnEnable " + this.name, this);
#endif
			OnInitialize(); // Awake is not reliable, so we initialize here
			CSGNodeHierarchyManager.UpdateAvailability(this);
		}


		public abstract Bounds CalculateBounds();/*
		{
			CSGBrushMeshAsset[] assets = GetUsedBrushMeshAssets();
			if (assets == null || assets.Length == 0)
				return CSGHierarchyItem.EmptyBounds;

			if (assets.Length == 1)
			{
				if (!assets[0])
					return CSGHierarchyItem.EmptyBounds;
			}
			
			var modelTransform		= CSGNodeHierarchyManager.FindModelTransformOfTransform(hierarchyItem.Transform);
			var localToWorldMatrix	= hierarchyItem.Transform.localToWorldMatrix;
			var bounds = CSGHierarchyItem.EmptyBounds;
			var haveBounds = false;
			foreach (var asset in assets)
			{
				if (!asset)
					continue;
				var assetBounds = asset.CalculateBounds(localToWorldMatrix);
				if (assetBounds.size.sqrMagnitude == 0)
					continue;
				if (!haveBounds)
				{
					bounds = assetBounds;
					haveBounds = true;
				} else
					bounds.Encapsulate(assetBounds);
			}
			return bounds;
		}*/

		public bool EncapsulateBounds(ref Bounds outBounds)
		{
			hierarchyItem.EncapsulateBounds(ref outBounds);
			return true;
		}

		protected void OnDisable()
		{
#if BE_CHATTY
			Debug.Log("OnDisable " + this.name, this);
#endif
			// Note: cannot call OnCleanup here
			CSGNodeHierarchyManager.UpdateAvailability(this);
		}
		
		// Called when the parent property of the transform has changed.
		protected void OnTransformParentChanged()
		{
#if BE_CHATTY
			Debug.Log("OnTransformParentChanged " + this.name, this);
#endif
			CSGNodeHierarchyManager.OnTransformParentChanged(this);
		}
	
		// Called when the list of children of the transform has changed.
		protected void OnTransformChildrenChanged()
		{
#if BE_CHATTY
			Debug.Log("OnTransformChildrenChanged " + this.name, this);
#endif
			CSGNodeHierarchyManager.OnTransformChildrenChanged(this);
		}
	
		public bool				Dirty					{ get { return CSGNodeHierarchyManager.IsNodeDirty(this); } }
		public virtual int		NodeID					{ get { return CSGTreeNode.InvalidNode.NodeID; } }
		internal virtual bool	SkipThisNode			{ get { return !isActiveAndEnabled; } }

		// Can this Node contain child CSGNodes?
		public virtual bool		CanHaveChildNodes		{ get { return false; } }

		// Does this node have a container treeNode that can hold child treeNodes?
		public virtual bool		HasContainerTreeNode	{ get { return CanHaveChildNodes; } }
		public virtual bool		CreatesTreeNode			{ get { return true; } }
		
		public virtual CSGTreeNode	GetTreeNodeByIndex(int index)
		{
			return CSGTreeNode.InvalidNode;
		}

		public abstract void SetDirty();

		internal abstract CSGTreeNode[] CreateTreeNodes();
		internal abstract void			ClearTreeNodes(bool clearCaches = false);
		internal virtual void			UpdateTransformation() { }
		public virtual void				UpdateBrushMeshInstances() { }

		internal virtual void SetChildren(List<CSGTreeNode> childNodes) { }

		internal virtual void CollectChildNodesForParent(List<CSGTreeNode> childNodes) { }

		public virtual CSGBrushMeshAsset[] GetUsedBrushMeshAssets() { return null; }
		
		public abstract int GetAllTreeBrushCount();

		// Get all brushes directly contained by this CSGNode
		public abstract void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes);

		public virtual CSGSurfaceAsset FindSurfaceAsset(CSGTreeBrush brush, int surfaceID)
		{
			return null;
		}

		public virtual CSGSurfaceAsset[] GetAllSurfaceAssets(CSGTreeBrush brush)
		{
			return null;
		}
		
		public virtual SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
		{
			return null;
		}

		public virtual SurfaceReference[] GetAllSurfaceReferences(CSGTreeBrush brush)
		{
			return null;
		}
		
		public virtual SurfaceReference[] GetAllSurfaceReferences()
		{
			return null;
		}


		public virtual Vector3 SetPivot(Vector3 newWorldPosition)
		{
			var transform		= hierarchyItem.Transform;
			var currentPosition = transform.position;
			if (currentPosition == newWorldPosition)
				return Vector3.zero;
			transform.position = newWorldPosition;
			var delta = newWorldPosition - currentPosition;
			AddPivotOffset(-delta);
			return delta;
		}

		internal virtual void AddPivotOffset(Vector3 worldSpaceDelta)
		{
			for (int c = 0; c < hierarchyItem.Children.Count; c++)
			{
				var child = hierarchyItem.Children[c];
				child.Component.AddPivotOffset(worldSpaceDelta);
			}
		}

#if UNITY_EDITOR
			// The icon used in the hierarchy
		public abstract GUIContent Icon { get; }

		public virtual bool ConvertToBrushes()
		{
			return false;
		}
#endif
	}
}