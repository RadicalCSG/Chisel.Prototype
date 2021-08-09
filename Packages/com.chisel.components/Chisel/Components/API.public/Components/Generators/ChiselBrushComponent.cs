using UnityEngine;
using Chisel.Core;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Profiling;
using System;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    public sealed class ChiselBrushComponent : ChiselNodeGeneratorComponent<ChiselBrushDefinition>
    {
        public const string kNodeTypeName = ChiselBrushDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public BrushMesh BrushMesh
        {
            get { return definition.brushOutline; }
            set { if (value == definition.brushOutline) return; definition.brushOutline = value; OnValidateState(); }
        }
        #endregion

        CSGTreeBrush GenerateTopNode(in CSGTree tree, CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
            {
                if (node.Valid)
                    node.Destroy();
                return tree.CreateBrush(userID: userID, operation: operation);
            }
            if (brush.Operation != operation)
                brush.Operation = operation;
            return brush;
        }

        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, int userID)
        {    
            Profiler.BeginSample("OnValidateDefinition");
            OnValidateDefinition();
            Profiler.EndSample();

            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
            {
                Profiler.BeginSample("GenerateTopNode");
                node = GenerateTopNode(in tree, brush, userID, operation);
                Profiler.EndSample();
            }
            return true;
        }

        protected override int GetDefinitionHash()
        {
            return definition.brushOutline?.GetHashCode() ?? 0;
        }

        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            ChiselNodeHierarchyManager.OnBrushMeshUpdate(this, ref node);
        }
    }
}