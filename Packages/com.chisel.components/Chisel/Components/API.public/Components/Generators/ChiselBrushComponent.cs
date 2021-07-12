using UnityEngine;
using Chisel.Core;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
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
            Profiler.BeginSample("ChiselBrushComponent");
            try
            {
                var brush = (CSGTreeBrush)node;
                if (!brush.Valid)
                    return;

                Profiler.BeginSample("BuildSurfaceDefinitionBlob");
                var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.Temp);
                Profiler.EndSample();

                if (!surfaceDefinitionBlob.IsCreated)
                    return;

                using (surfaceDefinitionBlob)
                {
                    Profiler.BeginSample("CreateBrushBlob");
                    var brushMesh = BrushMeshFactory.CreateBrushBlob(definition.brushOutline, in surfaceDefinitionBlob);
                    Profiler.EndSample();

                    Profiler.BeginSample("Set");
                    // TODO: deregister previous brushMesh (when different)
                    if (brushMesh.IsCreated)
                        brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
                    else
                        brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                    Profiler.EndSample();
                }
            }
            finally { Profiler.EndSample(); }
        }
    }
}