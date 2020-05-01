using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Chisel.Utilities;
using UnityEditor.ShortcutManagement;
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVScaleTool : ChiselEditToolBase
    {
        const string kToolName = "UV Scale";
        public override string ToolName => kToolName;

        public static bool IsActive() { return EditorTools.activeToolType == typeof(ChiselUVScaleTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVScaleMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { EditorTools.SetActiveTool<ChiselUVScaleTool>(); }
        #endregion

        #region Activate/Deactivate
        public override void OnActivate()
        {
            base.OnActivate();
            ChiselUVToolCommon.Instance.OnActivate();
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Surface;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            ChiselUVToolCommon.Instance.OnDeactivate();
        }
        #endregion

        #region Scene GUI
        public override void OnSceneSettingsGUI(SceneView sceneView)
        {
            ChiselUVToolCommon.Instance.OnSceneSettingsGUI(sceneView);
        }

        static readonly int kSurfaceEditModeHash		= "SurfaceEditMode".GetHashCode();
        static readonly int kSurfaceScaleHash			= "SurfaceScale".GetHashCode();
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            ChiselOptionsOverlay.AdditionalSettings = OnSceneSettingsGUI;

            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint			= ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);
            repaint = SurfaceScaleTool(selectionType,  dragArea) || repaint;

            // Set cursor depending on selection type and/or active tool
            {
                MouseCursor cursor;
                switch (selectionType)
                {
                    default: cursor = MouseCursor.ScaleArrow; break;
                    case SelectionType.Additive:    cursor = MouseCursor.ArrowPlus; break;
                    case SelectionType.Subtractive: cursor = MouseCursor.ArrowMinus; break;
                }
                EditorGUIUtility.AddCursorRect(dragArea, cursor);
            }
            
            // Repaint the scene-views if we need to
            if (repaint &&
                // avoid infinite loop
                Event.current.type != EventType.Layout &&
                Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }
        #endregion

        #region Surface Scale Tool
        private static bool SurfaceScaleTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceScaleHash, FocusType.Keyboard, dragArea);
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;

            bool needRepaint = false;            
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support scaling texture using keyboard
                case EventType.Repaint:
                {
                    // TODO: show scaling of uv
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!ChiselUVToolCommon.IsToolEnabled(id))
                        break;

                    ChiselUVToolCommon.StartToolDragging();
                    // TODO: implement
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
