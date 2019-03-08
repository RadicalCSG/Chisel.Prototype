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

        static bool InEditCameraMode
        {
            get
            {
                return (Tools.viewTool == ViewTool.Pan || Tools.viewTool == ViewTool.None);
            }
        }

        static bool ToolIsDragging		{ get; set; }
        static bool MouseIsDown			{ get; set; }

        

        #region Hover Surfaces
        static readonly HashSet<SurfaceReference> hoverSurfaces		= new HashSet<SurfaceReference>();
        

        static bool UpdateHoverSurfaces(Vector2 mousePosition, Rect dragArea, SelectionType selectionType, bool clearHovering)
        {
            try
            {
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

                var foundSurfaces = CSGClickSelectionManager.FindSurfaceReference(mousePosition, false);
                if (foundSurfaces == null)
                {
                    modified = (hoverSurfaces != null) || modified;
                    return modified;
                }

                if (foundSurfaces.Length == hoverSurfaces.Count)
                    modified = !hoverSurfaces.ContainsAll(foundSurfaces) || modified;
                else
                    modified = true;

                hoverSurfaces.AddRange(foundSurfaces);
                return modified;
            }
            finally
            {
                CSGSurfaceSelectionManager.SetHovering(selectionType, hoverSurfaces);
            }
        }
        #endregion

        public void OnEnable()
        {
            // TODO: shouldn't just always set this param
            Tools.hidden = true; 
        }

        public void OnDisable()
        {
        }

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var defaultID = GUIUtility.GetControlID(kSurfaceEditModeHash, FocusType.Passive, dragArea);
            HandleUtility.AddDefaultControl(defaultID);

            var selectionType = CSGRectSelectionManager.GetCurrentSelectionType();
            var repaint = SurfaceSelection(dragArea, selectionType);

            var cursor = MouseCursor.Arrow;
            switch (Tools.current)
            {
                case Tool.Move:		repaint = SurfaceMoveTool(selectionType,   dragArea) || repaint; cursor = MouseCursor.MoveArrow;   break;
                case Tool.Rotate:	repaint = SurfaceRotateTool(selectionType, dragArea) || repaint; cursor = MouseCursor.RotateArrow; break;
                case Tool.Scale:	repaint = SurfaceScaleTool(selectionType,  dragArea) || repaint; cursor = MouseCursor.ScaleArrow;  break;
                //case Tool.Rect: break;
            }
            
            // TODO: support scaling texture using keyboard
            // TODO: support rotating texture using keyboard
            // TODO: support moving texture using keyboard
            
            switch (selectionType)
            {
                case SelectionType.Additive:    cursor = MouseCursor.ArrowPlus; break;
                case SelectionType.Subtractive: cursor = MouseCursor.ArrowMinus; break;
            }
            EditorGUIUtility.AddCursorRect(dragArea, cursor);
            
            if (repaint &&
                // avoid infinite loop
                Event.current.type != EventType.Layout &&
                Event.current.type != EventType.Repaint)
                SceneView.RepaintAll();
        }

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

        static bool CanEnableTool(int id)
        {
            if (GUIUtility.hotControl != 0)
                return false;
            
            var evt = Event.current;
            if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                (GUIUtility.keyboardControl != id || evt.button != 2))
                return false;
            return true;
        }

        static bool IsToolEnabled(int id)
        {
            return GUIUtility.hotControl == id;
        }

        static void EnableTool(int id, Vector2 mousePosition)
        {
            jumpedMousePosition = mousePosition;
            EditorGUIUtility.SetWantsMouseJumping(1);
            GUIUtility.hotControl = GUIUtility.keyboardControl = id;
            Event.current.Use();
        }

        static void DisableTool()
        {
            EditorGUIUtility.SetWantsMouseJumping(0);
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            Event.current.Use();
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

                    MouseIsDown = true;
                    ToolIsDragging = false;
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id)
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
                    if (GUIUtility.hotControl != id)
                        break;
                    
                    MouseIsDown = false;
                    if (!ToolIsDragging)
                    {
                        // if we clicked on the surface, instead of dragged it, just click select it
                        ClickSelection(dragArea, selectionType);
                        break;
                    }
                    ToolIsDragging = false;
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
        static Vector2              jumpedMousePosition;

        private static void InitToolFirstClick(Vector2 mousePosition)
        {
            startSurfaceIntersection	= CSGClickSelectionManager.FindSurfaceIntersection(mousePosition);
            selectedSurfaceReferences	= CSGSurfaceSelectionManager.Selection.ToArray();
            selectedBrushMeshAsset		= CSGSurfaceSelectionManager.SelectedBrushMeshes.ToArray();
            selectedUVMatrices			= new UVMatrix[selectedSurfaceReferences.Length];

            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
                selectedUVMatrices[i] = selectedSurfaceReferences[i].Polygon.description.UV0;
                        
            var nodeTransform		= startSurfaceIntersection.surface.node.hierarchyItem.Transform;
            var modelTransform		= CSGNodeHierarchyManager.FindModelTransformOfTransform(nodeTransform);

            worldStartPosition		= modelTransform.localToWorldMatrix.MultiplyPoint (startSurfaceIntersection.intersection.worldIntersection);
            worldProjectionPlane	= modelTransform.localToWorldMatrix.TransformPlane(startSurfaceIntersection.intersection.worldPlane);
            
            // more accurate for small movements
            worldDragPlane		= worldProjectionPlane;

            // (unfinished) prevents drag-plane from intersecting near plane (makes movement slow down to a singularity when further away from click position)
            //worldDragPlane	= new Plane(Camera.current.transform.forward, worldStartPosition); 

            // TODO: ideally we'd interpolate the behaviour of the worldPlane between near and far behavior
        }

        private static Vector3 GetCurrentWorldClick(Vector2 mousePosition)
        {
            var currentWorldIntersection = worldStartPosition;
                    
            // TODO: snapping?
            var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            var enter = 0.0f;
            if (worldDragPlane.UnsignedRaycast(mouseRay, out enter))
                currentWorldIntersection = mouseRay.GetPoint(enter);


            return currentWorldIntersection;
        }

        private static bool SurfaceScaleTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceScaleHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool repaint = false;
            
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                // TODO: show delta scale of texture
                // TODO: add ability to cancel movement when pressing escape
                case EventType.MouseDown:
                {
                    if (!CanEnableTool(id))
                        break;
                    EnableTool(id, evt.mousePosition);
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    jumpedMousePosition += evt.delta;
                    if (!ToolIsDragging)
                        InitToolFirstClick(jumpedMousePosition);
                    ToolIsDragging = true;
                    evt.Use();
                    
                    var currentWorldIntersection = GetCurrentWorldClick(jumpedMousePosition);

                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    DisableTool();
                    selectedSurfaceReferences = null;
                    selectedBrushMeshAsset = null;
                    selectedUVMatrices = null;
                    break;
                }
            }
            return repaint;
        }

        static bool haveRotateStartAngle	= false;
        static Vector3 startVector;
        const float kMinRotateDiameter		= 0.25f * 25.0f;
        private static bool SurfaceRotateTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceRotateHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool repaint = false;
            
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                // TODO: show delta rotation of texture
                // TODO: add ability to cancel movement when pressing escape
                case EventType.MouseDown:
                {
                    if (!CanEnableTool(id))
                        break;
                    EnableTool(id, evt.mousePosition);
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    jumpedMousePosition += evt.delta;
                    if (!ToolIsDragging)
                    {
                        InitToolFirstClick(jumpedMousePosition);
                        haveRotateStartAngle = false;
                    }
                    ToolIsDragging = true;
                    evt.Use();

                    
                    var startSurface				= startSurfaceIntersection.surface;

                    var currentWorldIntersection	= GetCurrentWorldClick(jumpedMousePosition);
                    var worldSpaceMovement			= currentWorldIntersection - worldStartPosition;

                    var localNormal					= startSurface.node.hierarchyItem.Transform.worldToLocalMatrix.
                                                        MultiplyVector(worldDragPlane.normal);

                    Vector3 tangent;
                    Vector3 biNormal;
                    MathExtensions.CalculateTangents(worldDragPlane.normal, out tangent, out biNormal);

                    var projectedWorldMovement = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
                                                 biNormal * Vector3.Dot(biNormal, worldSpaceMovement);
                    
                    if (UnitySceneExtensions.Snapping.AxisLockX) projectedWorldMovement.x = 0;
                    if (UnitySceneExtensions.Snapping.AxisLockY) projectedWorldMovement.y = 0;
                    if (UnitySceneExtensions.Snapping.AxisLockZ) projectedWorldMovement.z = 0;


                    if (!haveRotateStartAngle)
                    {
                        var handleSize		= HandleUtility.GetHandleSize(worldStartPosition);	
                        var minDiameterSqr	= handleSize * kMinRotateDiameter;
                        if (projectedWorldMovement.sqrMagnitude > minDiameterSqr)
                        {
                            haveRotateStartAngle = true;
                            startVector = projectedWorldMovement;
                        }
                    } else
                    {
                        var deltaAngle			= MathExtensions.SignedAngle(startVector, projectedWorldMovement, worldDragPlane.normal);
                        var worldSpaceRotationQ	= Quaternion.AngleAxis(deltaAngle, worldDragPlane.normal);
                        var worldspaceRotation	= Matrix4x4.TRS( worldStartPosition, Quaternion.identity, Vector3.one) *
                                                  Matrix4x4.TRS(Vector3.zero, worldSpaceRotationQ, Vector3.one) *
                                                  Matrix4x4.TRS(-worldStartPosition, Quaternion.identity, Vector3.one)
                                                  ;
                        var deltaRadians		= deltaAngle * Mathf.Deg2Rad;
                        
                        Undo.RecordObjects(selectedBrushMeshAsset, "Rotate UV coordinates"); 
                        for (int i = 0; i < selectedSurfaceReferences.Length; i++) 
                        { 
                            var worldToLocal			= selectedSurfaceReferences[i].node.hierarchyItem.LocalToWorldMatrix;
                            var polygon					= selectedSurfaceReferences[i].Polygon;
                            var orientation				= selectedSurfaceReferences[i].Orientation;
                            
                            var worldToPlaneSpace		= orientation.localToPlaneSpace * worldToLocal;
                            var planeToWorldSpace		= Matrix4x4.Inverse(worldToPlaneSpace);

                            var rotationInWorldSpace	= worldToPlaneSpace * worldspaceRotation * planeToWorldSpace;
                            var vectorW					= rotationInWorldSpace.GetColumn(2);
                            var rotateToPlane			= Quaternion.FromToRotation(vectorW, Vector3.forward);
                            rotationInWorldSpace = Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInWorldSpace;

                            var uvMatrix = selectedUVMatrices[i];
                            
                            var vectorU = rotationInWorldSpace.MultiplyVector(uvMatrix.U);
                            var vectorV = rotationInWorldSpace.MultiplyVector(uvMatrix.V);
                            var center	= rotationInWorldSpace.MultiplyPoint((uvMatrix.U.w * (Vector3)uvMatrix.U) + (uvMatrix.V.w * (Vector3)uvMatrix.V));

                            uvMatrix.U = vectorU;
                            uvMatrix.V = vectorV;
                            uvMatrix.U.w = -Vector3.Dot(vectorU, center); 
                            uvMatrix.V.w = -Vector3.Dot(vectorV, center);
                            
                            polygon.description.UV0 = uvMatrix;

                            selectedSurfaceReferences[i].brushMeshAsset.SetDirty();
                        }
                    }
    

                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;

                    DisableTool();
                    selectedSurfaceReferences = null;
                    selectedBrushMeshAsset = null;
                    selectedUVMatrices = null;
                    break;
                }
            }
            return repaint;
        }

        private static bool SurfaceMoveTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceMoveHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool repaint = false;
            
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                // TODO: show delta movement of texture
                // TODO: add ability to cancel movement when pressing escape
                case EventType.MouseDown:
                {
                    if (!CanEnableTool(id))
                        break;
                    EnableTool(id, evt.mousePosition);
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    jumpedMousePosition += evt.delta;
                    if (!ToolIsDragging)
                        InitToolFirstClick(jumpedMousePosition);
                    ToolIsDragging = true;
                    evt.Use();
                    
                    var startSurface				= startSurfaceIntersection.surface;

                    var currentWorldIntersection	= GetCurrentWorldClick(jumpedMousePosition);
                    var worldSpaceMovement			= currentWorldIntersection - worldStartPosition;
                    
                    Vector3 tangent;
                    Vector3 biNormal;
                    MathExtensions.CalculateTangents(worldDragPlane.normal, out tangent, out biNormal);

                    var projectedWorldMovement = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
                                                 biNormal * Vector3.Dot(biNormal, worldSpaceMovement);
                                                                    
                    var localNormal		= startSurface.node.hierarchyItem.Transform.worldToLocalMatrix.
                                            MultiplyVector(worldProjectionPlane.normal);
                    
                    if (UnitySceneExtensions.Snapping.AxisLockX) projectedWorldMovement.x = 0;
                    if (UnitySceneExtensions.Snapping.AxisLockY) projectedWorldMovement.y = 0;
                    if (UnitySceneExtensions.Snapping.AxisLockZ) projectedWorldMovement.z = 0;
                                        
                    // TODO: snap edges of textures against vertices

                    Undo.RecordObjects(selectedBrushMeshAsset, "Moved UV coordinates");
                    for (int i = 0; i < selectedSurfaceReferences.Length; i++)
                    { 
                        var worldToLocal		= selectedSurfaceReferences[i].node.hierarchyItem.WorldToLocalMatrix;
                        var localToWorld		= selectedSurfaceReferences[i].node.hierarchyItem.LocalToWorldMatrix;
                        var polygon				= selectedSurfaceReferences[i].Polygon;
                        var orientation			= selectedSurfaceReferences[i].Orientation;
                        
                        var uvMatrix			= selectedUVMatrices[i];
#if true
                        var worldToPlaneSpace	= orientation.localToPlaneSpace * worldToLocal;
                        var planeSpaceMovement	= worldToPlaneSpace.MultiplyVector(projectedWorldMovement);
                        uvMatrix.U.w -= Vector3.Dot(uvMatrix.U, planeSpaceMovement);
                        uvMatrix.V.w -= Vector3.Dot(uvMatrix.V, planeSpaceMovement);
#else
                        var rotation			= Quaternion.FromToRotation(localNormal, orientation.plane.normal);
                        var worldToPlaneSpace	= orientation.localToPlaneSpace * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one) * worldToLocal;
                        var planeSpaceMovement	= worldToPlaneSpace.MultiplyVector(projectedWorldMovement);

                        uvMatrix.U.w -= Vector3.Dot(uvMatrix.U, planeSpaceMovement);
                        uvMatrix.V.w -= Vector3.Dot(uvMatrix.V, planeSpaceMovement);
#endif                   
                        polygon.description.UV0 = uvMatrix;

                        selectedSurfaceReferences[i].brushMeshAsset.SetDirty();
                    }
                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;

                    DisableTool();
                    selectedSurfaceReferences = null;
                    selectedBrushMeshAsset = null;
                    selectedUVMatrices = null;
                    break;
                }
            }
            return repaint;
        }
    }
}
