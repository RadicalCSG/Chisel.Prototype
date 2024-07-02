using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnityEngine.Profiling;

namespace Chisel.Editors
{
    public sealed class ChiselCompositeDetails : ChiselNodeDetails<ChiselComposite>
    {
        public override GUIContent GetHierarchyIcon(ChiselComposite node)
        {
            return ChiselDefaultGeneratorDetails.GetHierarchyIcon(node.Operation, node.ChiselNodeTypeName);
        }
    }

    [CustomEditor(typeof(ChiselComposite))]
    [CanEditMultipleObjects]
    public sealed class ChiselCompositeEditor : ChiselNodeEditor<ChiselComposite>
    {
        const string kCompositeHasNoChildren = "This operation has no chisel nodes as children and will not create any geometry.\nAdd some chisel nodes to see something.";

        [MenuItem(kGameObjectMenuCompositePath + ChiselComposite.kNodeTypeName + " Composite", false, kGameObjectMenuCompositePriority)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselComposite.kNodeTypeName); }

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
            operationProp   = serializedObject.FindProperty(ChiselComposite.kOperationFieldName);
            passThroughProp = serializedObject.FindProperty(ChiselComposite.kPassThroughFieldName);
        }

        internal void OnDisable()
        {
            operationProp = null;
            passThroughProp = null;
        }
        
        protected override void OnEditSettingsGUI(SceneView sceneView)
        {
            if (Tools.current != Tool.Custom)
                return;

            ShowInspectorHeader(operationProp);
        }

        public override void OnInspectorGUI()
        {
            if (!target)
                return;

            Profiler.BeginSample("OnInspectorGUI");
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
                            var composite = target as ChiselComposite;
                            if (!composite)
                                continue;

                            ChiselNodeHierarchyManager.UpdateAvailability(composite);
                        }
                    }
                    OnShapeChanged(target as ChiselComposite);
                }
                bool hasNoChildren = false;
                foreach (var target in serializedObject.targetObjects)
                {
                    var composite = target as ChiselComposite;
                    if (!composite)
                        continue;
                    if (composite.transform.childCount == 0)
                    {
                        hasNoChildren = true;
                    }
                }
                if (hasNoChildren)
                {
                    EditorGUILayout.HelpBox(kCompositeHasNoChildren, MessageType.Warning, true);
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                Profiler.EndSample();
            }
        }
    }
}