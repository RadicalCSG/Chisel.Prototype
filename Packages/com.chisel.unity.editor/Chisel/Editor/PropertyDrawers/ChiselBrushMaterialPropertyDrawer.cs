using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

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

        public static bool OnPreviewGUI(Rect region, GUIStyle background, Material material)
        {
            var newPreviewDir = Drag2D(previewDir, region);
            if (newPreviewDir != previewDir)
            {
                previewDir = newPreviewDir;
                PreviewTextureManager.Clear();
            }

            var texture = PreviewTextureManager.GetPreviewTexture(region, previewDir, material);
            if (!texture)
            {
                if (Event.current.type == EventType.Repaint)
                    background.Draw(region, false, false, false, false);
                return false;
            }
            GUI.DrawTexture(region, texture, ScaleMode.StretchToFill, false); 
            return true;
        }

#if !UNITY_2023_1_OR_NEWER
        public override bool CanCacheInspectorGUI(SerializedProperty property) { return true; }
#endif


        public static float DefaultMaterialLayerUsageHeight
        {
            get
            {
                float materialHeight, layerUsageHeight;
                materialHeight = EditorGUIUtility.singleLineHeight;
                layerUsageHeight = LayerUsageFlagsPropertyDrawer.DefaultHeight;
                return materialHeight + kSpacing + layerUsageHeight;
            }
        }

        public static float DefaultHeight
        {
            get
            {
                float physicsMaterialHeight;
                physicsMaterialHeight   = EditorGUIUtility.singleLineHeight;
                return physicsMaterialHeight + kSpacing + DefaultMaterialLayerUsageHeight;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
                return 0;
            return DefaultHeight;
        }

        public static void ShowMaterialLayerUsage(Rect position, SerializedProperty renderMaterialProp, SerializedProperty layerUsageProp)
        {

            var previewSize         = position.height;            
            var lineHeight          = EditorGUIUtility.singleLineHeight;
            var materialPropRect    = new Rect(position.x, position.y, position.width, lineHeight);
            var layerUsagePropRect  = new Rect(position.x, position.y + lineHeight + kSpacing, position.width, previewSize - lineHeight + kSpacing);


            var showMaterial = (position.width > 260 && renderMaterialProp != null);
            if (showMaterial)
            {
                materialPropRect.xMin   += previewSize + kSpacing;
                layerUsagePropRect.xMin += previewSize + kSpacing;
            }
            var prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            {
                if (renderMaterialProp != null) EditorGUI.PropertyField(materialPropRect, renderMaterialProp, GUIContent.none, false);                
                if (layerUsageProp     != null) EditorGUI.PropertyField(layerUsagePropRect, layerUsageProp, GUIContent.none, false);
            }
            EditorGUI.indentLevel = prevIndent;

            // Render material preview
            if (showMaterial)
            {
                var materialPreviewRect = new Rect(position.x, position.y, previewSize, previewSize);
                if (styles == null)
                    styles = new Styles();
                OnPreviewGUI(materialPreviewRect, styles.background, renderMaterialProp.objectReferenceValue as Material);
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.BeginProperty(position, label, property); 
            try
            {
                var materialProperty = property;
                SerializedProperty layerUsageProp       = materialProperty.FindPropertyRelative(ChiselBrushMaterial.kLayerUsageFieldName);
                SerializedProperty renderMaterialProp   = materialProperty.FindPropertyRelative(ChiselBrushMaterial.kRenderMaterialFieldName);
                SerializedProperty physicsMaterialProp  = materialProperty.FindPropertyRelative(ChiselBrushMaterial.kPhysicsMaterialFieldName);

                // Don't make child fields be indented
                var originalPosition = position;
                position            = EditorGUI.IndentedRect(position);
                var indentOffset    = position.x - originalPosition.x;

                var physicMaterialPrefixRect    = originalPosition;
                physicMaterialPrefixRect.height = EditorGUIUtility.singleLineHeight;
                originalPosition.yMin += physicMaterialPrefixRect.height + kSpacing;
                
                var previewSize             = originalPosition.height;
                var materialPrefixRect      = originalPosition;
                materialPrefixRect.height   = EditorGUIUtility.singleLineHeight;
                originalPosition.yMin += materialPrefixRect.height + kSpacing;

                var hasLabel = ChiselGUIUtility.LabelHasContent(label);

                EditorGUI.BeginChangeCheck();
                {

                    {
                        var physicMaterialLabelID   = EditorGUIUtility.GetControlID(kBrushPhysicMaterialEditorHashCode, FocusType.Keyboard, physicMaterialPrefixRect);
                        var physicMaterialPropRect  = EditorGUI.PrefixLabel(physicMaterialPrefixRect, physicMaterialLabelID, !hasLabel ? GUIContent.none : kPhysicRenderMaterialContents);

                        physicMaterialPropRect.xMin -= indentOffset;
                        if (physicsMaterialProp != null)
                            EditorGUI.PropertyField(physicMaterialPropRect, physicsMaterialProp, GUIContent.none, false);
                    }

                
                    Rect    propRect;
                    { 
                        var materialLabelID = EditorGUIUtility.GetControlID(kBrushMaterialEditorHashCode, FocusType.Keyboard, materialPrefixRect);
                        propRect	        = EditorGUI.PrefixLabel(materialPrefixRect, materialLabelID, !hasLabel ? GUIContent.none : kRenderMaterialContents);
                    }

                    propRect.height = previewSize;
                    ShowMaterialLayerUsage(propRect, renderMaterialProp, layerUsageProp);
                }
                if (EditorGUI.EndChangeCheck())
                {
#if MATERIAL_IS_SCRIPTABLEOBJECT
                    materialObject.ApplyModifiedProperties();
#else
                    materialProperty.serializedObject.ApplyModifiedProperties();
#endif
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.EndProperty();
        }
    }
}
