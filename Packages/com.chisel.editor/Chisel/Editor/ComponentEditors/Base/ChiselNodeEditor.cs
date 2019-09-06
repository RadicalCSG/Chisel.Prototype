using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public abstract class ChiselNodeEditorBase : Editor
    {
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
    }

    public abstract class ChiselNodeEditor<T> : ChiselNodeEditorBase
        where T : ChiselNode
    {
        static readonly GUIContent kDefaultModelContents = new GUIContent("This node is not a child of a model, and is added to the default model. It is recommended that you explicitly add this node to a model.");

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

        public static void ForceUpdateNodeContents(SerializedObject serializedObject)
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var target in serializedObject.targetObjects)
            {
                var node = target as ChiselNode;
                if (!node)
                    continue;
                CSGNodeHierarchyManager.NotifyContentsModified(node);
                node.SetDirty();
            }
        }

        // This method is used by Component specific classes (e.g. ChiselBoxEditor) 
        // to create a menu Item that creates a gameObject with a specific component (e.g. ChiselBox)
        // Since MenuItems can only be created with attributes, and strings must be constant, 
        // we can only do this from place where we specifically know which component the menu is for.
        protected static void CreateAsGameObjectMenuCommand(MenuCommand menuCommand, string name)
        {
            var component   = ChiselComponentFactory.Create<T>(name, ChiselModelManager.GetActiveModelOrCreate());
            var gameObject  = component.gameObject;
            GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
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

        public static void ShowDefaultModelMessage(UnityEngine.Object[] targetObjects)
        {
            if (!IsPartOfDefaultModel(targetObjects))
                return;

            EditorGUILayout.HelpBox(kDefaultModelContents.text, MessageType.Warning);
        }

        static HashSet<ChiselNode> modifiedNodes = new HashSet<ChiselNode>();
        public static void CheckForTransformationChanges(SerializedObject serializedObject)
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

                    // TODO: probably not a good idea to use matrices for this, since it calculates this all the way up the transformation tree
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
                    CSGNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
                }
            }
        }

        static readonly GUIContent convertToBrushesContent  = new GUIContent("Convert to Brushes");
        static readonly GUIContent convertToBrushContent    = new GUIContent("Convert to Brush");


        // TODO: put somewhere else
        internal static void ConvertIntoBrushesButton(SerializedObject serializedObject)
        {
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
                if (!GUILayout.Button(convertToBrushesContent, GUILayout.ExpandHeight(true)))
                    return;
            } else
            if (singular)
            {
                if (!GUILayout.Button(convertToBrushContent, GUILayout.ExpandHeight(true)))
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

        class Styles
        {
            public GUIStyle[] leftButton = new GUIStyle[2];
            public GUIStyle[] midButton = new GUIStyle[2];
            public GUIStyle[] rightButton = new GUIStyle[2];

            public Styles()
            {
                leftButton[0] = new GUIStyle(EditorStyles.miniButtonLeft) { stretchWidth = false, stretchHeight = false };
                leftButton[0].padding.top += 1;
                leftButton[0].padding.bottom += 3;
                leftButton[1] = new GUIStyle(leftButton[0]);
                leftButton[1].normal.background = leftButton[0].active.background;

                midButton[0] = new GUIStyle(EditorStyles.miniButtonMid) { stretchWidth = false, stretchHeight = false };
                midButton[0].padding.top += 1;
                midButton[0].padding.bottom += 3;
                midButton[1] = new GUIStyle(midButton[0]);
                midButton[1].normal.background = midButton[0].active.background;

                rightButton[0] = new GUIStyle(EditorStyles.miniButtonRight) { stretchWidth = false, stretchHeight = false };
                rightButton[0].padding.top += 1;
                rightButton[0].padding.bottom += 3;
                rightButton[1] = new GUIStyle(rightButton[0]);
                rightButton[1].normal.background = rightButton[0].active.background;
            }
        };

        static Styles styles;

        static bool Toggle(bool selected, GUIContent[] content, GUIStyle[] style)
        {
            var selectedContent = selected ? content[1] : content[0];
            var selectedStyle = selected ? style[1] : style[0];

            return GUILayout.Button(selectedContent, selectedStyle);
        }

        public static void ShowOperationChoices(SerializedProperty operationProp)
        {
            EditorGUILayout.BeginHorizontal();
            ShowOperationChoicesInternal(operationProp);
            EditorGUILayout.EndHorizontal();
        }

        // TODO: put somewhere else
        public static void ShowOperationChoicesInternal(SerializedProperty operationProp)
        {
            if (operationProp == null)
                return;

            if (styles == null)
                styles = new Styles();

            const string AdditiveIconName = "csg_addition";
            const string SubtractiveIconName = "csg_subtraction";
            const string IntersectingIconName = "csg_intersection";

            const string AdditiveIconTooltip = "Additive CSG Operation";
            const string SubtractiveIconTooltip = "Subtractive CSG Operation";
            const string IntersectingIconTooltip = "Intersecting CSG Operation";

            var additiveIcon = ChiselEditorResources.GetIconContent(AdditiveIconName, AdditiveIconTooltip);
            var subtractiveIcon = ChiselEditorResources.GetIconContent(SubtractiveIconName, SubtractiveIconTooltip);
            var intersectingIcon = ChiselEditorResources.GetIconContent(IntersectingIconName, IntersectingIconTooltip);

            using (new EditorGUIUtility.IconSizeScope(new Vector2(16, 16)))     // This ensures that the icons will be the same size on regular displays and HDPI displays
                                                                                // Note that the loaded images are different sizes on different displays
            {
                var operation = operationProp.hasMultipleDifferentValues ? ((CSGOperationType)255) : ((CSGOperationType)operationProp.enumValueIndex);
                if (Toggle((operation == CSGOperationType.Additive), additiveIcon, styles.leftButton))
                    operationProp.enumValueIndex = (int)CSGOperationType.Additive;
                if (Toggle((operation == CSGOperationType.Subtractive), subtractiveIcon, styles.midButton))
                    operationProp.enumValueIndex = (int)CSGOperationType.Subtractive;
                if (Toggle((operation == CSGOperationType.Intersecting), intersectingIcon, styles.rightButton))
                    operationProp.enumValueIndex = (int)CSGOperationType.Intersecting;
            }
        }

        public override void OnInspectorGUI()
        {
            CheckForTransformationChanges(serializedObject);
            ShowDefaultModelMessage(serializedObject.targetObjects);
        }
    }

    public class ChiselDefaultGeneratorDetails : IChiselNodeDetails
    {
        const string kAdditiveIconName		= "csg_addition";
        const string kSubtractiveIconName	= "csg_subtraction";
        const string kIntersectingIconName	= "csg_intersection";
        
        public GUIContent GetHierarchyIconForGenericNode(ChiselNode node)
        {
            var generator = node as ChiselGeneratorComponent;
            if (generator == null)
                return GUIContent.none;
            switch (generator.Operation)
            {
                default:
                case CSGOperationType.Additive:     return ChiselEditorResources.GetIconContent(kAdditiveIconName,     $"Additive {node.NodeTypeName}")[0];
                case CSGOperationType.Subtractive:  return ChiselEditorResources.GetIconContent(kSubtractiveIconName,  $"Subtractive {node.NodeTypeName}")[0];
                case CSGOperationType.Intersecting: return ChiselEditorResources.GetIconContent(kIntersectingIconName, $"Intersecting {node.NodeTypeName}")[0];
            }
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
                    if (iterator.name == "definition") // TODO: use ChiselDefinedGeneratorComponent<DefinitionType>.kDefinitionName somehow
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


        protected virtual void ResetInspector() { ResetDefaultInspector(); } 
        protected virtual void InitInspector() { InitDefaultInspector(); }

        protected virtual void OnInspector() { OnDefaultInspector(); }

        protected virtual void OnTargetModifiedInInspector() { }
        protected virtual void OnTargetModifiedInScene() { }
        protected virtual bool OnGeneratorValidate(T generator) { return generator.isActiveAndEnabled; }
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
        }

        void OnEnable()
        {
            if (!target)
            {
                Reset();
                return;
            }

            operationProp = serializedObject.FindProperty(ChiselGeneratorComponent.kOperationFieldName);
            UpdateSelection();

            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
            InitInspector();
        }

        void UpdateSelection()
        {
            var selectedGameObject = Selection.gameObjects.ToList();
            var removeTargets = new HashSet<T>();
            foreach (var target in targets)
            {
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
            if (PreviewTextureManager.Update())
                Repaint();

            base.OnInspectorGUI();
            try
            {
                EditorGUI.BeginChangeCheck();
                {
                    GUILayout.BeginHorizontal();
                    ShowOperationChoicesInternal(operationProp);
                    if (typeof(T) != typeof(ChiselBrush)) ConvertIntoBrushesButton(serializedObject);
                    GUILayout.EndHorizontal();

                    OnInspector();
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public void OnSceneGUI()
        {
            if (!target || !ChiselEditModeManager.EditMode.EnableComponentEditors)
                return;

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

            var modelMatrix = CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
            var brush       = generator.TopNode;
            
            // NOTE: could loop over multiple instances from here, once we support that
            {
                using (new UnityEditor.Handles.DrawingScope(UnityEditor.Handles.yAxisColor, modelMatrix * brush.NodeToTreeSpaceMatrix))
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
