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

        #region Snapping

        // TODO: put somewhere else, so we can enable/disable default uv snapping behavour in the editor
        [Flags]
        enum UVSnapSettings
        {
            None                = 0,
            GeometryGrid        = 1,
            GeometryEdges       = 2,
            GeometryVertices    = 4,
            UVGrid              = 8,    // TODO: implement
            UVBounds            = 16    // TODO: implement
        }

        static UVSnapSettings editorSnapSettings  = (UVSnapSettings)~0;
        static UVSnapSettings toolSnapOverrides   = (UVSnapSettings)~0;

        static UVSnapSettings CurrentSnapSettings { get { return editorSnapSettings & toolSnapOverrides; } }
        static bool pointHasSnapped     = false;
        static bool forceVertexSnapping = false;

        const float kMinSnapDistance    = 0.5f;
        const float kDistanceEpsilon    = 0.0006f;
        const float kAlignmentEpsilon   = 1 - kDistanceEpsilon;


        static void SnapGridIntersection(UVSnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist)
        {
            if ((snapSettings & UVSnapSettings.GeometryGrid) == UVSnapSettings.None)
                return;

            var grid				= UnitySceneExtensions.Grid.defaultGrid;
            var gridSnappedPoint	= Snapping.SnapPoint(intersectionPoint, grid);

            var worldPlane  = surfaceReference.WorldPlane.Value;

            var xAxis       = grid.Right;
            var yAxis       = grid.Up;
            var zAxis       = grid.Forward;
            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, worldPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, worldPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, worldPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            if (Mathf.Abs(worldPlane.GetDistanceToPoint(gridSnappedPoint)) < kDistanceEpsilon)
            {
                bestDist = (gridSnappedPoint - intersectionPoint).magnitude * preferenceFactor;
                snappedPoint = gridSnappedPoint;
            } else
            {
                float dist;
                var ray = new Ray(gridSnappedPoint, xAxis);
                if ((snapAxis & Axis.X) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = yAxis;
                if ((snapAxis & Axis.Y) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snappedPoint = planePoint;
                    }
                }
                ray.direction = zAxis;
                if ((snapAxis & Axis.Z) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                {
                    var planePoint = ray.GetPoint(dist);
                    var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                    if (abs_dist < bestDist)
                    {
                        bestDist = abs_dist;
                        snappedPoint = planePoint;
                    }
                }
            }
        }

        static void SnapSurfaceVertices(UVSnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist)
        {
            if (surfaceReference == null)
                return;

            if ((snapSettings & UVSnapSettings.GeometryVertices) == UVSnapSettings.None)
                return;

            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;
            var subMesh             = surfaceReference.SubMesh;
            Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < subMesh.Polygons.Length);

            var polygon             = subMesh.Polygons[surfaceReference.surfaceIndex];
            var edges               = subMesh.HalfEdges;
            var vertices            = subMesh.Vertices;
            var firstEdge           = polygon.firstEdge;
            var lastEdge            = firstEdge + polygon.edgeCount;
            
            var bestDistSqr			= float.PositiveInfinity;
            var bestVertex			= snappedPoint;
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var worldSpaceVertex = localToWorldSpace.MultiplyPoint(vertices[edges[e].vertexIndex]);
                var dist_sqr         = (worldSpaceVertex - intersectionPoint).sqrMagnitude;
                if (dist_sqr < bestDistSqr)
                {
                    bestDistSqr = dist_sqr;
                    bestVertex = worldSpaceVertex;
                }
            }

            if (float.IsInfinity(bestDistSqr))
                return;

            var closestVertexDistance = Mathf.Sqrt(bestDistSqr) * preferenceFactor;
            if (closestVertexDistance < bestDist)
            {
                bestDist = closestVertexDistance;
                snappedPoint = bestVertex;
            }
        }

        static void SnapSurfaceEdges(UVSnapSettings snapSettings, SurfaceReference surfaceReference, Vector3 intersectionPoint, float preferenceFactor, ref Vector3 snappedPoint, ref float bestDist)
        {
            if (surfaceReference == null)
                return;

            if ((snapSettings & UVSnapSettings.GeometryEdges) == UVSnapSettings.None)
                return;

            var localToWorldSpace	= surfaceReference.LocalToWorldSpace;
            var subMesh             = surfaceReference.SubMesh;
            Debug.Assert(surfaceReference.surfaceIndex >= 0 && surfaceReference.surfaceIndex < subMesh.Polygons.Length);

            var grid        = UnitySceneExtensions.Grid.defaultGrid;
            var xAxis       = grid.Right;
            var yAxis       = grid.Up;
            var zAxis       = grid.Forward;
            var intersectionPlane  = surfaceReference.WorldPlane.Value;

            var snapAxis    = Axis.X | Axis.Y | Axis.Z;
            if (Mathf.Abs(Vector3.Dot(xAxis, intersectionPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.X;
            if (Mathf.Abs(Vector3.Dot(yAxis, intersectionPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Y;
            if (Mathf.Abs(Vector3.Dot(zAxis, intersectionPlane.normal)) >= kAlignmentEpsilon) snapAxis &= ~Axis.Z;

            var polygons                = subMesh.Polygons;
            var polygon                 = polygons[surfaceReference.surfaceIndex];
            var halfEdges               = subMesh.HalfEdges;
            var halfEdgePolygonIndices  = subMesh.HalfEdgePolygonIndices;
            var vertices                = subMesh.Vertices;
            var firstEdge               = polygon.firstEdge;
            var lastEdge                = firstEdge + polygon.edgeCount;

            for (int e = firstEdge; e < lastEdge; e++)
            {
                var twinIndex       = halfEdges[e].twinIndex;
                var polygonIndex    = halfEdgePolygonIndices[e];

                var surfaceIndex    = polygonIndex; // FIXME: throughout the code we're making assumptions about polygonIndices being the same as surfaceIndices, 
                                                    //         this needs to be fixed
                var localPlane      = subMesh.Surfaces[surfaceIndex].localPlane;
                var worldPlane      = localToWorldSpace.TransformPlane(localPlane);

                if ((CurrentSnapSettings & UVSnapSettings.GeometryGrid) != UVSnapSettings.None)
                {
                    var edgeDirection = Vector3.Cross(intersectionPlane.normal, worldPlane.normal);

                    var edgeSnapAxis = snapAxis;
                    if (Mathf.Abs(Vector3.Dot(xAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.X;
                    if (Mathf.Abs(Vector3.Dot(yAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Y;
                    if (Mathf.Abs(Vector3.Dot(zAxis, edgeDirection)) >= kAlignmentEpsilon) edgeSnapAxis &= ~Axis.Z;

                    if (edgeSnapAxis == Axis.None)
                        continue;

                    float dist;
                    var ray = new Ray(snappedPoint, xAxis);
                    if ((edgeSnapAxis & Axis.X) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                    {
                        var planePoint = ray.GetPoint(dist);
                        var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                        if (abs_dist < bestDist)
                        {
                            bestDist = abs_dist;
                            snappedPoint = planePoint;
                        }
                    }
                    ray.direction = yAxis;
                    if ((edgeSnapAxis & Axis.Y) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                    {
                        var planePoint = ray.GetPoint(dist);
                        var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                        if (abs_dist < bestDist)
                        {
                            bestDist = abs_dist;
                            snappedPoint = planePoint;
                        }
                    }
                    ray.direction = zAxis;
                    if ((edgeSnapAxis & Axis.Z) != Axis.None && worldPlane.UnsignedRaycast(ray, out dist))
                    {
                        var planePoint = ray.GetPoint(dist);
                        var abs_dist = (planePoint - intersectionPoint).magnitude * preferenceFactor;
                        if (abs_dist < bestDist)
                        {
                            bestDist = abs_dist;
                            snappedPoint = planePoint;
                        }
                    }
                } else
                { 
                    var closestPoint    = worldPlane.ClosestPointOnPlane(intersectionPoint);
                    var dist            = (closestPoint - intersectionPoint).magnitude * preferenceFactor;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        snappedPoint = closestPoint;
                    }
                }
            }
        }

        static Vector3 SnapIntersection(Vector3 intersectionPoint, SurfaceReference surfaceReference, out bool haveWeSnapped)
        {
            if (surfaceReference == null)
            {
                haveWeSnapped = false;
                return intersectionPoint;
            }

            // TODO: visualize what we're snapping against

            var bestDist			= float.PositiveInfinity;
            var snappedPoint		= intersectionPoint;
            var handleSize          = HandleUtility.GetHandleSize(intersectionPoint);
            // When holding V we force to ONLY and ALWAYS snap against vertices
            var snapSettings        = forceVertexSnapping ? UVSnapSettings.GeometryVertices : CurrentSnapSettings;

            // Snap to closest point on grid
            SnapGridIntersection(snapSettings, surfaceReference, intersectionPoint, 1.5f, ref snappedPoint, ref bestDist);

            // snap to vertices of surface that are closest to the intersection point
            SnapSurfaceVertices(snapSettings, surfaceReference, intersectionPoint, 1.0f, ref snappedPoint, ref bestDist);

            // snap to edges of surface that are closest to the intersection point
            SnapSurfaceEdges(snapSettings, surfaceReference, intersectionPoint, 2.0f, ref snappedPoint, ref bestDist);

            // TODO: snap to UV space bounds (and more?)


            var gridSnappingenabled = (CurrentSnapSettings & UVSnapSettings.GeometryGrid) != UVSnapSettings.None;
            var minSnapDistance     = (forceVertexSnapping || gridSnappingenabled) ? float.PositiveInfinity : (handleSize * kMinSnapDistance);

            if (bestDist < minSnapDistance)
            {
                haveWeSnapped = true;
                intersectionPoint = snappedPoint;
            } else
                haveWeSnapped = false;

            return intersectionPoint;
        }

        static float SnapAngle(float rotatedAngle)
        {
            if (!Snapping.RotateSnappingEnabled)
                return rotatedAngle;
            return ((int)(rotatedAngle / Snapping.RotateSnappingStep)) * Snapping.RotateSnappingStep;
        }

        #endregion

        #region Hover Surfaces
        static CSGTreeBrushIntersection? hoverIntersection;
        static SurfaceReference hoverSurfaceReference;

        static readonly HashSet<SurfaceReference> hoverSurfaces = new HashSet<SurfaceReference>();

        static void RenderIntersectionPoint(Vector3 position)
        {
            if (!hoverIntersection.HasValue)
                return;
            var intersectionPoint   = hoverIntersection.Value.surfaceIntersection.worldIntersection;
            var normal              = hoverIntersection.Value.surfaceIntersection.worldPlane.normal;
            SceneHandles.RenderBorderedCircle(position, HandleUtility.GetHandleSize(position) * 0.02f);
        }

        static void RenderVertexBox(Vector3 position)
        {
            if (!hoverIntersection.HasValue)
                return;
            var intersectionPoint   = hoverIntersection.Value.surfaceIntersection.worldIntersection;
            var normal              = hoverIntersection.Value.surfaceIntersection.worldPlane.normal;
            Handles.RectangleHandleCap(-1, position, Camera.current.transform.rotation, HandleUtility.GetHandleSize(position) * 0.1f, EventType.Repaint);
        }

        static void RenderIntersection()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (ToolIsDragging)
                return;

            if (!hoverIntersection.HasValue)
                return;

            var position = hoverIntersection.Value.surfaceIntersection.worldIntersection;
            RenderIntersectionPoint(position);
            if (forceVertexSnapping)
                RenderVertexBox(position);
        }

        static bool UpdateHoverSurfaces(Vector2 mousePosition, Rect dragArea, SelectionType selectionType, bool clearHovering)
        {
            try
            {
                hoverIntersection = null;
                hoverSurfaceReference = null;

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
                SurfaceReference surfaceReference;
                var foundSurfaces = CSGClickSelectionManager.FindSurfaceReference(mousePosition, false, out intersection, out surfaceReference);
                if (foundSurfaces == null)
                {
                    modified = (hoverSurfaces != null) || modified;
                    hoverIntersection = null;
                    return modified;
                }

                if (!float.IsInfinity(intersection.surfaceIntersection.distance))
                {
                    intersection.surfaceIntersection.worldIntersection = SnapIntersection(intersection.surfaceIntersection.worldIntersection, surfaceReference, out pointHasSnapped);
                }
                hoverIntersection = intersection;
                hoverSurfaceReference = surfaceReference;
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
            hoverSurfaceReference = null;
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


            toolSnapOverrides = (UVSnapSettings)~0;
            pointHasSnapped = false;
        }

        static void DisableTool()
        {
            EditorGUIUtility.SetWantsMouseJumping(0); // disable allowing the user to move the mouse over the bounds of the screen
            GUIUtility.hotControl = GUIUtility.keyboardControl = 0; // remove the active control so that the user can use another control
            Event.current.Use(); // make sure no-one else can use our event


            toolSnapOverrides = (UVSnapSettings)~0;
            pointHasSnapped = false;
        }

        static void CancelTool()
        {
            DisableTool();
            Undo.RevertAllInCurrentGroup();
            Event.current.Use();
            GUIUtility.ExitGUI(); // avoids a nullreference exception in sceneview
        }

        const float kMaxControlDistance = 3.0f;
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
                    // Unless something else is closer, make sure our tool is selected
                    HandleUtility.AddControl(id, kMaxControlDistance);
                    break;
                }

                case EventType.ValidateCommand:
                {
                    if (IsToolEnabled(id))
                    {
                        if (evt.keyCode == KeyCode.Escape)
                        {
                            evt.Use();
                            break;
                        }
                    }
                    if (!EditorGUIUtility.editingTextField)
                    {
                        if (evt.keyCode == KeyCode.V)
                        {
                            evt.Use();
                            break;
                        }
                    }
                    break;
                }
                case EventType.KeyDown:
                {
                    if (IsToolEnabled(id))
                    {
                        if (evt.keyCode == KeyCode.Escape)
                        {
                            evt.Use();
                            break;
                        }
                    }
                    if (!EditorGUIUtility.editingTextField)
                    {
                        if (evt.keyCode == KeyCode.V)
                        {
                            forceVertexSnapping = true;
                            evt.Use();
                            break;
                        }
                    }
                    break;
                }
                case EventType.KeyUp:
                {
                    if (IsToolEnabled(id))
                    {
                        if (evt.keyCode == KeyCode.Escape)
                        {
                            CancelTool();
                            break;
                        }
                    }
                    if (forceVertexSnapping && evt.keyCode == KeyCode.V)
                    {
                        forceVertexSnapping = false;
                        if (!EditorGUIUtility.editingTextField)
                            evt.Use();
                        break;
                    }
                    break;
                }

                case EventType.MouseMove:
                {
                    // In case we somehow missed a MouseUp event, we reset this bool
                    MouseIsDown = false;
                    break;
                }
                case EventType.MouseDown:
                {
                    // We can only use a tool when the mouse cursor is inside the draggable scene area
                    if (!dragArea.Contains(evt.mousePosition))
                        return false;

                    // We can only use a tool when we're hovering over a surfaces
                    if (hoverSurfaces == null || hoverSurfaces.Count == 0)
                        return false;

                    if (!CanEnableTool(id))
                        break;

                    // We want to be able to tell the difference between dragging and clicking,
                    // so we keep track if we dragged or not. In this case we haven't started dragging yet.
                    ToolIsDragging = false;
                    MouseIsDown = true;

                    EnableTool(id); 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;

                    if (!ToolIsDragging)
                    {
                        // If we haven't dragged the tool yet, check if the surface underneath 
                        // the mouse is selected or not, if it isn't: select it exclusively
                        if (!CSGSurfaceSelectionManager.IsAnySelected(hoverSurfaces))
                            ClickSelection(dragArea, selectionType);
                    }

                    // In the tool specific code, calling StartToolDragging will set ToolIsDragging to true, 
                    // which will allow us to tell the difference between clicking and dragging.

                    break;
                }
                case EventType.MouseUp:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    MouseIsDown = false;

                    // We want to be able to tell the difference between clicking and dragging, 
                    // so we use ToolIsDragging here to determine if we clicked.
                    if (!ToolIsDragging)
                    {
                        // If we clicked on the surface, instead of dragged it, just click select it
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

        static SurfaceReference     startSurfaceReference;
        static SurfaceReference[]	selectedSurfaceReferences;
        static CSGBrushMeshAsset[]	selectedBrushMeshAsset;
        static UVMatrix[]			selectedUVMatrices;
        static Plane                worldDragPlane;
        static Plane                worldProjectionPlane;
        static Vector3				worldStartPosition;
        static Vector3				worldIntersection;
        static Vector3              worldDragDeltaVector;
        static Vector2              jumpedMousePosition;

        private static bool StartToolDragging()
        {
            jumpedMousePosition += Event.current.delta;
            Event.current.Use();
            if (ToolIsDragging)
            {
                UpdateDragVector();
                return false;
            }

            // We set ToolIsDragging to true to be able to tell the difference between dragging and clicking
            ToolIsDragging = true;

            // Find the intersecting surfaces
            startSurfaceReference           = hoverSurfaceReference;
            var currentIntersection         = hoverIntersection.Value.surfaceIntersection;

            selectedSurfaceReferences	= CSGSurfaceSelectionManager.Selection.ToArray();

            // We need all the brushMeshAssets for all the surfaces we're moving, so that we can record them for an undo
            selectedBrushMeshAsset		= CSGSurfaceSelectionManager.SelectedBrushMeshes.ToArray();

            // We copy all the original surface uvMatrices, so we always apply rotations and transformations relatively to the original
            // This makes it easier to recover from edge cases and makes it more accurate, floating point wise.
            selectedUVMatrices			= new UVMatrix[selectedSurfaceReferences.Length];
            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
                selectedUVMatrices[i] = selectedSurfaceReferences[i].Polygon.description.UV0;
            
            // Find the intersection point/plane in model space
            var nodeTransform		= startSurfaceReference.node.hierarchyItem.Transform;
            var modelTransform		= CSGNodeHierarchyManager.FindModelTransformOfTransform(nodeTransform);
            worldStartPosition		= modelTransform.localToWorldMatrix.MultiplyPoint (hoverIntersection.Value.surfaceIntersection.worldIntersection);
            worldProjectionPlane	= modelTransform.localToWorldMatrix.TransformPlane(hoverIntersection.Value.surfaceIntersection.worldPlane);
            worldIntersection = worldStartPosition;

            // TODO: we want to be able to determine delta movement over a plane. Ideally it would match the position of the cursor perfectly.
            //		 unfortunately when moving the cursor towards the horizon of the plane, relative to the camera, the delta movement 
            //		 becomes too large or even infinity. Ideally we'd switch to a camera facing plane for these cases and determine movement in 
            //		 a less perfect way that would still allow the user to move or rotate things in a reasonable way.

            // more accurate for small movements
            worldDragPlane		= worldProjectionPlane;

            // TODO: (unfinished) prevents drag-plane from intersecting near plane (makes movement slow down to a singularity when further away from click position)
            //worldDragPlane	= new Plane(Camera.current.transform.forward, worldStartPosition); 

            // TODO: ideally we'd interpolate the behavior of the worldPlane between near and far behavior
            UpdateDragVector();
            return true;
        }

        private static Vector3 GetCurrentWorldClick(Vector2 mousePosition)
        {
            var currentWorldIntersection = worldStartPosition;                    
            var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            var enter = 0.0f;
            if (worldDragPlane.UnsignedRaycast(mouseRay, out enter))
                currentWorldIntersection = mouseRay.GetPoint(enter);
            currentWorldIntersection = SnapIntersection(currentWorldIntersection, startSurfaceReference, out pointHasSnapped);
            return currentWorldIntersection;
        }

        static void UpdateDragVector()
        {
            worldIntersection = GetCurrentWorldClick(jumpedMousePosition);
            var worldSpaceMovement = worldIntersection - worldStartPosition;

            Vector3 tangent;
            Vector3 biNormal;
            MathExtensions.CalculateTangents(worldDragPlane.normal, out tangent, out biNormal);

            var deltaVector = tangent  * Vector3.Dot(tangent,  worldSpaceMovement) +
                              biNormal * Vector3.Dot(biNormal, worldSpaceMovement);

            if (UnitySceneExtensions.Snapping.AxisLockX) deltaVector.x = 0;
            if (UnitySceneExtensions.Snapping.AxisLockY) deltaVector.y = 0;
            if (UnitySceneExtensions.Snapping.AxisLockZ) deltaVector.z = 0;

            worldDragDeltaVector = deltaVector;
        }
        #endregion
        
        #region Surface Scale Tool
        private static bool SurfaceScaleTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceScaleHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
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
                    if (!IsToolEnabled(id))
                        break;
                    
                    StartToolDragging();

                    var dragVector = worldDragDeltaVector;
                    break;
                }
            }
            return needRepaint;
        }
        #endregion

        #region Surface Rotate Tool
        static void RotateSurfacesInWorldSpace(Vector3 center, Vector3 normal, float rotateAngle)
        {
            // Get the rotation on that plane, around 'worldStartPosition'
            var worldspaceRotation = MathExtensions.RotateAroundAxis(center, normal, rotateAngle);

            Undo.RecordObjects(selectedBrushMeshAsset, "Rotate UV coordinates");
            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
            {
                var rotationInPlaneSpace = selectedSurfaceReferences[i].WorldSpaceToPlaneSpace(in worldspaceRotation);

                // TODO: Finish this. If we have multiple surfaces selected, we want other non-aligned surfaces to move/rotate in a nice way
                //		 last thing we want is that these surfaces are rotated in such a way that the uvs are rotated into infinity.
                //		 ideally the rotation would change into a translation on 90 angles, think selecting all surfaces on a cylinder 
                //	     and rotating the cylinder cap. You would want the sides to move with the rotation and not actually rotate themselves.
                var rotateToPlane = Quaternion.FromToRotation(rotationInPlaneSpace.GetColumn(2), Vector3.forward);
                var fixedRotation = Matrix4x4.TRS(Vector3.zero, rotateToPlane, Vector3.one) * rotationInPlaneSpace;

                selectedSurfaceReferences[i].PlaneSpaceTransformUV(in fixedRotation, in selectedUVMatrices[i]);
            }
        }

        static Vector3 fromWorldVector;
        static bool		haveRotateStartAngle	= false;
        static float	rotateAngle	            = 0;

        const float		kMinRotateDiameter		= 1.0f;
        private static bool SurfaceRotateTool(SelectionType selectionType, Rect dragArea)
        {
            var id = GUIUtility.GetControlID(kSurfaceRotateHash, FocusType.Keyboard, dragArea);
            if (!SurfaceToolBase(id, selectionType, dragArea))
                return false;
            
            bool needRepaint = false;            
            if (!IsToolEnabled(id))
            {
                needRepaint = haveRotateStartAngle;
                haveRotateStartAngle = false;
                pointHasSnapped = false;
            }
            
            switch (Event.current.GetTypeForControl(id))
            {
                // TODO: support rotating texture using keyboard?
                case EventType.Repaint:
                {
                    if (haveRotateStartAngle)
                    {
                        var toWorldVector   = worldDragDeltaVector;
                        var magnitude       = toWorldVector.magnitude;
                        toWorldVector /= magnitude;

                        // TODO: need a nicer visualization here, show delta rotation, angles etc.
                        Handles.DrawWireDisc(worldStartPosition, worldProjectionPlane.normal, magnitude);
                        if (haveRotateStartAngle)
                        {
                            var snappedToWorldVector = Quaternion.AngleAxis(rotateAngle, worldDragPlane.normal) * fromWorldVector;
                            Handles.DrawDottedLine(worldStartPosition, worldStartPosition + (fromWorldVector      * magnitude), 4.0f);
                            Handles.DrawDottedLine(worldStartPosition, worldStartPosition + (snappedToWorldVector * magnitude), 4.0f);
                        } else
                            Handles.DrawDottedLine(worldStartPosition, worldStartPosition + (toWorldVector * magnitude), 4.0f);
                    }
                    if (IsToolEnabled(id))
                    {
                        if (haveRotateStartAngle &&
                            pointHasSnapped)
                        {
                            RenderIntersectionPoint(worldIntersection);
                            RenderVertexBox(worldIntersection);
                        }
                    } 
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;
                    
                    if (StartToolDragging())
                    {
                        haveRotateStartAngle = false;
                        pointHasSnapped = false;
                    }

                    var toWorldVector = worldDragDeltaVector;
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
                            pointHasSnapped = false;
                            fromWorldVector = toWorldVector.normalized;
                            rotateAngle = 0;

                            // We override the snapping settings to only allow snapping against vertices, 
                            // we do this only after we have our starting vector, so that when we rotate we're not constantly
                            // snapping against the grid when we really just want to be able to snap against the current rotation step.
                            // On the other hand, we do want to be able to snap against vertices ..
                            toolSnapOverrides = UVSnapSettings.GeometryVertices; 
                        }
                    } else
                    {
                        // Get the angle between 'from' and 'to' on the plane we're dragging over
                        rotateAngle = MathExtensions.SignedAngle(fromWorldVector, toWorldVector.normalized, worldDragPlane.normal);
                        
                        // If we snapped against something, ignore angle snapping
                        if (!pointHasSnapped) rotateAngle = SnapAngle(rotateAngle);

                        RotateSurfacesInWorldSpace(worldStartPosition, worldDragPlane.normal, -rotateAngle); // TODO: figure out why this is reversed
                    }
                    break;
                }
            }
            return needRepaint;
        }
        #endregion

        #region Surface Move Tool
        static void TranslateSurfacesInWorldSpace(Vector3 translation)
        {
            var movementInWorldSpace = Matrix4x4.TRS(translation, Quaternion.identity, Vector3.one); 
            Undo.RecordObjects(selectedBrushMeshAsset, "Moved UV coordinates");
            for (int i = 0; i < selectedSurfaceReferences.Length; i++)
            {
                // Translates the uv surfaces in a given direction. Since the z direction, relatively to the surface, 
                // is basically removed in this calculation, it should behave well when we move multiple selected surfaces
                // in any direction.
                selectedSurfaceReferences[i].WorldSpaceTransformUV(in movementInWorldSpace, in selectedUVMatrices[i]);
            }
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
                case EventType.Repaint:
                {
                    if (!ToolIsDragging)
                        break;

                    RenderIntersectionPoint(worldIntersection);
                    // TODO: show delta movement of uv
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (!IsToolEnabled(id))
                        break;

                    StartToolDragging();
                    TranslateSurfacesInWorldSpace(-worldDragDeltaVector); // TODO: figure out why this is reversed
                    break;
                }
            }
            return needRepaint;
        }
        #endregion
    }
}
