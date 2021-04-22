using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Profiling;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Unity.Jobs;
using Unity.Collections;

namespace Chisel.Components
{

    public abstract class ChiselDefinedGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselGenerator, new()
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

        void OnValidateDefinition()
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

        protected override JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return default;
            using (surfaceDefinitionBlob)
            {
                return definition.Generate(surfaceDefinitionBlob, ref node, userID, operation);
            }
        }

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            return !base.HasValidState() &&
                    definition.HasValidState();
        }
    }

    public abstract class ChiselGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName         = nameof(operation);

        [HideInInspector] CSGTreeNode Node = default;

        public abstract ChiselSurfaceDefinition SurfaceDefinition { get; }

        [SerializeField, HideInInspector] protected CSGOperationType operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected Matrix4x4 localTransformation = Matrix4x4.identity;
        [SerializeField, HideInInspector] protected Vector3 pivotOffset = Vector3.zero;

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

        protected override void OnDisable()
        {
            ResetTreeNodes();
            base.OnDisable();
        }

        protected override void OnResetInternal()
        {
            UpdateGenerator();
            UpdateBrushMeshInstances();
            base.OnResetInternal();
        }

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (!ValidNodes)
                return false;

            if (ChiselGeneratedComponentManager.IsDefaultModel(hierarchyItem.Model))
                return false;

            return true;
        }

        protected override void OnValidateState()
        {
            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            UpdateGenerator();
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

            if (Node.Type == CSGNodeType.Brush)
            {
                Node.LocalTransformation = LocalTransformationWithPivot;
            } else
            {
                // TODO: Remove this once we have a proper transformation pipeline
                for (int i = 0; i < Node.Count; i++)
                {
                    var child = Node[i];
                    child.LocalTransformation = LocalTransformationWithPivot;
                }
            }
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            ResetTreeNodes();
        }

        internal override CSGTreeNode CreateTreeNode()
        {
            if (ValidNodes)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);
            
            Profiler.BeginSample("GenerateAllTreeNodes");
            GenerateAllTreeNodes();
            Profiler.EndSample();

            Profiler.BeginSample("UpdateGenerator");
            UpdateGenerator();
            Profiler.EndSample();

            Profiler.BeginSample("UpdateInternalTransformation");
            UpdateInternalTransformation();
            Profiler.EndSample();


            if (!ValidNodes)
                return default;
            
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

        public void GenerateAllTreeNodes()
        {
            if (Node.Valid)
            {
                Node.Destroy();
                Node = default;
            }
        }

        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
        }

        public virtual void UpdateGenerator()
        {
            var instanceID = GetInstanceID();

            Profiler.BeginSample("UpdateGeneratorInternal");
            try 
            { 
                var jobHandle = UpdateGeneratorInternal(ref Node, userID: instanceID);
                jobHandle.Complete();
            }
            finally { Profiler.EndSample(); }

            if (!Node.Valid)
                return;

            Profiler.BeginSample("UpdateBrushMeshInstances");
            try { UpdateBrushMeshInstances(); }
            finally { Profiler.EndSample(); }
        }

        protected abstract JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID);
    }
}