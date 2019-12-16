using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Editors
{
    internal static class ChiselEditorUtility
    {
        static System.Reflection.PropertyInfo contextWidthProperty;
        static ChiselEditorUtility()
        {
            contextWidthProperty = typeof(EditorGUIUtility).GetProperty("contextWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);			
        }

        public static float GetContextWidth()
        {
            return (float)contextWidthProperty.GetValue(null, null);
        }

        public static Camera GetMainCamera()
        {
            // main camera, if we have any
            var mainCamera = Camera.main;
            if (mainCamera != null)
                return mainCamera;

            // if we have one camera, return it
            var allCameras = Camera.allCameras;
            if (allCameras != null && allCameras.Length == 1)
                return allCameras[0];

            // otherwise no "main" camera
            return null;
        }

        // Note: this can return "use player settings" value too!
        // In order to check things like "is using deferred", use IsUsingDeferredRenderingPath
        public static RenderingPath GetSceneViewRenderingPath()
        {
            var mainCamera = GetMainCamera();
            if (mainCamera != null)
                return mainCamera.renderingPath;
            return RenderingPath.UsePlayerSettings;
        }

        public static bool IsUsingDeferredRenderingPath()
        {
            var target				= EditorUserBuildSettings.selectedBuildTargetGroup;
            var tier				= Graphics.activeTier;
            var currentTierSettings = EditorGraphicsSettings.GetTierSettings(target, tier);
            var renderingPath		= GetSceneViewRenderingPath();
            return (renderingPath == RenderingPath.DeferredShading) ||
                (renderingPath == RenderingPath.UsePlayerSettings && currentTierSettings.renderingPath == RenderingPath.DeferredShading);
        }

        public static bool IsDeferredReflections()
        {
            return (GraphicsSettings.GetShaderMode(BuiltinShaderType.DeferredReflections) != BuiltinShaderMode.Disabled);
        }

        static readonly int EnumFieldsHashCode = "EnumFields".GetHashCode();
        public static void EnumFlagsField(GUIContent label, SerializedProperty property, Type type, GUIStyle style, params GUILayoutOption[] options)
        {
#if UNITY_2017_3_OR_ABOVE
            var enumValue = (Enum)Enum.ToObject(type, property.intValue);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.EnumFlagsField
            if (EditorGUI.EndChangeCheck())
                property.intValue = (int)Enum.ToObject(type, result);
#else
            var position = EditorGUILayout.GetControlRect();
            int controlID = EditorGUIUtility.GetControlID(EnumFieldsHashCode, FocusType.Keyboard, position);
            var propertyRect = EditorGUI.PrefixLabel(position, controlID, label);
            FunctioningMaskField.MaskField(propertyRect, type, property);
#endif
        }

        internal static void Popup(Rect position, SerializedProperty property, GUIContent[] displayedOptions, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int idx = EditorGUI.Popup(position, label, property.hasMultipleDifferentValues ? -1 : property.intValue, displayedOptions);
            if (EditorGUI.EndChangeCheck())
                property.intValue = idx;
            EditorGUI.EndProperty();
        }

        internal static void ConsumeUnusedMouseEvents(int hash, Rect position)
        { 
            int controlID = GUIUtility.GetControlID(hash, FocusType.Keyboard, position);
            var type = Event.current.GetTypeForControl(controlID);
            switch (type)
            {
                case EventType.MouseDown: { if (position.Contains(Event.current.mousePosition)) { GUIUtility.hotControl = controlID; GUIUtility.keyboardControl = controlID; Event.current.Use(); } break; }
                case EventType.ScrollWheel:
                case EventType.MouseMove: { if (position.Contains(Event.current.mousePosition)) { Event.current.Use(); } break; }
                case EventType.MouseUp:   { if (GUIUtility.hotControl == controlID) { GUIUtility.hotControl = 0; GUIUtility.keyboardControl = 0; Event.current.Use(); } break; }
                case EventType.MouseDrag: { if (GUIUtility.hotControl == controlID) { Event.current.Use(); } break; }
            }
        }
    }
}
