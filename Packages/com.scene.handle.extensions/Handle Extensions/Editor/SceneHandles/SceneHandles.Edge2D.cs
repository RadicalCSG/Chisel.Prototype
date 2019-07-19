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

        public static Vector3 Edge2DHandleOffset(int id, Vector3 from, Vector3 to, Vector3 position, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize, CapFunction capFunction, Axes axes = Axes.None, Vector3? snappingSteps = null) 
        {
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    if (InCameraOrbitMode)
                        break;
                    UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to) * 0.5f);
                    break;
                }
                case EventType.Repaint:
                {
                    var sceneView = SceneView.currentDrawingSceneView;
                    if (sceneView &&
                        !InCameraOrbitMode)
                    {
                        if (UnityEditor.HandleUtility.nearestControl == id || EditorGUIUtility.hotControl == id)
                        {
                            var rect = sceneView.position;
                            rect.min = Vector2.zero;
                            EditorGUIUtility.AddCursorRect(rect, SceneHandleUtility.GetCursorForEdge(from, to));
                        }
                    }
                    if (EditorGUIUtility.keyboardControl == id)
                        SceneHandles.DrawAAPolyLine(3.0f, from, to);
                    else
                        SceneHandles.DrawAAPolyLine(2.5f, from, to);
                    break;
                }
            }


            var points = new Vector3[] { from, to };
            var result = Slider2D.Do(id, points, position, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes, snappingSteps: snappingSteps);
            return result[0] - from;
        }
    }
}
