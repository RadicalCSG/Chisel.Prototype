using System;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(SurfaceDestinationFlags))]
    public sealed class SurfaceDestinationFlagsPropertyDrawer : PropertyDrawer
    {
        private readonly static GUIContent	kVisibleContent			= new("Visible", "When set, the surface will be visible, otherwise it will not be rendered.");
		private readonly static GUIContent	kCollidableContent		= new("Collidable", "When set, the surface will be part of the generated collider, otherwise it will not be part of the collider.");
		private readonly static GUIContent  kCastShadowsContent		= new("Cast Shadows", "When set, the surface cast shadows on other surfaces, otherwise light will pass through it. Note that the surface does not need to be visible to block light.");
		private readonly static GUIContent  kReceiveShadowsContent	= new("Receive Shadows", "When set, the surface have shadows cast onto it, as long as the surface is visible. This cannot be disabled in deferred rendering modes.");
		
        public static float DefaultHeight
        {
            get
            {
                return (EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none) * 3);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool prevShowMixedValue			= EditorGUI.showMixedValue;
            bool deferredRenderingPath		= ChiselEditorUtility.IsUsingDeferredRenderingPath();
            EditorGUI.showMixedValue        = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            { 
                var surfaceDestinationFlags = (SurfaceDestinationFlags)property.enumValueFlag;

                bool isCollidable		= (surfaceDestinationFlags & SurfaceDestinationFlags.Collidable) != 0;
                bool isRenderable		= (surfaceDestinationFlags & SurfaceDestinationFlags.Renderable) != 0;
                bool isCastShadows		= (surfaceDestinationFlags & SurfaceDestinationFlags.CastShadows) != 0;
                bool isReceiveShadows	= (surfaceDestinationFlags & SurfaceDestinationFlags.ReceiveShadows) != 0;
				//bool isDoubleSided	= (surfaceDestinationFlags & SurfaceDestinationFlags.DoubleSided) != 0;

				// Draw label
				position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
                
                EditorGUI.BeginChangeCheck();
                { 
                    // Don't make child fields be indented
                    var indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;

                    var toggleStyle     = EditorStyles.label;

                    var halfWidth       = position.width / 2.0f;
                    var textWidthRight  = toggleStyle.CalcSize(kReceiveShadowsContent).x;
                    var textWidthLeft   = toggleStyle.CalcSize(kCollidableContent).x;
                    var offset          = (position.width - (textWidthRight + textWidthLeft)) / 2;
                    if (offset < 0)
                    {
                        textWidthRight = position.width - textWidthLeft;
                    } else
                    {
                        textWidthLeft += offset;
                        textWidthRight += offset;
                    }

                    var button1 = position;
                    var button2 = position;
                    button1.height = button2.height = EditorGUIUtility.singleLineHeight;
                    button1.width = textWidthLeft;
                    button2.width = textWidthRight;
                    button2.x += button1.width;

                    var button3 = button1;
                    var button4 = button2;
                    button3.y += button1.height;
                    button4.y += button2.height;


					isRenderable	= EditorGUI.ToggleLeft(button1, kVisibleContent, isRenderable, toggleStyle);
					isCollidable    = EditorGUI.ToggleLeft(button2, kCollidableContent, isCollidable, toggleStyle);
                    isCastShadows	= EditorGUI.ToggleLeft(button3, kCastShadowsContent, isCastShadows, toggleStyle);
                    EditorGUI.BeginDisabledGroup(deferredRenderingPath || !isRenderable);
                    if	    (!isRenderable        ) EditorGUI.ToggleLeft(button4, kReceiveShadowsContent, false, toggleStyle);
                    else if (deferredRenderingPath) EditorGUI.ToggleLeft(button4, kReceiveShadowsContent, true,  toggleStyle);
                    else		 isReceiveShadows = EditorGUI.ToggleLeft(button4, kReceiveShadowsContent, isReceiveShadows, toggleStyle);
                    EditorGUI.EndDisabledGroup();

					// Set indent back to what it was
					EditorGUI.indentLevel = indent;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (isRenderable)		surfaceDestinationFlags |=  SurfaceDestinationFlags.Renderable;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.Renderable;
                
                    if (isCastShadows)		surfaceDestinationFlags |=  SurfaceDestinationFlags.CastShadows;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.CastShadows;
                
                    if (isReceiveShadows)	surfaceDestinationFlags |=  SurfaceDestinationFlags.ReceiveShadows;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.ReceiveShadows;
                
                    if (isCollidable)		surfaceDestinationFlags |=  SurfaceDestinationFlags.Collidable;
                    else					surfaceDestinationFlags &= ~SurfaceDestinationFlags.Collidable;

					property.intValue = (int)surfaceDestinationFlags;
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
