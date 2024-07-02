using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
        internal static int s_Edge1DHash = "Edge1DHash".GetHashCode();

        public static Vector3[] Edge1DHandle(Axis axis, Vector3[] points, Vector3 from, Vector3 to, Vector3 direction, float snappingStep, float handleSize, CapFunction capFunction, bool selectLockingAxisOnClick = false) 
        {
            var id = GUIUtility.GetControlID (s_Edge1DHash, FocusType.Keyboard);
            return Edge1DHandle(id, axis, points, from, to, direction, snappingStep, handleSize, capFunction, selectLockingAxisOnClick);
        }
        
        public static Vector3[] Edge1DHandle(int id, Axis axis, Vector3[] points, Vector3 from, Vector3 to, Vector3 direction, float snappingStep, float handleSize, CapFunction capFunction, bool selectLockingAxisOnClick = false) 
        {
            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to) * 2.0f);
                    break;
                }
                case EventType.Repaint:
                {
                    DrawLine(from, to);
                    break;
                }
            }

            var position = (from + to) * 0.5f;
            var result = Slider1D.Do(id, axis, points, position, direction, snappingStep, handleSize, capFunction, selectLockingAxisOnClick);
            return result;
        }
        
        public static float Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, float snappingStep, float handleSize, CapFunction capFunction) 
        {
            var id			= GUIUtility.GetControlID (s_Edge1DHash, FocusType.Keyboard);
            return Edge1DHandleOffset(id, axis, from, to, snappingStep, handleSize, capFunction);
        }

        public static float Edge1DHandleOffset(int id, Axis axis, Vector3 from, Vector3 to, float snappingStep, float handleSize, CapFunction capFunction)
        {
            var position    = (from + to) * 0.5f;
            var direction   = (Vector3)Matrix4x4.identity.GetColumn((int)axis);
            return Edge1DHandleOffset(id, axis, from, to, position, direction, snappingStep, handleSize, capFunction)[(int)axis];
        }

        public static float Edge1DHandle(Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0)
        {
            return Edge1DHandle(axis, from, to, snappingStep, handleSize, Chisel.Editors.SceneHandles.OutlinedDotHandleCap);
        }

        public static float Edge1DHandle(Axis axis, Vector3 from, Vector3 to, float snappingStep, float handleSize, CapFunction capFunction)
        {
            var id			= GUIUtility.GetControlID(s_Edge1DHash, FocusType.Keyboard);
            var position	= (from + to) * 0.5f;
            var direction	= (Vector3)Matrix4x4.identity.GetColumn((int)axis);
            return Edge1DHandleOffset(id, axis, from, to, position, direction, snappingStep, handleSize, capFunction)[(int)axis] + position[(int)axis];
        }

        public static float Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0)
        {
            return Edge1DHandleOffset(axis, from, to, snappingStep, handleSize, Chisel.Editors.SceneHandles.OutlinedDotHandleCap);
        }

        public static float Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, CapFunction capFunction)
        {
            return Edge1DHandleOffset(axis, from, to, 0, 0, capFunction);
        }

        public static float Edge1DHandleOffset(int id, Axis axis, Vector3 from, Vector3 to, CapFunction capFunction)
        {
            return Edge1DHandleOffset(id, axis, from, to, 0, 0, capFunction);
        }

        // TODO: improve this
        public static Vector3 Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep= 0 , float handleSize = 0)
        {
            var position = (from + to) * 0.5f;
            var id = GUIUtility.GetControlID(s_Edge1DHash, FocusType.Keyboard);
            return Edge1DHandleOffset(id, axis, from, to, position, direction, snappingStep, handleSize, Chisel.Editors.SceneHandles.OutlinedDotHandleCap);
        }

        public static Vector3 Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep, float handleSize, CapFunction capFunction)
        {
            var position = (from + to) * 0.5f;
            var id = GUIUtility.GetControlID(s_Edge1DHash, FocusType.Keyboard);
            return Edge1DHandleOffset(id, axis, from, to, position, direction, snappingStep, handleSize, capFunction);
        }

        public static Vector3 Edge1DHandleOffset(Axis axis, Vector3 from, Vector3 to, Vector3 position, Vector3 direction, float snappingStep, float handleSize, CapFunction capFunction)
        {
            var id = GUIUtility.GetControlID(s_Edge1DHash, FocusType.Keyboard);
            return Edge1DHandleOffset(id, axis, from, to, position, direction, snappingStep, handleSize, capFunction);
        }

        public static Vector3 Edge1DHandleOffset(int id, Axis axis, Vector3 from, Vector3 to, Vector3 position, Vector3 direction, float snappingStep, float handleSize, CapFunction capFunction)
        {
            if (snappingStep == 0)
                snappingStep = Snapping.MoveSnappingSteps[(int)axis];
            if (handleSize == 0)
                handleSize = UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f;

            var evt = Event.current;
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    UnityEditor.HandleUtility.AddControl(id, UnityEditor.HandleUtility.DistanceToLine(from, to));
                    break;
                }
                case EventType.Repaint:
                {
                    SetCursor(id, from, to);
                    linePoints[0] = from;
                    linePoints[1] = to;
                    SceneHandles.DrawAAPolyLine(3.0f, linePoints);
                    break;
                }
            }


            var points = new Vector3[] { from, to };
            var result = Slider1DHandle(id, axis, points, position, direction, snappingStep, handleSize, capFunction);
            return result[0] - from;
        }
    }
}
