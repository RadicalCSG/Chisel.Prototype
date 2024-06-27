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
using Snapping = UnitySceneExtensions.Snapping;
using UnityEditor.EditorTools;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVScaleTool : ChiselEditToolBase
    {
        const string kToolName = "UV Scale";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVScaleTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVScaleMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVScaleTool>(); }
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
        
        public override SnapSettings ToolUsedSnappingModes { get { return UnitySceneExtensions.SnapSettings.AllUV; } }

        static readonly int kSurfaceEditModeHash		= "SurfaceScaleEditMode".GetHashCode();
        static readonly int kSurfaceScaleHash			= "SurfaceScale".GetHashCode();
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
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


        static bool     haveScaleStartLength = false;
        static float    compareDistance = 0;
        const float     kMinScaleDiameter = 2.0f;

        #region Surface Scale Tool
        static void ScaleSurfacesInWorldSpace(Vector3 center, Vector3 normal, float scale)
        {
            if (float.IsNaN(scale) ||
                float.IsInfinity(scale) ||
                scale == 0.0f)
                return;

            // Get the rotation on that plane, around 'worldStartPosition'
            var worldspaceRotation = MathExtensions.ScaleFromPoint(center, normal, scale);

            Undo.RecordObjects(ChiselUVToolCommon.selectedNodes, "Scale UV coordinates");
            for (int i = 0; i < ChiselUVToolCommon.selectedSurfaceReferences.Length; i++)
            {
                var rotationInPlaneSpace = ChiselUVToolCommon.selectedSurfaceReferences[i].WorldSpaceToPlaneSpace(worldspaceRotation);

                // TODO: Finish this. If we have multiple surfaces selected, we want other non-aligned surfaces to move/rotate in a nice way
                //		 last thing we want is that these surfaces are rotated in such a way that the uvs are rotated into infinity.
                //		 ideally the rotation would change into a translation on 90 angles, think selecting all surfaces on a cylinder 
                //	     and rotating the cylinder cap. You would want the sides to move with the rotation and not actually rotate themselves.
                var rotateToPlane = Quaternion.FromToRotation(rotationInPlaneSpace.GetColumn(2), Vector3.forward);
                var fixedRotation = Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInPlaneSpace;

                ChiselUVToolCommon.selectedSurfaceReferences[i].PlaneSpaceTransformUV(fixedRotation, ChiselUVToolCommon.selectedUVMatrices[i]);
            }
        }

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
                    if (haveScaleStartLength)
                    {
                        var toWorldVector   = ChiselUVToolCommon.worldDragDeltaVector;
                        var magnitude       = toWorldVector.magnitude;
                        toWorldVector /= magnitude;
                        if (haveScaleStartLength)
                        {
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (toWorldVector * compareDistance), 4.0f);
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (toWorldVector * magnitude), 4.0f);
                        } else
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (toWorldVector * magnitude), 4.0f);
                    }
                    if (ChiselUVToolCommon.IsToolEnabled(id))
                    {
                        if (haveScaleStartLength &&
                            ChiselUVToolCommon.pointHasSnapped)
                        {
                            ChiselUVToolCommon.RenderIntersectionPoint();
                            ChiselUVToolCommon.RenderSnapEvent();
                        }
                    } 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!ChiselUVToolCommon.IsToolEnabled(id))
                        break;
                    
                    if (ChiselUVToolCommon.StartToolDragging())
                    {
                        haveScaleStartLength = false;
                        ChiselUVToolCommon.pointHasSnapped = false;
                    }

                    var toWorldVector = ChiselUVToolCommon.worldDragDeltaVector;
                    if (!haveScaleStartLength)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(ChiselUVToolCommon.worldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinScaleDiameter;
                        // Only start scaling when we've moved the cursor far away enough from the center of scale
                        if (toWorldVector.sqrMagnitude > minDiameterSqr)
                        {
                            // Switch to scaling mode, we have a center and a start distance to compare with, 
                            // from now on, when we move the mouse we change the scale relative to this first distance.
                            haveScaleStartLength = true;
                            ChiselUVToolCommon.pointHasSnapped = false;
                            compareDistance = toWorldVector.sqrMagnitude;
                        }
                    } else
                    {
                        // TODO: drag from one position to another -> texture should fit in between and tile accordingly, taking rotation into account
                        ScaleSurfacesInWorldSpace(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldDragPlane.normal, compareDistance / toWorldVector.sqrMagnitude);
                    }
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
