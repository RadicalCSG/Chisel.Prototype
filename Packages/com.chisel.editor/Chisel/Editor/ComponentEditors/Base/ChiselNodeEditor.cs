
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using SceneHandles = UnitySceneExtensions.SceneHandles;
using ControlState = UnitySceneExtensions.ControlState;
using HandleRendering = UnitySceneExtensions.HandleRendering;
using Grid = UnitySceneExtensions.Grid;
using UnityEditor.EditorTools;

namespace Chisel.Editors
{
    public abstract class ChiselNodeEditorBase : Editor
    {
        const string kDefaultOperationName = "Operation";

        // Ugly hack around stupid Unity issue
        static bool delayedUndoAllChanges = false;
        public static void UndoAllChanges()
        {
            delayedUndoAllChanges = true;
        }

        public static void HandleCancelEvent()
        {
            if (delayedUndoAllChanges)
            {
                delayedUndoAllChanges = false;
                Undo.RevertAllInCurrentGroup();
            }
        }


        static void SetMenuOperation(MenuCommand menuCommand, CSGOperationType operationType)
        {
            var context     = (menuCommand.context as GameObject);
            var gameObject  = (context == null) ? Selection.activeGameObject : context;
            if (!gameObject)
                return;

            var generator = gameObject.GetComponent<ChiselGeneratorComponent>();
            if (generator && generator.Operation != operationType)
            {
                Undo.RecordObject(generator, "Modified Operation");
                generator.Operation = operationType;
            }
            var operation = gameObject.GetComponent<ChiselOperation>();
            if (operation && operation.Operation != operationType)
            {
                Undo.RecordObject(generator, "Modified Operation");
                operation.Operation = operationType;
            }
        }

        static bool MenuValidateOperation(MenuCommand menuCommand)
        {
            var context     = (menuCommand.context as GameObject);
            var gameObject  = (context == null) ? Selection.activeGameObject : context;
            if (!gameObject)
                return false;

            return gameObject.GetComponent<ChiselGeneratorComponent>() ||
                    gameObject.GetComponent<ChiselOperation>();
        }

        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Additive), false, -1)] protected static void SetAdditiveOperation(MenuCommand menuCommand) { SetMenuOperation(menuCommand, CSGOperationType.Additive); }
        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Additive), true)] protected static bool ValidateAdditiveOperation(MenuCommand menuCommand) { return MenuValidateOperation(menuCommand); }
        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Subtractive), false, -1)] protected static void SetSubtractiveOperation(MenuCommand menuCommand) { SetMenuOperation(menuCommand, CSGOperationType.Subtractive); }
        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Subtractive), true)] protected static bool ValidateSubtractiveOperation(MenuCommand menuCommand) { return MenuValidateOperation(menuCommand); }
        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Intersecting), false, -1)] protected static void SetIntersectingOperation(MenuCommand menuCommand) { SetMenuOperation(menuCommand, CSGOperationType.Intersecting); }
        [MenuItem("GameObject/Chisel/Set operation/" + nameof(CSGOperationType.Intersecting), true)] protected static bool ValidateIntersectingOperation(MenuCommand menuCommand) { return MenuValidateOperation(menuCommand); }



        [MenuItem("GameObject/Group in Operation", false, -1)]
        protected static void EncapsulateInOperation(MenuCommand menuCommand)
        {
            var gameObjects = Selection.gameObjects;
            if (gameObjects == null ||
                gameObjects.Length <= 1)
                return;

            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i].GetComponent<ChiselGeneratorComponent>() ||
                    gameObjects[i].GetComponent<ChiselOperation>())
                    continue;
                return;
            }

            // TODO: sort gameObjects by their siblingIndex / hierarchy position
            
            var childTransform      = gameObjects[0].transform;
            var childSiblingIndex   = childTransform.GetSiblingIndex();
            var childParent         = childTransform.parent;

            var operation           = ChiselComponentFactory.Create<ChiselOperation>(kDefaultOperationName, childParent);
            var operationGameObject = operation.gameObject;
            var operationTransform  = operation.transform;
            operationTransform.SetSiblingIndex(childSiblingIndex);
            Undo.RegisterCreatedObjectUndo(operationGameObject, "Create " + operationGameObject.name);
            
            for (int i = 0; i < gameObjects.Length; i++)
                Undo.SetTransformParent(gameObjects[i].transform, operationTransform, "Moved GameObject under Operation");

            Selection.activeObject = operationGameObject;

            // This forces the operation to be opened when we create it
            EditorGUIUtility.PingObject(operationGameObject);
            for (int i = 0; i < gameObjects.Length; i++)
                EditorGUIUtility.PingObject(gameObjects[i]);
        }

        [MenuItem("GameObject/Group in Operation", true)]
        protected static bool ValidateEncapsulateInOperation(MenuCommand menuCommand)
        {
            var gameObjects = Selection.gameObjects;
            if (gameObjects == null ||
                gameObjects.Length == 1)
                return false;

            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i].GetComponent<ChiselGeneratorComponent>() ||
                    gameObjects[i].GetComponent<ChiselOperation>())
                    continue;
                return false;
            }
            return true;
        }

        public static bool InSceneSettingsContext = false;
    }

    public abstract class ChiselNodeEditor<T> : ChiselNodeEditorBase
        where T : ChiselNode
    {
        public virtual Bounds OnGetFrameBounds() { return CalculateBounds(targets); }
        public virtual bool HasFrameBounds() { if (!target) return false; return true; }

        public static Bounds CalculateBounds(UnityEngine.Object[] targets)
        {
            var bounds = new Bounds();
            foreach (var target in targets)
            {
                var node = target as ChiselNode;
                if (!node)
                    continue;

                node.EncapsulateBounds(ref bounds);
            }
            return bounds;
        }

        public static Vector3[] CalculateGridBounds(ChiselNode[] targetNodes)
        {
            var worldToGridSpace = Grid.ActiveGrid.WorldToGridSpace;
            var gridToWorldSpace = Grid.ActiveGrid.GridToWorldSpace;
            var bounds = new Bounds();
            foreach (var node in targetNodes)
            {
                if (!node)
                    continue;

                node.EncapsulateBounds(ref bounds, worldToGridSpace);
            }

            if (bounds.extents.sqrMagnitude == 0)
                return new[] { Vector3.zero };

            var min = bounds.min;
            var max = bounds.max;
            var points = new[]
            {
                Vector3.zero,
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, min.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, min.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, max.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, max.y, min.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, min.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, min.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(min.x, max.y, max.z)),
                gridToWorldSpace.MultiplyPoint(new Vector3(max.x, max.y, max.z)),
            };
            
            return points;
        }

        static Vector3[]    selectionBoundPoints;
        static bool         gridBoundsDirty = true;

        void ResetGridBounds()
        {
            gridBoundsDirty = true;
        }

        void UpdateGridBounds()
        {
            if (!gridBoundsDirty)
                return;
            selectionBoundPoints = CalculateGridBounds(targetNodes);
            gridBoundsDirty = false;
        }

        protected void OnShapeChanged()
        {
            ResetGridBounds();
        }

        public static void ForceUpdateNodeContents(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var target in serializedObject.targetObjects)
            {
                var node = target as ChiselNode;
                if (!node)
                    continue;
                ChiselNodeHierarchyManager.NotifyContentsModified(node);
                node.SetDirty();
            }
        }

        // This method is used by Component specific classes (e.g. ChiselBoxEditor) 
        // to create a menu Item that creates a gameObject with a specific component (e.g. ChiselBox)
        // Since MenuItems can only be created with attributes, and strings must be constant, 
        // we can only do this from place where we specifically know which component the menu is for.
        protected static void CreateAsGameObjectMenuCommand(MenuCommand menuCommand, string name)
        {
            T component;
            // If we use the command object on a gameobject in the hierarchy, choose that gameobject
            // Otherwise: choose the activeModel (if available)
            var context             = (menuCommand.context as GameObject);
            var parentGameObject    = (context != null) ? context : (ChiselModelManager.ActiveModel != null) ? ChiselModelManager.ActiveModel.gameObject : null;
            var parentTransform     = (parentGameObject == null) ? null : parentGameObject.transform;

            // If we used the command object on a generator, choose it's parent to prevent us from 
            // adding a generator as a child to a generator
            if (parentTransform &&
                parentTransform.GetComponent<ChiselGeneratorComponent>())
            {
                parentTransform = parentTransform.parent;
                parentGameObject = (parentTransform == null) ? null : parentTransform.gameObject;
            }

            // Create the gameobject
            if (parentTransform)
                component = ChiselComponentFactory.Create<T>(name, parentTransform);
            else
                component = ChiselComponentFactory.Create<T>(name);

            var gameObject  = component.gameObject;
            GameObjectUtility.SetParentAndAlign(gameObject, parentGameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);


            // Find the appropriate model to make active after we created the generator
            ChiselModel model;
            if (typeof(T) != typeof(ChiselModel))
            {
                model = gameObject.GetComponentInParent<ChiselModel>();
                // If we don't have a parent model, create one and put the generator underneath it
                if (!model)
                {
                    model = ChiselModelManager.CreateNewModel(gameObject.transform.parent);
                    
                    // Make sure we create the model at the exact same location as the generator
                    var modelGameObject     = model.gameObject;
                    var modelTransform      = model.transform;
                    var childSiblingIndex   = modelTransform.GetSiblingIndex();
                    modelTransform.SetSiblingIndex(childSiblingIndex);

                    Undo.RegisterCreatedObjectUndo(modelGameObject, "Create " + modelGameObject.name);
                    MoveTargetsUnderModel(new[] { component }, model);
                }
            } else
                model = component as ChiselModel;

            // Set the active model before we select the gameobject, otherwise we'll be selecting the model instead
            ChiselModelManager.ActiveModel = model;
            Selection.activeObject = gameObject;
        }

        public static bool IsPartOfDefaultModel(UnityEngine.Object[] targetObjects)
        {
            if (targetObjects == null)
                return false;

            for (int i = 0; i < targetObjects.Length; i++)
            {
                ChiselNode node = targetObjects[i] as ChiselNode;
                if (Equals(node, null))
                {
                    var gameObject = targetObjects[i] as GameObject;
                    if (gameObject)
                        node = gameObject.GetComponent<ChiselNode>();
                }
                if (node)
                {
                    if (ChiselGeneratedComponentManager.IsDefaultModel(node.hierarchyItem.Model))
                        return true;
                }
            }
            return false;
        }


        static readonly string     kDefaultModelContents = "This node is not a child of a model, " +
                                                           "It is recommended that you explicitly add this node to a model.";
        static readonly GUIContent kCreateAndAddToModel  = new GUIContent("Insert into new model");
        static readonly GUIContent kAddToActiveModel     = new GUIContent("Move to active model");

        static void MoveTargetsUnderModel(UnityEngine.Object[] targetObjects, ChiselModel model)
        {
            var modelTransform  = model.transform;
            var modelGameObject = model.gameObject;
            for (int t = 0; t < targetObjects.Length; t++)
            {
                var targetComponent = (targetObjects[t] as MonoBehaviour);
                var targetTransform = targetComponent.transform;
                Undo.SetTransformParent(targetTransform, modelTransform, "Moved GameObject under Model");
            }

            Selection.activeObject = modelGameObject;
            ChiselModelManager.ActiveModel = model;

            // This forces the model to be opened when we create it
            EditorGUIUtility.PingObject(modelGameObject);
            for (int t = 0; t < targetObjects.Length; t++)
                EditorGUIUtility.PingObject(targetObjects[t]);
        }

        public static void ShowDefaultModelMessage(UnityEngine.Object[] targetObjects)
        {
            if (!IsPartOfDefaultModel(targetObjects))
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(kDefaultModelContents, MessageType.Warning);
            if (GUILayout.Button(kCreateAndAddToModel))
            {
                // TODO: sort gameObjects by their siblingIndex / hierarchy position, pick top one

                var childTransform      = (targetObjects[0] as MonoBehaviour).transform;
                var childSiblingIndex   = childTransform.GetSiblingIndex();
                var childParent         = childTransform.parent;

                var model               = ChiselModelManager.CreateNewModel(childParent);
                var modelGameObject     = model.gameObject;
                var modelTransform      = model.transform;
                modelTransform.SetSiblingIndex(childSiblingIndex);
                Undo.RegisterCreatedObjectUndo(modelGameObject, "Create " + modelGameObject.name);
                MoveTargetsUnderModel(targetObjects, model);
            }
            if (ChiselModelManager.ActiveModel && GUILayout.Button(kAddToActiveModel))
            {
                // TODO: sort gameObjects by their siblingIndex / hierarchy position

                MoveTargetsUnderModel(targetObjects, ChiselModelManager.ActiveModel);
            }
            EditorGUILayout.EndVertical();
        }

        static HashSet<ChiselNode> modifiedNodes = new HashSet<ChiselNode>();
        public void CheckForTransformationChanges(SerializedObject serializedObject)
        {
            if (Event.current.type == EventType.Layout)
            {
                modifiedNodes.Clear();
                foreach (var target in serializedObject.targetObjects)
                {
                    var node = target as ChiselNode;
                    if (!node)
                        continue;

                    var transform = node.transform;

                    // TODO: probably not a good idea to use these matrices for this, since it calculates this all the way up the transformation tree
                    var curLocalToWorldMatrix = transform.localToWorldMatrix;
                    var oldLocalToWorldMatrix = node.hierarchyItem.LocalToWorldMatrix;
                    if (curLocalToWorldMatrix.m00 != oldLocalToWorldMatrix.m00 ||
                        curLocalToWorldMatrix.m01 != oldLocalToWorldMatrix.m01 ||
                        curLocalToWorldMatrix.m02 != oldLocalToWorldMatrix.m02 ||
                        curLocalToWorldMatrix.m03 != oldLocalToWorldMatrix.m03 ||

                        curLocalToWorldMatrix.m10 != oldLocalToWorldMatrix.m10 ||
                        curLocalToWorldMatrix.m11 != oldLocalToWorldMatrix.m11 ||
                        curLocalToWorldMatrix.m12 != oldLocalToWorldMatrix.m12 ||
                        curLocalToWorldMatrix.m13 != oldLocalToWorldMatrix.m13 ||

                        curLocalToWorldMatrix.m20 != oldLocalToWorldMatrix.m20 ||
                        curLocalToWorldMatrix.m21 != oldLocalToWorldMatrix.m21 ||
                        curLocalToWorldMatrix.m22 != oldLocalToWorldMatrix.m22 ||
                        curLocalToWorldMatrix.m23 != oldLocalToWorldMatrix.m23 //||

                        //curLocalToWorldMatrix.m30 != oldLocalToWorldMatrix.m30 ||
                        //curLocalToWorldMatrix.m31 != oldLocalToWorldMatrix.m31 ||
                        //curLocalToWorldMatrix.m32 != oldLocalToWorldMatrix.m32 ||
                        //curLocalToWorldMatrix.m33 != oldLocalToWorldMatrix.m33
                        )
                    {
                        node.hierarchyItem.LocalToWorldMatrix = curLocalToWorldMatrix;
                        node.hierarchyItem.WorldToLocalMatrix = transform.worldToLocalMatrix;
                        modifiedNodes.Add(node);
                    }
                }
                if (modifiedNodes.Count > 0)
                {
                    ChiselNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
                    ResetGridBounds(); // TODO: should only do this when rotating, not when moving
                }
            }
        }

        static readonly GUIContent convertToBrushesContent  = new GUIContent("Convert to Brushes");
        static readonly GUIContent convertToBrushContent    = new GUIContent("Convert to Brush");


        // TODO: put somewhere else
        internal static void ConvertIntoBrushesButton(Rect rect, SerializedObject serializedObject)
        {
            rect.height = 22;
            bool singular = false;
            bool multiple = false;
            foreach (var targetObject in serializedObject.targetObjects)
            {
                var node = targetObject as ChiselNode;
                if (!node)
                    continue;
                var count = node.GetAllTreeBrushCount();
                singular = (count == 1) || singular;
                multiple = (count > 1) || multiple;
            }
            if (multiple)
            {
                if (!GUI.Button(rect, convertToBrushesContent))
                    return;
            } else
            if (singular)
            {
                if (!GUI.Button(rect, convertToBrushContent))
                    return;
            } else
                return;

            bool modified = false;
            foreach (var targetObject in serializedObject.targetObjects)
            {
                var node = targetObject as ChiselNode;
                if (!node)
                    continue;

                modified = node.ConvertToBrushes() || modified;
            }

            if (modified)
                EditorGUIUtility.ExitGUI();
        }

        public void ShowInspectorHeader(SerializedProperty operationProp)
        {
            GUILayout.Space(3);
            const float kBottomPadding = 3;
            var rect = EditorGUILayout.GetControlRect(hasLabel: false, height: EditorGUIUtility.singleLineHeight + kBottomPadding);
            rect.yMax -= kBottomPadding;
            var buttonRect = rect;
            buttonRect.xMax -= ChiselOperationGUI.GetOperationChoicesInternalWidth(showAuto: false);
            if (typeof(T) != typeof(ChiselBrush))
            {
                ConvertIntoBrushesButton(buttonRect, serializedObject);
                ChiselOperationGUI.ShowOperationChoicesInternal(rect, operationProp, showLabel: false);
            } else
            {
                ChiselOperationGUI.ShowOperationChoicesInternal(rect, operationProp, showLabel: true);
            }
        }

        protected abstract void OnEditSettingsGUI(SceneView sceneView);

        ChiselNode[] targetNodes;


        protected virtual void InitInspector()
        {
            targetNodes = new ChiselNode[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                targetNodes[i] = targets[i] as ChiselNode;
            ResetGridBounds();

            Grid.GridModified -= OnGridModified;
            Grid.GridModified += OnGridModified;

            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified += OnNodeHierarchyModified;
        }

        protected virtual void ShutdownInspector()
        {
            Grid.GridModified -= OnGridModified;
            ChiselNodeHierarchyManager.NodeHierarchyModified -= OnNodeHierarchyModified;
        }

        private void OnNodeHierarchyModified()
        {
            ResetGridBounds();
            Repaint();
        }

        private void OnGridModified()
        {
            ResetGridBounds();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            CheckForTransformationChanges(serializedObject);
            ShowDefaultModelMessage(serializedObject.targetObjects);
        }

        static SceneHandles.PositionHandleIDs s_HandleIDs = new SceneHandles.PositionHandleIDs();
        static void OnMoveTool()
        {
            var position = Tools.handlePosition;
            var rotation = Tools.handleRotation;

            EditorGUI.BeginChangeCheck();
            // TODO: make this work with bounds!
            SceneHandles.Initialize(ref s_HandleIDs);
            selectionBoundPoints[0] = position;
            var newPosition = SceneHandles.PositionHandle(ref s_HandleIDs, selectionBoundPoints, position, rotation)[0];
            if (EditorGUI.EndChangeCheck())
            {
                var delta = newPosition - position;
                var transforms = Selection.transforms;
                if (transforms != null && transforms.Length > 0)
                {				
                    MoveTransformsTo(transforms, delta);
                }
            }

            if ((s_HandleIDs.combinedState & ControlState.Hot) == ControlState.Hot)
            {
                var handleSize = UnityEditor.HandleUtility.GetHandleSize(s_HandleIDs.originalPosition);
                SceneHandles.RenderBorderedCircle(s_HandleIDs.originalPosition, handleSize * 0.05f);
                var newHandleSize = UnityEditor.HandleUtility.GetHandleSize(newPosition);
                HandleRendering.DrawCameraAlignedCircle(newPosition, newHandleSize * 0.1f, Color.white, Color.black);
            }
        }

        protected void OnDefaultSceneTools()
        {
            UpdateGridBounds();

            // TODO: somehow make snapped controls work with *any* transform
            switch (Tools.current)
            {
                case Tool.Move:         Tools.hidden = true; OnMoveTool(); break;
                case Tool.Rotate:       Tools.hidden = false; break;// TODO: implement 
                case Tool.Scale:        Tools.hidden = false; break;// TODO: implement
                case Tool.Rect:         Tools.hidden = false; break;// TODO: implement
                case Tool.Transform:    Tools.hidden = false; break;// TODO: implement
                default:
                {
                    Tools.hidden = false;
                    break;
                }
            }
        }


        static void MoveTransformsTo(Transform[] transforms, Vector3 delta)
        {
            Undo.RecordObjects(transforms, "Move Transforms");
            foreach (var transform in transforms)
                transform.position += delta;
        }

        public virtual void OnSceneGUI()
        {
            if (!target)
                return;

            if (Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
            {
                OnDefaultSceneTools();
                return;
            }
        }
    }

    public class ChiselDefaultGeneratorDetails : IChiselNodeDetails
    {
        const string kAdditiveIconName          = "csg_" + nameof(CSGOperationType.Additive);
        const string kSubtractiveIconName       = "csg_" + nameof(CSGOperationType.Subtractive);
        const string kIntersectingIconName      = "csg_" + nameof(CSGOperationType.Intersecting);

        public static GUIContent GetHierarchyIcon(CSGOperationType operation, string name)
        {
            return GetIconContent(operation, name)[0];
        }

        public static GUIContent[] GetIconContent(CSGOperationType operation, string name)
        {
            switch (operation)
            {
                default:
                case CSGOperationType.Additive:     return ChiselEditorResources.GetIconContent(kAdditiveIconName,      $"{nameof(CSGOperationType.Additive)} {name}");
                case CSGOperationType.Subtractive:  return ChiselEditorResources.GetIconContent(kSubtractiveIconName,   $"{nameof(CSGOperationType.Subtractive)} {name}");
                case CSGOperationType.Intersecting: return ChiselEditorResources.GetIconContent(kIntersectingIconName,  $"{nameof(CSGOperationType.Intersecting)} {name}");
            }
        }
        
        public GUIContent GetHierarchyIconForGenericNode(ChiselNode node)
        {
            var generator = node as ChiselGeneratorComponent;
            if (generator == null)
                return GUIContent.none;

            return GetHierarchyIcon(generator.Operation, node.NodeTypeName);
        }

        public bool HasValidState(ChiselNode node)
        {
            var generator = node as ChiselGeneratorComponent;
            if (generator == null)
                return false;

            return node.HasValidState();
        }
    }

    public abstract class ChiselGeneratorEditor<T> : ChiselNodeEditor<T>
        where T : ChiselGeneratorComponent
    {
        protected void ResetDefaultInspector()
        {
            definitionSerializedProperty = null;
            children.Clear();
        }

        // Note: name is the same for every generator, but is hidden inside a generic class, hence the use of ChiselBrushDefinition
        const string kDefinitionName = ChiselDefinedGeneratorComponent<ChiselBrushDefinition>.kDefinitionName;

        List<SerializedProperty> children = new List<SerializedProperty>();
        SerializedProperty definitionSerializedProperty;
        protected void InitDefaultInspector()
        {
            ResetDefaultInspector();

            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.name == kDefinitionName)
                    {
                        definitionSerializedProperty = iterator.Copy();
                        break;
                    }
                } while (iterator.NextVisible(false));
            }

            if (definitionSerializedProperty == null)
                return;

            iterator = definitionSerializedProperty.Copy();
            if (iterator.NextVisible(true))
            {
                do
                {
                    //Debug.Log(iterator.name);
                    children.Add(iterator.Copy());
                } while (iterator.NextVisible(false));
            }
        }

        protected void OnDefaultInspector()
        {
            EditorGUI.BeginChangeCheck();
            {
                for (int i = 0; i < children.Count; i++)
                    EditorGUILayout.PropertyField(children[i], true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                OnTargetModifiedInInspector();
            }
        }

        protected void OnDefaultSettingsGUI(System.Object target, SceneView sceneView)
        {
            InSceneSettingsContext = true;
            try
            {
                serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        EditorGUILayout.PropertyField(children[i], true);
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    OnTargetModifiedInInspector();
                }
            }
            finally
            {
                InSceneSettingsContext = false;
            }
        }


        protected virtual void ResetInspector() { ResetDefaultInspector(); } 
        protected override void InitInspector() { base.InitInspector(); InitDefaultInspector(); }

        protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;

            // TODO: figure out how to make this work with multiple (different) editors when selecting a combination of nodes
            OnDefaultSettingsGUI(target, sceneView);
            ShowInspectorHeader(operationProp);
        }

        protected virtual void OnInspector() { OnDefaultInspector(); }

        protected virtual void OnTargetModifiedInInspector() { OnShapeChanged(); }
        protected virtual void OnTargetModifiedInScene() { OnShapeChanged(); }
        protected virtual bool OnGeneratorValidate(T generator) { return generator.isActiveAndEnabled && generator.HasValidState(); }
        protected virtual void OnGeneratorSelected(T generator) { }
        protected virtual void OnGeneratorDeselected(T generator) { }
        protected abstract void OnScene(SceneView sceneView, T generator);

        SerializedProperty operationProp;
        void Reset() { operationProp = null; ResetInspector(); }
        protected virtual void OnUndoRedoPerformed() { }

        private HashSet<UnityEngine.Object> knownTargets = new HashSet<UnityEngine.Object>();
        private HashSet<UnityEngine.Object> validTargets = new HashSet<UnityEngine.Object>();

        void OnDisable()
        {
            UpdateSelection();
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            PreviewTextureManager.CleanUp();
            Reset();
            ChiselEditGeneratorTool.OnEditSettingsGUI = null;
            ChiselEditGeneratorTool.CurrentEditorName = null;
            Tools.hidden = false;

            EditorTools.activeToolChanged -= OnToolModeChanged;
            ShutdownInspector();
        }

        void OnEnable()
        {
            if (!target)
            {
                Reset();
                return;
            }

            EditorTools.activeToolChanged -= OnToolModeChanged;
            EditorTools.activeToolChanged += OnToolModeChanged;

            ChiselEditGeneratorTool.OnEditSettingsGUI = OnEditSettingsGUI;
            ChiselEditGeneratorTool.CurrentEditorName = (target as T).NodeTypeName;
            operationProp = serializedObject.FindProperty(ChiselGeneratorComponent.kOperationFieldName);
            UpdateSelection();

            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
            InitInspector();
        }

        void OnToolModeChanged()
        {
            if (Tools.current != Tool.Custom)
            {
                ChiselEditToolBase.ClearLastRememberedType();
                return;
            }
            if (!typeof(ChiselEditToolBase).IsAssignableFrom(EditorTools.activeToolType))
            {
                ChiselEditToolBase.ClearLastRememberedType();
            }
        }

        protected bool HasValidState()
        {
            foreach (var target in targets)
            {
                var generator = target as T;
                if (!generator)
                    continue;
                if (!generator.HasValidState())
                    return false;
            }
            return true;
        }

        void UpdateSelection()
        {
            var selectedGameObject = Selection.gameObjects.ToList();
            var removeTargets = new HashSet<T>();
            foreach (var target in targets)
            {
                if (!target)
                    continue;

                if (!knownTargets.Add(target))
                    continue;
                
                var generator = target as T;
                if (!OnGeneratorValidate(generator))
                    continue;
                
                OnGeneratorSelected(target as T);
                validTargets.Add(generator);
            }
            
            foreach (var knownTarget in knownTargets)
            {
                if (!targets.Contains(knownTarget))
                {
                    var removeTarget = target as T;
                    if (validTargets.Contains(removeTarget))
                    {
                        OnGeneratorDeselected(removeTarget);
                        validTargets.Remove(removeTarget);
                    }
                    removeTargets.Add(removeTarget);
                } else
                {
                    var removeTarget = target as T;
                    if (removeTarget == null ||
                        !selectedGameObject.Contains(removeTarget.gameObject))
                    {
                        OnGeneratorDeselected(removeTarget);
                        validTargets.Remove(removeTarget);
                        removeTargets.Add(removeTarget);
                        continue;
                    }
                }
            }

            foreach (var removeTarget in removeTargets)
                knownTargets.Remove(removeTarget);
        }

        public override void OnInspectorGUI()
        {
            if (!target)
                return;
            serializedObject.Update();

            base.OnInspectorGUI();
            try
            {
                EditorGUI.BeginChangeCheck();
                {
                    ShowInspectorHeader(operationProp);
                    OnInspector();
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }

            if (PreviewTextureManager.Update())
                Repaint();
        }

        public override void OnSceneGUI()
        {
            if (!target)
                return;

            if (Tools.current != Tool.Custom || !ChiselEditGeneratorTool.IsActive())
            {
                OnDefaultSceneTools();
                return;
            }

            var generator = target as T;
            if (GUIUtility.hotControl == 0)
            {
                if (!OnGeneratorValidate(generator))
                {
                    if (validTargets.Contains(generator))
                    {
                        OnGeneratorDeselected(generator);
                        validTargets.Remove(generator);
                    }
                    return;
                }
                if (!validTargets.Contains(generator))
                {
                    OnGeneratorSelected(generator);
                    validTargets.Add(generator);
                }
            }

            var sceneView   = SceneView.currentDrawingSceneView;

            var modelMatrix     = ChiselNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
            var generatorNode   = generator.TopNode;
            if (!generatorNode.Valid)
                return;
            
            // NOTE: could loop over multiple instances from here, once we support that
            {
                using (new UnityEditor.Handles.DrawingScope(UnityEditor.Handles.yAxisColor, modelMatrix * generator.LocalTransformationWithPivot))
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        OnScene(sceneView, generator);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        OnTargetModifiedInScene();
                    }
                }
            }
        }
    }
}
