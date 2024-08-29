using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public enum TurnState
    {
        None,
        ClockWise,
        AntiClockWise
    }

    public static class TurnHandle
    {
        static GUIContent clockWiseRotation         = new GUIContent("↻");
        static GUIContent antiClockWiseRotation     = new GUIContent("↺");

        // TODO: put somewhere else
        public static Color iconColor = new Color(201f / 255, 200f / 255, 144f / 255, 1.00f);

        public static TurnState DoHandle(Bounds bounds)
        {
            var cameraPosition  = Camera.current.transform.position;
            var matrix          = Handles.matrix;

            var min			= new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
            var max			= new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

            var center      = (max + min) * 0.5f;

            var worldCenter     = matrix.MultiplyPoint(center);
            var iconDirection   = (cameraPosition - worldCenter).normalized;

            var dotX        = Vector3.Dot(matrix.MultiplyVector(new Vector3(1, 0, 0)), iconDirection);
            var dotZ        = Vector3.Dot(matrix.MultiplyVector(new Vector3(0, 0, 1)), iconDirection);
            var dotY        = Vector3.Dot(matrix.MultiplyVector(new Vector3(0, 1, 0)), iconDirection);

            if ((dotX > -0.2f) && (dotX < 0.2f)) dotX = 0;
            if ((dotZ > -0.2f) && (dotZ < 0.2f)) dotZ = 0;

            var axisY  = (dotY > 0);
            var axis0X = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotX < 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis0Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX < 0) : ((dotZ > 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis1X = (dotX == 0) ? (dotZ < 0) : (dotZ == 0) ? (dotX > 0) : ((dotX > 0) ^ (dotX < 0) ^ (dotZ > 0));
            var axis1Z = (dotX == 0) ? (dotZ > 0) : (dotZ == 0) ? (dotX > 0) : ((dotZ < 0) ^ (dotX < 0) ^ (dotZ > 0));

            var pLabel0     = new Vector3(axis0X ? min.x : max.x, axisY ? max.y : min.y, axis0Z ? min.z : max.z);
            var pLabel1     = new Vector3(axis1X ? min.x : max.x, axisY ? max.y : min.y, axis1Z ? min.z : max.z);

            var prevColor = Handles.color;
            Handles.color = iconColor;

            var result = TurnState.None;

            // TODO: consider putting both buttons next to each other
            //  - buttons closer to each other, which is nicer when you need to go back and forth (although you could just click 3 times to go back)
            //  - since you'd only have 1 button group, the chance is higher it's outside of the screen. 
            //    so a better solution should be found to make sure the button group doesn't overlap the stairs, yet is close to it, and on screen.
            if (SceneHandles.ClickableLabel(pLabel1, (pLabel1 - center).normalized, clockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
            {
                result = TurnState.ClockWise;
            }

            if (SceneHandles.ClickableLabel(pLabel0, (pLabel0 - center).normalized, antiClockWiseRotation, fontSize: 32, fontStyle: FontStyle.Bold))
            {
                result = TurnState.AntiClockWise;
            }

            Handles.color = prevColor;

            return result;
        }
    }
}
