using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    public sealed class ChiselShapeEditMode : IChiselToolMode
    {
        const string kToolName = "Shape Edit";
        public string ToolName => kToolName;

        public bool EnableComponentEditors  { get { return true; } }
        public bool CanSelectSurfaces       { get { return false; } }
        public bool ShowCompleteOutline     { get { return true; } }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = ChiselKeyboardDefaults.ShortCutEditModeBase + kToolName + " Mode";
        [Shortcut(kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToShapeEditMode, displayName = kEditModeShotcutName)]
        public static void SwitchToShapeEditMode() { ChiselEditModeManager.EditModeType = typeof(ChiselShapeEditMode); }
        #endregion

        public void OnEnable()
        {
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.None;
            // TODO: shouldn't just always set this param
            Tools.hidden = true; 
        }

        public void OnDisable()
        {

        }

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // NOTE: Actual work is done by Editor classes
        }
    }
}
