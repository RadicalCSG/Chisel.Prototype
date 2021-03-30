using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Profiling;
using Unity.Mathematics;

namespace Chisel.Components
{

    public abstract class ChiselDefinedBrushGeneratorComponent<DefinitionType> : ChiselBrushGeneratorComponent
        where DefinitionType : IChiselGenerator, IBrushGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new DefinitionType();

        protected override void OnResetInternal()           { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal()        { definition.Validate(); base.OnValidateInternal(); }
        protected override bool UpdateGeneratorInternal(CSGTreeBrush brush) { return definition.Generate(brush); }
    }

    public abstract class ChiselBrushGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName         = nameof(operation);


        [HideInInspector] CSGTreeBrush Node = default;

        [SerializeField, HideInInspector] protected CSGOperationType operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected Matrix4x4 localTransformation = Matrix4x4.identity;
        [SerializeField, HideInInspector] protected Vector3 pivotOffset = Vector3.zero;

        public CSGTreeNode TopNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Node; } }
        bool ValidNodes { get { return Node.Valid; } }

#if UNITY_EDITOR
        public VisibilityState UpdateVisibility(UnityEditor.SceneVisibilityManager instance)
        {
            var resultState     = VisibilityState.Unknown;
            var visible         = !instance.IsHidden(gameObject);
            var pickingEnabled  = !instance.IsPickingDisabled(gameObject);
            if (Node.Valid)
            {
                Node.Visible        = visible;
                Node.PickingEnabled = pickingEnabled;

                if (visible)
                    resultState |= VisibilityState.AllVisible;
                else
                    resultState |= VisibilityState.AllInvisible;
            }
            return resultState;
        }
#endif

        public override CSGTreeNode GetTreeNodeByIndex(int index)
        {
            if (index != 0)
                return CSGTreeNode.InvalidNode;
            return Node;
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

        //**Temporary hack to ensure that a BrushContainerAsset remains unique when duplicated so that we can control when we share a BrushContainerAsset**//
        #region HandleDuplication
        void HandleDuplication()
        {
        }
        #endregion
        //**//

        protected override void OnValidateInternal()
        {
            HandleDuplication();

            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            UpdateGenerator();
            UpdateBrushMeshInstances();

            ChiselNodeHierarchyManager.NotifyContentsModified(this);
            base.OnValidateInternal();
        }

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

        void UpdateInternalTransformation()
        {
            if (!ValidNodes)
                return;

            Node.LocalTransformation = LocalTransformationWithPivot;
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Node.Valid)
                Node.Destroy();
            Node = default;
        }

        public void GenerateAllTreeNodes()
        {
            var instanceID			= GetInstanceID();

            if (Node.Valid)
            {
                Node.Destroy();
                Node = default;
            }
            
            Node = CSGTreeBrush.Create(userID: instanceID, operation: operation);
            Node.Operation = operation;
        }

        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
            
            if (Node.Valid &&
                Node.Operation != operation)
                Node.Operation = operation;
        }

        internal override void ClearTreeNodes(bool clearCaches = false)
        {
            if (Node.Valid)
            {
                Node.Destroy();
                Node = default;
            }
        }

        CSGTreeNode[] nodes = new CSGTreeNode[1];

        internal override CSGTreeNode[] CreateTreeNodes()
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

            if (Node.Operation != operation)
                Node.Operation = operation;
            nodes[0] = Node;
            return nodes;
        }

        public override NodeID NodeID { get { return TopNode.NodeID; } }
        
        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            TopNode.SetDirty();
        }

        public override void CollectCSGTreeNodes(List<CSGTreeNode> childNodes)
        {
            childNodes.Add(TopNode);
        }

        public override bool GetUsedGeneratedBrushes(List<ChiselBrushContainerAsset> usedBrushes)
        {
            //throw new NotImplementedException();
            return false;
        }

        // TODO: clean this up
        public delegate IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelfDelegate(CSGTreeBrush brush);
        public static GetSelectedVariantsOfBrushOrSelfDelegate GetSelectedVariantsOfBrushOrSelf;

        static readonly HashSet<CSGTreeBrush> s_FoundBrushes = new HashSet<CSGTreeBrush>();

        public override Bounds CalculateBounds()
        {
            if (!Node.Valid)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
            var minMax			= new MinMaxAABB { };
            var boundsCount     = 0;

            s_FoundBrushes.Clear();
            GetAllTreeBrushes(s_FoundBrushes, false);
            foreach (var brush in s_FoundBrushes)
            {
                if (!brush.Valid)
                    continue;

                var transformation  = modelMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix;
                var childBounds     = Node.Bounds;
                var size            = childBounds.Max - childBounds.Min;
                var magnitude       = math.lengthsq(size);
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = ((float4)transformation.GetColumn(3)).xyz;
                    var halfSize = size * 0.5f;
                    childBounds = new MinMaxAABB { Min = center - halfSize, Max = center + halfSize };
                }
                if (magnitude != 0)
                {
                    if (boundsCount == 0)
                        minMax = childBounds;
                    else
                        minMax.Encapsulate(childBounds);
                    boundsCount++;
                }
            }
            if (boundsCount == 0)
                return ChiselHierarchyItem.EmptyBounds;
            var bounds = new Bounds();
            bounds.SetMinMax(minMax.Min, minMax.Max);
            return bounds;
        }
        
        public override Bounds CalculateBounds(Matrix4x4 boundsTransformation)
        {
            if (!Node.Valid)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
            var minMax			= new MinMaxAABB { };
            var boundsCount     = 0;

            var foundBrushes    = new HashSet<CSGTreeBrush>();
            GetAllTreeBrushes(foundBrushes, false);
            foreach (var brush in foundBrushes)
            {
                if (!brush.Valid)
                    continue;
                var transformation  = modelMatrix * (Matrix4x4)brush.NodeToTreeSpaceMatrix * boundsTransformation;
                var childBounds     = Node.GetBounds(transformation);
                var size            = childBounds.Max - childBounds.Min;
                var magnitude       = math.lengthsq(size);
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = ((float4)transformation.GetColumn(3)).xyz;
                    var halfSize = size * 0.5f;
                    childBounds = new MinMaxAABB { Min = center - halfSize, Max = center + halfSize };
                }
                if (magnitude != 0)
                {
                    if (boundsCount == 0)
                        minMax = childBounds;
                    else
                        minMax.Encapsulate(childBounds);
                    boundsCount++;
                }
            }
            if (boundsCount == 0)
                return ChiselHierarchyItem.EmptyBounds;
            var bounds = new Bounds();
            bounds.SetMinMax(minMax.Min, minMax.Max);
            return bounds;
        }
        
        public override int GetAllTreeBrushCount()
        {
            return 1;
        }

        // Get all brushes directly contained by this CSGNode (not its children)
        public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
        {
            if (foundBrushes == null ||
                !Node.Valid)
                return;

#if UNITY_EDITOR
            if (ignoreSynchronizedBrushes)
            {
                var result = GetSelectedVariantsOfBrushOrSelf(Node);
                if (result != null)
                    foundBrushes.AddRange(result);
            } else
#endif
                foundBrushes.Add(Node);
        }

        public override ChiselBrushMaterial FindBrushMaterialBySurfaceIndex(CSGTreeBrush brush, int surfaceID)
        {
            throw new NotImplementedException();
            /*
            if (!Node.Valid)
                return null;
            
            if (brush.NodeID != TopNode.NodeID)
                return null;

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(Node.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return null;
            ref var brushMesh = ref brushMeshBlob.Value;
                
            var surfaceIndex = -1;
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                if (brushMesh.polygons[i].surfaceID == surfaceID)
                {
                    surfaceIndex = i;
                    break;
                }
            }
                    
            if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                return null;

            var surface = brushMesh.polygons[surfaceIndex].surface;
            if (surface == null)
                return null;

            return surface.brushMaterial;*/
        }

        public override ChiselBrushMaterial[] GetAllBrushMaterials(CSGTreeBrush brush)
        {
            throw new NotImplementedException();
            /*
            if (!Node.Valid)
                return null;

            if (brush.NodeID != TopNode.NodeID)
                return null;

            var surfaces = new HashSet<ChiselBrushMaterial>();

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(Node.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return null;
            ref var brushMesh = ref brushMeshBlob.Value;

            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surface = brushMesh.polygons[i].surface;
                if (surface == null)
                    continue;
                surfaces.Add(surface.brushMaterial);
            }

            return surfaces.ToArray();*/
        }

        public override SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
        {
            throw new NotImplementedException();
            /*
            if (!Node.Valid)
                return null;
                
            if (brush.NodeID != TopNode.NodeID)
                return null;
            
            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(Node.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return null;
            ref var brushMesh = ref brushMeshBlob.Value;
                
            var surfaceIndex = -1;
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                if (brushMesh.polygons[i].surfaceID == surfaceID)
                {
                    surfaceIndex = i;
                    break;
                }
            }
                    
            if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                return null;

            return new SurfaceReference(this, brushContainerAsset, 0, 0, surfaceIndex, surfaceID);
            */
        }

        public override SurfaceReference[] GetAllSurfaceReferences()
        {
            throw new NotImplementedException();
            /*
            if (!Node.Valid)
                return null;
            
            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(Node.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return null;
            ref var brushMesh = ref brushMeshBlob.Value;
                
            var surfaces	= new HashSet<SurfaceReference>();
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surfaceID = brushMesh.polygons[i].surfaceID;
                throw new NotImplementedException();
                surfaces.Add(new SurfaceReference(this, brushContainerAsset, 0, 0, i, surfaceID));
            }
            return surfaces.ToArray();
            */
        }

        public override SurfaceReference[] GetAllSurfaceReferences(CSGTreeBrush brush)
        {
            throw new NotImplementedException();
            /*
            if (!Node.Valid)
                return null;

            if (brush.NodeID != TopNode.NodeID)
                return null;
                
            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(Node.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return null;
            ref var brushMesh = ref brushMeshBlob.Value;

            var surfaces	= new HashSet<SurfaceReference>();
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surfaceID = brushMesh.polygons[i].surfaceID;
                throw new NotImplementedException();
                surfaces.Add(new SurfaceReference(this, brushContainerAsset, 0, 0, i, surfaceID));
            }

            return surfaces.ToArray();
            */
        }

        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            PivotOffset += this.transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            base.AddPivotOffset(worldSpaceDelta);
        }

        public virtual void UpdateGenerator()
        {
            if (!Node.Valid)
                return;

            /*
            // BrushMeshes of generators must always be unique
            Profiler.BeginSample("ChiselBrushContainerAsset.Create");
            try
            {
                if (!brushContainerAsset ||
                    !ChiselBrushContainerAssetManager.IsBrushMeshUnique(brushContainerAsset))
                    brushContainerAsset = ChiselBrushContainerAsset.Create("Generated " + NodeTypeName);
            }
            finally { Profiler.EndSample(); }
            */
            Profiler.BeginSample("UpdateGeneratorInternal");
            try { UpdateGeneratorInternal(Node); }
            finally { Profiler.EndSample(); }

            //brushContainerAsset.SetDirty();

            Profiler.BeginSample("UpdateBrushMeshInstances");
            try { UpdateBrushMeshInstances(); }
            finally { Profiler.EndSample(); }
        }

        protected abstract bool UpdateGeneratorInternal(CSGTreeBrush brush);

#if UNITY_EDITOR
        public override bool ConvertToBrushes()
        {
            throw new NotImplementedException();
            /*
            if (brushContainerAsset == null ||
                brushContainerAsset.BrushMeshes == null ||
                brushContainerAsset.BrushMeshes.Length == 0)
                return false;

            string groupName;
            var topGameObject       = this.gameObject;
            var sourceBrushMeshes   = this.brushContainerAsset.BrushMeshes;
            var sourceOperations    = this.brushContainerAsset.Operations;
            var topOperation        = this.operation;
            var pivotOffset         = this.pivotOffset;
            var localTransformation = this.localTransformation;
            UnityEditor.Undo.DestroyObjectImmediate(this);
            topGameObject.SetActive(false);
            try
            {
                if (sourceBrushMeshes.Length == 1)
                {
                    groupName = "Converted Shape to Brush";
                    var brush = ChiselComponentFactory.AddComponent<ChiselBrush>(topGameObject);
                    brush.LocalTransformation       = localTransformation;
                    brush.PivotOffset               = pivotOffset;
                    brush.Operation                 = topOperation;
                    brush.definition = new ChiselBrushDefinition
                    {
                        brushOutline = new BrushMesh(sourceBrushMeshes[0])
                    };
                    // TODO: create surfacedefinition
                } else
                {
                    groupName = $"Converted {NodeTypeName} to Multiple Brushes";
                    var compositeComponent = ChiselComponentFactory.AddComponent<ChiselComposite>(topGameObject);
                    compositeComponent.Operation = topOperation;
                    var parentTransform = topGameObject.transform;
                    for (int i = 0; i < sourceBrushMeshes.Length; i++)
                    {
                        var brush = ChiselComponentFactory.Create<ChiselBrush>("Brush (" + (i + 1) + ")", parentTransform, localTransformation);
                        brush.LocalTransformation       = localTransformation;
                        brush.PivotOffset               = pivotOffset; 
                        brush.Operation                 = sourceOperations[i];
                        brush.definition = new ChiselBrushDefinition
                        {
                            brushOutline = new BrushMesh(sourceBrushMeshes[i])
                        };
                        // TODO: create surfacedefinition
                    }
                }
            }
            finally
            {
                topGameObject.SetActive(true);
            }
            UnityEditor.Undo.SetCurrentGroupName(groupName);
            return true;*/
        }
#endif

    }
}