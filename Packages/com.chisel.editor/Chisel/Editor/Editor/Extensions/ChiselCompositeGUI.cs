using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnityEditor.EditorTools;

namespace Chisel.Editors
{
    public static class ChiselCompositeGUI
    {
        const int       kOperationToggleWidth   = 32;
        const int       kAutomaticToggleWidth   = 80;
        const string    kAutoIconName           = "Automatic";

        class Styles
        {
            public GUIStyle   leftButton;
            public GUIStyle   leftButtonLabel;
            public GUIStyle   midButton;
            public GUIStyle   rightButton;
            
            public Styles()
            {
                var button = GUI.skin.button;
                leftButton = new GUIStyle("AppCommandLeft");
                leftButton.fixedWidth = 0;
                leftButtonLabel = new GUIStyle(EditorStyles.label);
                leftButtonLabel.padding.top += 2;
                leftButtonLabel.alignment = TextAnchor.MiddleCenter;
                leftButtonLabel.fixedWidth = 0;
                leftButtonLabel.active.textColor = EditorGUIUtility.isProSkin ? button.active.textColor : Color.white;
                leftButtonLabel.onActive.textColor = EditorGUIUtility.isProSkin ? button.onActive.textColor : Color.white;
                leftButtonLabel.normal.textColor = button.normal.textColor;
                leftButtonLabel.onNormal.textColor = button.onNormal.textColor;
                leftButtonLabel.hover.textColor = button.hover.textColor;
                leftButtonLabel.onHover.textColor = button.onHover.textColor;
                leftButtonLabel.focused.textColor = button.focused.textColor;
                leftButtonLabel.onFocused.textColor = button.onFocused.textColor;
                midButton = new GUIStyle("AppCommandMid");
                midButton.fixedWidth = 0;
                rightButton = new GUIStyle("AppCommandMid");
                rightButton.fixedWidth = 0;
            }
        };

        static Styles styles;

        static bool Toggle(ref Rect toggleRect, bool selected, GUIContent[] content, GUIStyle style)
        {
            var selectedContent = selected ? content[1] : content[0];

            EditorGUI.BeginChangeCheck();
            var result = GUI.Toggle(toggleRect, selected, selectedContent, style);
            toggleRect.x += toggleRect.width;

            return EditorGUI.EndChangeCheck() && result;
        }
        static bool ToggleLabel(ref Rect toggleRect, bool selected, GUIContent[] content, GUIStyle style)
        {
            var selectedContent = selected ? content[1] : content[0];

            EditorGUI.BeginChangeCheck();
            var result = GUI.Toggle(toggleRect, selected, GUIContent.none, style);
            if (Event.current.type == EventType.Repaint)
                styles.leftButtonLabel.Draw(toggleRect, selectedContent, false, selected, false, false);
            toggleRect.x += toggleRect.width;

            return EditorGUI.EndChangeCheck() && result;
        }

        public static void ChooseGeneratorOperation(ref CSGOperationType? operation)
        {
            if (styles == null)
                styles = new Styles();

            const float kBottomPadding = 3;
            var rect = EditorGUILayout.GetControlRect(hasLabel: true, height: EditorGUIUtility.singleLineHeight + kBottomPadding);
            rect.yMax -= kBottomPadding;
            EditorGUI.BeginChangeCheck();
            rect.yMin+=2;
            EditorGUI.PrefixLabel(rect, EditorGUIUtility.TrTextContent("Operation"));
            rect.yMin-=2;
            var result = ChiselCompositeGUI.ShowOperationChoicesInternal(rect, operation);
            if (EditorGUI.EndChangeCheck()) { operation = result; }
        }

        public static void ShowOperationChoicesInternal(Rect rect, SerializedProperty operationProp, bool showLabel = false)
        {
            if (operationProp == null)
                return;

            EditorGUI.BeginChangeCheck();
            if (showLabel)
            {
                rect.yMin += 2;
                EditorGUI.PrefixLabel(rect, EditorGUIUtility.TrTextContent(operationProp.displayName));
                rect.yMin -= 2;
            }
            var result  = ShowOperationChoicesInternal(rect, operationProp.hasMultipleDifferentValues ? (CSGOperationType?)null : (CSGOperationType)operationProp.enumValueIndex, false);
            if (EditorGUI.EndChangeCheck() && result.HasValue)
            {
                operationProp.enumValueIndex = (int)result.Value;
                operationProp.serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            }
        }

        const int kLeftStylePadding = 0;
        public static int GetOperationChoicesInternalWidth(bool showAuto = true)
        {
            var width = (kOperationToggleWidth * 3);
            if (showAuto)
                width += kAutomaticToggleWidth;
            else
                width += kLeftStylePadding;
            return width;
        }

        public static CSGOperationType? ShowOperationChoicesInternal(Rect rect, CSGOperationType? operation, bool showAuto = true)
        {
            if (styles == null)
                styles = new Styles();

            var additiveIcon        = ChiselDefaultGeneratorDetails.GetIconContent(CSGOperationType.Additive,     "Boolean Operation");
            var subtractiveIcon     = ChiselDefaultGeneratorDetails.GetIconContent(CSGOperationType.Subtractive,  "Boolean Operation");
            var intersectingIcon    = ChiselDefaultGeneratorDetails.GetIconContent(CSGOperationType.Intersecting, "Boolean Operation");

            using (new EditorGUIUtility.IconSizeScope(new Vector2(16, 16)))     // This ensures that the icons will be the same size on regular displays and HDPI displays
                                                                                // Note that the loaded images are different sizes on different displays
            {
                Rect toggleRect = rect;
                toggleRect.xMin = toggleRect.xMax - GetOperationChoicesInternalWidth(showAuto);

                if (showAuto)
                {
                    toggleRect.width = kAutomaticToggleWidth;
                    var autoIcon = ChiselEditorResources.GetIconContent(kAutoIconName, $"Automatic boolean operation");
                    if (ToggleLabel(ref toggleRect, !operation.HasValue, autoIcon, styles.leftButton))
                        return null;
                    toggleRect.width = kOperationToggleWidth;
                } else
                    toggleRect.width = kOperationToggleWidth + kLeftStylePadding;

                var operationType = !operation.HasValue ? ((CSGOperationType)255) : (operation.Value);
                if (Toggle(ref toggleRect, (operationType == CSGOperationType.Additive), additiveIcon, showAuto ? styles.midButton : styles.leftButton))
                    return CSGOperationType.Additive;
                if (!showAuto)
                    toggleRect.width -= kLeftStylePadding;
                if (Toggle(ref toggleRect, (operationType == CSGOperationType.Subtractive), subtractiveIcon, styles.midButton))
                    return CSGOperationType.Subtractive;
                if (Toggle(ref toggleRect, (operationType == CSGOperationType.Intersecting), intersectingIcon, styles.rightButton))
                    return CSGOperationType.Intersecting;
                return operationType;
            }
        }
    }
}
