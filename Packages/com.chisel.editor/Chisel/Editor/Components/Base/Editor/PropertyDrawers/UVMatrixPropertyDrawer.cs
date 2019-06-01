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
        const float kSpace = 2;

        public static float DefaultHeight
        {
            get
            {
                return kSpace + (2 * EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector4, GUIContent.none));
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        static readonly GUIContent[] xyzw = new []
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z"),
            new GUIContent("W"),
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // TODO: decompose uv matrix into translation, rotation and scale relative to it's plane (cross product of u/v is normal)
            
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
        }
    }
}
