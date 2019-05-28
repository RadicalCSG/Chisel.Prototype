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
        static GUIContent   surfacesContent         = new GUIContent("Surfaces");
        static GUIContent   descriptionContent      = new GUIContent("Description");
        static GUIContent   brushMaterialContent    = new GUIContent("Brush Material");
        static GUIContent[] surfacePropertyContent  = new[]
        {
            new GUIContent("Surface 0"),
            new GUIContent("Surface 1"),
            new GUIContent("Surface 2"),
            new GUIContent("Surface 3"),
            new GUIContent("Surface 4"),
            new GUIContent("Surface 5")
        };
        
        SerializedProperty boundsProp;
        SerializedProperty surfaceDescriptionProp;
        SerializedProperty brushMaterialProp;
        

        protected override void ResetInspector()
        { 
            boundsProp				= null;
            surfaceDescriptionProp	= null;
            brushMaterialProp		= null;
        }
        
        protected override void InitInspector()
        { 
            boundsProp				= serializedObject.FindProperty("definition.bounds");
            surfaceDescriptionProp	= serializedObject.FindProperty("definition.surfaceDescriptions");
            brushMaterialProp		= serializedObject.FindProperty("definition.brushMaterials");

            surfacesVisible = SessionState.GetBool(kSurfacesVisibleKey, false);
        }

        const string kSurfacesVisibleKey = "CSGLinearStairsEditor.SubmeshesVisible";
        bool surfacesVisible;
        bool[]  surfacePropertyVisible = new bool[6]{ true,true,true,true,true,true };

        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(boundsProp);

            EditorGUI.BeginChangeCheck();
            surfacesVisible = EditorGUILayout.Foldout(surfacesVisible, surfacesContent);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetBool(kSurfacesVisibleKey, surfacesVisible);
            if (surfacesVisible)
            {
                EditorGUI.indentLevel++;
                SerializedProperty elementProperty;
                for (int i = 0; i < surfaceDescriptionProp.arraySize; i++)
                {
                    surfacePropertyVisible[i] = EditorGUILayout.Foldout(surfacePropertyVisible[i], surfacePropertyContent[i]);
                    EditorGUI.indentLevel++;
                    if (surfacePropertyVisible[i])
                    {
                        elementProperty = surfaceDescriptionProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProperty, descriptionContent, true);

                        elementProperty = brushMaterialProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProperty, brushMaterialContent, true);
                    }
                    EditorGUI.indentLevel--;
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