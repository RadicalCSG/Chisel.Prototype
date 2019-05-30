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
    [CustomPropertyDrawer(typeof(ChiselBrushMaterial))]
    public sealed class ChiselBrushMaterialPropertyDrawer : PropertyDrawer
    {
        static readonly int kBrushPhysicMaterialEditorHashCode	= (nameof(ChiselBrushMaterialPropertyDrawer) + "PhysicMaterial").GetHashCode();
        static readonly int kBrushMaterialEditorHashCode	    = (nameof(ChiselBrushMaterialPropertyDrawer) + "Material"      ).GetHashCode();
        static readonly int kSliderHash                         = (nameof(ChiselBrushMaterialPropertyDrawer) + "Slider"        ).GetHashCode();

        const float kSpacing = 2;

        static readonly GUIContent	kPhysicRenderMaterialContents   = new GUIContent("Physics Material");
        static readonly GUIContent	kRenderMaterialContents         = new GUIContent("Render Material");
        
        class Styles { public readonly GUIStyle background = new GUIStyle("CurveEditorBackground"); };
        static Styles styles;

        static Vector2 previewDir = new Vector2(-120, 20);
        public static Vector2 Drag2D(Vector2 scrollPosition, Rect position)
        {
            int id = GUIUtility.GetControlID(kSliderHash, FocusType.Passive);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                {
                    if (position.Contains(evt.mousePosition) && position.width > 10)
                    {
                        GUIUtility.hotControl = id;
                        evt.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                    }
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;
                    
                    scrollPosition -= evt.delta * (evt.shift ? 3 : 1) / Mathf.Min(position.width, position.height) * 140.0f;
                    scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90, 90);
                    evt.Use();
                    GUI.changed = true;
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == id)
                        GUIUtility.hotControl = 0;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
                }
            }
            return scrollPosition;
        }

        public bool OnPreviewGUI(Rect region, GUIStyle background, Material material)
        {
            var newPreviewDir = Drag2D(previewDir, region);
            if (newPreviewDir != previewDir)
            {
                previewDir = newPreviewDir;
                PreviewTextureManager.Clear();
            }

            var texture = PreviewTextureManager.GetPreviewTexture(region, previewDir, material);
            if (!texture)
                return false;
            GUI.DrawTexture(region, texture, ScaleMode.StretchToFill, false); 
            return true;
        }

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return true;
        }

        
        public static float DefaultHeight
        {
            get
            {
                float physicsMaterialHeight, materialHeight, layerUsageHeight;
                materialHeight          =
                physicsMaterialHeight   = EditorGUI.GetPropertyHeight(SerializedPropertyType.ExposedReference, GUIContent.none);
                layerUsageHeight        = LayerUsageFlagsPropertyDrawer.DefaultHeight;
                return physicsMaterialHeight + kSpacing + materialHeight + kSpacing + layerUsageHeight;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return DefaultHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property); 
            try
            {
                SerializedProperty layerUsageProp       = property.FindPropertyRelative(ChiselBrushMaterial.kLayerUsageName);
                SerializedProperty renderMaterialProp   = property.FindPropertyRelative(ChiselBrushMaterial.kRenderMaterialName);
                SerializedProperty physicsMaterialProp  = property.FindPropertyRelative(ChiselBrushMaterial.kPhysicsMaterialName);

                // Don't make child fields be indented
                var originalPosition = position;
                position            = EditorGUI.IndentedRect(position);
                var indentOffset    = position.x - originalPosition.x;
                
                var showMaterial    = (CSGEditorUtility.GetContextWidth() > 430);

                var physicMaterialPrefixRect    = originalPosition;
                physicMaterialPrefixRect.height = EditorGUI.GetPropertyHeight(SerializedPropertyType.ExposedReference, GUIContent.none);
                originalPosition.yMin += physicMaterialPrefixRect.height + kSpacing;
                
                var previewSize             = originalPosition.height;
                var materialPrefixRect      = originalPosition;
                materialPrefixRect.height   = EditorGUI.GetPropertyHeight(SerializedPropertyType.ExposedReference, GUIContent.none);
                originalPosition.yMin += materialPrefixRect.height + kSpacing;
                
                var materialPreviewRect     = materialPrefixRect;
                materialPreviewRect.width   = previewSize;
                materialPreviewRect.height  = previewSize;

                var layerUsagePropRect      = originalPosition;
                
                EditorGUI.BeginChangeCheck();
                {

                    { 
                        var materialLabelID     = EditorGUIUtility.GetControlID(kBrushMaterialEditorHashCode, FocusType.Keyboard, materialPrefixRect);
                        var materialPropRect	= EditorGUI.PrefixLabel(materialPrefixRect, materialLabelID, kRenderMaterialContents);
                        materialPreviewRect.x   = materialPropRect.x;
                        materialPropRect.xMin -= indentOffset;
                        if (showMaterial)
                            materialPropRect.xMin += previewSize + kSpacing;
                        layerUsagePropRect.xMin = materialPropRect.xMin;
                        EditorGUI.PropertyField(materialPropRect, renderMaterialProp, GUIContent.none, false);
                    }

                    { 
                        EditorGUI.PropertyField(layerUsagePropRect, layerUsageProp, GUIContent.none, false);
                    }

                    {
                        var physicMaterialLabelID   = EditorGUIUtility.GetControlID(kBrushPhysicMaterialEditorHashCode, FocusType.Keyboard, physicMaterialPrefixRect);
                        var physicMaterialPropRect  = EditorGUI.PrefixLabel(physicMaterialPrefixRect, physicMaterialLabelID, kPhysicRenderMaterialContents);

                        physicMaterialPropRect.xMin -= indentOffset;
                        EditorGUI.PropertyField(physicMaterialPropRect, physicsMaterialProp, GUIContent.none, false);
                    }

                    // Render material preview
                    if (showMaterial)
                    {
                        if (styles == null)
                            styles = new Styles();
                        OnPreviewGUI(materialPreviewRect, styles.background, renderMaterialProp.objectReferenceValue as Material);
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.EndProperty();
        }
    }
}
