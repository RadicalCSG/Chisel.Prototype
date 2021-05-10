using UnityEngine;
using Chisel.Core;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Profiling;

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

        CSGTreeBrush GenerateTopNode(CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
            {
                if (node.Valid)
                    node.Destroy();
                return CSGTreeBrush.Create(userID: userID, operation: operation);
            }
            if (brush.Operation != operation)
                brush.Operation = operation;
            return brush;
        }

        protected override void UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            Profiler.BeginSample("ChiselBrushComponent");
            try
            { 
                var brush = (CSGTreeBrush)node;
                OnValidateDefinition();
                var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
                if (!surfaceDefinitionBlob.IsCreated)
                    return; 
                using (surfaceDefinitionBlob)
                {
                    node = brush = GenerateTopNode(brush, userID, operation);
                    var brushMesh = BrushMeshFactory.CreateBrushBlob(definition.brushOutline, in surfaceDefinitionBlob);
                    
                    if (!brushMesh.IsCreated)
                        brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                    else
                        brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
                }
            }
            finally { Profiler.EndSample(); }
        }
    }
}