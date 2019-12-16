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
    [CustomPropertyDrawer(typeof(SmoothingGroup))]
    public sealed class SmoothingGroupPropertyDrawer : PropertyDrawer
    {
        static readonly GUIContent[] kBits = new[]
        {
            new GUIContent( "1"), new GUIContent( "2"), new GUIContent( "3"), new GUIContent( "4"),
            new GUIContent( "5"), new GUIContent( "6"), new GUIContent( "7"), new GUIContent( "8"),
            
            new GUIContent( "9"), new GUIContent("10"), new GUIContent("11"), new GUIContent("12"),
            new GUIContent("13"), new GUIContent("14"), new GUIContent("15"), new GUIContent("16"),
            
            new GUIContent("17"), new GUIContent("18"), new GUIContent("19"), new GUIContent("20"),
            new GUIContent("21"), new GUIContent("22"), new GUIContent("23"), new GUIContent("24"),
            
            new GUIContent("25"), new GUIContent("26"), new GUIContent("27"), new GUIContent("28"),
            new GUIContent("29"), new GUIContent("30"), new GUIContent("31"), new GUIContent("32"),
        };

        const int kBitsWide = 8;
        const int kBitsHigh = 4;
        const float kSpacing = 2.0f;

        public static float GetDefaultHeight(string propertyPath, bool hasLabel)
        {
            var height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Generic, GUIContent.none);
            if (!hasLabel || SessionState.GetBool(propertyPath, false))
                height += (EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none) + kSpacing) * kBitsHigh;
            return height;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetDefaultHeight(property.propertyPath, label != null);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            bool visible = true;
            // Draw label
            if (label != null)
            {
                EditorGUI.BeginChangeCheck();
                visible = SessionState.GetBool(property.propertyPath, false);
                var foldOutPosition = position;
                foldOutPosition.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Generic, GUIContent.none);
                visible = EditorGUI.Foldout(foldOutPosition, visible, label);
                if (EditorGUI.EndChangeCheck())
                    SessionState.SetBool(property.propertyPath, visible);
                position.yMin += foldOutPosition.height;
            }

            if (visible)
            {
                position.xMin += EditorGUIUtility.labelWidth;
                
                // Don't make child fields be indented
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                var leftStyle   = EditorStyles.miniButtonLeft;
                var rightStyle  = EditorStyles.miniButtonRight;
                var middleStyle = EditorStyles.miniButtonMid;
            
                bool prevShowMixedValue			= EditorGUI.showMixedValue;
                bool deferredRenderingPath		= ChiselEditorUtility.IsUsingDeferredRenderingPath();
                EditorGUI.showMixedValue        = prevShowMixedValue || property.hasMultipleDifferentValues;
                try
                {
                    SerializedProperty ValueProp    = property.FindPropertyRelative(nameof(SmoothingGroup.value));
                    var smoothingGroup              = (uint)ValueProp.intValue;
                
                    EditorGUI.BeginChangeCheck();
                    {
                        var startPosition = position;
                        position.width /= kBitsWide;
                        position.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none);
                        for (int y = 0, i = 0; y < kBitsHigh; y++)
                        {
                            position.x = startPosition.x;
                            for (int x = 0; x < kBitsWide; x++, i++)
                            {
                                var toggleStyle = (x == 0        ) ? leftStyle :
                                                  (x == kBitsWide - 1) ? rightStyle :
                                                                     middleStyle;
                                var bit = (1u << i);
                                var enabled = GUI.Toggle(position, (smoothingGroup & bit) != 0, kBits[i], toggleStyle);
                                if (enabled)
                                    smoothingGroup = (smoothingGroup | bit);
                                else
                                    smoothingGroup = (smoothingGroup & ~bit);
                                position.x += position.width;
                            }
                            position.y += position.height + kSpacing;
                        }
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        ValueProp.intValue = (int)smoothingGroup;
                    }
                }
                catch (ExitGUIException) { }
                catch (Exception ex) { Debug.LogException(ex); }
            
                // Set indent back to what it was
                EditorGUI.indentLevel = indent;
                EditorGUI.showMixedValue = prevShowMixedValue;
            }
            EditorGUI.EndProperty();
        }
    }
}
