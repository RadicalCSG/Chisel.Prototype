using Chisel.Assets;
using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Chisel.Utilities;

namespace Chisel.Editors
{
    // TODO: make it possible to 'F' focus on selected surfaces
    // TODO: hovering on surfaces in inspector should highlight in scene
    public sealed class CSGSurfaceEditMode : ICSGToolMode
    {
        static readonly int kSurfaceEditModeHash		= "SurfaceEditMode".GetHashCode();
        static readonly int kSurfaceDragSelectionHash	= "SurfaceDragSelection".GetHashCode();
        static readonly int kSurfaceScaleHash			= "SurfaceScale".GetHashCode();
        static readonly int kSurfaceRotateHash			= "SurfaceRotate".GetHashCode();
        static readonly int kSurfaceMoveHash			= "SurfaceMove".GetHashCode();
		
        static bool InEditCameraMode	{ get { return (Tools.viewTool == ViewTool.Pan || Tools.viewTool == ViewTool.None); } }
        static bool ToolIsDragging		{ get; set; }
        static bool MouseIsDown			{ get; set; }
		

		// TODO: shouldn't 'just' always show the default tools
		public void OnEnable() { Tools.hidden = true; }
		public void OnDisable() { }
		

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
			var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType	= CSGRectSelectionManager.GetCurrentSelectionType();
            var repaint			= SurfaceSelection(dragArea, selectionType);
			var cursor			= MouseCursor.Arrow;

			// Handle tool specific actions
			switch (Tools.current)
            {
                case Tool.Move:		repaint = SurfaceMoveTool(selectionType,   dragArea) || repaint; cursor = MouseCursor.MoveArrow;   break;
                case Tool.Rotate:	repaint = SurfaceRotateTool(selectionType, dragArea) || repaint; cursor = MouseCursor.RotateArrow; break;
                case Tool.Scale:	repaint = SurfaceScaleTool(selectionType,  dragArea) || repaint; cursor = MouseCursor.ScaleArrow;  break;
                //case Tool.Rect:	break;
            }
			
			// Set cursor depending on selection type and/or active tool
			{ 
				switch (selectionType)
				{
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

		#region Hover Surfaces
		static CSGTreeBrushIntersection? hoverIntersection;
		static readonly HashSet<SurfaceReference> hoverSurfaces = new HashSet<SurfaceReference>();


		static CSGTreeBrushIntersection SnapIntersection(CSGTreeBrushIntersection intersection)
		{
			// TODO: snap to closest vertices of surface
			// TODO: snap to edges
			// TODO: if we're snapping to grid:
			// TODO:	snap to grid on plane
			// TODO:	snap to where grid lines cross edges

			return intersection;
		}

		static void RenderIntersection()
		{
			if (ToolIsDragging)
				return;

			if (!hoverIntersection.HasValue)
				return;

			var intersectionPoint = hoverIntersection.Value.surfaceIntersection.worldIntersection;
			var normal = hoverIntersection.Value.surfaceIntersection.worldPlane.normal;
			SceneHandles.RenderBorderedCircle(intersectionPoint, HandleUtility.GetHandleSize(intersectionPoint) * 0.02f);
		}

		static bool UpdateHoverSurfaces(Vector2 mousePosition, Rect dragArea, SelectionType selectionType, bool clearHovering)
        {
            try
            {
				hoverIntersection = null;

				bool modified = false;
                if (clearHovering || !InEditCameraMode)
                {
                    if (hoverSurfaces.Count != 0)
                    {
                        hoverSurfaces.Clear();
                        modified = true;
                    }
                }

                if (!dragArea.Contains(mousePosition))
                    return modified;

                if (!InEditCameraMode)
                    return modified;

				CSGTreeBrushIntersection intersection;
				var foundSurfaces = CSGClickSelectionManager.FindSurfaceReference(mousePosition, false, out intersection);
				if (foundSurfaces == null)
                {
                    modified = (hoverSurfaces != null) || modified;
					hoverIntersection = null;
					return modified;
				}

				hoverIntersection = SnapIntersection(intersection);
				if (foundSurfaces.Length == hoverSurfaces.Count)
                    modified = !hoverSurfaces.ContainsAll(foundSurfaces) || modified;
                else
                    modified = true;

				if (foundSurfaces.Length > 0)
					hoverSurfaces.AddRange(foundSurfaces);
                return modified;
            }
            finally
            {
                CSGSurfaceSelectionManager.SetHovering(selectionType, hoverSurfaces);
            }
        }
        #endregion
		
		#region Selection
		static bool ClickSelection(Rect dragArea, SelectionType selectionType)
        {
            return CSGSurfaceSelectionManager.UpdateSelection(selectionType, hoverSurfaces);
        }
        
        static bool SurfaceSelection(Rect dragArea, SelectionType selectionType)
        {
            var id = GUIUtility.GetControlID(kSurfaceDragSelectionHash, FocusType.Keyboard, dragArea);
            
            bool repaint = false;

            var evt = Event.current;
            if (evt.type == EventType.MouseMove ||
                evt.type == EventType.MouseDown)
            {
                if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                    repaint = true;
                
                if (!InEditCameraMode)
                    return repaint;
            }

            if (InEditCameraMode && !ToolIsDragging)
            {
                if (MouseIsDown) 
                    CSGOutlineRenderer.VisualizationMode = VisualizationMode.None;
                else
                    CSGOutlineRenderer.VisualizationMode = VisualizationMode.Surface | VisualizationMode.Outline;
            } else
                CSGOutlineRenderer.VisualizationMode = VisualizationMode.None;


            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;

                    if (hoverSurfaces != null && hoverSurfaces.Count > 0)
                        HandleUtility.AddControl(id, 3.0f);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != 0)
                        break;
                    
                    // we only do drag selection when we use a modifier (shift, control etc.)
                    if (selectionType == SelectionType.Replace)
                        break;
                    
                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;
                    
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    evt.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, false);
                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || evt.button != 0)
                        break;
                    
                    GUIUtility.hotControl = 0;
                    GUIUtility.keyboardControl = 0;
                    evt.Use();

                    if (CSGSurfaceSelectionManager.UpdateSelection(selectionType, hoverSurfaces))
                        repaint = true;

                    if (UpdateHoverSurfaces(evt.mousePosition, dragArea, selectionType, true))
                        repaint = true;
                    break;
                }
            }

            return repaint;
        }

		static void ResetSelection()
		{
			hoverIntersection = null;
			selectedSurfaceReferences = null;
			selectedBrushMeshAsset = null;
			selectedUVMatrices = null;
		}
		#endregion

		#region Tool Base
		static bool CanEnableTool(int id)
        {
			// Is another control enabled at the moment?
			if (GUIUtility.hotControl != 0)
				return false;
			            
            var evt = Event.current;
			// Is our tool currently the control nearest to the mouse?
			if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
				(GUIUtility.keyboardControl != id || evt.button != 2))
				return false;
            return true;
        }

        static bool IsToolEnabled(int id)
		{
			// Is our control enabled at the moment?
			return GUIUtility.hotControl == id;
        }

        static void EnableTool(int id)
        {
            EditorGUIUtility.SetWantsMouseJumping(1);   // enable allowing the user to move the mouse over the bounds of the screen
			jumpedMousePosition = Event.current.mousePosition;  // remember the current mouse position so we can update it using Event.current.delta, 
																// since mousePosition won't make sense any more when mouse jumping
			GUIUtility.hotControl = GUIUtility.keyboardControl = id; // set our tool as the active control
            Event.current.Use(); // make sure no-one else can use our event
		}

        static void DisableTool()
        {
            EditorGUIUtility.SetWantsMouseJumping(0); // disable allowing the user to move the mouse over the bounds of the screen
            GUIUtility.hotControl = GUIUtility.keyboardControl = 0; // remove the active control so that the user can use another control
			Event.current.Use(); // make sure no-one else can use our event
		}

        private static bool SurfaceToolBase(int id, SelectionType selectionType, Rect dragArea)
        {
            // we only do tools when we do not use a modifier (shift, control etc.)
            if (selectionType != SelectionType.Replace)
                return false;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    HandleUtility.AddControl(id, 3.0f);
                    break;
                }
                case EventType.MouseMove:
                {
                    MouseIsDown = false;
                    break;
				}
				case EventType.MouseDown:
                {
					// we can only use a tool when the mouse cursor is inside the draggable scene area
					if (!dragArea.Contains(evt.mousePosition))
						return false;

					// we can only use a tool when we're hovering over a surfaces
					if (hoverSurfaces == null || hoverSurfaces.Count == 0)
						return false;

					if (!CanEnableTool(id))
						break;

					// We want to be able to tell the difference between dragging and clicking,
					// so we don't initialize dragging until we actually do
					MouseIsDown = true;
                    ToolIsDragging = false;
					EnableTool(id); 
                    break;
                }
                case EventType.MouseDrag:
                {
					if (!IsToolEnabled(id))
                        break;
                    
                    if (!ToolIsDragging)
                    {
                        // if we haven't dragged the tool yet, check if the surface underneath 
                        // the mouse is selected or not, if it isn't: select it exclusively
                        if (!CSGSurfaceSelectionManager.IsAnySelected(hoverSurfaces))
                            ClickSelection(dragArea, selectionType);
                    }
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    MouseIsDown = false;
					// We clicked, but didn't actually drag, so we can do a click selection
                    if (!ToolIsDragging)
                    {
                        // if we clicked on the surface, instead of dragged it, just click select it
                        ClickSelection(dragArea, selectionType);
                    }

					ToolIsDragging = false;
					DisableTool();
					ResetSelection();
					break;
				}
				case EventType.Repaint:
				{
					RenderIntersection();
					break;
				}
			}
            return true;
        }

        static SurfaceIntersection	startSurfaceIntersection;
        static SurfaceReference[]	selectedSurfaceReferences;
        static CSGBrushMeshAsset[]	selectedBrushMeshAsset;
        static UVMatrix[]			selectedUVMatrices;
        static Plane                worldDragPlane;
        static Plane                worldProjectionPlane;
        static Vector3				worldStartPosition;
		static Vector3				currentWorldIntersection;
        static Vector2              jumpedMousePosition;

        private static bool StartToolDragging()
		{
			jumpedMousePosition += Event.current.delta;
			Event.current.Use();
			if (ToolIsDragging)
				return false;

			ToolIsDragging = true;

			// Find the intersecting surfaces
			startSurfaceIntersection	= CSGClickSelectionManager.FindSurfaceIntersection(jumpedMousePosition);
            selectedSurfaceReferences	= CSGSurfaceSelectionManager.Selection.ToArray();

			// We need all the brushMeshAssets for all the surfaces we're moving, so that we can record them for an undo
            selectedBrushMeshAsset		= CSGSurfaceSelectionManager.SelectedBrushMeshes.ToArray();

			// We copy all the original surface uvMatrices, so we always apply rotations and transformations relatively to the original
			// This makes it easier to recover from edge cases and makes it more accurate, floating point wise.
            selectedUVMatrices			= new UVMatrix[selectedSurfaceReferences.Length];
            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
                selectedUVMatrices[i] = selectedSurfaceReferences[i].Polygon.description.UV0;
            
			// Find the intersection point/plane in model space
            var nodeTransform		= startSurfaceIntersection.surface.node.hierarchyItem.Transform;
            var modelTransform		= CSGNodeHierarchyManager.FindModelTransformOfTransform(nodeTransform);
            worldStartPosition		= modelTransform.localToWorldMatrix.MultiplyPoint (startSurfaceIntersection.intersection.worldIntersection);
            worldProjectionPlane	= modelTransform.localToWorldMatrix.TransformPlane(startSurfaceIntersection.intersection.worldPlane);
			currentWorldIntersection = worldStartPosition;

			// TODO: we want to be able to determine delta movement over a plane. Ideally it would match the position of the cursor perfectly.
			//		 unfortunately when moving the cursor towards the horizon of the plane, relative to the camera, the delta movement 
			//		 becomes too large or even infinity. Ideally we'd switch to a camera facing plane for these cases and determine movement in 
			//		 a less perfect way that would still allow the user to move or rotate things in a reasonable way.

			// more accurate for small movements
			worldDragPlane		= worldProjectionPlane;

			// TODO: (unfinished) prevents drag-plane from intersecting near plane (makes movement slow down to a singularity when further away from click position)
			//worldDragPlane	= new Plane(Camera.current.transform.forward, worldStartPosition); 

			// TODO: ideally we'd interpolate the behavior of the worldPlane between near and far behavior
			return true;
		}

        private static Vector3 GetCurrentWorldClick(Vector2 mousePosition)
        {
            var currentWorldIntersection = worldStartPosition;                    
            var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            var enter = 0.0f;
            if (worldDragPlane.UnsignedRaycast(mouseRay, out enter))
                currentWorldIntersection = mouseRay.GetPoint(enter);
            return currentWorldIntersection;
        }

		private static Vector3 MouseDragDeltaVector
		{
			get
			{ 
				var startSurface = startSurfaceIntersection.surface;

				currentWorldIntersection = GetCurrentWorldClick(jumpedMousePosition);
				var worldSpaceMovement = worldStartPosition - currentWorldIntersection;

				Vector3 tangent;
				Vector3 biNormal;
				MathExtensions.CalculateTangents(worldDragPlane.normal, out tangent, out biNormal);

				var deltaVector = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
								  biNormal * Vector3.Dot(biNormal, worldSpaceMovement);

				// TODO: does this still make sense?
				if (UnitySceneExtensions.Snapping.AxisLockX) deltaVector.x = 0;
				if (UnitySceneExtensions.Snapping.AxisLockY) deltaVector.y = 0;
				if (UnitySceneExtensions.Snapping.AxisLockZ) deltaVector.z = 0;

				return deltaVector;
			}
		}
		#endregion
		

		private static bool SurfaceScaleTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceScaleHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;

			bool needRepaint = false;            
            switch (Event.current.GetTypeForControl(id))
			{
				// TODO: support scaling texture using keyboard
				// TODO: add ability to cancel movement when pressing escape
				case EventType.Repaint:
				{
					// TODO: show scaling of uv
					break;
				}
				case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    StartToolDragging();
					var dragVector = MouseDragDeltaVector;
					break;
                }
            }
            return needRepaint;
        }


		static Vector3 fromWorldVector;
		static bool		haveRotateStartAngle	= false;

        const float		kMinRotateDiameter		= 1.0f;
        private static bool SurfaceRotateTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceRotateHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool needRepaint = false;            
            switch (Event.current.GetTypeForControl(id))
			{
				// TODO: support rotating texture using keyboard
				// TODO: add ability to cancel movement when pressing escape
				case EventType.Repaint:
				{
					if (haveRotateStartAngle)
					{
						// TODO: need a nicer representation here, show delta rotation, angles etc.
						Handles.DrawWireDisc(worldStartPosition, worldProjectionPlane.normal, (currentWorldIntersection - worldStartPosition).magnitude);
					} 
					break;
				}
				case EventType.MouseDrag:
				{
					if (!IsToolEnabled(id))
                        break;
                    
                    if (StartToolDragging())
					    haveRotateStartAngle = false;

                    var toWorldVector = MouseDragDeltaVector;
                    if (!haveRotateStartAngle)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(worldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinRotateDiameter;
						// Only start rotating when we've moved the cursor far away enough from the center of rotation
						if (toWorldVector.sqrMagnitude > minDiameterSqr)
						{
							// Switch to rotation mode, we have a center and a start angle to compare with, 
							// from now on, when we move the mouse we change the rotation angle relative to this first angle.
							haveRotateStartAngle = true;
                            fromWorldVector = toWorldVector;
                        }
                    } else
                    {
						// Get the angle between 'from' and 'to' on the plane we're dragging over
                        var worldspaceAngle		= MathExtensions.SignedAngle(toWorldVector, fromWorldVector, worldDragPlane.normal);
						// Get the rotation on that plane, around 'worldStartPosition'
						var worldspaceRotation	= MathExtensions.RotateAroundAxis(worldStartPosition, worldDragPlane.normal, worldspaceAngle);
                        
                        Undo.RecordObjects(selectedBrushMeshAsset, "Rotate UV coordinates"); 
                        for (int i = 0; i < selectedSurfaceReferences.Length; i++)
						{ 
                            var rotationInPlaneSpace	= selectedSurfaceReferences[i].WorldSpaceToPlaneSpace(in worldspaceRotation);

							// TODO: Finish this, if we have multiple surfaces selected, we want other non-aligned surfaces to move/rotate in a nice way
							//		 last thing we want is that these surfaces are rotated in such a way that the uvs are rotated into infinity.
							//		 ideally the rotation would change into a translation on 90 angles, think selecting all surfaces on a cylinder 
							//	     and rotating the cylinder cap. You would want the sides to move with the rotation and not actually rotate themselves.
							var rotateToPlane			= Quaternion.FromToRotation(rotationInPlaneSpace.GetColumn(2), Vector3.forward);
                            var fixedRotation			= Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInPlaneSpace;

							selectedSurfaceReferences[i].PlaneSpaceTransformUV(in fixedRotation, in selectedUVMatrices[i]);
                        }
                    }
                    break;
                }
            }
            return needRepaint;
        }

        private static bool SurfaceMoveTool(SelectionType selectionType, Rect dragArea)
		{
			var id = GUIUtility.GetControlID(kSurfaceMoveHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;

			bool needRepaint = false;
			switch (Event.current.GetTypeForControl(id))
			{
				// TODO: support moving texture using keyboard
				// TODO: add ability to cancel movement when pressing escape
				case EventType.Repaint:
				{
					// TODO: show delta movement of uv
					break;
				}
				case EventType.MouseDrag:
				{
					if (!IsToolEnabled(id))
                        break;

					StartToolDragging();
                    
					var movementInWorldSpace = Matrix4x4.TRS(MouseDragDeltaVector, Quaternion.identity, Vector3.one);
					Undo.RecordObjects(selectedBrushMeshAsset, "Moved UV coordinates");
					for (int i = 0; i < selectedSurfaceReferences.Length; i++)
					{
						// Translates the uv surfaces in a given direction. Since the z direction, relatively to the surface, 
						// is basically removed in this calculation, it should behave well when we move multiple selected surfaces
						// in any direction.
						selectedSurfaceReferences[i].WorldSpaceTransformUV(in movementInWorldSpace, in selectedUVMatrices[i]);
					}
                    break;
                }
            }
            return needRepaint;
        }
    }
}
