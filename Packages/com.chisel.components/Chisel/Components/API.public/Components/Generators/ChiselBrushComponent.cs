using UnityEngine;
using Chisel.Core;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Components
{
    [ExecuteInEditMode]
    [HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [AddComponentMenu("Chisel/" + kNodeTypeName)]
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

        protected override JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            var brush = (CSGTreeBrush)node;
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return default;
            using (surfaceDefinitionBlob)
            using (var brushMeshRef = new NativeReference<BlobAssetReference<BrushMeshBlob>>(Allocator.TempJob))
            {
                node = brush = GenerateTopNode(brush, userID, operation);
                var handle = definition.Generate(brushMeshRef, surfaceDefinitionBlob);
                handle.Complete();

                if (!brushMeshRef.Value.IsCreated)
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                else
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshRef.Value) };
                return default;
            }
        }
    }
}