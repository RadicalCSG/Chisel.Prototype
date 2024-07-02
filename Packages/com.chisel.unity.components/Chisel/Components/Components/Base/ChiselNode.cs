using System;
using Chisel.Core;
using UnityEngine;

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
        
        protected virtual void OnCleanup() 
        { 
            ResetTreeNodes();
        }

        public virtual void OnInitialize()
        {
        }

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

        protected void OnDestroy() 
        { 
            ChiselNodeHierarchyManager.Unregister(this); 
            OnCleanup();
        }


        public void OnValidate() { OnValidateState(); }
        protected virtual void OnValidateState() { SetDirty(); }

        public abstract void GetWarningMessages(IChiselMessageHandler messages);

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

        public virtual void UpdateTransformation()
        {
            var node = TopTreeNode;
            if (!node.Valid)
                return;

            var transform = hierarchyItem.Transform;
            if (transform == null)
                return;

            // TODO: Optimize
            var localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            // We can't just use the transformation of this brush, because it might have gameobjects as parents that 
            // do not have any chisel components. So we need to consider all transformations in between as well.
            do
            {
                transform = transform.parent;
                if (transform == null)
                    break;

                // If we find a ChiselNode we continue, unless it's a Composite set to passthrough
                if (transform.TryGetComponent<ChiselNode>(out var component))
                {
                    var composite = component as ChiselComposite;
                    if (composite == null || !composite.PassThrough)
                        break;
                }

                localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale) * localTransformation;
            } while (true);
            node.LocalTransformation = localTransformation;
        }

        // TODO: remove need for this
        public virtual void UpdateGeneratorNodes() { }
        internal abstract CSGTreeNode RebuildTreeNodes();

        public void ResetTreeNodes(bool doNotDestroy = false)
        {
            var topNode = TopTreeNode;
            if (!doNotDestroy && topNode.Valid)
                topNode.Destroy();
            TopTreeNode = default;
        }

        protected void DestroyChildTreeNodes()
        {
            var topNode = TopTreeNode;
            if (topNode.Valid)
                topNode.DestroyChildren();
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
            UpdateTransformation();
        }
    }
}