using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Chisel.Editors
{

    internal class FunctioningMaskField : UnityEditor.PopupWindowContent
    {
        static readonly GUIContent      noneContent			= new GUIContent("None");
        static readonly GUIContent      allContent			= new GUIContent("All");
        static readonly GUIContent      mixedContent        = new GUIContent("Mixed ...");

        private class EnumValues
        {
            public readonly GUIContent[]    flagContents;
            public readonly int[]           flagValues;
            public readonly int             allFlags;
            public readonly int             lines;

            public EnumValues(Type type)
            {
                flagContents = Enum.GetNames(type).Select(x => new GUIContent(ObjectNames.NicifyVariableName(x))).ToArray();

                var enumArray = Enum.GetValues(type);
                flagValues = new int[enumArray.Length];
                for (int i = 0; i < enumArray.Length; i++)
                {
                    var obj = enumArray.GetValue(i);
                    var temp = Enum.ToObject(type, obj);
                    flagValues[i] = Convert.ToInt32(temp);
                }
                
                allFlags = 0;
                for (int i = 0; i < flagValues.Length; i++)
                    allFlags |= flagValues[i];

                bool foundNone = false;
                bool foundAll = false;
                for (int i = 0; i < flagContents.Length; i++)
                {
                    var flag = flagValues[i];
                    if (flag == 0 && (flagContents[i].text == "None" || flagContents[i].text == "Nothing")) foundNone = true;
                    if (flag == allFlags) foundAll = true;
                }

                lines = flagValues.Length + (foundNone ? 0 : 1) + (foundAll ? 0 : 1);
            }

            static readonly Dictionary<Type, EnumValues> TypeEnumValues = new Dictionary<Type, EnumValues>();

            public static EnumValues GetValuesForType(Type type)
            {
                EnumValues values;
                if (!TypeEnumValues.TryGetValue(type, out values))
                {
                    values = new EnumValues(type);
                    TypeEnumValues[type] = values;
                }
                return values;
            }
        }


        private Action<int> action;
        private int currentValue;
        private EnumValues flags;
        private GUIStyle menuStyle = null;
        private bool showExtraOptions;
        
        const float kFrameWidth = 1f;
        const float kSingleLineHeight = 16f;

        FunctioningMaskField(Type type, int value, bool showExtraOptions, Action<int> action)
        {
            this.flags = EnumValues.GetValuesForType(type);
            this.currentValue = value;
            this.action = action;
            this.showExtraOptions = showExtraOptions;
        }


        public override Vector2 GetWindowSize()
        {
            GUIStyle style = menuStyle;
            if (style == null)
                return Vector2.one;
            float windowHeight;
            if (showExtraOptions)
                windowHeight = 2f * kFrameWidth + kSingleLineHeight * flags.lines;
            else
                windowHeight = 2f * kFrameWidth + kSingleLineHeight * flags.flagValues.Length;
            float width = 0;
            for (int i = 0; i < flags.flagContents.Length; i++)
            {
                float minWidth;
                float maxWidth;
                style.CalcMinMaxWidth(flags.flagContents[i], out minWidth, out maxWidth);
                width = Mathf.Max(minWidth, width);
            }
            var windowSize = new Vector2(width + 4, windowHeight);
            return windowSize;
        }


        public override void OnGUI(Rect rect)
        {
            // We do not use the layout event
            if (Event.current.type == EventType.Layout)
                return;

            // Content
            Draw(rect);

            // Use mouse move so we get hover state correctly in the menu item rows
            if (Event.current.type == EventType.MouseMove)
                Event.current.Use();

            // Escape closes the window
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
        }

        private void Draw(Rect rect)
        {
            if (menuStyle == null)
                menuStyle = "MenuItem"; 
            
            var drawPos = new Rect(kFrameWidth, kFrameWidth, rect.width - 2 * kFrameWidth, kSingleLineHeight);

            if (showExtraOptions)
            {
                GUIContent content = noneContent;
                for (int i = 0; i < flags.flagContents.Length; i++)
                {
                    var flag = flags.flagValues[i];
                    if (flag == 0 && (flags.flagContents[i].text == "None" || flags.flagContents[i].text == "Nothing"))
                    {
                        content = flags.flagContents[i];
                        break;
                    }
                }

                EditorGUI.BeginChangeCheck();
                GUI.Toggle(drawPos, currentValue == 0, content, menuStyle);
                if (EditorGUI.EndChangeCheck())
                {
                    currentValue = 0;
                    action(currentValue);
                }
                drawPos.y += kSingleLineHeight;

                bool foundAll = false;
                for (int i = 0; i < flags.flagContents.Length; i++)
                {
                    var flag = flags.flagValues[i];
                    if (flag == flags.allFlags)
                    {
                        DrawListElement(drawPos, flags.flagContents[i], ref currentValue, flag, menuStyle);
                        drawPos.y += kSingleLineHeight;
                        foundAll = true;
                        break;
                    }
                }

                if (!foundAll)
                {
                    EditorGUI.BeginChangeCheck();
                    GUI.Toggle(drawPos, currentValue == flags.allFlags, allContent, menuStyle);
                    if (EditorGUI.EndChangeCheck())
                    {
                        currentValue = flags.allFlags;
                        action(currentValue);
                    }
                    drawPos.y += kSingleLineHeight;
                }
            }

            for (int i = 0; i < flags.flagContents.Length; i++)
            {
                var flag = flags.flagValues[i];
                if (showExtraOptions)
                {
                    if ((flag == 0 && (flags.flagContents[i].text == "None" || flags.flagContents[i].text == "Nothing")) || flag == flags.allFlags)
                        continue;
                }
                DrawListElement(drawPos, flags.flagContents[i], ref currentValue, flag, menuStyle);
                drawPos.y += kSingleLineHeight;
            }
        }

        void DrawListElement(Rect rect, GUIContent toggleName, ref int currentFlags, int flag, GUIStyle style)
        {
            EditorGUI.BeginChangeCheck();
            bool result;
            if (flag == 0)
                result = GUI.Toggle(rect, true, toggleName, style);
            else
                result = GUI.Toggle(rect, ((currentFlags & flag) != 0), toggleName, style);
            if (EditorGUI.EndChangeCheck())
            {
                if (result) currentFlags = currentFlags | flag;
                else        currentFlags = currentFlags & ~flag;
                action(currentFlags);
            }
        }

        static GUIContent FindContent(Type type, bool showExtraOptions, int value)
        {
            var values = EnumValues.GetValuesForType(type);

            if (showExtraOptions)
            {
                if (value == 0)
                {
                    for (int i = 0; i < values.flagContents.Length; i++)
                    {
                        var flag = values.flagValues[i];
                        if (flag == 0 && (values.flagContents[i].text == "None" || values.flagContents[i].text == "Nothing"))
                        {
                            return values.flagContents[i];
                        }
                    }
                    return noneContent;
                } else
                if (value == values.allFlags)
                {
                    for (int i = 0; i < values.flagContents.Length; i++)
                    {
                        var flag = values.flagValues[i];
                        if (flag == values.allFlags)
                        {
                            return values.flagContents[i];
                        }
                    }
                    return allContent;
                }
            }

            var content = mixedContent;
            int count = 0;
            for (int i = 0; i < values.flagContents.Length; i++)
            {
                var flag = values.flagValues[i];
                if ((value & flag) != 0)
                {
                    count++;
                    content = values.flagContents[i];
                    if (count > 1)
                        break;
                }
            }
            return content;
        }

        public static void MaskField(Rect position, Type type, SerializedProperty property, bool showExtraOptions = true, GUIStyle style = null)
        {
            if (property == null)
                return;
            
            var targets			= property.serializedObject.targetObjects;
            var propertyPath	= property.propertyPath;
            Action<int> action = delegate (int newValue)
            {
                if (targets != null)
                {
                    var serializedObject = new SerializedObject(targets);
                    serializedObject.Update();
                    var currentProperty = serializedObject.FindProperty(propertyPath);
                    currentProperty.intValue = newValue;
                    // TODO: this can crash unity .. 
                    serializedObject.ApplyModifiedProperties();
                }
            };

            MaskField(position, type, property.intValue, action, showExtraOptions, style);
        }
        
        public static void MaskField(Rect position, Type type, int value, Action<int> action, bool showExtraOptions = true, GUIStyle style = null)
        {
            GUIContent label = mixedContent;
            if (!EditorGUI.showMixedValue)
                label = FindContent(type, showExtraOptions, value);

            MaskField(position, label, type, value, action, showExtraOptions, style);
        }
        
        public static void MaskField(Rect position, GUIContent label, Type type, int value, Action<int> action, bool showExtraOptions = true, GUIStyle style = null)
        {
            if (style == null)
                style = EditorStyles.popup;

            if (GUI.Button(position, label, style))
            {
                var popupWindow = new FunctioningMaskField(type, value, showExtraOptions, action);
                PopupWindow.Show(position, popupWindow);
            }
        }
    }
}
