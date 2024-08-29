using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{

    [CustomPropertyDrawer(typeof(SurfaceDetailFlags))]
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
            EditorGUI.showMixedValue        = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            {
                var surfaceDetailFlags  = (SurfaceDetailFlags)property.intValue;

                bool isTextureLocked    = (surfaceDetailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) == 0;
                
                EditorGUI.BeginChangeCheck();
                { 
                    isTextureLocked		= EditorGUI.ToggleLeft(position, kTextureLockedContent, isTextureLocked);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (isTextureLocked) surfaceDetailFlags &= ~SurfaceDetailFlags.TextureIsInWorldSpace;
                    else			     surfaceDetailFlags |=  SurfaceDetailFlags.TextureIsInWorldSpace;
        
                    property.intValue = (int)surfaceDetailFlags;
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
