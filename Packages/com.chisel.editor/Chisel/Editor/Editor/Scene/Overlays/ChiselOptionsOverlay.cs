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


        const int kPrimaryOrder = int.MaxValue;
        
        const string                    kOverlayTitle   = "Chisel";
        static readonly ChiselOverlay   OverlayWindow   = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);

        static GUIContent kRebuildButton;

        static SortedList<string, ChiselEditToolBase> editModes = new SortedList<string, ChiselEditToolBase>();

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

        const float kButtonSize = 32;
        static GUILayoutOption[] buttonOptions = new[]
        {
            GUILayout.Width(kButtonSize),
            GUILayout.Height(kButtonSize)
        };


        static bool Toggle(ChiselEditToolBase editMode, Type editModeType)
        {
            var content = editMode.m_ToolIcon;
            var selected = EditorTools.activeToolType == editModeType;
            return GUILayout.Toggle(selected, content, styles.toggleStyle, buttonOptions);
        }

        static void EditModeButton(ChiselEditToolBase editMode, bool enabled)
        { 
            var editModeType = editMode.GetType();
            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();
                var value = Toggle(editMode, editModeType);
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
        }

        static Styles styles = null;
        static void InitStyles()
        {
            if (styles == null)
            {
                ChiselEditorSettings.Load();
                styles = new Styles
                {
                    toggleStyle = new GUIStyle(GUI.skin.button)
                    {
                        padding = new RectOffset(3, 3, 3, 3)
                    }
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
                        GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);

                        foreach (var editMode in editModes.Values)
                        {
                            EditModeButton(editMode, enabled);
                        }
                        GUILayout.FlexibleSpace();

                        // TODO: assign hotkey to rebuild, and possibly move it elsewhere to avoid it seemingly like a necessary action.

                        if (GUILayout.Toggle(false, kRebuildButton, styles.toggleStyle, buttonOptions))
                        {
                            Rebuild();
                        }
                        GUILayout.EndHorizontal();
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
