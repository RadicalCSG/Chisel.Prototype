using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;

namespace Chisel.Editors
{
    
    [CustomPropertyDrawer(typeof(ChiselSurfaceDefinition))]
    public sealed class ChiselSurfaceDefinitionPropertyDrawer : PropertyDrawer
    {
        // TODO: make these shared resources since this name is used in several places (with identical context)
        static readonly GUIContent  kSurfacesContent        = new GUIContent("Surfaces");
        static readonly GUIContent  kDescriptionContent     = new GUIContent("Description");
        static readonly GUIContent  kBrushMaterialContent   = new GUIContent("Brush Material");
        const string                kSurfacePropertyName    = "Surface {0}";
        const string                kSurfacePathName        = "{0}[{1}]";
        static GUIContent           surfacePropertyContent  = new GUIContent();
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var surfacesProp = property.FindPropertyRelative(nameof(ChiselSurfaceDefinition.surfaces));

            EditorGUI.BeginProperty(position, label, surfacesProp);
            bool prevShowMixedValue = EditorGUI.showMixedValue;

            EditorGUI.BeginChangeCheck();
            var path                = surfacesProp.propertyPath;
            var surfacesVisible     = SessionState.GetBool(path, false);
            surfacesVisible = EditorGUILayout.Foldout(surfacesVisible, kSurfacesContent);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(path, surfacesVisible);
            if (surfacesVisible)
            {
                EditorGUI.indentLevel++;
                SerializedProperty elementProperty;
                for (int i = 0; i < surfacesProp.arraySize; i++)
                {
                    surfacePropertyContent.text = string.Format(kSurfacePropertyName, (i+1));
                    elementProperty = surfacesProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(elementProperty, surfacePropertyContent, true);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
