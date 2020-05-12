using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.EditorTools;

namespace Chisel.Editors
{
    // TODO: add tooltips
    public static class ChiselOptionsOverlay
    {
        const string kRebuildIconName   = "rebuild";
        const string kRebuildTooltip    = "Force rebuild all generated meshes";

        public static void Rebuild()
        {
            UnityEngine.Profiling.Profiler.BeginSample("Rebuild");
            try
            {
                var startTime = Time.realtimeSinceStartup;
                ChiselNodeHierarchyManager.Rebuild();
                var csg_endTime = Time.realtimeSinceStartup;
                Debug.Log($"Full CSG rebuild done in {((csg_endTime - startTime) * 1000)} ms. ");
            }
            finally
            {
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }

        public static ChiselOverlay.WindowFunction AdditionalSettings;
        public static Tool ShowSnappingTool = Tool.None;
        public static bool ShowSnappingToolUV = true;

        const int kPrimaryOrder = int.MaxValue;
        
        const string                    kOverlayTitle   = "Chisel";
        static readonly ChiselOverlay   OverlayWindow   = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);


        static SortedList<string, ChiselEditToolBase> editModes = new SortedList<string, ChiselEditToolBase>();

        static GUIContent kRebuildButton;
        [InitializeOnLoadMethod]
        internal static void Initialize()
        {
            kRebuildButton = ChiselEditorResources.GetIconContent(kRebuildIconName, kRebuildTooltip)[0];
        }

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



        static bool Toggle(Rect position, ChiselEditToolBase editMode, Type editModeType)
        {
            var selected = EditorTools.activeToolType == editModeType;
            var content = selected ? editMode.ActiveIconContent : editMode.IconContent;
            return GUI.Toggle(position, selected, content, styles.toggleStyle);
        }

        static void EditModeButton(Rect position, ChiselEditToolBase editMode, bool enabled)
        { 
            var editModeType = editMode.GetType();
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();
                var value = Toggle(position, editMode, editModeType);
                if (EditorGUI.EndChangeCheck() && value)
                {
                    EditorTools.SetActiveTool(editModeType);
                    ChiselEditorSettings.Save();
                }
            }
        }

        class Styles
        {
            public GUIStyle toggleStyle;
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
                    toggleStyle = new GUIStyle(GUI.skin.button)
                    {
                        padding     = new RectOffset(kButtonPadding, kButtonPadding, kButtonPadding, kButtonPadding),
                        margin      = new RectOffset(kButtonMargin,  kButtonMargin,  kButtonMargin,  kButtonMargin - 2),
                        fixedWidth  = kButtonSize,
                        fixedHeight = kButtonSize,
                    },
                    buttonRowStyle = new GUIStyle(GUIStyle.none)
                };
            }
        }

        // TODO: move somewhere else
        public static bool HaveNodesInSelection()
        {
            return Selection.GetFiltered<ChiselNode>(SelectionMode.OnlyUserModifiable).Length > 0;
        }


        static void DisplayControls(SceneView sceneView)
        {
            InitStyles();
            EditorGUI.BeginChangeCheck();
            {
                AdditionalSettings?.Invoke(sceneView);

                var enabled = HaveNodesInSelection();

                if (editModes.Values.Count > 0)
                {
                    using (new EditorGUI.DisabledScope(!enabled))
                    {
                        var style       = styles.toggleStyle;
                        var groupRect   = EditorGUILayout.GetControlRect(false, kButtonSize + style.margin.vertical, ChiselOverlay.kMinWidthLayout);
                        groupRect.xMin -= 3;
                        groupRect.yMin += 3;

                        var startX      = style.margin.left + groupRect.x + 3;
                        var buttonStep  = kButtonSize + style.margin.left;
                        var position    = new Rect(startX, groupRect.y, kButtonSize, style.fixedHeight);

                        int xPos = 0;
                        foreach (var editMode in editModes.Values)
                        {
                            position.x = startX + (xPos * buttonStep);
                            EditModeButton(position, editMode, enabled);
                            xPos++;
                        }

                        // TODO: assign hotkey to rebuild, and possibly move it elsewhere to avoid it seemingly like a necessary action.

                        xPos = 7;
                        position.x = startX + (xPos * buttonStep);
                        if (GUI.Toggle(position, false, kRebuildButton, styles.toggleStyle))
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

        public static void SetTitle(string title)
        {
            OverlayWindow.Title = title;
        }

        public static void Show()
        {
            OverlayWindow.Show();
        }
    }
}
