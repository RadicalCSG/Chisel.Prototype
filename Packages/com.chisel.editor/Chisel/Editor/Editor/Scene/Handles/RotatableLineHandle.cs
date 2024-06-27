using UnityEditor;
using UnityEngine;
using UnitySceneExtensions;
using Snapping = UnitySceneExtensions.Snapping;

namespace Chisel.Editors
{
    public static class RotatableLineHandle
    {
        static readonly int s_RotatedEdge2DHash = "RotatedEdge2D".GetHashCode();

        public static float DoHandle(float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize = 0.0f, UnitySceneExtensions.SceneHandles.CapFunction capFunction = null, Axes axes = Axes.None)
        {
            var id = GUIUtility.GetControlID (s_RotatedEdge2DHash, FocusType.Keyboard);
            return DoHandle(id, angle, origin, diameter, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes);
        }

        static float rotatedStartAngle = 0.0f;
        static float rotatedAngleOffset = 0.0f;
        public static float DoHandle(int id, float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, float handleSize = 0.0f, UnitySceneExtensions.SceneHandles.CapFunction capFunction = null, Axes axes = Axes.None)
        {
            var from		= origin;
            var vector		= Quaternion.AngleAxis(angle, handleDir) * Vector3.forward;
            var to			= from + (vector * diameter);
            var position	= from + (vector * (diameter * 0.5f));

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to) * 0.5f);
                    break;
                }
                case EventType.Repaint:
                {
                    SceneHandles.SetCursor(id, from, to);

                    if (EditorGUIUtility.keyboardControl == id)
                    {
                        SceneHandles.DrawAAPolyLine(3.0f, from, to);
                    } else
                    {
                        SceneHandles.DrawAAPolyLine(2.5f, from, to);
                    }
                    break;
                }
            }

            if (handleSize == 0.0f)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f;

            
            if (evt.GetTypeForControl(id) == EventType.MouseDown &&
                GUIUtility.hotControl == 0 &&
                ((UnityEditor.HandleUtility.nearestControl == id && evt.button == 0) ||
                 (GUIUtility.keyboardControl == id && evt.button == 2)))
            {
                rotatedStartAngle = angle;
                rotatedAngleOffset = 0.0f;
            }

            var newPosition = UnitySceneExtensions.SceneHandles.Slider2D.Do(id, to, position, Vector3.zero, handleDir, slideDir1, slideDir2, handleSize, capFunction, axes);

            if (GUIUtility.hotControl != id)
                return angle;

            rotatedAngleOffset += Utilities.GeometryMath.SignedAngle(vector, (newPosition - origin).normalized, handleDir);
            
            
            // TODO: put somewhere else
            if (!Snapping.RotateSnappingActive)
            {
                return rotatedStartAngle + rotatedAngleOffset;
            }

            var rotateSnap = ChiselEditorSettings.RotateSnap;
            var newAngle		= rotatedStartAngle + rotatedAngleOffset;
            var snappedAngle	= (int)Mathf.Round(newAngle / rotateSnap) * rotateSnap;
            return snappedAngle;
        }
    }
}
