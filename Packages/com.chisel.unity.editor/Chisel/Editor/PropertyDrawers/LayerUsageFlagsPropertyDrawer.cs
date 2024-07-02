using System;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(LayerUsageFlags))]
    public sealed class LayerUsageFlagsPropertyDrawer : PropertyDrawer
    {
        readonly static GUIContent	VisibleContent			= new GUIContent("Visible", "When set, the surface will be visible, otherwise it will not be rendered.");
        readonly static GUIContent	CollidableContent		= new GUIContent("Collidable", "When set, the surface will be part of the generated collider, otherwise it will not be part of the collider.");
        readonly static GUIContent  CastShadowsContent		= new GUIContent("Cast Shadows", "When set, the surface cast shadows on other surfaces, otherwise light will pass through it. Note that the surface does not need to be visible to block light.");
        readonly static GUIContent  ReceiveShadowsContent	= new GUIContent("Receive Shadows", "When set, the surface have shadows cast onto it, as long as the surface is visible. This cannot be disabled in deferred rendering modes.");
        
        public static float DefaultHeight
        {
            get
            {
                return (EditorGUI.GetPropertyHeight(SerializedPropertyType.Boolean, GUIContent.none) * 2);
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
                var layerUsage = (LayerUsageFlags)property.intValue;

                bool isRenderable		= (layerUsage & LayerUsageFlags.Renderable) != 0;
                bool isCastShadows		= (layerUsage & LayerUsageFlags.CastShadows) != 0;
                bool isReceiveShadows	= (layerUsage & LayerUsageFlags.ReceiveShadows) != 0;
                bool isCollidable		= (layerUsage & LayerUsageFlags.Collidable) != 0;

                // Draw label
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);
                
                EditorGUI.BeginChangeCheck();
                { 
                    // Don't make child fields be indented
                    var indent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;

                    var toggleStyle     = EditorStyles.label;

                    var halfWidth       = position.width / 2.0f;
                    var textWidthRight  = toggleStyle.CalcSize(ReceiveShadowsContent).x;
                    var textWidthLeft   = toggleStyle.CalcSize(CollidableContent).x;
                    var offset      = (position.width - (textWidthRight + textWidthLeft)) / 2;
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

                    isRenderable		= EditorGUI.ToggleLeft(button1, VisibleContent, isRenderable, toggleStyle);
                    EditorGUI.BeginDisabledGroup(deferredRenderingPath || !isRenderable);
                    if	    (!isRenderable        ) EditorGUI.ToggleLeft(button2, ReceiveShadowsContent, false, toggleStyle);
                    else if (deferredRenderingPath) EditorGUI.ToggleLeft(button2, ReceiveShadowsContent, true,  toggleStyle);
                    else		 isReceiveShadows = EditorGUI.ToggleLeft(button2, ReceiveShadowsContent, isReceiveShadows, toggleStyle);
                    EditorGUI.EndDisabledGroup();
                    isCollidable		= EditorGUI.ToggleLeft(button3, CollidableContent,	isCollidable,  toggleStyle);
                    isCastShadows		= EditorGUI.ToggleLeft(button4, CastShadowsContent,	isCastShadows, toggleStyle);

                    // Set indent back to what it was
                    EditorGUI.indentLevel = indent;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    if (isRenderable)		layerUsage |=  LayerUsageFlags.Renderable;
                    else					layerUsage &= ~LayerUsageFlags.Renderable;
                
                    if (isCastShadows)		layerUsage |=  LayerUsageFlags.CastShadows;
                    else					layerUsage &= ~LayerUsageFlags.CastShadows;
                
                    if (isReceiveShadows)	layerUsage |=  LayerUsageFlags.ReceiveShadows;
                    else					layerUsage &= ~LayerUsageFlags.ReceiveShadows;
                
                    if (isCollidable)		layerUsage |=  LayerUsageFlags.Collidable;
                    else					layerUsage &= ~LayerUsageFlags.Collidable;
        
                    property.intValue = (int)layerUsage;
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
