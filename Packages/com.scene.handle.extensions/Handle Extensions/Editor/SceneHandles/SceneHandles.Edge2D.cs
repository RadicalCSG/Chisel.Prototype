using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
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
                        if (EditorGUIUtility.keyboardControl == id)
                            SceneHandles.DrawAAPolyLine(3.5f, from, to);
                        else
                            SceneHandles.DrawAAPolyLine(2.5f, from, to);
                    }
                    break;
                }
            }


            var points = new Vector3[] { from, to };
            var result = Slider2D.Do(id, points, position, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, snappingSteps: snappingSteps);
            return result[0] - from;
        }
    }
}
