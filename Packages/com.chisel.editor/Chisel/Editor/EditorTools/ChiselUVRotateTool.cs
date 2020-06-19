using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Chisel.Utilities;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using Snapping = UnitySceneExtensions.Snapping;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene

    [EditorTool("Chisel " + kToolName + " Tool", typeof(ChiselNode))]
    sealed class ChiselUVRotateTool : ChiselEditToolBase
    {
        const string kToolName = "UV Rotate";
        public override string ToolName => kToolName;

        public static bool IsActive() { return ToolManager.activeToolType == typeof(ChiselUVRotateTool); }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = kToolName + " Mode";
        [Shortcut(ChiselKeyboardDefaults.ShortCutEditModeBase + kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToUVRotateMode, displayName = kEditModeShotcutName)]
        public static void ActivateTool() { ToolManager.SetActiveTool<ChiselUVRotateTool>(); }
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

        #region In-scene Options GUI
        public override string OptionsTitle => $"UV Options";
        public override void OnInSceneOptionsGUI(SceneView sceneView)
        {
            ChiselUVToolCommon.Instance.OnSceneSettingsGUI(sceneView);
        }

        static readonly int kSurfaceEditModeHash		= "SurfaceRotateEditMode".GetHashCode();
        static readonly int kSurfaceRotateHash			= "SurfaceRotate".GetHashCode();
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            ChiselOptionsOverlay.AdditionalSettings = OnInSceneOptionsGUI;

            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= ChiselRectSelectionManager.GetCurrentSelectionType();
            var repaint			= ChiselUVToolCommon.SurfaceSelection(dragArea, selectionType);            
            repaint = SurfaceRotateTool(selectionType, dragArea) || repaint; 
            
            // Set cursor depending on selection type and/or active tool
            {
                MouseCursor cursor; 
                switch (selectionType)
                {
                    default: cursor = MouseCursor.RotateArrow; break;
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

        #region Surface Rotate Tool
        static void RotateSurfacesInWorldSpace(Vector3 center, Vector3 normal, float rotateAngle)
        {
            // Get the rotation on that plane, around 'worldStartPosition'
            var worldspaceRotation = MathExtensions.RotateAroundAxis(center, normal, rotateAngle);

            Undo.RecordObjects(ChiselUVToolCommon.selectedBrushContainerAsset, "Rotate UV coordinates");
            for (int i = 0; i < ChiselUVToolCommon.selectedSurfaceReferences.Length; i++)
            {
                var rotationInPlaneSpace = ChiselUVToolCommon.selectedSurfaceReferences[i].WorldSpaceToPlaneSpace(in worldspaceRotation);

                // TODO: Finish this. If we have multiple surfaces selected, we want other non-aligned surfaces to move/rotate in a nice way
                //		 last thing we want is that these surfaces are rotated in such a way that the uvs are rotated into infinity.
                //		 ideally the rotation would change into a translation on 90 angles, think selecting all surfaces on a cylinder 
                //	     and rotating the cylinder cap. You would want the sides to move with the rotation and not actually rotate themselves.
                var rotateToPlane = Quaternion.FromToRotation(rotationInPlaneSpace.GetColumn(2), Vector3.forward);
                var fixedRotation = Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInPlaneSpace;

                ChiselUVToolCommon.selectedSurfaceReferences[i].PlaneSpaceTransformUV(in fixedRotation, in ChiselUVToolCommon.selectedUVMatrices[i]);
            }
        }

        static Vector3  fromWorldVector;
        static bool		haveRotateStartAngle	= false;
        static float	rotateAngle	            = 0;

        const float		kMinRotateDiameter		= 1.0f;

        private static bool SurfaceRotateTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceRotateHash, FocusType.Keyboard, dragArea);
            if (!ChiselUVToolCommon.SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool needRepaint = false;            
            if (!ChiselUVToolCommon.IsToolEnabled(id))
            {
                needRepaint = haveRotateStartAngle;
                haveRotateStartAngle = false;
                ChiselUVToolCommon.pointHasSnapped = false;
            }
            
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support rotating texture using keyboard?
                case EventType.Repaint:
                {
                    if (haveRotateStartAngle)
                    {
                        var toWorldVector   = ChiselUVToolCommon.worldDragDeltaVector;
                        var magnitude       = toWorldVector.magnitude;
                        toWorldVector /= magnitude;

                        // TODO: need a nicer visualization here, show delta rotation, angles etc.
                        Handles.DrawWireDisc(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldProjectionPlane.normal, magnitude);
                        if (haveRotateStartAngle)
                        {
                            var snappedToWorldVector = Quaternion.AngleAxis(rotateAngle, ChiselUVToolCommon.worldDragPlane.normal) * fromWorldVector;
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (fromWorldVector      * magnitude), 4.0f);
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (snappedToWorldVector * magnitude), 4.0f);
                        } else
                            Handles.DrawDottedLine(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldStartPosition + (toWorldVector * magnitude), 4.0f);
                    }
                    if (ChiselUVToolCommon.IsToolEnabled(id))
                    {
                        if (haveRotateStartAngle &&
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
                        haveRotateStartAngle = false;
                        ChiselUVToolCommon.pointHasSnapped = false;
                    }

                    var toWorldVector = ChiselUVToolCommon.worldDragDeltaVector;
                    if (!haveRotateStartAngle)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(ChiselUVToolCommon.worldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinRotateDiameter;
                        // Only start rotating when we've moved the cursor far away enough from the center of rotation
                        if (toWorldVector.sqrMagnitude > minDiameterSqr)
                        {
                            // Switch to rotation mode, we have a center and a start angle to compare with, 
                            // from now on, when we move the mouse we change the rotation angle relative to this first angle.
                            haveRotateStartAngle = true;
                            ChiselUVToolCommon.pointHasSnapped = false;
                            fromWorldVector = toWorldVector.normalized;
                            rotateAngle = 0;

                            // We override the snapping settings to only allow snapping against vertices & edges, 
                            // we do this only after we have our starting vector, so that when we rotate we're not constantly
                            // snapping against the grid when we really just want to be able to snap against the current rotation step.
                            // On the other hand, we do want to be able to snap against vertices ..
                            ChiselUVToolCommon.toolSnapOverrides = SnapSettings.UVGeometryVertices | SnapSettings.UVGeometryEdges; 
                        }
                    } else
                    {
                        // Get the angle between 'from' and 'to' on the plane we're dragging over
                        rotateAngle = MathExtensions.SignedAngle(fromWorldVector, toWorldVector.normalized, ChiselUVToolCommon.worldDragPlane.normal);
                        
                        // If we snapped against something, ignore angle snapping
                        if (!ChiselUVToolCommon.pointHasSnapped) rotateAngle = ChiselUVToolCommon.SnapAngle(rotateAngle);

                        RotateSurfacesInWorldSpace(ChiselUVToolCommon.worldStartPosition, ChiselUVToolCommon.worldDragPlane.normal, -rotateAngle); // TODO: figure out why this is reversed
                    }
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
