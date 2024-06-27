using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    //[EditorTool("Chisel " + kToolName + " Tool")]
    class ChiselPlacementTool : ChiselEditToolBase
    {
        static ChiselPlacementTool instance = null;
        ChiselPlacementTool() { instance = this; }

        public const string kToolName = "Placement";
        public override string ToolName => kToolName;

        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllGeometry; } }

        public override GUIContent Content { get { return ChiselGeneratorManager.GeneratorMode.Content; } }

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselPlacementTool); }
        
        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToCreateEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselPlacementTool>(); }

        public static void DeactivateTool(bool selectNode = false)
        {
            if (!IsActive())
                return;
            // Unity has unreliable events
            ChiselGeneratorManager.GeneratorMode.OnDeactivate();
            ToolManager.RestorePreviousPersistentTool();
            if (!IsActive())
                return;

            if (selectNode && ChiselToolbarUtility.HaveNodesInSelection())
            {
                ChiselEditGeneratorTool.ActivateTool();
                if (!IsActive())
                    return;
            }

            ToolManager.RestorePreviousTool();
            if (!IsActive())
                return;

            Tools.current = Tool.Move;
        }
        #endregion

        public override void OnActivate()
        {
            ChiselPlacementOptionsOverlay.Show();
            base.OnActivate();
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
            ChiselGeneratorManager.GeneratorMode.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
            UpdatePlacementToolIcon();
        }

        public override void OnDeactivate()
        {
            ChiselPlacementOptionsOverlay.Hide();
            base.OnDeactivate();
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            ChiselGeneratorManager.GeneratorMode.OnDeactivate();
        }

        public void OnSelectionChanged()
        {
            DeactivateTool(selectNode: true);
        }

        public virtual void Cancel()
        {
            var generatorMode = ChiselGeneratorManager.GeneratorMode;
            if (generatorMode == null)
                return;
            
            if (!generatorMode.IsGenerating)
            {
                DeactivateTool();
                GUIUtility.ExitGUI();
            } else
            {
                if (generatorMode != null)
                    generatorMode.Reset();
                Undo.RevertAllInCurrentGroup();
                GUIUtility.ExitGUI();
            }
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var generatorMode = ChiselGeneratorManager.GeneratorMode;
            if (generatorMode == null)
                return;

            switch (Event.current.type)
            {
                case EventType.KeyDown:
                {
                    if (Event.current.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (Event.current.keyCode == ChiselKeyboardDefaults.kCancelKey)
                    {
                        Cancel();
                        Event.current.Use();
                    }
                    break;
                }
            }

            generatorMode.ShowSceneGUI(sceneView, dragArea);
        }

        public static void UpdatePlacementToolIcon()
        {
            if (instance != null)
            {
                instance.UpdateIcon();
            }
        }
    }
}
