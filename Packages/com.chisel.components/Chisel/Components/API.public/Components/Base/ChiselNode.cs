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

        public abstract string          ChiselNodeTypeName  { get; }
        public abstract CSGTreeNode     TopTreeNode         { get; protected set; }
        internal virtual bool           IsActive            { get { return isActiveAndEnabled; } }
        public virtual bool             IsContainer         { get { return false; } }


        [NonSerialized] [HideInInspector] public readonly ChiselHierarchyItem hierarchyItem;


        public ChiselNode() { hierarchyItem = new ChiselHierarchyItem(this); ChiselNodeHierarchyManager.Register(this); }        
        public virtual void OnInitialize() { }
        protected virtual void OnCleanup() { }
        protected virtual void OnDestroy() { ChiselNodeHierarchyManager.Unregister(this); OnCleanup(); }
        
        protected void OnEnable()
        {
            OnInitialize(); // Awake is not reliable, so we initialize here
            ChiselNodeHierarchyManager.UpdateAvailability(this);
        }

        protected virtual void OnDisable()
        {
            // Note: cannot call OnCleanup here
            ChiselNodeHierarchyManager.UpdateAvailability(this);
        }


        public void OnValidate() { OnValidateState(); }
        protected virtual void OnValidateState() { SetDirty(); }


        public abstract bool HasValidState();

        public void Reset() { OnResetInternal(); }
        protected virtual void OnResetInternal() { OnInitialize(); }
        public abstract void SetDirty();



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

        public virtual void UpdateTransformation() { }


        internal abstract CSGTreeNode CreateTreeNode();
        public void ResetTreeNodes()
        {
            var topNode = TopTreeNode;
            if (topNode.Valid)
                topNode.Destroy();
            TopTreeNode = default;
        }

        public virtual void UpdateBrushMeshInstances() { }



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
    }
}