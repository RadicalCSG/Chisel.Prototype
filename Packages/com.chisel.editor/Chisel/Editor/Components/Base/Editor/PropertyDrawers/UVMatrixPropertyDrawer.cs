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
    [CustomPropertyDrawer(typeof(UVMatrix))]
    public sealed class UVMatrixPropertyDrawer : PropertyDrawer
    {
        const float kSpacing = 2;

        public static float DefaultHeight
        {
            get
            {
                return (2 * (kSpacing + EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none))) + EditorGUI.GetPropertyHeight(SerializedPropertyType.Float, GUIContent.none);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        /*
        static readonly GUIContent[] xyzw = new []
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z"),
            new GUIContent("W"),
        };
        */
        static readonly GUIContent kTranslationContent  = new GUIContent("Translation");
        static readonly GUIContent kScaleContent        = new GUIContent("Scale");
        static readonly GUIContent kRotationContent     = new GUIContent("Rotation");

        class UVMatrixState
        {
            public bool     initialized = false;
            public Vector2  translation;
            public Vector3  normal;
            public float    rotation;
            public Vector2  scale;
            public UVMatrix uvMatrix;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty UProp    = property.FindPropertyRelative(nameof(UVMatrix.U));
            SerializedProperty VProp    = property.FindPropertyRelative(nameof(UVMatrix.V));

            int translationID   = GUIUtility.GetControlID(FocusType.Keyboard);
            int scaleID         = GUIUtility.GetControlID(FocusType.Keyboard);
            int rotationID      = GUIUtility.GetControlID(FocusType.Keyboard);

            var uvMatrix    = new UVMatrix(UProp.vector4Value, VProp.vector4Value);
            var state       = (UVMatrixState)EditorGUIUtility.GetStateObject(typeof(UVMatrixState), translationID);
            if (!state.initialized)
            {
                uvMatrix.Decompose(out state.translation, out state.normal, out state.rotation, out state.scale);
                state.uvMatrix = uvMatrix;
                state.initialized = true;
            }

            EditorGUI.BeginProperty(position, label, property);
            {
                EditorGUI.BeginChangeCheck();

                var prevMixedValues = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;

                var translationContent  = (label == null) ? GUIContent.none : kTranslationContent;
                var scaleContent        = (label == null) ? GUIContent.none : kScaleContent;
                var rotationContent     = (label == null) ? GUIContent.none : kRotationContent;

                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none);
                var fieldRect = EditorGUI.PrefixLabel(position, translationID, translationContent);
                state.translation = EditorGUI.Vector2Field(fieldRect,  GUIContent.none, state.translation);
                position.y += position.height + kSpacing;

                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, GUIContent.none);
                fieldRect = EditorGUI.PrefixLabel(position, scaleID, scaleContent);
                state.scale = EditorGUI.Vector2Field(fieldRect,        GUIContent.none,       state.scale);
                position.y += position.height + kSpacing;

                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Float, GUIContent.none);
                fieldRect = EditorGUI.PrefixLabel(position, rotationID, rotationContent);
                state.rotation = EditorGUI.FloatField(fieldRect,       GUIContent.none,    state.rotation);
                position.y += position.height + kSpacing;

                EditorGUI.showMixedValue = prevMixedValues;

                if (EditorGUI.EndChangeCheck())
                {
                    uvMatrix = UVMatrix.TRS(state.translation, state.normal, state.rotation, state.scale);

                    UProp.vector4Value = uvMatrix.U;
                    VProp.vector4Value = uvMatrix.V;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUI.EndProperty();
            /*
            
            SerializedProperty UxProp   = property.FindPropertyRelative($"{nameof(UVMatrix.U)}.{nameof(UVMatrix.U.x)}");
            SerializedProperty VxProp   = property.FindPropertyRelative($"{nameof(UVMatrix.V)}.{nameof(UVMatrix.V.x)}");

            EditorGUI.BeginProperty(position, label, property);
            {
                position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector4, GUIContent.none);
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
                
                EditorGUI.MultiPropertyField(position, xyzw, UxProp);
                position.y += position.height + kSpace;
                    
                EditorGUI.MultiPropertyField(position, xyzw, VxProp);
            }
            EditorGUI.EndProperty();
            */
        }
    }
}
