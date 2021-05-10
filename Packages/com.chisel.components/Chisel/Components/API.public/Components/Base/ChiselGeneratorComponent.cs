using System;
using System.Linq;
using AOT;
using Chisel.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    public abstract class ChiselBrushGeneratorComponent<DefinitionType, Generator> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBrushGenerator
        where DefinitionType : SerializedBrushGenerator<Generator>, new()
    {
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

        static readonly GeneratorBrushJobPool<Generator> s_JobPool = new GeneratorBrushJobPool<Generator>();

        protected override void UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            var brush = (CSGTreeBrush)node;
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            node = brush = GenerateTopNode(brush, userID, operation);
            var settings = definition.GetBrushGenerator();
            s_JobPool.ScheduleUpdate(brush, settings, surfaceDefinitionBlob);
        }
    }

    public abstract class ChiselBranchGeneratorComponent<Generator, DefinitionType> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBranchGenerator
        where DefinitionType : SerializedBranchGenerator<Generator>, new()
    {
        CSGTreeBranch GenerateTopNode(CSGTreeBranch branch, int userID, CSGOperationType operation)
        {
            if (!branch.Valid)
            {
                if (branch.Valid)
                    branch.Destroy();
                return CSGTreeBranch.Create(userID: userID, operation: operation);
            }
            if (branch.Operation != operation)
                branch.Operation = operation;
            return branch;
        }

        static readonly GeneratorBranchJobPool<Generator> s_JobPool = new GeneratorBranchJobPool<Generator>();

        protected override void UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            var branch = (CSGTreeBranch)node;
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            node = branch = GenerateTopNode(branch, userID, operation);
            var settings = definition.GetBranchGenerator();
            s_JobPool.ScheduleUpdate(branch, settings, surfaceDefinitionBlob);
        }
    }

    public abstract class ChiselNodeGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselNodeGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new DefinitionType();

        public ChiselSurfaceDefinition surfaceDefinition;
        public override ChiselSurfaceDefinition SurfaceDefinition { get { return surfaceDefinition; } }

        public override ChiselBrushMaterial GetBrushMaterial(int descriptionIndex) { return surfaceDefinition.GetBrushMaterial(descriptionIndex); }
        public override SurfaceDescription GetSurfaceDescription(int descriptionIndex) { return surfaceDefinition.GetSurfaceDescription(descriptionIndex); }
        public override void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description) { surfaceDefinition.SetSurfaceDescription(descriptionIndex, description); }
        public override UVMatrix GetSurfaceUV0(int descriptionIndex) { return surfaceDefinition.GetSurfaceUV0(descriptionIndex); }
        public override void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0) { surfaceDefinition.SetSurfaceUV0(descriptionIndex, uv0); }

        protected override void OnResetInternal()
        { 
            definition.Reset(); 
            surfaceDefinition?.Reset(); 
            base.OnResetInternal(); 
        }

        protected void OnValidateDefinition()
        {
            definition.Validate();
            if (surfaceDefinition == null)
            {
                surfaceDefinition = new ChiselSurfaceDefinition();
                surfaceDefinition.Reset();
            }
            surfaceDefinition.EnsureSize(definition.RequiredSurfaceCount);
            definition.UpdateSurfaces(ref surfaceDefinition);
        }

        protected override void OnValidateState()
        {
            OnValidateDefinition();
            base.OnValidateState(); 
        }

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override void GetWarningMessages(IChiselMessageHandler messages)
        {
            base.GetWarningMessages(messages);
            definition.GetWarningMessages(messages);
        }
    }

    [DisallowMultipleComponent]
    public abstract class ChiselGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName = nameof(operation);

        [HideInInspector] CSGTreeNode Node = default;

        public abstract ChiselSurfaceDefinition SurfaceDefinition { get; }

        [SerializeField, HideInInspector] protected CSGOperationType    operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected Matrix4x4           localTransformation = Matrix4x4.identity;
        [SerializeField, HideInInspector] protected Vector3             pivotOffset = Vector3.zero;

        public override CSGTreeNode TopTreeNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Node; } protected set { Node = value; } }
        bool ValidNodes { get { return Node.Valid; } }
        

        public CSGOperationType Operation
        {
            get
            {
                return operation;
            }
            set
            {
                if (value == operation)
                    return;
                operation = value;

                if (ValidNodes)
                    Node.Operation = operation;

                // Let the hierarchy manager know that the contents of this node has been modified
                //	so we can rebuild/update sub-trees and regenerate meshes
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        public Vector3 PivotOffset
        {
            get
            {
                return pivotOffset;
            }
            set
            {
                if (value == pivotOffset)
                    return;
                pivotOffset = value;

                UpdateInternalTransformation();

                // Let the hierarchy manager know that this node has moved, so we can regenerate meshes
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(this);
            }
        }

        public Matrix4x4 LocalTransformation
        {
            get
            {
                return localTransformation;
            }
            set
            {
                if (value == localTransformation)
                    return;

                localTransformation = value;

                UpdateInternalTransformation();

                // Let the hierarchy manager know that this node has moved, so we can regenerate meshes
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(this);
            }
        }

        public Matrix4x4 PivotTransformation
        {
            get
            {
                // TODO: fix this mess

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 InversePivotTransformation
        {
            get
            {
                // TODO: fix this mess

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(-pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 LocalTransformationWithPivot
        {
            get
            {
                // TODO: fix this mess

                var localTransformationWithPivot = transform.localToWorldMatrix;
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    localTransformationWithPivot *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);

                var modelTransform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(transform);
                if (modelTransform)
                    localTransformationWithPivot = modelTransform.worldToLocalMatrix * localTransformationWithPivot;
                return localTransformationWithPivot;
            }
        }

        public abstract ChiselBrushMaterial GetBrushMaterial(int descriptionIndex);
        public abstract SurfaceDescription GetSurfaceDescription(int descriptionIndex);
        public abstract void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description);
        public abstract UVMatrix GetSurfaceUV0(int descriptionIndex);
        public abstract void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0);


        // TODO: improve warning messages
        const string kFailedToGenerateNodeMessage = "Failed to generate internal representation of generator (this should never happen)";
        const string kGeneratorIsPartOfDefaultModel = "This generator is part of the default model, please place it underneath a GameObject with a " + ChiselModel.kNodeTypeName + " component";

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override void GetWarningMessages(IChiselMessageHandler messages)
        {
            if (!ValidNodes)
            {
                messages.Warning(kFailedToGenerateNodeMessage);
                return;
            }

            var brush = (CSGTreeBrush)Node;
            if (brush.Valid && brush.BrushMesh == BrushMeshInstance.InvalidInstance)
            {
                messages.Warning(kFailedToGenerateNodeMessage);
                return;
            }

            if (ChiselGeneratedComponentManager.IsDefaultModel(hierarchyItem.Model))
                messages.Warning(kGeneratorIsPartOfDefaultModel);
        }

        protected override void OnValidateState()
        {
            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            UpdateBrushMeshInstances();

            ChiselNodeHierarchyManager.NotifyContentsModified(this);
            base.OnValidateState();
        }

        public override void UpdateTransformation()
        {
            // TODO: recalculate transformation based on hierarchy up to (but not including) model
            var transform = hierarchyItem.Transform;
            if (!transform)
                return;

            // TODO: fix this mess
            var localToWorldMatrix = transform.localToWorldMatrix;
            var modelTransform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(transform);
            if (modelTransform)
                localTransformation = modelTransform.worldToLocalMatrix * localToWorldMatrix;
            else
                localTransformation = localToWorldMatrix;

            if (!ValidNodes)
                return;

            UpdateInternalTransformation();
        }

        void UpdateInternalTransformation()
        {
            if (!ValidNodes)
                return;

            Node.LocalTransformation = LocalTransformationWithPivot;
        }

        protected override void OnResetInternal()
        {
            UpdateBrushMeshInstances();
            base.OnResetInternal();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyChildTreeNodes();
        }

        internal override CSGTreeNode RebuildTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);

            Profiler.BeginSample("UpdateGenerator");
            try
            {
                var instanceID = GetInstanceID();
                UpdateGeneratorInternal(ref Node, userID: instanceID);
            }
            finally { Profiler.EndSample(); }

            if (!ValidNodes)
                return default;

            Profiler.BeginSample("UpdateBrushMeshInstances");
            try { UpdateBrushMeshInstances(); }
            finally { Profiler.EndSample(); }
            
            if (Node.Operation != operation)
                Node.Operation = operation;
            return Node;
        }

        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            TopTreeNode.SetDirty();
        }


        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            PivotOffset += this.transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            base.AddPivotOffset(worldSpaceDelta);
        }

        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
        }

        protected abstract void UpdateGeneratorInternal(ref CSGTreeNode node, int userID);
    }
}