using Chisel.Assets;
using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    // TODO: need to snap to edges
    // TODO: need to snap to vertices
    public sealed class PointDrawing
    {
        internal static int s_PointDrawingHash = "PointDrawingHash".GetHashCode();
        public static void PointDrawHandle(Rect dragArea, ref List<Vector3> points, out Matrix4x4 transformation, out CSGModel modelBeneathCursor, UnitySceneExtensions.SceneHandles.CapFunction capFunction)
        {
            var id = GUIUtility.GetControlID(s_PointDrawingHash, FocusType.Keyboard);
            PointDrawing.Do(id, dragArea, ref points, out transformation, out modelBeneathCursor, capFunction);
        }


        // TODO: how to get rid of this??
        public static void Reset()
        {
            s_CurrentPointIndex = 0;
            UnitySceneExtensions.Grid.HoverGrid = null;
        }

        private static PlaneIntersection    s_StartIntersection;			
        private static int					s_CurrentPointIndex = 0;
        private static Matrix4x4            s_Transform;
        private static Matrix4x4            s_InvTransform;
        private static Snapping2D			s_Snapping2D = new Snapping2D();

        // TODO: put these constants somewhere
        public const KeyCode    kCancelKey			= KeyCode.Escape;
        public const KeyCode    kCommitKey			= KeyCode.Return;
        public const string		kSoftDeleteCommand	= "SoftDelete";
        const float             kPointScale			= 0.05f;

        static Vector3? GetPointAtPosition(Vector2 mousePosition, Rect dragArea)
        {
            UnitySceneExtensions.Grid.HoverGrid = null;
            if (s_CurrentPointIndex == 0)
            {
                s_StartIntersection = CSGClickSelectionManager.GetPlaneIntersection(mousePosition, dragArea);
                if (s_StartIntersection != null)
                {
                    // TODO: try to cache this ..
                    var activeGridUp					= UnitySceneExtensions.Grid.ActiveGrid.Up;
                    var activeGridForward				= UnitySceneExtensions.Grid.ActiveGrid.Forward;
                    var activeGridCenter				= UnitySceneExtensions.Grid.ActiveGrid.Center;
                    var surfaceGridPlane				= s_StartIntersection.plane;
                    var surfaceGridUp					= surfaceGridPlane.normal;
                    var surfaceGridForward				= -MathExtensions.ClosestTangentAxis(surfaceGridUp); // Q: why if this is not negated, we end up-side-down?

                    var activeGridFromWorldRotation		= Quaternion.LookRotation(activeGridForward, activeGridUp);
                    var worldFromActiveGridRotation		= Quaternion.Inverse(activeGridFromWorldRotation);
                    var surfaceGridFromWorldRotation	= Quaternion.LookRotation(surfaceGridForward, surfaceGridUp);
                    var activeGridToSurfaceGridRotation	= surfaceGridFromWorldRotation * worldFromActiveGridRotation;


                    // Make sure the center of the new grid is as close to the active grid center as possible
                    Vector3 surfaceGridCenter = activeGridCenter;
                    var forwardRay	= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.up);
                    var backRay		= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.down);
                    var leftRay		= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.left);
                    var rightRay	= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.right);
                    var upRay		= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.forward);
                    var downRay		= new Ray(activeGridCenter, worldFromActiveGridRotation * Vector3.back);

                    var bestDist = float.PositiveInfinity;
                    float dist;

                    if (surfaceGridPlane.UnsignedRaycast(forwardRay, out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = forwardRay.GetPoint(dist); } }
                    if (surfaceGridPlane.UnsignedRaycast(backRay,    out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = backRay   .GetPoint(dist); } }
                    if (surfaceGridPlane.UnsignedRaycast(leftRay,    out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = leftRay   .GetPoint(dist); } }
                    if (surfaceGridPlane.UnsignedRaycast(rightRay,   out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = rightRay  .GetPoint(dist); } }
                    if (bestDist > 100000) // prefer rays on the active-grid, only go up/down from the active-grid when we have no other choice
                    {
                        if (surfaceGridPlane.UnsignedRaycast(upRay,   out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = upRay     .GetPoint(dist); } }
                        if (surfaceGridPlane.UnsignedRaycast(downRay, out dist)) { var abs_dist = Mathf.Abs(dist); if (abs_dist < bestDist) { bestDist = abs_dist; surfaceGridCenter = downRay   .GetPoint(dist); } }
                    }

                    // TODO: try to snap the new surface grid point in other directions on the active-grid? (do we need to?)
                    
                    s_Transform = Matrix4x4.TRS(surfaceGridCenter - activeGridCenter, activeGridToSurfaceGridRotation, Vector3.one) * 
                                        UnitySceneExtensions.Grid.ActiveGrid.GridToWorldSpace;
                    s_InvTransform = s_Transform.inverse;
 
                    s_Snapping2D.Initialize(new UnitySceneExtensions.Grid(s_Transform), mousePosition, s_StartIntersection.point, UnityEditor.Handles.matrix);
                }
            }

            if (s_StartIntersection != null)
            {
                if (s_Snapping2D.DragTo(mousePosition, SnappingMode.Always))
                {
                    UnitySceneExtensions.Grid.HoverGrid = s_Snapping2D.WorldSlideGrid;
                    return s_Snapping2D.WorldSnappedPosition;
                }
            }
            return null;
        }

        static void UpdatePoints(List<Vector3> points, Vector3? point)
        {
            if (point.HasValue)
            {
                while (points.Count > 0 && points.Count > s_CurrentPointIndex)
                    points.RemoveAt(points.Count - 1);

                var localPosition = s_InvTransform.MultiplyPoint(point.Value);

                while (points.Count <= s_CurrentPointIndex)
                    points.Add(localPosition);

                points[s_CurrentPointIndex] = localPosition;
            } else
            {
                while (points.Count > 0 && points.Count >= s_CurrentPointIndex)
                    points.RemoveAt(points.Count - 1);
            }
        }

        static void Commit(Event evt, Rect dragArea, ref List<Vector3> points)
        {
            s_CurrentPointIndex++;
            UpdatePoints(points, GetPointAtPosition(evt.mousePosition, dragArea));

            // reset the starting position
            s_StartIntersection = CSGClickSelectionManager.GetPlaneIntersection(evt.mousePosition, dragArea);
            evt.Use();
        }

        static void Cancel(Event evt, ref List<Vector3> points)
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            evt.Use();

            Reset();
            points.Clear();
        }

        public static void Do(int id, Rect dragArea, ref List<Vector3> points, out Matrix4x4 transformation, out CSGModel modelBeneathCursor, UnitySceneExtensions.SceneHandles.CapFunction capFunction)
        {
            modelBeneathCursor = null;
            var evt = Event.current;
            var type = evt.GetTypeForControl(id);
            switch (type)
            {
                case EventType.ValidateCommand: { if (evt.commandName == kSoftDeleteCommand) { evt.Use(); break; } break; }
                case EventType.ExecuteCommand:	{ if (evt.commandName == kSoftDeleteCommand) { Cancel(evt, ref points); break; } break; }

                case EventType.KeyDown:			{ if (evt.keyCode == kCancelKey || 
                                                  evt.keyCode == kCommitKey) { evt.Use(); break; } break; }
                case EventType.KeyUp:			{ if (evt.keyCode == kCancelKey) { Cancel(evt, ref points); break; } else 
                                                  if (evt.keyCode == kCommitKey) { Commit(evt, dragArea, ref points); break; } break; }

                case EventType.MouseDrag:
                case EventType.MouseMove:
                {
                    if (GUIUtility.hotControl != 0)
                        break;

                    UpdatePoints(points, GetPointAtPosition(evt.mousePosition, dragArea));
                    break;
                }
                case EventType.Layout:
                {
                    UnityEditor.HandleUtility.AddControl(id, 0.0f);
                    break;
                }
                case EventType.Repaint:
                {
                    if (s_StartIntersection == null)
                        break;

                    if (points.Count == 0)
                        break;

                    using (new UnityEditor.Handles.DrawingScope(Matrix4x4.identity))
                    {
                        var orientation = s_StartIntersection.orientation;
                        if (capFunction != null)
                        {
                            using (new UnityEditor.Handles.DrawingScope(s_Transform))
                            {
                                for (int i = 0; i < points.Count; i++)
                                    capFunction(id, points[i], orientation, UnityEditor.HandleUtility.GetHandleSize(points[i]) * kPointScale, type);
                            }
                        }

                        /*
                        if (points.Count == 0)
                        {
                            using (new UnityEditor.Handles.DrawingScope(transformation))
                            {
                                UnityEditor.Handles.ArrowHandleCap(id, points[0], orientation, UnityEditor.HandleUtility.GetHandleSize(points[0]), evt.type);
                            }
                        }*/

                        var selectedColor = UnityEditor.Handles.selectedColor;
                        selectedColor.a = 0.5f;
                        using (new UnityEditor.Handles.DrawingScope(selectedColor))
                        {
                            HandleRendering.RenderSnapping3D(s_Snapping2D.WorldSlideGrid, s_Snapping2D.WorldSnappedExtents, s_Snapping2D.GridSnappedPosition, s_Snapping2D.SnapResult, true);
                        }
                    }
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != 0)
                        break;
                    
                    if ((UnityEditor.HandleUtility.nearestControl != id || evt.button != 0) &&
                        (GUIUtility.keyboardControl != id || evt.button != 2))
                        break;
                        
                    if (s_StartIntersection == null)
                        break;
                    
                    s_CurrentPointIndex++;
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
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

                    // reset the starting position
                    UpdatePoints(points, GetPointAtPosition(evt.mousePosition, dragArea));
                    break;
                }
            }
            if (s_StartIntersection != null)
            {
                modelBeneathCursor = s_StartIntersection.model;
                transformation = s_Transform;
            } else
                transformation = Matrix4x4.identity;
        }
    }
}
