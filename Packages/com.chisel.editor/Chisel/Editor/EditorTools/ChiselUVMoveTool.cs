using Chisel.Core;
using Chisel.Components;
using Chisel.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;
using UnityEditor.EditorTools;
using Snapping = UnitySceneExtensions.Snapping;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVMoveTool : ChiselEditToolBase
    {
        const string kToolName = "UV Move";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVMoveTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVMoveMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVMoveTool>(); }
        #endregion
        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllUV; } }

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

        static readonly int kSurfaceEditModeHash		= "SurfaceMoveEditMode".GetHashCode();
        static readonly int kSurfaceMoveHash			= "SurfaceMove".GetHashCode();


        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Keyboard, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint			= ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);            
            repaint = SurfaceMoveTool(selectionType,   dragArea) || repaint; 
            
            // Set cursor depending on selection type and/or active tool
            {
                MouseCursor cursor;
                switch (selectionType)
                {
                    default: cursor = MouseCursor.MoveArrow; break;
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

        #region Surface Move Tool
        static void TranslateSurfacesInWorldSpace(Vector3 translation)
        {
            var movementInWorldSpace = Matrix4x4.TRS(-translation, Quaternion.identity, Vector3.one); 
            Undo.RecordObjects(ChiselUVToolCommon.selectedNodes, "Moved UV coordinates");
            for (int i = 0; i < ChiselUVToolCommon.selectedSurfaceReferences.Length; i++)
            {
                // Translates the uv surfaces in a given direction. Since the z direction, relatively to the surface, 
                // is basically removed in this calculation, it should behave well when we move multiple selected surfaces
                // in any direction.
                ChiselUVToolCommon.selectedSurfaceReferences[i].WorldSpaceTransformUV(movementInWorldSpace, ChiselUVToolCommon.selectedUVMatrices[i]);
            }
        }

        private static bool SurfaceMoveTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceMoveHash, FocusType.Keyboard, dragArea);
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;

            bool needRepaint = false;
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support moving texture using keyboard
                case EventType.Repaint:
                {
                    if (!ChiselUVToolCommon.ToolIsDragging)
                        break;

                    ChiselUVToolCommon.RenderIntersectionPoint();
                    ChiselUVToolCommon.RenderSnapEvent();

                    // TODO: show delta movement of uv
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!ChiselUVToolCommon.IsToolEnabled(id))
                        break;

                    ChiselUVToolCommon.StartToolDragging();
                    TranslateSurfacesInWorldSpace(ChiselUVToolCommon.worldDragDeltaVector);
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
