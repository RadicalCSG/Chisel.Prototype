using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    // TODO: why did resetting this generator not work?
    [CustomEditor(typeof(CSGBox))]
    [CanEditMultipleObjects]
    public sealed class CSGBoxEditor : GeneratorEditor<CSGBox>
    {
        // TODO: make these shared resources since this name is used in several places (with identical context)
        static GUIContent   surfacesContent         = new GUIContent("Surfaces");
        static GUIContent   descriptionContent      = new GUIContent("Description");
        static GUIContent   surfaceAssetContent     = new GUIContent("Surface Asset");
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
        SerializedProperty surfaceAssetProp;
        
        protected override void ResetInspector()
        { 
            boundsProp				= null;
            surfaceDescriptionProp	= null;
            surfaceAssetProp		= null;
        }
        
        protected override void InitInspector()
        { 
            boundsProp				= serializedObject.FindProperty("bounds");
            surfaceDescriptionProp	= serializedObject.FindProperty("surfaceDescriptions");
            surfaceAssetProp		= serializedObject.FindProperty("surfaceAssets");

            surfacesVisible = SessionState.GetBool(kSurfacesVisibleKey, false);
        }

        const string kSurfacesVisibleKey = "CSGLinearStairsEditor.SubmeshesVisible";
        bool surfacesVisible;
        bool[]  surfacePropertyVisible = new bool[6]{ true,true,true,true,true,true };

        
        protected override void OnInspector()
        { 
            EditorGUILayout.PropertyField(boundsProp);
            /*
            var bounds = boundsProp.boundsValue;
            var min = bounds.min;
            var max = bounds.max;
                    
            EditorGUI.showMixedValue = boundsProp.hasMultipleDifferentValues;
            using (new EditorGUI.DisabledGroupScope(EditorGUI.showMixedValue)) // TODO: see if we can make this work with mixed values eventually
            { 
                var originalSize		= (max - min);
                var originalCenter		= (max + min) * 0.5f;
                
                Vector3 size			= originalSize;
                Vector3 center			= originalCenter;

                EditorGUI.BeginChangeCheck();
                {
                    var sizeRect		= EditorGUILayout.GetControlRect();
                    int sizeId			= EditorGUIUtility.GetControlID(sizeHashCode, FocusType.Keyboard, sizeRect);
                    var sizePropRect	= EditorGUI.PrefixLabel(sizeRect, sizeId, sizeContent);
                    size = EditorGUI.Vector3Field(sizePropRect, GUIContent.none, size);

                    var centerRect		= EditorGUILayout.GetControlRect();
                    int centerId		= EditorGUIUtility.GetControlID(centerHashCode, FocusType.Keyboard, centerRect);
                    var centerPropRect	= EditorGUI.PrefixLabel(centerRect, centerId, centerContent);
                    center = EditorGUI.Vector3Field(centerPropRect, GUIContent.none, center);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    var halfSize = size * 0.5f;
                    bounds.SetMinMax()
                    boundsProp.bounds = center - halfSize;
                    boundsProp.vector3Value = center + halfSize;
                }
            }

            EditorGUI.showMixedValue = false;*/


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

                        elementProperty = surfaceAssetProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProperty, surfaceAssetContent, true);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
        }

        Bounds originalBounds;
        
        protected override void OnSceneInit(CSGBox generator)
        {
            originalBounds = generator.Bounds;
        }
        
        protected override void OnScene(CSGBox generator)
        {
            EditorGUI.BeginChangeCheck();

            var newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(originalBounds, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Bounds = newBounds;
            }
        }
    }
}