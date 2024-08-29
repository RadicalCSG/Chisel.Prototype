using System.Runtime.CompilerServices;
using Chisel.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Components
{
    public abstract class ChiselBrushGeneratorComponent<DefinitionType, Generator> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBrushGenerator
        where DefinitionType : SerializedBrushGenerator<Generator>, new()
    {
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

        static readonly GeneratorBrushJobPool<Generator> s_JobPool = new GeneratorBrushJobPool<Generator>();
        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, int userID)
        {
            if (!OnValidateDefinition())
                return false;

            var brush = (CSGTreeBrush)node;
            node = GenerateTopNode(in tree, brush, userID, operation);
            return true;
        }

        protected override int GetDefinitionHash()
        {
            return definition.GetHashCode();
        }


        const Allocator defaultAllocator = Allocator.TempJob;
        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
                return;

            var surfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(in surfaceArray, defaultAllocator);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            var settings = definition.GetBrushGenerator();
            s_JobPool.ScheduleUpdate(brush, settings, surfaceDefinitionBlob);
        }

		public override void GetMessages(IChiselMessageHandler messageHandler)
		{
            definition?.GetMessages(messageHandler);
		}
	}

    public abstract class ChiselBranchGeneratorComponent<Generator, DefinitionType> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBranchGenerator
        where DefinitionType : SerializedBranchGenerator<Generator>, new()
    {
        CSGTreeBranch GenerateTopNode(in CSGTree tree, CSGTreeBranch branch, int userID, CSGOperationType operation)
        {
            if (!branch.Valid)
            {
                if (branch.Valid)
                    branch.Destroy();
                return tree.CreateBranch(userID: userID, operation: operation);
            }
            if (branch.Operation != operation)
                branch.Operation = operation;
            return branch;
        }

        static readonly GeneratorBranchJobPool<Generator> s_JobPool = new GeneratorBranchJobPool<Generator>();
        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, int userID)
        {
			if (!OnValidateDefinition())
				return false;

			var branch = (CSGTreeBranch)node;
            node = GenerateTopNode(in tree, branch, userID, operation);
            return true;
        }

        protected override int GetDefinitionHash()
        {
            return definition.GetHashCode();
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            var branch = (CSGTreeBranch)node;
            if (!branch.Valid)
                return;

            var surfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(in surfaceArray, defaultAllocator);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            var settings = definition.GetBranchGenerator();
            s_JobPool.ScheduleUpdate(branch, settings, surfaceDefinitionBlob);
        }
    }

    public abstract class ChiselNodeGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselNodeGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new();

        public ChiselSurfaceArray surfaceArray;
        public override ChiselSurfaceArray SurfaceDefinition { get { return surfaceArray; } }

        public override ChiselSurface GetSurface(int descriptionIndex) { return surfaceArray.GetSurface(descriptionIndex); }
        public override SurfaceDetails GetSurfaceDetails(int descriptionIndex) { return surfaceArray.GetSurfaceDetails(descriptionIndex); }
        public override void SetSurfaceDetails(int descriptionIndex, SurfaceDetails description) { surfaceArray.SetSurfaceDetails(descriptionIndex, description); }
        public override UVMatrix GetSurfaceUV0(int descriptionIndex) { return surfaceArray.GetSurfaceUV0(descriptionIndex); }
        public override void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0) { surfaceArray.SetSurfaceUV0(descriptionIndex, uv0); }

        protected override void OnResetInternal()
        { 
            definition.Reset(); 
            surfaceArray?.Reset(); 
            base.OnResetInternal(); 
        }

        protected bool OnValidateDefinition(bool logErrors = false)
		{
            bool success = true;
            try
            {
                success = definition.Validate();
				if (surfaceArray == null)
                {
                    surfaceArray = new ChiselSurfaceArray();
                    surfaceArray.Reset();
                }
                surfaceArray.EnsureSize(definition.RequiredSurfaceCount);
                definition.UpdateSurfaces(ref surfaceArray);
            }
            catch (System.Exception ex)
			{
				success = false;
				Debug.LogException(ex, this);
			}
            if (!true && logErrors)
			    Debug.LogError($"Validation failed for {this.name}", this);
            return true;
		}

        protected override void OnValidateState()
        {
            if (OnValidateDefinition())
			    base.OnValidateState();
        }

		// Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
		public override void GetMessages(IChiselMessageHandler messages)
        {
            base.GetMessages(messages);
            definition.GetMessages(messages);
        }
    }

    public interface IChiselHasOperation
    {
        CSGOperationType Operation { get; set; }
    }

    [DisallowMultipleComponent]
    public abstract class ChiselGeneratorComponent : ChiselNode, IChiselHasOperation
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName = nameof(operation);

        [HideInInspector] CSGTreeNode Node = default;

        public abstract ChiselSurfaceArray SurfaceDefinition { get; }

        [SerializeField, HideInInspector] protected CSGOperationType    operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField] protected Vector3                              pivotOffset         = Vector3.zero;

        public override CSGTreeNode TopTreeNode 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
            {
                if (!ValidNodes) return CSGTreeNode.Invalid;
                return Node; 
            } 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set 
            { 
                Node = value; 
            } 
        }
        
        bool ValidNodes 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
            { 
                return Node.Valid; 
            } 
        }
        

        public CSGOperationType Operation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return operation;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public Matrix4x4 LocalPivotTransformation
        {
            get
            {
                // TODO: Optimize
                var transform       = hierarchyItem.Transform;

                var localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
                // We need to add the pivot to it. This is here so that when we change the pivot we do 
                // not actually need to modify meshes of brushes.
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    localTransformation *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);

                return localTransformation;
            }
        }

        public Matrix4x4 LocalPivotTransformationWithHiddenParents
        {
            get
            {
                // TODO: Optimize
                var transform = hierarchyItem.Transform;

                var localTransformation = LocalPivotTransformation;

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
                        var composite = component as ChiselCompositeComponent;
                        if (composite == null || !composite.PassThrough)
                            break;
                    }

                    localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale) * localTransformation;
                } while (true);

                return localTransformation;
            }
        }

        public Matrix4x4 GlobalTransformation
        {
            get
            {
                // TODO: Optimize
                var transform = hierarchyItem.Transform;

                var localTransformation = LocalPivotTransformation;
                var parentTransform = transform.parent;
                if (parentTransform != null)
                    localTransformation = parentTransform.localToWorldMatrix * localTransformation;

                return localTransformation;
            }
        }

        public abstract ChiselSurface GetSurface(int descriptionIndex);
        public abstract SurfaceDetails GetSurfaceDetails(int descriptionIndex);
        public abstract void SetSurfaceDetails(int descriptionIndex, SurfaceDetails description);
        public abstract UVMatrix GetSurfaceUV0(int descriptionIndex);
        public abstract void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0);


        // TODO: improve warning messages
        const string kFailedToGenerateNodeMessage = "Failed to generate internal representation of generator (this should never happen)";
		const string kBrushNodeIsInvalidMessage = "The internal brush representation is invalid.";
		const string kGeneratorIsPartOfDefaultModel = "This generator is part of the default model, please place it underneath a GameObject with a " + ChiselModelComponent.kNodeTypeName + " component";

		// Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
		public override void GetMessages(IChiselMessageHandler messages)
        {
			if (!ValidNodes)
            {
                if (this is ChiselBrushComponent)
                    messages.Warning(kBrushNodeIsInvalidMessage);
                else
				    messages.Warning(kFailedToGenerateNodeMessage);
				return;
            }

            var brush = (CSGTreeBrush)Node;
            if (brush.Valid && brush.BrushMesh == BrushMeshInstance.InvalidInstance)
			{
				if (this is ChiselBrushComponent)
					messages.Warning(kBrushNodeIsInvalidMessage);
				else
					messages.Warning(kFailedToGenerateNodeMessage);
				return;
            }

            if (ChiselGeneratedComponentManager.IsDefaultModel(hierarchyItem.Model))
            {
                messages.Warning(kGeneratorIsPartOfDefaultModel);
            }
		}

		protected override void OnValidateState()
        {
            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            if (TopTreeNode.Dirty)
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            base.OnValidateState();
        }

        public override void UpdateTransformation()
        {
            // TODO: recalculate transformation based on hierarchy up to (but not including) model
            var transform = hierarchyItem.Transform;
            if (!transform)
                return;
            
            UpdateInternalTransformation();
        }

        void UpdateInternalTransformation()
        {
            Node.LocalTransformation = LocalPivotTransformationWithHiddenParents;
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

        [HideInInspector] int prevMaterialHash;
        [HideInInspector] int prevDefinitionHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearHashes()
        {
            prevMaterialHash = 0;
            prevDefinitionHash = 0;
#if UNITY_EDITOR
            ChiselGeneratedComponentManager.EnsureVisibilityInitialized(this); 
#endif
        }

        public override void UpdateGeneratorNodes()
        {
            if (!Node.Valid)
            {
                var model = this.hierarchyItem.Model;
                if (model == null)
                {
                    ClearHashes();
                    return;
                }

                var treeRoot = model.Node;
                var instanceID = GetInstanceID();
                if (EnsureTopNodeCreatedInternal(in treeRoot, ref Node, userID: instanceID))
                    ClearHashes();

                if (!Node.Valid)
                    return;
            }

            UpdateMeshesWhenModified();

            if (ValidNodes)
            {
                UpdateBrushMeshInstances();

                if (Node.Operation != operation)
                    Node.Operation = operation;
            }
        }

        internal override CSGTreeNode RebuildTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);

            ClearHashes();
            UpdateGeneratorNodes();

            if (!ValidNodes)
                return default;
            return Node;
        }

        void UpdateMeshesWhenModified()
        {
            var currMaterialHash    = SurfaceDefinition?.GetHashCode() ?? 0;
            var currDefinitionHash  = GetDefinitionHash();
            if (prevMaterialHash != currMaterialHash || prevDefinitionHash != currDefinitionHash ||
                Node.Operation != operation)
            {
                prevMaterialHash    = currMaterialHash;
                prevDefinitionHash  = currDefinitionHash;

                if (Node.Operation != operation)
                {
                    Node.Operation = operation;
                    // Let the hierarchy manager know that the contents of this node has been modified
                    //	so we can rebuild/update sub-trees and regenerate meshes
                    ChiselNodeHierarchyManager.NotifyContentsModified(this);
                }

                var treeRoot = this.hierarchyItem.Model.Node;
                UpdateGeneratorNodesInternal(in treeRoot, ref Node);
            }
        }

        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            TopTreeNode.SetDirty();
            UpdateMeshesWhenModified();
        }


        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            var transform = hierarchyItem.Transform;
            var localSpaceDelta = transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            if (localSpaceDelta.x == 0 &&
                localSpaceDelta.y == 0 &&
                localSpaceDelta.z == 0)
                return;
            PivotOffset += localSpaceDelta;
            UpdateInternalTransformation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
        }

        protected abstract int GetDefinitionHash();

        protected abstract bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, int userID);
        protected abstract void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node);
	}
}