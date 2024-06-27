using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
using System.Reflection;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

// Just a misc. collections of buttons we don't have a more sensible place for
namespace Chisel.Editors
{
    [EditorToolbarElement(id, typeof(SceneView))]
    class CenterPivotOnSelectionButton : EditorToolbarButton
    {
        public const string id       = nameof(ChiselGlobalToolbar) + "/" + nameof(CenterPivotOnSelectionButton);
        public const string kTooltip = "Center Pivot On Selection Center";
        public const string kIcon    = "centerPivotOnSelectionCenter";


        public CenterPivotOnSelectionButton()
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            this.clicked += OnClicked;
            ChiselSelectionManager.GeneratorSelectionUpdated += UpdateEnabledState;
            UpdateEnabledState();
        }

        protected void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreNodesSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        private void OnClicked()
        {
            PIvotUtility.CenterPivotOnSelection();
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class CenterPivotOnEachNodeInSelectionButton : EditorToolbarButton
    {
        public const string id          = nameof(ChiselGlobalToolbar) + "/" + nameof(CenterPivotOnEachNodeInSelectionButton);
        public const string kTooltip    = "Center Pivot On Each Node In Selection";
        public const string kIcon       = "centerPivotOnEach";


        public CenterPivotOnEachNodeInSelectionButton()
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            this.clicked += OnClicked;
            ChiselSelectionManager.GeneratorSelectionUpdated += UpdateEnabledState;
            UpdateEnabledState();
        }

        protected void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreNodesSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        private void OnClicked()
        {
            PIvotUtility.CenterPivotOnEachNodeInSelection();
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class RebuildAllButton : EditorToolbarButton
    {
        public const string id          = nameof(ChiselGlobalToolbar) + "/" + nameof(RebuildAllButton);
        public const string kTooltip    = "Force rebuild all generated meshes";
        public const string kIcon       = "rebuild";
        

        public RebuildAllButton() 
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            this.clicked += OnClicked;
        }

        private void OnClicked()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Rebuild");
            try { ChiselNodeHierarchyManager.Rebuild(); }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class ConvertToBrushesButton : EditorToolbarButton
    {
        public const string id          = nameof(ChiselGlobalToolbar) + "/" + nameof(ConvertToBrushesButton);
        public const string kTooltip    = "Convert generator to brush(es)";
        public const string kIcon       = "convertToBrush";
        

        public ConvertToBrushesButton()  
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            this.clicked += OnClicked;
            ChiselSelectionManager.GeneratorSelectionUpdated += UpdateEnabledState;
            UpdateEnabledState();
        }

        void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreGeneratorsSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        private void OnClicked()
        {
            UnityEngine.Profiling.Profiler.BeginSample("ConvertToBrushes");
            try
            {
                bool modified = false;
                foreach (var generator in ChiselSelectionManager.SelectedGenerators)
                {
                    if (!generator.TopTreeNode.Valid)
                        continue;
                    modified = ConvertToBrushes(generator) || modified;
                }

                if (modified)
                    EditorGUIUtility.ExitGUI();
            }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
        }

        // TODO: put somewhere else
        public static bool ConvertToBrushes(ChiselGeneratorComponent chiselNode)
        {
            chiselNode.OnValidate();
            if (!chiselNode.TopTreeNode.Valid)
                return false;

            var topGameObject       = chiselNode.gameObject;
            var gameObjectIsActive  = topGameObject.activeSelf;
            var surfaceDefinition   = chiselNode.SurfaceDefinition;
            var nodeTypeName        = chiselNode.ChiselNodeTypeName;

            // Destroying this Generator Component will destroy the treeNode
            // So we need to copy the treeNode
            var topNode = chiselNode.TopTreeNode;
            // Set the treeNode to default in the component
            // (so when we destroy it the original treeNode doesn't get destroyed)
            chiselNode.ResetTreeNodes(doNotDestroy: true);
            // Destroy the component
            UnityEditor.Undo.DestroyObjectImmediate(chiselNode);
            // ... and then destroy the treeNode ourselves after we're done with it

            var topNodeType = topNode.Type;

            bool result = false;
            try
            {
                // We set the gameobject to not be active, this will prevent a lot of messages to 
                // be send by Unity while we're still building the entire sub-hierarchy
                topGameObject.SetActive(false);
                var topComponent = ConvertTreeNodeToBrushes(topGameObject, in surfaceDefinition, topNode, chiselNode.PivotOffset);
                result = topComponent != null;
            }
            finally 
            { 
                // Activate the gameobject again (if it was active in the first place)
                topGameObject.SetActive(gameObjectIsActive);

                // Destroy the treeNode that was part of the original Generator Component
                if (topNode.Valid)
                    topNode.Destroy();
            }
            UnityEditor.Undo.SetCurrentGroupName((topNodeType == CSGNodeType.Brush) ? $"Converted {nodeTypeName} to brush" : $"Converted {nodeTypeName} to multiple brushes");
            return result;
        }
        
        static ChiselNode ConvertTreeNodeToBrushes(GameObject parent, in ChiselSurfaceDefinition surfaceDefinition, CSGTreeNode node, Vector3 pivotOffset)
        {
            if (node.Type == CSGNodeType.Brush)
            {
                var brushNode       = (CSGTreeBrush)node;
                var brushComponent  = ChiselComponentFactory.AddComponent<ChiselBrushComponent>(parent);
                //brushComponent.transform.SetLocal(brushNode.LocalTransformation);
                brushComponent.Operation = brushNode.Operation;
                brushComponent.PivotOffset = pivotOffset;

                ConvertBrush(brushNode, in surfaceDefinition, out var brushMesh, out var newSurfaceDefinition);
                brushComponent.surfaceDefinition = newSurfaceDefinition;
                brushComponent.definition = new ChiselBrushDefinition { brushOutline = brushMesh };
                return brushComponent;
            }

            if (node.Count == 1)
                return ConvertTreeNodeToBrushes(parent, in surfaceDefinition, node[0], pivotOffset);
            
            var compositeComponent = ChiselComponentFactory.AddComponent<ChiselComposite>(parent);
            //compositeComponent.transform.SetLocal(node.LocalTransformation);
            compositeComponent.Operation = node.Operation;
            var parentTransform = compositeComponent.transform;
            for (int i = 0; i < node.Count; i++)
                ConvertChildTreeNodesToGameObjects(parentTransform, in surfaceDefinition, node[i]);
            return compositeComponent;
        }
        
        static bool ConvertBrush(CSGTreeBrush srcBrush, in ChiselSurfaceDefinition srcSurfaceDefinition, out BrushMesh newBrushMesh, out ChiselSurfaceDefinition newSurfaceDefinition)
        {
            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(srcBrush.BrushMesh.BrushMeshID);
            newBrushMesh = BrushMeshManager.ConvertToBrushMesh(brushMeshBlob);

            newSurfaceDefinition = new ChiselSurfaceDefinition
            {
                surfaces = new ChiselSurface[newBrushMesh.polygons.Length]
            };
            for (int p = 0; p < newBrushMesh.polygons.Length; p++)
            {
                var oldDescriptionIndex = newBrushMesh.polygons[p].descriptionIndex;
                newSurfaceDefinition.surfaces[p] = srcSurfaceDefinition.surfaces[oldDescriptionIndex];
                newBrushMesh.polygons[p].descriptionIndex = p;
            }
            return true;
        }

        static ChiselNode ConvertChildTreeNodesToGameObjects(Transform parent, in ChiselSurfaceDefinition surfaceDefinition, CSGTreeNode node)
        {
            if (node.Type == CSGNodeType.Brush)
            {
                var brushNode       = (CSGTreeBrush)node;
                var brushComponent  = ChiselComponentFactory.Create<ChiselBrushComponent>("Brush", parent);
                brushComponent.transform.SetLocal(brushNode.LocalTransformation);
                brushComponent.Operation                 = brushNode.Operation;

                ConvertBrush(brushNode, in surfaceDefinition, out var brushMesh, out var newSurfaceDefinition);
                brushComponent.surfaceDefinition = newSurfaceDefinition;
                brushComponent.definition = new ChiselBrushDefinition { brushOutline = brushMesh };
                return brushComponent;
            } 
            
            var compositeComponent = ChiselComponentFactory.Create<ChiselComposite>("Composite", parent);
            //compositeComponent.transform.SetLocal(node.LocalTransformation);
            //compositeComponent.LocalTransformation = Matrix4x4.identity;
            compositeComponent.Operation = node.Operation;
            var parentTransform = compositeComponent.transform;
            for (int i = 0; i < node.Count; i++)
                ConvertChildTreeNodesToGameObjects(parentTransform, in surfaceDefinition, node[i]);
            return compositeComponent;
        }

    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class ChiselSetAdditiveOperationToggle : EditorToolbarToggle
    {
        public const string     id          = nameof(ChiselGlobalToolbar) + "/" + nameof(ChiselSetAdditiveOperationToggle);
        const CSGOperationType  kType       = CSGOperationType.Additive;
        const string            kName       = nameof(CSGOperationType.Additive);
        public const string     kTooltip    = kName + " boolean Operation";
        public const string     kIcon       = "csg_" + kName;

        public ChiselSetAdditiveOperationToggle()
        {
            UpdateEnabledState();
            ChiselSelectionManager.OperationNodesSelectionUpdated -= UpdateEnabledState;
            ChiselSelectionManager.OperationNodesSelectionUpdated += UpdateEnabledState;
            UpdateValue();
            ChiselSelectionManager.NodeOperationUpdated -= UpdateValue;
            ChiselSelectionManager.NodeOperationUpdated += UpdateValue;
            InitLabel();
        }

        protected override void InitLabel()
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            base.InitLabel();
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            if (newValue)
                ChiselSelectionManager.SetOperationForSelection(kType);
        }

        protected void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreOperationNodesSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        protected void UpdateValue()
        {
            var currentOperation = ChiselSelectionManager.OperationOfSelectedNodes;
            var mixedValue = !currentOperation.HasValue;
            //this.showMixedValue = mixedValue;
            bool newValue = !mixedValue && currentOperation.Value == kType;
            if (this.value != newValue)
                this.SetValueWithoutNotify(newValue);
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class ChiselSetSubtractiveOperationToggle : EditorToolbarToggle
    {
        public const string     id          = nameof(ChiselGlobalToolbar) + "/" + nameof(ChiselSetSubtractiveOperationToggle);
        const CSGOperationType  kType       = CSGOperationType.Subtractive;
        const string            kName       = nameof(CSGOperationType.Subtractive);
        public const string     kTooltip    = kName + " boolean Operation";
        public const string     kIcon       = "csg_" + kName;

        public ChiselSetSubtractiveOperationToggle()
        {
            UpdateEnabledState();
            ChiselSelectionManager.OperationNodesSelectionUpdated -= UpdateEnabledState;
            ChiselSelectionManager.OperationNodesSelectionUpdated += UpdateEnabledState;
            UpdateValue();
            ChiselSelectionManager.NodeOperationUpdated -= UpdateValue;
            ChiselSelectionManager.NodeOperationUpdated += UpdateValue;
            InitLabel();
        }

        protected override void InitLabel()
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            base.InitLabel();
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            if (newValue)
                ChiselSelectionManager.SetOperationForSelection(kType);
        }

        protected void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreOperationNodesSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        protected void UpdateValue()
        {
            var currentOperation = ChiselSelectionManager.OperationOfSelectedNodes;
            var mixedValue = !currentOperation.HasValue;
            //this.showMixedValue = mixedValue;
            bool newValue = !mixedValue && currentOperation.Value == kType;
            if (this.value != newValue)
                this.SetValueWithoutNotify(newValue);
        }
    }

    [EditorToolbarElement(id, typeof(SceneView))]
    class ChiselSetIntersectingOperationToggle : EditorToolbarToggle
    {
        public const string     id          = nameof(ChiselGlobalToolbar) + "/" + nameof(ChiselSetIntersectingOperationToggle);
        const CSGOperationType  kType       = CSGOperationType.Intersecting;
        const string            kName       = nameof(CSGOperationType.Intersecting);
        public const string     kTooltip    = kName + " boolean Operation";
        public const string     kIcon       = "csg_" + kName;

        public ChiselSetIntersectingOperationToggle()
        {
            UpdateEnabledState();
            ChiselSelectionManager.OperationNodesSelectionUpdated -= UpdateEnabledState;
            ChiselSelectionManager.OperationNodesSelectionUpdated += UpdateEnabledState;
            UpdateValue();
            ChiselSelectionManager.NodeOperationUpdated -= UpdateValue;
            ChiselSelectionManager.NodeOperationUpdated += UpdateValue;
            InitLabel();
        }

        protected override void InitLabel()
        {
            ChiselToolbarUtility.SetupToolbarElement(this, kIcon, kTooltip);
            base.InitLabel();
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            if (newValue)
                ChiselSelectionManager.SetOperationForSelection(kType);
        }

        protected void UpdateEnabledState()
        {
            bool enabled = ChiselSelectionManager.AreOperationNodesSelected;
            if (this.enabledSelf != enabled)
                this.SetEnabled(enabled);
        }

        protected void UpdateValue()
        {
            var currentOperation = ChiselSelectionManager.OperationOfSelectedNodes;
            var mixedValue = !currentOperation.HasValue;
            //this.showMixedValue = mixedValue;
            bool newValue = !mixedValue && currentOperation.Value == kType;
            if (this.value != newValue)
                this.SetValueWithoutNotify(newValue);
        }
    }

    [Overlay(typeof(SceneView), "Chisel Global")]
    public class ChiselGlobalToolbar : ToolbarOverlay // TODO: better name
    {
        ChiselGlobalToolbar() : base(
            ConvertToBrushesButton.id,
            ChiselSetAdditiveOperationToggle.id,
            ChiselSetSubtractiveOperationToggle.id,
            ChiselSetIntersectingOperationToggle.id,
            CenterPivotOnSelectionButton.id,
            CenterPivotOnEachNodeInSelectionButton.id,
            RebuildAllButton.id)
        {
        }
    }
}
