using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselComposite : ChiselNode
    {
        // This ensures names remain identical and the field actually exists, or a compile error occurs.
        public const string kOperationFieldName     = nameof(operation);
        public const string kPassThroughFieldName   = nameof(passThrough);

        public const string kNodeTypeName = "Composite";
        public override string NodeTypeName { get { return kNodeTypeName; } }

        // bool   HandleAsOne     = false;

        [HideInInspector]
        public CSGTreeBranch Node;

        [SerializeField,HideInInspector] bool passThrough = false; // NOTE: name is used in ChiselCompositeEditor
        public bool PassThrough { get { return passThrough; } set { if (value == passThrough) return; passThrough = value; ChiselNodeHierarchyManager.UpdateAvailability(this); } }
        
        [SerializeField,HideInInspector] CSGOperationType operation; // NOTE: name is used in ChiselCompositeEditor
        public CSGOperationType Operation { get { return operation; } set { if (value == operation) return; operation = value; if (Node.Valid) Node.Operation = operation; } }


        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (PassThrough)
                return true;
            if (!Node.Valid)
                return false;
            // A composite makes no sense without any children
            return (transform.childCount > 0);
        }

        protected override void OnValidateInternal()
        {
            if (!Node.Valid)
                return;

            if (Node.Operation != operation)
                Node.Operation = operation;

            base.OnValidateInternal();
        }

        internal override void			ClearTreeNodes (bool clearCaches = false) { Node.SetInvalid(); }	
        internal override CSGTreeNode[] CreateTreeNodes()
        {
            if (passThrough)
                return new CSGTreeNode[] { };
            if (Node.Valid)
                Debug.LogWarning($"{nameof(ChiselComposite)} already has a treeNode, but trying to create a new one", this);		
            Node = CSGTreeBranch.Create(userID: GetInstanceID());
            Node.Operation = operation;
            return new CSGTreeNode[] { Node };
        }

        internal override bool	IsActive	        { get { return !PassThrough && isActiveAndEnabled; } }

        public override bool	CanHaveChildNodes	{ get { return IsActive; } }

        public override int		NodeID			    { get { return Node.NodeID; } }


        public override void	SetDirty()		    { if (Node.Valid) Node.SetDirty(); }

        internal override void SetChildren(List<CSGTreeNode> childNodes)
        {
            if (!Node.Valid)
            {
                Debug.LogWarning($"SetChildren called on a {nameof(ChiselComposite)} that isn't properly initialized", this);
                return;
            }
            if (!Node.SetChildren(childNodes.ToArray()))
                Debug.LogError("Failed to assign list of children to tree node");
        }

        public override void CollectCSGTreeNodes(List<CSGTreeNode> childNodes)
        {
            childNodes.Add(Node);
        }

        public override int GetAllTreeBrushCount()
        {
            return 0;
        }

        // Get all brushes directly contained by this ChiselNode (not its children)
        public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
        {
            // An composite doesn't contain a CSGTreeBrush node
        }
        
        // TODO: cache this
        public override Bounds CalculateBounds()
        {
            var bounds = ChiselHierarchyItem.EmptyBounds;
            var haveBounds = false;
            for (int c = 0; c < hierarchyItem.Children.Count; c++)
            {
                var child = hierarchyItem.Children[c];
                if (!child.Component)
                    continue;
                var childBounds = child.Component.CalculateBounds();
                if (childBounds.size.sqrMagnitude == 0)
                    continue;
                if (!haveBounds)
                {
                    bounds = childBounds;
                    haveBounds = true;
                } else
                    bounds.Encapsulate(childBounds);
            }
            return bounds;
        }
        // TODO: cache this
        public override Bounds CalculateBounds(Matrix4x4 transformation)
        {
            var bounds = ChiselHierarchyItem.EmptyBounds;
            var haveBounds = false;
            for (int c = 0; c < hierarchyItem.Children.Count; c++)
            {
                var child = hierarchyItem.Children[c];
                if (!child.Component)
                    continue;
                var childBounds = child.Component.CalculateBounds(transformation);
                if (childBounds.size.sqrMagnitude == 0)
                    continue;
                if (!haveBounds)
                {
                    bounds = childBounds;
                    haveBounds = true;
                } else
                    bounds.Encapsulate(childBounds);
            }
            return bounds;
        }
    }
}