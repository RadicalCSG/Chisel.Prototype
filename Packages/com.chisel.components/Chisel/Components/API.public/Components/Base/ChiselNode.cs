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
        public const string kDocumentationBaseURL = "http://example.com/docs/"; // TODO: put somewhere else / put documentation online
        public const string kDocumentationExtension = ".html";

        public abstract string NodeTypeName { get; }
        /*
        public virtual Vector3 PivotOffset
        {
            get
            {
                return Vector3.zero;
            }
            set { }
        }
        */
        public ChiselNode()			{ hierarchyItem = new ChiselHierarchyItem(this); ChiselNodeHierarchyManager.Register(this); }
        protected void OnDestroy()	{ ChiselNodeHierarchyManager.Unregister(this); OnCleanup(); }
        public void OnValidate()	{ OnValidateInternal(); }

        protected virtual void OnValidateInternal() { SetDirty(); }

        public void Reset() { OnResetInternal(); }
        protected virtual void OnResetInternal() { OnInitialize(); }

        public abstract bool HasValidState();

        public virtual void OnInitialize() { }
        protected virtual void OnCleanup() {  }

        [NonSerialized][HideInInspector] public readonly ChiselHierarchyItem hierarchyItem;

        protected void OnEnable()
        {
            OnInitialize(); // Awake is not reliable, so we initialize here
            ChiselNodeHierarchyManager.UpdateAvailability(this);
        }


        public abstract Bounds CalculateBounds();
        public abstract Bounds CalculateBounds(Matrix4x4 transformation);

        public bool EncapsulateBounds(ref Bounds outBounds)
        {
            hierarchyItem.EncapsulateBounds(ref outBounds);
            return true;
        }

        public bool EncapsulateBounds(ref Bounds outBounds, Matrix4x4 transformation)
        {
            hierarchyItem.EncapsulateBounds(ref outBounds, transformation);
            return true;
        }

        protected virtual void OnDisable()
        {
            // Note: cannot call OnCleanup here
            ChiselNodeHierarchyManager.UpdateAvailability(this);
        }
        
        // Called when the parent property of the transform has changed.
        protected void OnTransformParentChanged()
        {
            ChiselNodeHierarchyManager.OnTransformParentChanged(this);
        }
    
        // Called when the list of children of the transform has changed.
        protected void OnTransformChildrenChanged()
        {
            ChiselNodeHierarchyManager.OnTransformChildrenChanged(this);
        }
    
        public bool				Dirty					{ get { return ChiselNodeHierarchyManager.IsNodeDirty(this); } }
        public virtual CompactNodeID NodeID				{ get { return CSGTreeNode.InvalidNode.NodeID; } }
        internal virtual bool	IsActive			    { get { return isActiveAndEnabled; } }

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
        public virtual void			    UpdateTransformation() { }
        public virtual void				UpdateBrushMeshInstances() { }

        internal virtual void SetChildren(List<CSGTreeNode> childNodes) { }

        public virtual void CollectCSGTreeNodes(List<CSGTreeNode> childNodes) { }

        public virtual bool GetUsedGeneratedBrushes(List<ChiselBrushContainerAsset> usedBrushes) { return false; }
        
        public abstract int GetAllTreeBrushCount();

        // Get all brushes directly contained by this CSGNode
        public abstract void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes);

        public virtual ChiselBrushMaterial FindBrushMaterialBySurfaceIndex(CSGTreeBrush brush, int surfaceID)
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