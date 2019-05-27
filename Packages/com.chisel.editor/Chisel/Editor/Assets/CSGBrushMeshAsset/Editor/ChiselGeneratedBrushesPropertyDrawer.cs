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
    [CustomPropertyDrawer(typeof(ChiselGeneratedBrushes))]
    public sealed class CSGBrushMeshAssetPropertyDrawer : PropertyDrawer
    {
        static readonly GUIContent CreateBoxButtonContent = new GUIContent("Create Box");
        CSGBrushMeshAssetEditor editor;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool prevShowMixedValue			= EditorGUI.showMixedValue;
            bool hasMultipleDifferentValues = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            { 
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);

                // Don't make child fields be indented
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
            
                if (!hasMultipleDifferentValues &&
                    object.Equals(null, property.objectReferenceValue))
                {
                    if (GUI.Button(position, CreateBoxButtonContent))
                    {
                        var brushMaterial	= ChiselBrushMaterial.CreateInstance(CSGMaterialManager.DefaultWallMaterial, CSGMaterialManager.DefaultPhysicsMaterial);
                        var asset			= BrushMeshAssetFactory.CreateBoxAsset(Vector3.one, brushMaterial);
                        property.objectReferenceValue = asset;
                        property.serializedObject.ApplyModifiedProperties();
                        GUI.changed = true;
                    }
                } else
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUI.PropertyField(position, property, GUIContent.none);
                        if (!hasMultipleDifferentValues)
                        {
                            EditorGUI.indentLevel = indent + 1;
                            {
                                CachedEditorUtility.ShowEditor<CSGBrushMeshAssetEditor>(property.objectReferenceValue, ref editor);
                            }
                            EditorGUI.indentLevel = indent;
                        }
                    } 
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }

                // Set indent back to what it was
                EditorGUI.indentLevel = indent;
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
