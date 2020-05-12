using Chisel.Components;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    class ChiselEditGeneratorTool : ChiselEditToolBase
    {
        const string kToolName = "Edit Generator";
        public override string ToolName => kToolName;

        public static bool IsActive() { return EditorTools.activeToolType == typeof(ChiselEditGeneratorTool); }


        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToShapeEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { EditorTools.SetActiveTool<ChiselEditGeneratorTool>(); }
        #endregion

        public static ChiselOverlay.WindowFunction OnEditSettingsGUI; 
        public static string CurrentEditorName;
         
        public override void OnSceneSettingsGUI(SceneView sceneView)
        {
            DefaultSceneSettingsGUI(sceneView);
        }

        public static void DefaultSceneSettingsGUI(SceneView sceneView)
        {
            OnEditSettingsGUI?.Invoke(sceneView);
        }

        public override void OnActivate()
        {
            base.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // NOTE: Actual work is done by Editor classes
            if (string.IsNullOrEmpty(CurrentEditorName))
                ChiselOptionsOverlay.SetTitle("Edit");
            else
                ChiselOptionsOverlay.SetTitle($"Edit {CurrentEditorName}");
            ChiselOptionsOverlay.AdditionalSettings = OnSceneSettingsGUI;
            ChiselOptionsOverlay.ShowSnappingTool = Tool.Move;
            ChiselOptionsOverlay.ShowSnappingToolUV = false;
        }
    }
}
