using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using UnityEditor.EditorTools;
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    public static class ChiselToolsOverlay
    {
        const string kRebuildIconName   = "rebuild";
        const string kRebuildTooltip    = "Force rebuild all generated meshes";

        public static void Rebuild()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Rebuild");
            try
            {
                ChiselNodeHierarchyManager.Rebuild();
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        const int kPrimaryOrder = 98;

        const string                    kOverlayTitle   = "Chisel Tools";
        static readonly ChiselOverlay   OverlayWindow   = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);


        static SortedList<string, ChiselEditToolBase> editModes = new SortedList<string, ChiselEditToolBase>();

        static GUIContent kRebuildButton;

        // TODO: move to dedicated manager
        internal static void Register(ChiselEditToolBase editMode)
        {
            if (editMode.GetType() == typeof(ChiselCreateTool))
                return;
            editModes[editMode.ToolName] = editMode;
        }


        public static void UpdateCreateToolIcon()
        {
            if (!editModes.TryGetValue(ChiselCreateTool.kToolName, out ChiselEditToolBase toolBase))
                return;
            toolBase.UpdateIcon();
        }



        static bool Toggle(Rect position, ChiselEditToolBase editMode, Type editModeType, GUIStyle style)
        {
            var selected = ToolManager.activeToolType == editModeType;
            var content = selected ? editMode.ActiveIconContent : editMode.IconContent;
            return GUI.Toggle(position, selected, content, style);
        }

        static void EditModeButton(Rect position, bool enabled, ChiselEditToolBase editMode, GUIStyle style)
        {
            var editModeType = editMode.GetType();
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();
                var value = Toggle(position, editMode, editModeType, style);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    ToolManager.SetActiveTool(editModeType);
                    ChiselEditorSettings.Save();
                }
            }
        }

        class Styles
        {
            public GUIStyle toggleStyle;
            public GUIStyle toggleStyleLeft;
            public GUIStyle toggleStyleMid;
            public GUIStyle toggleStyleRight;
            public GUIStyle buttonRowStyle;
        }

        static Styles styles = null;
        public const int kButtonSize    = 32 + (kButtonPadding * 2);
        public const int kButtonMargin  = 1;
        public const int kButtonPadding = 2;
        static void InitStyles()
        {
            if (styles == null)
            {
                ChiselEditorSettings.Load();
                styles = new Styles
                {
                    toggleStyle = new GUIStyle("AppCommand")
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    toggleStyleLeft = new GUIStyle("AppCommandLeft")
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    toggleStyleMid = new GUIStyle("AppCommandMid")
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    toggleStyleRight = new GUIStyle("AppCommandRight")
                    {
                        padding     = new RectOffset(kButtonPadding + kButtonMargin, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(0,  0,  kButtonMargin,  0),
                        fixedWidth  = kButtonSize + kButtonMargin,
                        fixedHeight = kButtonSize,
                    },
                    buttonRowStyle = new GUIStyle(GUIStyle.none)
                };
            }
        }

        // TODO: move somewhere else
        public static bool HaveNodesInSelection()
        {
            if (Selection.count == 0)
                return false;
            if (Selection.count == 1)
            {
                var chiselNode = Selection.activeObject as ChiselNode;
                if (chiselNode != null)
                    return true;

                var gameObject = Selection.activeObject as GameObject;
                if (gameObject != null)
                    if (gameObject.TryGetComponent<ChiselNode>(out _))
                        return true;
            }
            return Selection.GetFiltered<ChiselNode>(SelectionMode.Editable).Length > 0;
        }


        static void DisplayControls(SceneView sceneView)
        {
            InitStyles();
            EditorGUI.BeginChangeCheck();
            {
                var enabled = HaveNodesInSelection();

                var values = editModes.Values;
                if (values.Count > 0)
                {
                    using (new EditorGUI.DisabledScope(!enabled))
                    {
                        var style       = styles.toggleStyleMid;
                        var groupRect   = EditorGUILayout.GetControlRect(false, style.fixedHeight + style.margin.vertical, ChiselOverlay.kMinWidthLayout);
                        groupRect.xMin -= 3;
                        groupRect.yMin += 3;

                        var startX      = style.margin.left + groupRect.x + 4;
                        var buttonStep  = style.fixedWidth + style.margin.left;
                        var position    = new Rect(startX, groupRect.y, style.fixedWidth, style.fixedHeight);

                        int xPos = 0;
                        var count = values.Count;
                        var index = 0;
                        for (int i = 0; i < count; i++)
                        {
                            var editMode = values[i];
                            var toggleStyle = (index ==         0)                ? styles.toggleStyleLeft :
                                              (index == count - 1) && (count < 7) ? styles.toggleStyleRight :
                                              styles.toggleStyleMid;
                            position.x = startX + (xPos * buttonStep);
                            EditModeButton(position, enabled, editMode, toggleStyle);
                            index++;
                            xPos++;
                        }

                        // TODO: assign hotkey to rebuild, and possibly move it elsewhere to avoid it seemingly like a necessary action.

                        xPos = 7;
                        position.x = startX + (xPos * buttonStep);
                        var buttonStyle = (index == 7) ? styles.toggleStyleRight :
                                          styles.toggleStyle;
                        if (kRebuildButton == null)
                            kRebuildButton = ChiselEditorResources.GetIconContent(kRebuildIconName, kRebuildTooltip)[0];
                        if (GUI.Toggle(position, false, kRebuildButton, buttonStyle))
                        {
                            Rebuild();
                        }
                    }
                }

                ChiselPlacementToolsSelectionWindow.RenderCreationTools();
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        public static void Show()
        {
            OverlayWindow.Show();
        }
    }
}
