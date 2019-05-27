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
    [CustomEditor(typeof(ChiselGeneratedBrushes))]
    [CanEditMultipleObjects]
    public sealed class CSGBrushMeshAssetEditor : Editor
    {
        static GUIContent	submeshesContent = new GUIContent("Submeshes");	
        SerializedProperty	submeshesProp;

        internal void OnEnable()
        {
            if (!target)
            {
                submeshesProp = null;
                return;
            }
            submeshesProp		= serializedObject.FindProperty("subMeshes");
            submeshesVisible	= SessionState.GetBool(kSubmeshesVisibleKey, true);
        }
        
        const string kSubmeshesVisibleKey = "CSGBrushMeshAssetEditor.SubmeshesVisible";
        bool submeshesVisible;

        public override void OnInspectorGUI()
        {
            if (!target)
                return;
            
            var oldSubMeshesVisible = submeshesVisible;

            if (submeshesProp != null && submeshesProp.arraySize > 0)
            {
                submeshesVisible = EditorGUILayout.Foldout(submeshesVisible, submeshesContent);
                if (submeshesVisible)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < submeshesProp.arraySize; i++)
                    {
                        var elementProperty = submeshesProp.GetArrayElementAtIndex(i).FindPropertyRelative("polygons");
                        EditorGUILayout.PropertyField(elementProperty, new GUIContent("Surfaces"), true); 
                    }
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (submeshesVisible != oldSubMeshesVisible) SessionState.SetBool(kSubmeshesVisibleKey, submeshesVisible);
        }
    }
}
 