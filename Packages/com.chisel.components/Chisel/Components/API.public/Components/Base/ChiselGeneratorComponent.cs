using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Profiling;

namespace Chisel.Components
{
    public abstract class ChiselDefinedGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new DefinitionType();

        protected override void OnResetInternal()           { definition.Reset(); base.OnResetInternal(); }
        protected override void OnValidateInternal()        { definition.Validate(); base.OnValidateInternal(); }
        protected override void UpdateGeneratorInternal()   { brushContainerAsset.Generate(definition); }
    }

    public abstract class ChiselGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName = nameof(operation);

        // This ensures names remain identical, or a compile error occurs.
        public const string kBrushContainerAssetName = nameof(brushContainerAsset);


        [HideInInspector] CSGTreeNode[] Nodes = new CSGTreeNode[] { new CSGTreeBrush() };

        [SerializeField, HideInInspector] protected CSGOperationType operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected ChiselBrushContainerAsset brushContainerAsset;	// NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected Matrix4x4 localTransformation = Matrix4x4.identity;
        [SerializeField, HideInInspector] protected Vector3 pivotOffset = Vector3.zero;

        public CSGTreeNode TopNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Nodes[0]; } }
        bool ValidNodes { get { return (Nodes != null && Nodes.Length > 0) && Nodes[0].Valid; } }

#if UNITY_EDITOR
        public VisibilityState UpdateVisibility(UnityEditor.SceneVisibilityManager instance)
        {
            var resultState = VisibilityState.Unknown;
            var visible = !instance.IsHidden(gameObject);
            var pickingEnabled = !instance.IsPickingDisabled(gameObject);
            foreach (var node in Nodes)
            {
                if (!node.Valid)
                    continue;
                var nodeState = CSGManager.SetBrushState(node.NodeID, visible, pickingEnabled);
                resultState |= nodeState;
            }
            return resultState;
        }
#endif

        public override CSGTreeNode GetTreeNodeByIndex(int index)
        {
            if (index < 0 || index > Nodes.Length)
                return CSGTreeNode.InvalidNode;
            return Nodes[index];
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
#if UNITY_EDITOR
            {
                if (brushContainerAsset == null)
                    return;
                if (brushContainerAsset.owner == null)
                {
                    brushContainerAsset.owner = this;
                } else
                if (brushContainerAsset.owner != this)
                {
                    brushContainerAsset = Instantiate(brushContainerAsset);
                    brushContainerAsset.owner = this;
                }
            }
#endif
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
                    Nodes[0].Operation = operation;

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

            var localTransformationWithPivot = LocalTransformationWithPivot;

            // TODO: fix this mess, branches do not have transformations
            if (Nodes.Length == 1)
            {
                Nodes[0].LocalTransformation = localTransformationWithPivot;
            } else
            { 
                for (int i = 1; i < Nodes.Length; i++)
                    Nodes[i].LocalTransformation = localTransformationWithPivot;
            }
        }

        public ChiselBrushContainerAsset BrushContainerAsset
        {
            get { return brushContainerAsset; }
            set
            {
                if (value == brushContainerAsset)
                    return;

                // Set the new BrushContainerAsset as current
                brushContainerAsset = value;

                UpdateBrushMeshInstances();

                // Let the hierarchy manager know that the contents of this node has been modified
                //	so we can rebuild/update sub-trees and regenerate meshes
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        int RequiredNodeLength(BrushMeshInstance[] instances)
        {
            return (instances == null || instances.Length == 0) ? 0 : ((instances.Length == 1) ? 1 : instances.Length + 1);
        }

        bool InitializeBrushMeshInstances()
        {
            var instances			= brushContainerAsset ? brushContainerAsset.Instances : null;

            // TODO: figure out why this can happen (mess around with spiral stairs)
            // TODO: does this have anything to do with spiral stairs not updating all submeshes when being modified?
            if (instances != null &&
                instances.Length !=
                brushContainerAsset.SubMeshCount)
            {
                brushContainerAsset.UpdateInstances();
                instances = brushContainerAsset ? brushContainerAsset.Instances : null;
            }

            if (instances == null)
                return false;

            var requiredNodeLength	= RequiredNodeLength(instances);
            
            if (Nodes != null && Nodes.Length == requiredNodeLength)
            {
                if (Nodes.Length == 1)
                {
                    var brush = (CSGTreeBrush)TopNode;
                    brush.BrushMesh = brushContainerAsset.Instances[0];
                    brush.Operation = brushContainerAsset.Operations[0];
                } else
                {
                    for (int i = 0; i < instances.Length; i++)
                    {
                        var brush = (CSGTreeBrush)Nodes[i + 1];
                        brush.BrushMesh = brushContainerAsset.Instances[i];
                        brush.Operation = brushContainerAsset.Operations[i];
                    }
                }
                return true;
            } else
            {
                bool needRebuild = (Nodes != null && instances != null && instances.Length > 0) && Nodes.Length != requiredNodeLength;
                if (Nodes.Length == 1)
                {
                    var brush = (CSGTreeBrush)TopNode;
                    if (brush.BrushMesh != BrushMeshInstance.InvalidInstance)
                    {
                        brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                        brush.Operation = CSGOperationType.Additive;
                    }
                } else
                {
                    for (int i = 1; i < Nodes.Length; i++)
                    {
                        var brush = (CSGTreeBrush)Nodes[i];
                        if (brush.BrushMesh != BrushMeshInstance.InvalidInstance)
                        {
                            brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                            brush.Operation = CSGOperationType.Additive;
                        }
                    }
                }
                if (needRebuild) // if we don't do this, we'll end up creating nodes infinitely, when the node can't make a valid brushMesh
                    ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return false;
            }
        }

        public void GenerateAllTreeNodes()
        {
            var instanceID			= GetInstanceID();
            var instances			= brushContainerAsset ? brushContainerAsset.Instances : null;
            var requiredNodeLength	= RequiredNodeLength(instances);

            if (requiredNodeLength == 0)
            {
                Nodes = new CSGTreeNode[0];
                //Nodes[0] = CSGTreeBrush.Create(userID: instanceID, operation: operation);
            } else
            if (requiredNodeLength == 1)
            {
                Nodes = new CSGTreeNode[1];
                Nodes[0] = CSGTreeBrush.Create(userID: instanceID, operation: operation);
                Nodes[0].Operation = operation;
            } else
            {
                Nodes = new CSGTreeNode[requiredNodeLength];
                var children = new CSGTreeNode[requiredNodeLength - 1];
                for (int i = 0; i < requiredNodeLength - 1; i++)
                    children[i] = CSGTreeBrush.Create(userID: instanceID);

                Nodes[0] = CSGTreeBranch.Create(instanceID, operation: operation, children: children);
                for (int i = 1; i < Nodes.Length; i++)
                    Nodes[i] = children[i - 1];
                Nodes[0].Operation = operation;
            }
            UpdateInternalTransformation();
        }

        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            InitializeBrushMeshInstances();
            SetDirty();
            
            if (Nodes[0].Operation != operation)
                Nodes[0].Operation = operation;
        }

        internal override void ClearTreeNodes(bool clearCaches = false)
        {
            for (int i = 0; i < Nodes.Length; i++)
                Nodes[i].SetInvalid();
        }

        internal override CSGTreeNode[] CreateTreeNodes()
        {
            if (ValidNodes)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);
            
            
            Profiler.BeginSample("UpdateGenerator");
            UpdateGenerator();
            Profiler.EndSample();

            Profiler.BeginSample("GenerateAllTreeNodes");
            GenerateAllTreeNodes();
            Profiler.EndSample();

            if (Nodes.Length ==  0)
                return Nodes;
            
            Profiler.BeginSample("UpdateInternalTransformation");
            UpdateInternalTransformation();
            Profiler.EndSample();


            if (Nodes[0].Operation != operation)
                Nodes[0].Operation = operation;
            return Nodes;
        }

        public override int NodeID								{ get { return TopNode.NodeID; } }
        
        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            if (Nodes.Length == 1)
            {
                TopNode.SetDirty();
            } else
            {
                for (int i = 1; i < Nodes.Length; i++)
                    Nodes[i].SetDirty();
            }
        }

        public override void CollectCSGTreeNodes(List<CSGTreeNode> childNodes)
        {
            if (Nodes.Length > 0)
                childNodes.Add(TopNode);
        }

        public override bool GetUsedGeneratedBrushes(List<ChiselBrushContainerAsset> usedBrushes)
        {
            if (brushContainerAsset == null ||
                brushContainerAsset.BrushMeshes == null ||
                brushContainerAsset.BrushMeshes.Length == 0)
                return false;
            usedBrushes.Add(brushContainerAsset);
            return true;
        }

        // TODO: clean this up
        public delegate IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelfDelegate(CSGTreeBrush brush);
        public static GetSelectedVariantsOfBrushOrSelfDelegate GetSelectedVariantsOfBrushOrSelf;

        public override Bounds CalculateBounds()
        {
            if (!brushContainerAsset)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
            var bounds			= ChiselHierarchyItem.EmptyBounds;

            var foundBrushes    = new HashSet<CSGTreeBrush>();
            GetAllTreeBrushes(foundBrushes, false);
            foreach (var brush in foundBrushes)
            {
                if (!brush.Valid)
                    continue;
                var transformation  = modelMatrix * brush.NodeToTreeSpaceMatrix;
                var childBounds     = brushContainerAsset.CalculateBounds(transformation);
                var magnitude       = childBounds.size.sqrMagnitude;
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = transformation.GetColumn(3);
                    childBounds = new Bounds(center, Vector3.zero);
                }
                if (childBounds.size.sqrMagnitude != 0)
                {
                    if (bounds.size.sqrMagnitude == 0)
                        bounds = childBounds;
                    else
                        bounds.Encapsulate(childBounds);
                }
            }

            return bounds;
        }
        
        public override Bounds CalculateBounds(Matrix4x4 boundsTransformation)
        {
            if (!brushContainerAsset)
                return ChiselHierarchyItem.EmptyBounds;

            var modelMatrix		= ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
            var bounds			= ChiselHierarchyItem.EmptyBounds;

            var foundBrushes    = new HashSet<CSGTreeBrush>();
            GetAllTreeBrushes(foundBrushes, false);
            foreach (var brush in foundBrushes)
            {
                if (!brush.Valid)
                    continue;
                var transformation  = modelMatrix * brush.NodeToTreeSpaceMatrix * boundsTransformation;
                var childBounds     = brushContainerAsset.CalculateBounds(transformation);
                var magnitude       = childBounds.size.sqrMagnitude;
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = transformation.GetColumn(3);
                    childBounds = new Bounds(center, Vector3.zero);
                }
                if (childBounds.size.sqrMagnitude != 0)
                {
                    if (bounds.size.sqrMagnitude == 0)
                        bounds = childBounds;
                    else
                        bounds.Encapsulate(childBounds);
                }
            }

            return bounds;
        }
        
        public override int GetAllTreeBrushCount()
        {
            if (Nodes.Length > 1)
                return Nodes.Length - 1;
            return Nodes.Length;
        }

        // Get all brushes directly contained by this CSGNode (not its children)
        public override void GetAllTreeBrushes(HashSet<CSGTreeBrush> foundBrushes, bool ignoreSynchronizedBrushes)
        {
            if (Nodes.Length > 1)
            {
#if UNITY_EDITOR
                if (!ignoreSynchronizedBrushes)
                {
                    for (int i = 1; i < Nodes.Length; i++)
                        foundBrushes.AddRange(GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)Nodes[i]));
                } else
#endif
                {
                    for (int i = 1; i < Nodes.Length; i++)
                        foundBrushes.Add((CSGTreeBrush)Nodes[i]);
                }
            } else
            {
#if UNITY_EDITOR
                if (ignoreSynchronizedBrushes)
                    foundBrushes.AddRange(GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)TopNode));
                else
#endif
                    foundBrushes.Add((CSGTreeBrush)TopNode);
            }
        }

        public override ChiselBrushMaterial FindBrushMaterialBySurfaceIndex(CSGTreeBrush brush, int surfaceID)
        {
            if (!brushContainerAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;
                    
                    var brushMesh = brushContainerAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        return null;
                    
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

                    return surface.brushMaterial;
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;
                
                var brushMesh = brushContainerAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;
                
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

                return surface.brushMaterial;
            }
        }

        public override ChiselBrushMaterial[] GetAllBrushMaterials(CSGTreeBrush brush)
        {
            if (!brushContainerAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;

                    var brushMesh = brushContainerAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;
                    
                    var surfaces	= new HashSet<ChiselBrushMaterial>();
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                    {
                        var surface = brushMesh.polygons[i].surface;
                        if (surface == null)
                            continue;
                        surfaces.Add(surface.brushMaterial);
                    }

                    return surfaces.ToArray();
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;

                var surfaces = new HashSet<ChiselBrushMaterial>();
                var brushMesh = brushContainerAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;

                for (int i = 0; i < brushMesh.polygons.Length; i++)
                {
                    var surface = brushMesh.polygons[i].surface;
                    if (surface == null)
                        continue;
                    surfaces.Add(surface.brushMaterial);
                }

                return surfaces.ToArray();
            }
        }

        public override SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
        {
            if (!brushContainerAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;
                    
                    var brushMesh = brushContainerAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;
                    
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

                    return new SurfaceReference(this, brushContainerAsset, n, n - 1, surfaceIndex, surfaceID);
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;

                if (brushContainerAsset.SubMeshCount == 0)
                    return null;

                var brushMesh = brushContainerAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;
                
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
            }
        }

        public override SurfaceReference[] GetAllSurfaceReferences()
        {
            if (!brushContainerAsset)
                return null;
            if (Nodes.Length > 1)
            {
                var surfaces	= new HashSet<SurfaceReference>();
                for (int n = 1; n < Nodes.Length; n++)
                {
                    var brushMesh = brushContainerAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;
                    
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                    {
                        var surfaceID	= brushMesh.polygons[i].surfaceID;
                        surfaces.Add(new SurfaceReference(this, brushContainerAsset, n, n - 1, i, surfaceID));
                    }

                }
                return surfaces.ToArray();
            } else
            {
                if (brushContainerAsset.SubMeshCount == 0)
                    return null;

                var brushMesh = brushContainerAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;
                
                var surfaces	= new HashSet<SurfaceReference>();
                for (int i = 0; i < brushMesh.polygons.Length; i++)
                {
                    var surfaceID = brushMesh.polygons[i].surfaceID;
                    surfaces.Add(new SurfaceReference(this, brushContainerAsset, 0, 0, i, surfaceID));
                }
                return surfaces.ToArray();
            }
        }

        public override SurfaceReference[] GetAllSurfaceReferences(CSGTreeBrush brush)
        {
            if (!brushContainerAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;

                    var brushMesh = brushContainerAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;

                    var surfaces	= new HashSet<SurfaceReference>();
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                    {
                        var surfaceID	= brushMesh.polygons[i].surfaceID;
                        surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)Nodes[n], 
                                                            brushContainerAsset, n, n - 1, i, surfaceID));
                    }

                    return surfaces.ToArray();
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;
                
                var brushMesh = brushContainerAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;

                var surfaces	= new HashSet<SurfaceReference>();
                for (int i = 0; i < brushMesh.polygons.Length; i++)
                {
                    var surfaceID = brushMesh.polygons[i].surfaceID;
                    surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)TopNode, 
                                                        brushContainerAsset, 0, 0, i, surfaceID));
                }

                return surfaces.ToArray();
            }
        }

        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            PivotOffset += this.transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            base.AddPivotOffset(worldSpaceDelta);
        }

        public virtual void UpdateGenerator()
        {
            // BrushMeshes of generators must always be unique
            Profiler.BeginSample("ChiselBrushContainerAsset.Create");
            if (!brushContainerAsset ||
                !ChiselBrushContainerAssetManager.IsBrushMeshUnique(brushContainerAsset))
                brushContainerAsset = ChiselBrushContainerAsset.Create("Generated " + NodeTypeName);
            Profiler.EndSample();

            Profiler.BeginSample("UpdateGeneratorInternal");
            UpdateGeneratorInternal();
            Profiler.EndSample();

            brushContainerAsset.SetDirty();

            Profiler.BeginSample("UpdateBrushMeshInstances");
            UpdateBrushMeshInstances();
            Profiler.EndSample();
        }

        protected abstract void UpdateGeneratorInternal();

#if UNITY_EDITOR
        public override bool ConvertToBrushes()
        {
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
                    groupName = "Converted " + NodeTypeName + " to Multiple Brushes";
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
            return true;
        }
#endif

    }
}