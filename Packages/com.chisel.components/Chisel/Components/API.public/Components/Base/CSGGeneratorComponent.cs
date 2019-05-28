using UnityEngine;
using System.Collections;
using Chisel.Core;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Chisel.Components
{
    public abstract class CSGGeneratorComponent : CSGNode
    {
        public CSGGeneratorComponent() : base() {  }

        public CSGTreeNode	TopNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Nodes[0]; } }

        public override CSGTreeNode	GetTreeNodeByIndex(int index)
        {
            if (index < 0 || index > Nodes.Length)
                return CSGTreeNode.InvalidNode;
            return Nodes[index];
        }

        [HideInInspector] CSGTreeNode[] Nodes = new CSGTreeNode[] { new CSGTreeBrush() };

        [SerializeField,HideInInspector] protected CSGOperationType		operation;		// NOTE: do not rename, name is directly used in editors
        [SerializeField,HideInInspector] protected ChiselGeneratedBrushes	brushMeshAsset;	// NOTE: do not rename, name is directly used in editors
        [SerializeField,HideInInspector] protected Matrix4x4			localTransformation = Matrix4x4.identity;
        [SerializeField,HideInInspector] protected Vector3				pivotOffset			= Vector3.zero;

        bool ValidNodes { get { return (Nodes != null && Nodes.Length > 0) && Nodes[0].Valid; } }
        
        protected override void OnResetInternal()
        {
            UpdateGenerator();
            UpdateBrushMeshInstances();
            base.OnResetInternal();
        }

        //**Temporary hack to ensure that a BrushMeshAsset remains unique when duplicated so that we can control when we share a BrushMeshAsset**//
        #region HandleDuplication
#if UNITY_EDITOR
        [SerializeField, HideInInspector] protected int instanceID = 0;
        [SerializeField, HideInInspector] protected int genGuidHashCode = 0;
#endif
        void HandleDuplication()
        {
#if UNITY_EDITOR
            {
                var currentInstanceID = this.GetInstanceID();
                if (instanceID == 0) { instanceID = currentInstanceID; genGuidHashCode = Guid.NewGuid().GetHashCode(); }
                else if (instanceID != currentInstanceID)
                {
                    var prevObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceID) as CSGGeneratorComponent;
                    // if our stored instanceID is the same as an existing generator and has the same guid, 
                    // we can assume we've been duplicated
                    if (prevObject && prevObject.genGuidHashCode == genGuidHashCode)
                    {
                        if (prevObject.brushMeshAsset == brushMeshAsset)
                            brushMeshAsset = Instantiate(brushMeshAsset);
                        genGuidHashCode = Guid.NewGuid().GetHashCode();
                    }
                    instanceID = currentInstanceID;
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
                return;

            UpdateGenerator();
            UpdateBrushMeshInstances();

            CSGNodeHierarchyManager.NotifyContentsModified(this);
            base.OnValidateInternal();
        }

        public CSGOperationType     Operation
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
                CSGNodeHierarchyManager.NotifyContentsModified(this);
            }
        }
        
        public Vector3     PivotOffset
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
                CSGNodeHierarchyManager.UpdateTreeNodeTranformation(this);
            }
        }

        public Matrix4x4			LocalTransformation
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
                CSGNodeHierarchyManager.UpdateTreeNodeTranformation(this);
            }
        }

        Matrix4x4 TopTransformation
        {
            get
            {
                var finalTransformation = localTransformation;
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    finalTransformation *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);
                return finalTransformation;
            }
        }

        void UpdateInternalTransformation()
        {
            if (!ValidNodes)
                return;

            Nodes[0].LocalTransformation = TopTransformation;
        }

        public ChiselGeneratedBrushes BrushMeshAsset
        {
            get { return brushMeshAsset; }
            set
            {
                if (value == brushMeshAsset)
                    return;

                // Set the new BrushMeshAsset as current
                brushMeshAsset = value;

                UpdateBrushMeshInstances();

                // Let the hierarchy manager know that the contents of this node has been modified
                //	so we can rebuild/update sub-trees and regenerate meshes
                CSGNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        int RequiredNodeLength(BrushMeshInstance[] instances)
        {
            return (instances == null || instances.Length == 0) ? 0 : ((instances.Length == 1) ? 1 : instances.Length + 1);
        }

        bool InitializeBrushMeshInstances()
        {
            var instances			= brushMeshAsset ? brushMeshAsset.Instances : null;

            // TODO: figure out why this can happen (mess around with spiral stairs)
            // TODO: does this have anything to do with spiral stairs not updating all submeshes when being modified?
            if (instances != null &&
                instances.Length !=
                brushMeshAsset.SubMeshCount)
            {
                brushMeshAsset.UpdateInstances();
                instances = brushMeshAsset ? brushMeshAsset.Instances : null;
            }

            var requiredNodeLength	= RequiredNodeLength(instances);
            
            if (Nodes != null && Nodes.Length == requiredNodeLength)
            {
                if (Nodes.Length == 0)
                {
                    var brush = (CSGTreeBrush)Nodes[0];
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                    brush.Operation = CSGOperationType.Additive;
                } else
                if (Nodes.Length == 1)
                {
                    var brush = (CSGTreeBrush)TopNode;
                    brush.BrushMesh = brushMeshAsset.Instances[0];
                    brush.Operation = brushMeshAsset.Operations[0];
                } else
                {
                    for (int i = 0; i < instances.Length; i++)
                    {
                        var brush = (CSGTreeBrush)Nodes[i + 1];
                        brush.BrushMesh = brushMeshAsset.Instances[i];
                        brush.Operation = brushMeshAsset.Operations[i];
                    }
                }
                return true;
            } else
            {
                bool needRebuild = (Nodes != null && instances != null && instances.Length > 0) && Nodes.Length != requiredNodeLength;
                if (Nodes.Length <= 1)
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
                    CSGNodeHierarchyManager.RebuildTreeNodes(this);
                return false;
            }
        }

        public void GenerateAllTreeNodes()
        {
            var instanceID			= GetInstanceID();
            var instances			= brushMeshAsset ? brushMeshAsset.Instances : null;
            var requiredNodeLength	= RequiredNodeLength(instances);

            if (requiredNodeLength == 0)
            {
                Nodes = new CSGTreeNode[1];
                Nodes[0] = CSGTreeBrush.Create(userID: instanceID, localTransformation: TopTransformation, operation: operation);
            } else
            if (requiredNodeLength == 1)
            {
                Nodes = new CSGTreeNode[1];
                Nodes[0] = CSGTreeBrush.Create(userID: instanceID, localTransformation: TopTransformation, operation: operation);
            } else
            {
                Nodes = new CSGTreeNode[requiredNodeLength];
                var children = new CSGTreeNode[requiredNodeLength - 1];
                for (int i = 0; i < requiredNodeLength - 1; i++)
                    children[i] = CSGTreeBrush.Create(userID: instanceID);

                Nodes[0] = CSGTreeBranch.Create(instanceID, operation, children);
                for (int i = 1; i < Nodes.Length; i++)
                    Nodes[i] = children[i - 1];
            }
            Nodes[0].Operation = operation;
            Nodes[0].LocalTransformation = TopTransformation;
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

        internal override void UpdateTransformation()
        {
            // TODO: recalculate transformation based on hierarchy up to (but not including) model
            var transform = hierarchyItem.Transform;
            if (!transform)
                return;

            var localToWorldMatrix = transform.localToWorldMatrix;
            var modelTransform = CSGNodeHierarchyManager.FindModelTransformOfTransform(transform);
            if (modelTransform)
                localTransformation = modelTransform.worldToLocalMatrix * localToWorldMatrix;
            else
                localTransformation = localToWorldMatrix;

            if (ValidNodes)
                UpdateInternalTransformation();
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
            
            
            UpdateGenerator();
            UpdateBrushMeshInstances();

            GenerateAllTreeNodes();

            InitializeBrushMeshInstances();
            
            UpdateInternalTransformation();


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

        internal override void CollectChildNodesForParent(List<CSGTreeNode> childNodes)
        {
            childNodes.Add(TopNode);
        }

        public override ChiselGeneratedBrushes[] GetUsedBrushMeshAssets()
        {
            return new ChiselGeneratedBrushes[] { brushMeshAsset };
        }

        // TODO: clean this up
        public delegate IEnumerable<CSGTreeBrush> GetSelectedVariantsOfBrushOrSelfDelegate(CSGTreeBrush brush);
        public static GetSelectedVariantsOfBrushOrSelfDelegate GetSelectedVariantsOfBrushOrSelf;

        public override Bounds CalculateBounds()
        {
            if (!brushMeshAsset)
                return CSGHierarchyItem.EmptyBounds;

            var modelMatrix		= CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(hierarchyItem.Transform);
            var bounds			= CSGHierarchyItem.EmptyBounds;

            var foundBrushes = new HashSet<CSGTreeBrush>();
            GetAllTreeBrushes(foundBrushes, false);
            foreach (var brush in foundBrushes)
            {
                var transformation = modelMatrix * brush.NodeToTreeSpaceMatrix;
                var assetBounds = brushMeshAsset.CalculateBounds(transformation);
                var magnitude = assetBounds.size.sqrMagnitude;
                if (float.IsInfinity(magnitude) ||
                    float.IsNaN(magnitude))
                {
                    var center = transformation.GetColumn(3);
                    assetBounds = new Bounds(center, Vector3.zero);
                }
                if (assetBounds.size.sqrMagnitude != 0)
                {
                    if (bounds.size.sqrMagnitude == 0)
                        bounds = assetBounds;
                    else
                        bounds.Encapsulate(assetBounds);
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

        public override ChiselBrushMaterial FindBrushMaterial(CSGTreeBrush brush, int surfaceID)
        {
            if (!brushMeshAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;
                    
                    var brushMesh = brushMeshAsset.BrushMeshes[n - 1];
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

                    return brushMesh.polygons[surfaceIndex].brushMaterial;
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;
                
                var brushMesh = brushMeshAsset.BrushMeshes[0];
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

                return brushMesh.polygons[surfaceIndex].brushMaterial;
            }
        }

        public override ChiselBrushMaterial[] GetAllBrushMaterials(CSGTreeBrush brush)
        {
            if (!brushMeshAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;

                    var brushMesh = brushMeshAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;
                    
                    var surfaces	= new HashSet<ChiselBrushMaterial>();
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                        surfaces.Add(brushMesh.polygons[i].brushMaterial);

                    return surfaces.ToArray();
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;

                var surfaces = new HashSet<ChiselBrushMaterial>();
                var brushMesh = brushMeshAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;
                
                for (int i = 0; i < brushMesh.polygons.Length; i++)
                    surfaces.Add(brushMesh.polygons[i].brushMaterial);

                return surfaces.ToArray();
            }
        }

        public override SurfaceReference FindSurfaceReference(CSGTreeBrush brush, int surfaceID)
        {
            if (!brushMeshAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;
                    
                    var brushMesh = brushMeshAsset.BrushMeshes[n - 1];
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

                    return new SurfaceReference(this, brushMeshAsset, n, n - 1, surfaceIndex, surfaceID);
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;

                if (brushMeshAsset.SubMeshCount == 0)
                    return null;

                var brushMesh = brushMeshAsset.BrushMeshes[0];
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
                
                return new SurfaceReference(this, brushMeshAsset, 0, 0, surfaceIndex, surfaceID);
            }
        }

        public override SurfaceReference[] GetAllSurfaceReferences()
        {
            if (!brushMeshAsset)
                return null;
            if (Nodes.Length > 1)
            {
                var surfaces	= new HashSet<SurfaceReference>();
                for (int n = 1; n < Nodes.Length; n++)
                {
                    var brushMesh = brushMeshAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;
                    
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                    {
                        var surfaceID	= brushMesh.polygons[i].surfaceID;
                        surfaces.Add(new SurfaceReference(this, brushMeshAsset, n, n - 1, i, surfaceID));
                    }

                }
                return surfaces.ToArray();
            } else
            {
                if (brushMeshAsset.SubMeshCount == 0)
                    return null;

                var brushMesh = brushMeshAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;
                
                var surfaces	= new HashSet<SurfaceReference>();
                for (int i = 0; i < brushMesh.polygons.Length; i++)
                {
                    var surfaceID = brushMesh.polygons[i].surfaceID;
                    surfaces.Add(new SurfaceReference(this, brushMeshAsset, 0, 0, i, surfaceID));
                }
                return surfaces.ToArray();
            }
        }

        public override SurfaceReference[] GetAllSurfaceReferences(CSGTreeBrush brush)
        {
            if (!brushMeshAsset)
                return null;
            if (Nodes.Length > 1)
            {
                for (int n = 1; n < Nodes.Length; n++)
                {
                    if (brush.NodeID != Nodes[n].NodeID)
                        continue;

                    var brushMesh = brushMeshAsset.BrushMeshes[n - 1];
                    if (brushMesh == null)
                        continue;

                    var surfaces	= new HashSet<SurfaceReference>();
                    for (int i = 0; i < brushMesh.polygons.Length; i++)
                    {
                        var surfaceID	= brushMesh.polygons[i].surfaceID;
                        surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)Nodes[n], 
                                                            brushMeshAsset, n, n - 1, i, surfaceID));
                    }

                    return surfaces.ToArray();
                }
                return null;
            } else
            {
                if (brush.NodeID != TopNode.NodeID)
                    return null;
                
                var brushMesh = brushMeshAsset.BrushMeshes[0];
                if (brushMesh == null)
                    return null;

                var surfaces	= new HashSet<SurfaceReference>();
                for (int i = 0; i < brushMesh.polygons.Length; i++)
                {
                    var surfaceID = brushMesh.polygons[i].surfaceID;
                    surfaces.Add(new SurfaceReference(this, //(CSGTreeBrush)TopNode, 
                                                        brushMeshAsset, 0, 0, i, surfaceID));
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
            if (!brushMeshAsset ||
                !ChiselGeneratedBrushesManager.IsBrushMeshUnique(brushMeshAsset))
            {
                brushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
                brushMeshAsset.name = "Generated " + NodeTypeName;
            }

            UpdateGeneratorInternal();

            UpdateBrushMeshInstances();
        }

        protected abstract void UpdateGeneratorInternal();

#if UNITY_EDITOR
        public override bool ConvertToBrushes()
        {
            var topGameObject = this.gameObject;
            UnityEditor.Undo.DestroyObjectImmediate(this);
            topGameObject.SetActive(false);
            bool success = false;
            try
            {
                if (brushMeshAsset.SubMeshCount == 1)
                {
                    var brush = UnityEditor.Undo.AddComponent<CSGBrush>(topGameObject);
                    brush.Operation = this.operation;
                    brush.BrushMeshAsset = brushMeshAsset;
                    brush.LocalTransformation = localTransformation;
                    brush.PivotOffset = pivotOffset;
                    UnityEditor.Undo.SetCurrentGroupName("Converted Shape to Brush");
                } else
                {
                    var operationComponent = UnityEditor.Undo.AddComponent<CSGOperation>(topGameObject);
                    operationComponent.Operation = this.operation;
                    var parentTransform = topGameObject.transform;
                    for (int i = 0; i < brushMeshAsset.SubMeshCount; i++)
                    {
                        var newBrushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
                        newBrushMeshAsset.SetSubMeshes(new[] { new BrushMesh(brushMeshAsset.BrushMeshes[i]) });
                        var brushGameObject = new GameObject("Brush (" + (i + 1) + ")");
                        UnityEditor.Undo.RegisterCreatedObjectUndo(brushGameObject, "Created GameObject");
                        brushGameObject.SetActive(false);
                        try
                        {
                            var brushTransform = brushGameObject.transform;
                            UnityEditor.Undo.SetTransformParent(brushTransform, parentTransform, "Move child brush underneath parent operation");
                            UnityEditor.Undo.RecordObject(brushTransform, "Reset child brush transform");
                            brushTransform.localPosition = Vector3.zero;
                            brushTransform.localRotation = Quaternion.identity;
                            brushTransform.localScale = Vector3.one;

                            var brush = UnityEditor.Undo.AddComponent<CSGBrush>(brushGameObject);
                            brush.BrushMeshAsset = newBrushMeshAsset;
                            brush.LocalTransformation = localTransformation;
                            brush.PivotOffset = pivotOffset;
                            brush.Operation = brushMeshAsset.Operations[i];
                        }
                        finally
                        {
                            brushGameObject.SetActive(true);
                        }
                    }
                    UnityEditor.Undo.SetCurrentGroupName("Converted " + NodeTypeName + " to Multiple Brushes");
                }
                success = true;
            }
            finally
            {
                topGameObject.SetActive(true);
            }
            return success;
        }
#endif

    }
}