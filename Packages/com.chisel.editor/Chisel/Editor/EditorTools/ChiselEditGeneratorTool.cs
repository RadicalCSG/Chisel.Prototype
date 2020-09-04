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
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    class ChiselEditGeneratorTool : ChiselEditToolBase
    {
        const string kToolName = "Edit Generator";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselEditGeneratorTool); }

        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllGeometry; } }


        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToShapeEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselEditGeneratorTool>(); }
        #endregion

        #region In-scene Options GUI
        public static ChiselOverlay.WindowFunction OnEditSettingsGUI; 
        public static string CurrentEditorName;

        public override string OptionsTitle => CurrentEditorName == null ? "Options" : $"{CurrentEditorName} Options";
        public override void OnInSceneOptionsGUI(SceneView sceneView)
        {
            OnEditSettingsGUI?.Invoke(sceneView);
        }
        #endregion

        public override void OnActivate()
        {
            base.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.KeyDown:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (GUIUtility.hotControl == 0)
                        {
                            evt.Use();
                            break;
                        }
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (evt.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        if (GUIUtility.hotControl == 0) 
                        {
                            Selection.activeTransform = null;
                            evt.Use();
                            GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
                            break;
                        }
                    }
                    break;
                }
            }

            // NOTE: Actual work is done by Editor classes
            ChiselOptionsOverlay.AdditionalSettings = OnEditSettingsGUI;
        }
    }
}
