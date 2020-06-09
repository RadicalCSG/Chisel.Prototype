using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public sealed class ChiselOperationDetails : ChiselNodeDetails<ChiselOperation>
    {
        public override GUIContent GetHierarchyIcon(ChiselOperation node)
        {
            return ChiselDefaultGeneratorDetails.GetHierarchyIcon(node.Operation, node.NodeTypeName);
        }

        public override bool HasValidState(ChiselOperation node)
        {
            return node.HasValidState();
        }
    }

    [CustomEditor(typeof(ChiselOperation))]
    [CanEditMultipleObjects]
    public sealed class ChiselOperationEditor : ChiselNodeEditor<ChiselOperation>
    {
        const string kOperationHasNoChildren = "This operation has no chisel nodes as children and will not create any geometry.\nAdd some chisel nodes to see something.";

        [MenuItem("GameObject/Chisel/Create/" + ChiselOperation.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselOperation.kNodeTypeName); }

        SerializedProperty operationProp;
        SerializedProperty passThroughProp;

        internal void OnEnable()
        {
            if (!target)
            {
                operationProp = null;
                passThroughProp = null;
                return;
            }

            // Fetch the objects from the GameObject script to display in the inspector
            operationProp   = serializedObject.FindProperty(ChiselOperation.kOperationFieldName);
            passThroughProp = serializedObject.FindProperty(ChiselOperation.kPassThroughFieldName);

            ChiselEditGeneratorTool.OnEditSettingsGUI = OnEditSettingsGUI;
            ChiselEditGeneratorTool.CurrentEditorName = "Operation";
        }

        internal void OnDisable()
        {
            operationProp = null;
            passThroughProp = null;
            ChiselEditGeneratorTool.OnEditSettingsGUI = null;
            ChiselEditGeneratorTool.CurrentEditorName = null;
        }
        
        protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;

            ShowInspectorHeader(operationProp);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            try
            {
                bool passThroughChanged = false;
                EditorGUI.BeginChangeCheck();
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(passThroughProp);
                    }
                    if (EditorGUI.EndChangeCheck()) { passThroughChanged = true; }
                    if (!passThroughProp.boolValue)
                        EditorGUILayout.PropertyField(operationProp);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    if (passThroughChanged)
                    {
                        foreach (var target in serializedObject.targetObjects)
                        {
                            var operation = target as ChiselOperation;
                            if (!operation)
                                continue;

                            ChiselNodeHierarchyManager.UpdateAvailability(operation);
                        }
                    }
                    OnShapeChanged();
                }
                bool hasNoChildren = false;
                foreach (var target in serializedObject.targetObjects)
                {
                    var operation = target as ChiselOperation;
                    if (!operation)
                        continue;
                    if (operation.transform.childCount == 0)
                    {
                        hasNoChildren = true;
                    }
                }
                if (hasNoChildren)
                {
                    EditorGUILayout.HelpBox(kOperationHasNoChildren, MessageType.Warning, true);
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}