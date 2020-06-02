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
using UnityObject = UnityEngine.Object;
 
namespace Chisel.Editors
{
    [EditorTool("Chisel " + kToolName + " Tool")]
    class ChiselCreateTool : ChiselEditToolBase
    {
        public const string kToolName = "Create";
        public override string ToolName => kToolName;

        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllGeometry; } }

        public override GUIContent Content
        {
            get 
            {
                return ChiselGeneratorManager.GeneratorMode.Content;
            } 
        }

        public static bool IsActive() { return EditorTools.activeToolType == typeof(ChiselCreateTool); }
        
        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToCreateEditMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { EditorTools.SetActiveTool<ChiselCreateTool>(); }

        public static void DeactivateTool(bool selectNode = false)
        {
            if (!IsActive())
                return;
            // Unity has unreliable events
            ChiselGeneratorManager.GeneratorMode.OnDeactivate();
            EditorTools.RestorePreviousPersistentTool();
            if (!IsActive())
                return;

            if (selectNode && ChiselToolsOverlay.HaveNodesInSelection())
            {
                ChiselEditGeneratorTool.ActivateTool();
                if (!IsActive())
                    return;
            }

            EditorTools.RestorePreviousTool();
            if (!IsActive())
                return;

            Tools.current = Tool.Move;
        }
        #endregion

        public override void OnActivate()
        {
            base.OnActivate();
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
            ChiselGeneratorManager.GeneratorMode.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            ChiselGeneratorManager.GeneratorMode.OnDeactivate();
        }

        public void OnSelectionChanged()
        {
            DeactivateTool(selectNode: true);
        }

        #region In-scene Options GUI
        public override string OptionsTitle => $"{ChiselGeneratorManager.GeneratorMode.ToolName} Options";
        public override void OnInSceneOptionsGUI(SceneView sceneView)
        {
            ChiselGeneratorManager.GeneratorMode.OnSceneSettingsGUI(sceneView);
        }
        #endregion

        public virtual void Cancel()
        {
            DeactivateTool();
            GUIUtility.ExitGUI();
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
                    if (Event.current.keyCode == KeyCode.Escape)
                    {
                        Event.current.Use();
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (Event.current.keyCode == KeyCode.Escape)
                    {
                        Cancel();
                        Event.current.Use();
                    }
                    break;
                }
            }



            ChiselOptionsOverlay.AdditionalSettings = OnInSceneOptionsGUI;
            generatorMode.ShowSceneGUI(sceneView, dragArea);

            /// TODO: pressing escape when not in the middle of creation something, should cancel this edit mode instead
        }
    }
}
