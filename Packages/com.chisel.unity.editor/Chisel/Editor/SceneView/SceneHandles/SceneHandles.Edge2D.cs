using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
        internal static int s_Edge2DHash = "Edge1DHash".GetHashCode();

        public static bool InCameraOrbitMode
        {
            get
            {
                return Tools.current == Tool.View ||
                       Tools.current == Tool.None ||
                       Event.current.alt;
            } 
        }

        public static void SetArrowCursor(MouseCursor cursor)
        {
            if (InCameraOrbitMode)
                return;

            var sceneView = SceneView.currentDrawingSceneView;
            if (!sceneView)
                return;
            
            var rect = sceneView.position;
            rect.min = Vector2.zero;
            EditorGUIUtility.AddCursorRect(rect, cursor);
        }

        public static void SetCursor(int id, MouseCursor cursor)
        {
            if (UnityEditor.HandleUtility.nearestControl != id && 
                EditorGUIUtility.hotControl != id)
                return;

            SetArrowCursor(cursor);
        }

        public static void SetCursor(int id, Vector3 from, Vector3 to)
        {
            if (UnityEditor.HandleUtility.nearestControl != id && 
                EditorGUIUtility.hotControl != id)
                return;

            SetArrowCursor(SceneHandleUtility.GetCursorForEdge(from, to));
        }

        static readonly Vector3[] linePoints = new Vector3[2];
        public static void DrawEdgeHandle(int id, Vector3 from, Vector3 to, bool setCursor, bool renderEdge = true, bool setControl = true, MouseCursor? cursor = null)
        {
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    if (setCursor && setControl)
                    {
                        if (InCameraOrbitMode)
                            break;
                        UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to) * 0.5f);
                    }
                    break;
                }
                case EventType.Repaint:
                {
                    if (setCursor &&
                        !InCameraOrbitMode)
                    {
                        if (!cursor.HasValue)
                            SetCursor(id, from, to);
                        else
                            SetCursor(id, cursor.Value);
                    }

                    if (renderEdge)
                    {
                        linePoints[0] = from;
                        linePoints[1] = to;
                        if (EditorGUIUtility.keyboardControl == id)
                            SceneHandles.DrawAAPolyLine(3.5f, linePoints);
                        else
                            SceneHandles.DrawAAPolyLine(2.5f, linePoints);
                    }
                    break;
                }
            }
        }

        public static Vector3 Edge2DHandleTangentOffset(int id, Vector3 from, Vector3 to, CapFunction capFunction = null, Vector3? snappingSteps = null, bool setCursor = true, bool renderEdge = true)
        {
            var edgeDelta   = from - to;
            var grid        = Grid.ActiveGrid;
            var edgeAxis    = grid.GetClosestAxis(edgeDelta);
            var axes        = grid.GetTangentAxesForAxis(edgeAxis, out Vector3 slideDir1, out Vector3 slideDir2);
            var midPoint    = (from + to) * 0.5f;
            var handleDir   = Vector3.Cross(slideDir1, slideDir2);
            return SceneHandles.Edge2DHandleOffset(id, from, to, midPoint, handleDir, slideDir1, slideDir2, 0, capFunction, axes, snappingSteps, setCursor: setCursor, renderEdge: renderEdge);
        }

        public static Vector3 Edge2DHandleOffset(int id, Vector3 from, Vector3 to, Vector3 slideDir1, Vector3 slideDir2, CapFunction capFunction = null, Axes axes = Axes.None, Vector3? snappingSteps = null, bool setCursor = true, bool renderEdge = true)
        {
            var midPoint = (from + to) * 0.5f;
            var handleDir = Vector3.Cross(slideDir1, slideDir2);
            return SceneHandles.Edge2DHandleOffset(id, from, to, midPoint, handleDir, slideDir1, slideDir2, 0, capFunction, axes, snappingSteps, setCursor: setCursor, renderEdge: renderEdge);
        }

        public static Vector3 Edge2DHandleOffset(int id, Vector3 from, Vector3 to, Vector3 position, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, Vector3? snappingSteps = null, bool setCursor = true, bool renderEdge = true)
        {
            DrawEdgeHandle(id, from, to, setCursor: setCursor, renderEdge: renderEdge);

            var points = new Vector3[] { from, to };
            var result = Slider2D.Do(id, points, position, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, snappingSteps: snappingSteps);
            return result[0] - from;
        }

        public static Vector3 Slider2DHandleTangentOffset(int id, Vector3 from, Vector3 to, CapFunction capFunction = null, Vector3? snappingSteps = null)
        {
            var edgeDelta = from - to;
            var grid = Grid.ActiveGrid;
            var edgeAxis = grid.GetClosestAxis(edgeDelta);
            var axes = grid.GetTangentAxesForAxis(edgeAxis, out Vector3 slideDir1, out Vector3 slideDir2);
            var midPoint = (from + to) * 0.5f;
            var handleDir = Vector3.Cross(slideDir1, slideDir2);

            var points = new Vector3[] { from, to };
            var result = Slider2D.Do(id, points, midPoint, Vector3.zero, handleDir, slideDir1, slideDir2, 0, capFunction, axes, snappingSteps: snappingSteps);
            return result[0] - from;
        }
    }
}
