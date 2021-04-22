using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using UnityEngine.Profiling;

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

        [HideInInspector]
        public CSGTreeBranch Node;
        public override CSGTreeNode TopNode { get { return Node; } protected set { Node = (CSGTreeBranch)value; } }

        [SerializeField,HideInInspector] bool passThrough = false; // NOTE: name is used in ChiselCompositeEditor
        public bool PassThrough { get { return passThrough; } set { if (value == passThrough) return; passThrough = value; ChiselNodeHierarchyManager.UpdateAvailability(this); } }
        
        [SerializeField,HideInInspector] CSGOperationType operation; // NOTE: name is used in ChiselCompositeEditor
        public CSGOperationType Operation { get { return operation; } set { if (value == operation) return; operation = value; if (Node.Valid) Node.Operation = operation; } }


        // Will show a warning icon in hierarchy when a generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (PassThrough)
                return true;
            if (!Node.Valid)
                return false;

            // A composite makes no sense without any children
            if (hierarchyItem != null)
                return (hierarchyItem.Children.Count > 0);
            return (transform.childCount > 0);
        }

        protected override void OnValidateState()
        {
            if (!Node.Valid)
                return;

            if (Node.Operation != operation)
                Node.Operation = operation;

            base.OnValidateState();
        }

        static CSGTreeNode[] kEmptyTreeNodeArray = new CSGTreeNode[] { };

        internal override CSGTreeNode[] CreateTreeNodes()
        {
            if (passThrough)
                return kEmptyTreeNodeArray;
            if (Node.Valid)
                Debug.LogWarning($"{nameof(ChiselComposite)} already has a treeNode, but trying to create a new one", this);		
            Node = CSGTreeBranch.Create(userID: GetInstanceID());
            Node.Operation = operation;
            return new CSGTreeNode[] { Node };
        }

        internal override bool	IsActive	        { get { return !PassThrough && isActiveAndEnabled; } }

        public override bool	CanHaveChildNodes	{ get { return IsActive; } }

        public override void	SetDirty()		    { if (Node.Valid) Node.SetDirty(); }

        internal override void SetChildren(List<CSGTreeNode> childNodes)
        {
            if (!Node.Valid)
            {
                Debug.LogWarning($"SetChildren called on a {nameof(ChiselComposite)} that isn't properly initialized", this);
                return;
            }
            if (!Node.SetChildren(childNodes))
                Debug.LogError("Failed to assign list of children to tree node");
        }

        public override void CollectCSGTreeNodes(List<CSGTreeNode> childNodes)
        {
            if (!PassThrough && Node.Valid)
                childNodes.Add(Node);
        }
    }
}