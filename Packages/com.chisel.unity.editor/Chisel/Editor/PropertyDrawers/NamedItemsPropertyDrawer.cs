using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    // TODO: see if we can get this to work on any array instead
    [CustomPropertyDrawer(typeof(NamedItemsAttribute))]
    public sealed class NamedItemsPropertyDrawer : PropertyDrawer
    {
        static readonly GUIContent kMissingSurfacesContents             = new GUIContent("This generator is not set up properly and doesn't have the correct number of surfaces.");
        static readonly GUIContent kMultipleDifferentSurfacesContents   = new GUIContent("Multiple generators are selected with different surfaces.");

        static GUIContent tempPropertyContent = new GUIContent();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
                return 0;
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty surfacesArrayProperty, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
            {
                EditorGUI.BeginProperty(position, label, surfacesArrayProperty);
                EditorGUI.EndProperty();
                return;
            }

            NamedItemsAttribute namedItems = attribute as NamedItemsAttribute;

            if (surfacesArrayProperty.type != nameof(ChiselSurfaceArray))
            {
                Debug.Assert(false);
                return;
            }

            surfacesArrayProperty.Next(true);

            if (namedItems.fixedSize > 0 && surfacesArrayProperty.arraySize != namedItems.fixedSize)
            {
                EditorGUILayout.HelpBox(kMissingSurfacesContents.text, MessageType.Warning);
            }

            if (surfacesArrayProperty.hasMultipleDifferentValues)
            {
                // TODO: figure out how to detect if we have multiple selected generators with arrays of same size, or not
                EditorGUILayout.HelpBox(kMultipleDifferentSurfacesContents.text, MessageType.None);
                return;
            }

            if (surfacesArrayProperty.arraySize == 0)
                return;

            EditorGUI.BeginChangeCheck();
            var path            = surfacesArrayProperty.propertyPath;
            var surfacesVisible = SessionState.GetBool(path, false);
            surfacesVisible = EditorGUILayout.BeginFoldoutHeaderGroup(surfacesVisible, label);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(path, surfacesVisible);

            if (surfacesVisible)
            {
                EditorGUI.indentLevel++;
                SerializedProperty elementProperty;
                int startIndex = 0;
                if (namedItems.surfaceNames != null &&
                    namedItems.surfaceNames.Length > 0)
                {
                    startIndex = namedItems.surfaceNames.Length;
                    for (int i = 0; i < Mathf.Min(namedItems.surfaceNames.Length, surfacesArrayProperty.arraySize); i++)
                    {
                        elementProperty = surfacesArrayProperty.GetArrayElementAtIndex(i);
                        tempPropertyContent.text = namedItems.surfaceNames[i];
                        EditorGUILayout.PropertyField(elementProperty, tempPropertyContent, true);
                    }
                }

                for (int i = startIndex; i < surfacesArrayProperty.arraySize; i++)
                {
                    tempPropertyContent.text = string.Format(namedItems.overflow, (i - startIndex) + 1);
                    elementProperty = surfacesArrayProperty.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(elementProperty, tempPropertyContent, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
