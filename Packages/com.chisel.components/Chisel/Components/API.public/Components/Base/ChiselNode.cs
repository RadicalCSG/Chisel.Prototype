using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Chisel.Components
{
    [DisallowMultipleComponent]
    //[ExcludeFromObjectFactoryAttribute]
    public abstract class ChiselNode : MonoBehaviour
    {
        public abstract string NodeTypeName { get; }

        public ChiselNode()			{ hierarchyItem = new CSGHierarchyItem(this); CSGNodeHierarchyManager.Register(this); }
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
            OnInitialize(); // Awake is not reliable, so we initialize here
            CSGNodeHierarchyManager.UpdateAvailability(this);
        }


        public abstract Bounds CalculateBounds();

        public bool EncapsulateBounds(ref Bounds outBounds)
        {
            hierarchyItem.EncapsulateBounds(ref outBounds);
            return true;
        }

        protected void OnDisable()
        {
            // Note: cannot call OnCleanup here
            CSGNodeHierarchyManager.UpdateAvailability(this);
        }
        
        // Called when the parent property of the transform has changed.
        protected void OnTransformParentChanged()
        {
            CSGNodeHierarchyManager.OnTransformParentChanged(this);
        }
    
        // Called when the list of children of the transform has changed.
        protected void OnTransformChildrenChanged()
        {
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

        public virtual ChiselBrushContainerAsset[] GetUsedGeneratedBrushes() { return null; }
        
        public abstract int GetAllTreeBrushCount();

        // Get all brushes directly contained by this CSGNode
        public abstract void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes);

        public virtual ChiselBrushMaterial FindBrushMaterial(CSGTreeBrush brush, int surfaceID)
        {
            return null;
        }

        public virtual ChiselBrushMaterial[] GetAllBrushMaterials(CSGTreeBrush brush)
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
        public virtual bool ConvertToBrushes()
        {
            return false;
        }
#endif
    }
}