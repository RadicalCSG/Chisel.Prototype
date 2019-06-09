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
    public sealed class CSGOperationDetails : ChiselNodeDetails<ChiselOperation>
    {
        const string AdditiveIconName		= "csg_addition";
        const string SubtractiveIconName	= "csg_subtraction";
        const string IntersectingIconName	= "csg_intersection";

        public override GUIContent GetHierarchyIcon(ChiselOperation node)
        {
            switch (node.Operation)
            {
                default:
                case CSGOperationType.Additive:     return ChiselEditorResources.GetIconContent(AdditiveIconName,     $"Additive {node.NodeTypeName}")[0];
                case CSGOperationType.Subtractive:  return ChiselEditorResources.GetIconContent(SubtractiveIconName,  $"Subtractive {node.NodeTypeName}")[0];
                case CSGOperationType.Intersecting: return ChiselEditorResources.GetIconContent(IntersectingIconName, $"Intersecting {node.NodeTypeName}")[0];
            }
        }
    }

    [CustomEditor(typeof(ChiselOperation))]
    [CanEditMultipleObjects]
    public sealed class CSGOperationEditor : ChiselNodeEditor<ChiselOperation>
    {
        [MenuItem("GameObject/Chisel/" + ChiselOperation.kNodeTypeName, false, 0)]
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
        }

        internal void OnDisable()
        {
            operationProp = null;
            passThroughProp = null;
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

                            CSGNodeHierarchyManager.UpdateAvailability(operation);
                        }
                    }
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}