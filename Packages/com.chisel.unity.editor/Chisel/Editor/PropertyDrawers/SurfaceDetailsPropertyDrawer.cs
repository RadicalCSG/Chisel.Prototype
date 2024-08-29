using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(SurfaceDetails))]
    public sealed class SurfaceDetailsPropertyDrawer : PropertyDrawer
    {
        const float kSpacing = 2;
        public static readonly GUIContent	kUV0Contents            = new("UV");
        public static readonly GUIContent	kSurfaceFlagsContents   = new("Surface Flags");
        public static readonly GUIContent	kSmoothingGroupContents = new("Smoothing Groups");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
                return 0;

            var smoothingGroupProp   = property.FindPropertyRelative(nameof(SurfaceDetails.smoothingGroup));
            return  UVMatrixPropertyDrawer.DefaultHeight +
                    kSpacing + 
                    SurfaceFlagsPropertyDrawer.DefaultHeight +
                    kSpacing + 
                    SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ChiselNodeEditorBase.InSceneSettingsContext)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.EndProperty();
                return;
            }
            var hasLabel = ChiselGUIUtility.LabelHasContent(label);

            var uv0Prop            = property.FindPropertyRelative(nameof(SurfaceDetails.UV0));
            var detailFlagsProp    = property.FindPropertyRelative(nameof(SurfaceDetails.detailFlags));
            var smoothingGroupProp = property.FindPropertyRelative(nameof(SurfaceDetails.smoothingGroup));

            EditorGUI.BeginProperty(position, label, property);
            bool prevShowMixedValue = EditorGUI.showMixedValue;
            try
            {
                property.serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                {
                    position.height = SurfaceFlagsPropertyDrawer.DefaultHeight;
                    EditorGUI.PropertyField(position, detailFlagsProp, !hasLabel ? GUIContent.none : kSurfaceFlagsContents, false);
                    position.y += position.height + kSpacing;

                    position.height = UVMatrixPropertyDrawer.DefaultHeight;
                    EditorGUI.PropertyField(position, uv0Prop, !hasLabel ? GUIContent.none : kUV0Contents, false);
                    position.y += position.height + kSpacing;

                    position.height = SmoothingGroupPropertyDrawer.GetDefaultHeight(smoothingGroupProp.propertyPath, true);
                    EditorGUI.PropertyField(position, smoothingGroupProp, !hasLabel ? GUIContent.none : kSmoothingGroupContents, false);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            
            EditorGUI.showMixedValue = prevShowMixedValue;
            EditorGUI.EndProperty();
        }
    }
}
