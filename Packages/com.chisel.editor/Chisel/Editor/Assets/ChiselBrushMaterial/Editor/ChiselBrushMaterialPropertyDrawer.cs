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
        static readonly int         BurshMaterialEditorHashCode	= typeof(ChiselBrushMaterialPropertyDrawer).Name.GetHashCode();
        static readonly GUIContent	RenderMaterialContents      = new GUIContent("Render Material");
        //static readonly GUIContent LayerUsageContents         = new GUIContent("Layer Usage");
        const float					spacing					    = 2;

        class Styles
        {
            public readonly GUIStyle background = new GUIStyle("CurveEditorBackground");
        };

        static Styles styles;
        static int sliderHash = "Slider".GetHashCode();

        PreviewRenderUtility previewUtility; // Note: cannot be static, can potentially make images appear on wrong editor (think lists of assets)
        static Vector2 previewDir = new Vector2(-120, 20);

        SerializedProperty layerUsageProp;
        SerializedProperty renderMaterialProp;
        SerializedProperty physicsMaterialProp;


        public static Vector2 Drag2D(Vector2 scrollPosition, Rect position)
        {
            int id = GUIUtility.GetControlID(sliderHash, FocusType.Passive);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                if (position.Contains(evt.mousePosition) && position.width > 50)
                {
                    GUIUtility.hotControl = id;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                }
                break;
                case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    scrollPosition -= evt.delta * (evt.shift ? 3 : 1) / Mathf.Min(position.width, position.height) * 140.0f;
                    scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90, 90);
                    evt.Use();
                    GUI.changed = true;
                }
                break;
                case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                    GUIUtility.hotControl = 0;
                EditorGUIUtility.SetWantsMouseJumping(0);
                break;
            }
            return scrollPosition;
        }

        static Mesh sphereMesh;

        internal Mesh GetPreviewSphere()
        {
            if (!sphereMesh)
                sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            return sphereMesh;
        }

        Texture GetPreviewTexture(Vector2 previewDir, Rect region, GUIStyle background)
        {
            if (previewUtility == null)
                previewUtility = new PreviewRenderUtility();

            //if (Event.current.type != EventType.Repaint)
            //	return;

            var material	= renderMaterialProp.objectReferenceValue as Material;
            var mesh		= GetPreviewSphere();
            var camera		= previewUtility.camera;

            Bounds bounds = mesh.bounds;
            float halfSize = bounds.extents.magnitude;
            float distance = 5.0f * halfSize;

            camera.transform.position = -Vector3.forward * distance;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = distance - halfSize * 1.1f;
            camera.farClipPlane = distance + halfSize * 1.1f;
            camera.fieldOfView = 30.0f;
            camera.orthographicSize = 1.0f;
            camera.clearFlags = CameraClearFlags.Nothing;

            previewUtility.lights[0].intensity = 1.4f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0);
            previewUtility.lights[1].intensity = 1.4f;

            previewUtility.BeginPreview(region, background);

            bool fog = RenderSettings.fog;
            Unsupported.SetRenderSettingsUseFogNoDirty(false);
            camera.Render();
            Unsupported.SetRenderSettingsUseFogNoDirty(fog);

            Quaternion rot = Quaternion.Euler(previewDir.y, 0, 0) * Quaternion.Euler(0, previewDir.x, 0);
            Vector3 pos = rot * (-bounds.center);

            previewUtility.DrawMesh(mesh, pos, rot, material, 0);

            return previewUtility.EndPreview();
        }
        
        public void OnPreviewGUI(Rect region, GUIStyle background)
        {
            previewDir = Drag2D(previewDir, region);
            var texture = GetPreviewTexture(previewDir, region, background);
            GUI.DrawTexture(region, texture, ScaleMode.StretchToFill, false);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property); 
            bool prevShowMixedValue			= EditorGUI.showMixedValue;
            //bool hasMultipleDifferentValues = prevShowMixedValue || property.hasMultipleDifferentValues;
            try
            { 
                // Draw label
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Keyboard), label);

                // Don't make child fields be indented
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
            
                EditorGUI.BeginChangeCheck();
                {
                    //if (EditorGUI.PropertyField(position, property, GUIContent.none))
                    { 
                        //EditorGUI.indentLevel = indent + 1;
                        {
                            layerUsageProp		= property.FindPropertyRelative("layerUsage");
                            renderMaterialProp	= property.FindPropertyRelative("renderMaterial");
                            physicsMaterialProp = property.FindPropertyRelative("physicsMaterial");

                            EditorGUILayout.PropertyField(physicsMaterialProp, true);

#if UNITY_2017_1_OR_ABOVE
                            float lineHeight = EditorGUILayout.singleLineHeight;
#else
                            float lineHeight = 16;
#endif
                            float materialSize = 3 * lineHeight;

                            // Calculate rects	
                            var materialPart		= EditorGUILayout.GetControlRect(GUILayout.Height(materialSize));
                            var materialLabelID		= EditorGUIUtility.GetControlID(BurshMaterialEditorHashCode, FocusType.Keyboard, materialPart);
                            var materialPropRect	= EditorGUI.PrefixLabel(materialPart, materialLabelID, RenderMaterialContents);
                            var materialPreviewRect = materialPropRect;
                            var showMaterial		= (CSGEditorUtility.GetContextWidth() > 320);

                            float materialPropHeight = EditorGUI.GetPropertyHeight(SerializedPropertyType.ExposedReference, GUIContent.none);
                            if (showMaterial)
                            {
                                materialPreviewRect.width = materialSize;
                                materialPropRect.x += materialSize + spacing;
                                materialPropRect.width -= materialSize + spacing;
                            }

                            var layerUsagePropRect = materialPropRect;
                            layerUsagePropRect.y += materialPropHeight;
                            materialPropRect.height = materialPropHeight;

                            // Show material prop
                            var prevIndentLevel = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = 0;
                            {
                                EditorGUI.PropertyField(materialPropRect, renderMaterialProp, GUIContent.none, true);
                                EditorGUI.PropertyField(layerUsagePropRect, layerUsageProp, GUIContent.none, true);
                            }
                            EditorGUI.indentLevel = prevIndentLevel;

                            // Render material preview
                            if (showMaterial)
                            {
                                if (styles == null)
                                    styles = new Styles();
                                OnPreviewGUI(materialPreviewRect, styles.background);
                            }
                        }
                        //EditorGUI.indentLevel = 0;
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }

                // Set indent back to what it was
                EditorGUI.indentLevel = indent;
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
