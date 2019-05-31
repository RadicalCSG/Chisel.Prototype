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
    public sealed class CSGBoxDetails : ChiselGeneratorDetails<ChiselBox>
    {
    }

    // TODO: why did resetting this generator not work?
    [CustomEditor(typeof(ChiselBox))]
    [CanEditMultipleObjects]
    public sealed class CSGBoxEditor : ChiselGeneratorEditor<ChiselBox>
    {
        static readonly GUIContent[] kSurfaceNameContent = new []
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
            boundsProp	    = null;
            surfacesProp    = null;
        }
        
        protected override void InitInspector()
        { 
            var definitionProp      = serializedObject.FindProperty(nameof(ChiselBox.definition));
            { 
                boundsProp	        = definitionProp.FindPropertyRelative(nameof(ChiselBox.definition.bounds));
                var surfDefProp     = definitionProp.FindPropertyRelative(nameof(ChiselBox.definition.surfaceDefinition));
                {
                    surfacesProp    = surfDefProp.FindPropertyRelative(nameof(ChiselBox.definition.surfaceDefinition.surfaces));
                }
            }
        }

        protected override void OnInspector()
        {
            EditorGUILayout.PropertyField(boundsProp);

            ShowSurfaces(surfacesProp, kSurfaceNameContent, 6);
        }

        protected override void OnScene(ChiselBox generator)
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