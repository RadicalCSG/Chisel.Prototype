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
    public class ChiselShapeEditMode : IChiselToolMode
    {
        #region Keyboard Shortcut
        const string kEditModeShotcutName = ChiselKeyboardDefaults.ShortCutEditModeBase + "Shape Edit Mode";
        [Shortcut(kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToShapeEditMode, displayName = kEditModeShotcutName)]
        public static void SwitchToShapeEditMode() { ChiselEditModeManager.EditMode = ChiselEditMode.ShapeEdit; }
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
