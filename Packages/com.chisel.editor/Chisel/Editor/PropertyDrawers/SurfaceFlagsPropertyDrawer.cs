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
    [CustomPropertyDrawer(typeof(SurfaceFlags))]
    public sealed class SurfaceFlagsPropertyDrawer : PropertyDrawer
    {
        readonly static GUIContent	kTextureLockedContent = new GUIContent("Lock texture to object", "When set, the texture will stay\n" +
																								  "in the same position relative to the\n" +
																								  "object when moved in the world. When \n" +
																								  "not set it'll stay in the same position\n" +
                                                                                                  "relative to the world.");
        
        public static float DefaultHeight
        {
            get
            {
                return EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
            
            // Don't make child fields be indented
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            
            bool prevShowMixedValue			= EditorGUI.showMixedValue;
            bool deferredRenderingPath		= CSGEditorUtility.IsUsingDeferredRenderingPath();
            EditorGUI.showMixedValue        = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            {
                var surfaceFlags = (SurfaceFlags)property.intValue;

                bool isTextureLocked    = (surfaceFlags & SurfaceFlags.TextureIsInWorldSpace) == 0;
                
                EditorGUI.BeginChangeCheck();
                { 
                    isTextureLocked		= EditorGUI.ToggleLeft(position, kTextureLockedContent, isTextureLocked);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (isTextureLocked)	surfaceFlags &= ~SurfaceFlags.TextureIsInWorldSpace;
                    else					surfaceFlags |=  SurfaceFlags.TextureIsInWorldSpace;
        
                    property.intValue = (int)surfaceFlags;
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            // Set indent back to what it was
            EditorGUI.indentLevel = indent;
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
