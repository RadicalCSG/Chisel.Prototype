using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Chisel.Editors
{
    public sealed class CSGBoxDetails : ChiselGeneratorDetails<CSGBox>
    {
    }

    // TODO: why did resetting this generator not work?
    [CustomEditor(typeof(CSGBox))]
    [CanEditMultipleObjects]
    public sealed class CSGBoxEditor : ChiselGeneratorEditor<CSGBox>
    {
        // TODO: make these shared resources since this name is used in several places (with identical context)
        static readonly GUIContent      kSurfacesContent        = new GUIContent("Surfaces");
        static readonly GUIContent[]    kSurfaceNames           = new []
        {
            new GUIContent("Top"),
            new GUIContent("Bottom"),
            new GUIContent("Right"),
            new GUIContent("Left"),
            new GUIContent("Front"),
            new GUIContent("Back")
        };
        
        SerializedProperty boundsProp;
        SerializedProperty surfacesProp;
        
        protected override void ResetInspector()
        { 
            boundsProp		        = null;
            surfacesProp   = null;
        }
        
        protected override void InitInspector()
        { 
            var definitionProp      = serializedObject.FindProperty(nameof(CSGCylinder.definition));
            { 
                boundsProp	        = definitionProp.FindPropertyRelative(nameof(CSGBox.definition.bounds));
                var surfDefProp     = definitionProp.FindPropertyRelative(nameof(CSGBox.definition.surfaceDefinition));
                {
                    surfacesProp    = surfDefProp.FindPropertyRelative(nameof(CSGBox.definition.surfaceDefinition.surfaces));
                }
            }
        }

        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(boundsProp);
            
            EditorGUI.BeginChangeCheck();
            var path                = surfacesProp.propertyPath;
            var surfacesVisible     = SessionState.GetBool(path, false);
            surfacesVisible = EditorGUILayout.Foldout(surfacesVisible, kSurfacesContent);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(path, surfacesVisible);
            if (surfacesVisible && surfacesProp.arraySize == 6)
            {
                EditorGUI.indentLevel++;
                SerializedProperty elementProperty;
                for (int i = 0; i < 6; i++)
                {
                    elementProperty = surfacesProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(elementProperty, kSurfaceNames[i], true);
                }
                EditorGUI.indentLevel--;
            }
        }

        protected override void OnScene(CSGBox generator)
        {
            EditorGUI.BeginChangeCheck();

            var newBounds = generator.Bounds;
            newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(newBounds, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Bounds = newBounds;
            }
        }
    }
}