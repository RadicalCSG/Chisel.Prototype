using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Profiling;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chisel.Components
{

    public abstract class ChiselDefinedGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new DefinitionType();


        public override ChiselBrushMaterial GetBrushMaterial(int descriptionIndex) { return definition.SurfaceDefinition.GetBrushMaterial(descriptionIndex); }
        public override SurfaceDescription GetSurfaceDescription(int descriptionIndex) { return definition.SurfaceDefinition.GetSurfaceDescription(descriptionIndex); }
        public override void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description) { definition.SurfaceDefinition.SetSurfaceDescription(descriptionIndex, description); }
        public override UVMatrix GetSurfaceUV0(int descriptionIndex) { return definition.SurfaceDefinition.GetSurfaceUV0(descriptionIndex); }
        public override void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0) { definition.SurfaceDefinition.SetSurfaceUV0(descriptionIndex, uv0); }

        protected override void OnResetInternal()           { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal()        { definition.Validate(); base.OnValidateInternal(); }
        protected override bool UpdateGeneratorInternal(ref CSGTreeNode node, int userID) { return definition.Generate(ref node, userID, operation); }
    }

    public abstract class ChiselGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName         = nameof(operation);


        [HideInInspector] CSGTreeNode Node = default;

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


        public abstract ChiselBrushMaterial GetBrushMaterial(int descriptionIndex);
        public abstract SurfaceDescription GetSurfaceDescription(int descriptionIndex);
        public abstract void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description);
        public abstract UVMatrix GetSurfaceUV0(int descriptionIndex);
        public abstract void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0);

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

        protected override void OnValidateInternal()
        {
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
            if (Node.Valid)
                Node.Destroy();
            Node = default;
        }

        internal override void ClearTreeNodes(bool clearCaches = false)
        {
            if (Node.Valid)
            {
                Node.Destroy();
                Node = default;
            }
        }

        static readonly CSGTreeNode[] nodes = new CSGTreeNode[1];
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
            if (TopNode.Valid)
                childNodes.Add(TopNode);
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
                var childBounds     = brush.Bounds;
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
                var childBounds     = brush.GetBounds(transformation);
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

            var brush = (CSGTreeBrush)Node;
            if (brush.Valid)
            {
#if UNITY_EDITOR
                if (ignoreSynchronizedBrushes)
                {
                    var result = GetSelectedVariantsOfBrushOrSelf(brush);
                    if (result != null)
                        foundBrushes.AddRange(result);
                } else
#endif
                    foundBrushes.Add(brush);
            } else
            {
                var nodes = new List<CSGTreeNode>();
                nodes.Add(Node);
                while (nodes.Count > 0)
                {
                    var lastIndex = nodes.Count - 1;
                    var current = nodes[lastIndex];
                    nodes.RemoveAt(lastIndex);
                    var nodeType = current.Type;
                    if (nodeType == CSGNodeType.Brush)
                    {
                        brush = (CSGTreeBrush)current;
#if UNITY_EDITOR
                        if (ignoreSynchronizedBrushes)
                        {
                            var result = GetSelectedVariantsOfBrushOrSelf(brush);
                            if (result != null)
                                foundBrushes.AddRange(result);
                        } else
#endif
                            foundBrushes.Add(brush);
                    } else
                    {
                        for (int i = current.Count - 1; i >= 0; i--)
                            nodes.Add(current[i]);
                    }
                }
            }
        }

        static readonly List<ChiselBrushMaterial> s_TempBrushMaterials = new List<ChiselBrushMaterial>();

        public override ChiselBrushMaterial FindBrushMaterialBySurfaceIndex(CSGTreeBrush brush, int surfaceID)
        {
            if (!Node.Valid)
                return null;

            if (surfaceID < 0)
                return null;

            s_TempBrushMaterials.Clear();
            if (!GetAllMaterials(Node, brush, s_TempBrushMaterials))
                return null;

            if (surfaceID >= s_TempBrushMaterials.Count)
                return null;

            var brushMaterial = s_TempBrushMaterials[surfaceID];
            s_TempBrushMaterials.Clear();
            return brushMaterial;
        }

        bool GetAllMaterials(CSGTreeBrush brush, CSGTreeBrush findBrush, List<ChiselBrushMaterial> brushMaterials)
        {
            if (brush.NodeID != findBrush.NodeID)
                return false;

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return true;

            ref var brushMesh = ref brushMeshBlob.Value;
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surface = brushMesh.polygons[i].surface;
                var brushMaterial = new ChiselBrushMaterial
                {
                    LayerUsage      = surface.layerDefinition.layerUsage,
                    RenderMaterial  = surface.layerDefinition.layerParameter1 == 0 ? default : ChiselMaterialManager.Instance.GetMaterial(surface.layerDefinition.layerParameter1),
                    PhysicsMaterial = surface.layerDefinition.layerParameter2 == 0 ? default : ChiselMaterialManager.Instance.GetPhysicMaterial(surface.layerDefinition.layerParameter2)
                };
                brushMaterials.Add(brushMaterial);
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool GetAllMaterials(CSGTreeNode node, CSGTreeBrush findBrush, List<ChiselBrushMaterial> brushMaterials)
        {
            switch(node.Type)
            {
                case CSGNodeType.Branch:    return GetAllMaterials((CSGTreeBranch)node, findBrush, brushMaterials);
                case CSGNodeType.Brush:     return GetAllMaterials((CSGTreeBrush)node, findBrush, brushMaterials);
                default: return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool GetAllMaterials(CSGTreeBranch branch, CSGTreeBrush findBrush, List<ChiselBrushMaterial> brushMaterials)
        {
            for (int i = 0; i < branch.Count; i++)
            {
                if (GetAllMaterials(branch[i], findBrush, brushMaterials))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool GetAllBrushMaterials(CSGTreeBrush brush, List<ChiselBrushMaterial> brushMaterials)
        {
            if (!Node.Valid)
                return false;

            GetAllMaterials(Node, brush, brushMaterials);
            return brushMaterials.Count > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool FindSurfaceReference(CSGTreeNode node, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            switch (node.Type)
            {
                case CSGNodeType.Branch: return FindSurfaceReference((CSGTreeBranch)node, findBrush, surfaceID, out surfaceReference);
                case CSGNodeType.Brush:  return FindSurfaceReference((CSGTreeBrush)node,  findBrush, surfaceID, out surfaceReference);
                default: return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool FindSurfaceReference(CSGTreeBranch branch, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            for (int i = 0; i < branch.Count; i++)
            {
                if (FindSurfaceReference(branch[i], findBrush, surfaceID, out surfaceReference))
                    return true;
            }
            return false;
        }

        bool FindSurfaceReference(CSGTreeBrush brush, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            if (findBrush.NodeID != brush.NodeID)
                return false;
            
            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(findBrush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return true;

            ref var brushMesh = ref brushMeshBlob.Value;

            var surfaceIndex = surfaceID;
            if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                return true;

            var descriptionIndex = brushMesh.polygons[surfaceIndex].descriptionIndex;

            //return new SurfaceReference(this, brushContainerAsset, 0, 0, surfaceIndex, surfaceIndex);
            surfaceReference = new SurfaceReference(this, descriptionIndex, brush, surfaceIndex);
            return true;
        }

        public override SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
        {
            if (!Node.Valid)
                return null;

            if (FindSurfaceReference(Node, brush, surfaceID, out var surfaceReference))
                return surfaceReference;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool GetAllSurfaces(CSGTreeNode node, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            switch (node.Type)
            {
                case CSGNodeType.Brush:  return GetAllSurfaces((CSGTreeBrush)node,  findBrush, surfaces);
                case CSGNodeType.Branch: return GetAllSurfaces((CSGTreeBranch)node, findBrush, surfaces);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool GetAllSurfaces(CSGTreeBranch branch, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            if (!branch.Valid)
                return false;

            for (int i = 0; i < branch.Count; i++)
            {
                var child = branch[i];
                if (GetAllSurfaces(child, findBrush, surfaces)) 
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool GetAllSurfaces(CSGTreeBrush brush, CSGTreeBrush? findBrush, List<SurfaceReference> surfaces)
        {
            if (!brush.Valid)
                return false;

            if (findBrush != null && findBrush?.NodeID != brush.NodeID)
                return true;

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(brush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return true;

            ref var brushMesh = ref brushMeshBlob.Value;
            for (int i = 0; i < brushMesh.polygons.Length; i++)
            {
                var surfaceIndex = i;
                var descriptionIndex = brushMesh.polygons[i].descriptionIndex;
                //surfaces.Add(new SurfaceReference(this, brushContainerAsset, 0, 0, i, surfaceID));
                surfaces.Add(new SurfaceReference(this, descriptionIndex, brush, surfaceIndex));
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool GetAllSurfaceReferences(CSGTreeBrush brush, List<SurfaceReference> surfaces)
        {
            if (!Node.Valid)
                return false;

            if (!GetAllSurfaces(Node, brush, surfaces))
                return false;
            return surfaces.Count > 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool GetAllSurfaceReferences(List<SurfaceReference> surfaces)
        {
            if (!Node.Valid)

            if (!GetAllSurfaces(Node, null, surfaces))
                    return false;
            return surfaces.Count > 0;
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
            try { UpdateGeneratorInternal(ref Node, userID: instanceID); }
            finally { Profiler.EndSample(); }

            if (!Node.Valid)
                return;

            //brushContainerAsset.SetDirty();

            Profiler.BeginSample("UpdateBrushMeshInstances");
            try { UpdateBrushMeshInstances(); }
            finally { Profiler.EndSample(); }
        }

        protected abstract bool UpdateGeneratorInternal(ref CSGTreeNode node, int userID);

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