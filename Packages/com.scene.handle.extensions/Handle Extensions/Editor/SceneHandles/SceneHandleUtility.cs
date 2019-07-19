using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed partial class SceneHandleUtility
    {
        private static readonly Vector3[] Points = {Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero};

        public static int focusControl
        {
            get
            {
                if (!GUI.enabled || (Tools.viewTool != ViewTool.None && Tools.viewTool != ViewTool.Pan))
                    return 0;
                
                if (GUIUtility.hotControl == 0)
                    return UnityEditor.HandleUtility.nearestControl;
                
                return GUIUtility.keyboardControl;
            }
        }
        
        public static float DistanceToRectangle  (Vector3 position, Quaternion rotation, float size)
        {
            return DistanceToRectangle(position, rotation, new Vector2(size, size));
        }

        public static float DistanceToRectangle  (Vector3 position, Quaternion rotation, Vector2 size)
        {
            var sideways = rotation * new Vector3 (size.x, 0, 0);
            var up		 = rotation * new Vector3 (0, size.y, 0);
            Points[0] = UnityEditor.HandleUtility.WorldToGUIPoint (position + sideways + up);
            Points[1] = UnityEditor.HandleUtility.WorldToGUIPoint (position + sideways - up);
            Points[2] = UnityEditor.HandleUtility.WorldToGUIPoint (position - sideways - up);
            Points[3] = UnityEditor.HandleUtility.WorldToGUIPoint (position - sideways + up);
            Points[4] = Points[0];

            var pos = Event.current.mousePosition;
            var oddNodes = false;
            for (int i = 0, j = 4; i < 5; i++)
            {
                if ((Points[i].y > pos.y) != (Points[j].y>pos.y))
                {
                    if (pos.x < (Points[j].x-Points[i].x) * (pos.y-Points[i].y) / (Points[j].y-Points[i].y) + Points[i].x)
                    {
                        oddNodes = !oddNodes;
                    }
                }
                j = i;
            }

            if (oddNodes)
                return 0;
            
            // Distance to closest edge (not so fast)
            var closestDist = -1f;
            for (int i = 0, j = 1; i < 4; i++)
            {
                var dist = UnityEditor.HandleUtility.DistancePointToLineSegment(pos, Points[i], Points[j++]);
                if (dist < closestDist || 
                    closestDist < 0)
                    closestDist = dist;
            }
            return closestDist;
        }

        
        private static readonly MouseCursor[] SegmentCursors = new MouseCursor[]
        {
            MouseCursor.ResizeVertical,			// |
            MouseCursor.ResizeUpLeft,			// \
            MouseCursor.ResizeHorizontal,		// -
            MouseCursor.ResizeUpRight,			// /
            MouseCursor.ResizeVertical,			// |
            MouseCursor.ResizeUpLeft,			// \
            MouseCursor.ResizeHorizontal,		// -
            MouseCursor.ResizeUpRight			// /
        };



        internal static float SignedAngle(Vector2 from, Vector2 to)
        {
            Vector2 from_norm = from.normalized, to_norm = to.normalized;
            float unsigned_angle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(from_norm, to_norm), -1F, 1F)) * Mathf.Rad2Deg;
            float sign = Mathf.Sign(from_norm.x * to_norm.y - from_norm.y * to_norm.x);
            return unsigned_angle * sign;
        }
                
        public static MouseCursor GetCursorForDirection(Vector2 direction, float angleOffset = 0)
        {
            const float segmentAngle = 360 / 8.0f;
            var angle = (360 + (SignedAngle(Vector2.up, direction) + 180 + angleOffset)) % 360;
            var segment = Mathf.FloorToInt(((angle / segmentAngle) + 0.5f) % 8.0f);

            return SegmentCursors[segment];
        }

        public static MouseCursor GetCursorForEdge(Vector3 from, Vector3 to)
        {
            var camera = SceneView.currentDrawingSceneView.camera;
            if (!camera)
                return MouseCursor.Arrow;

            if ((from - to).sqrMagnitude < 0.001f)
                return MouseCursor.MoveArrow;
                    
            var worldCenterPoint1 = SceneHandles.matrix.MultiplyPoint(from);
            var worldCenterPoint2 = SceneHandles.matrix.MultiplyPoint(to);
            var guiPoint1   = camera.WorldToScreenPoint(worldCenterPoint1);
            var guiPoint2   = camera.WorldToScreenPoint(worldCenterPoint2);
            var delta       = (guiPoint2 - guiPoint1).normalized;

            return GetCursorForDirection(delta, 90);
        }

        internal static MouseCursor GetCursorForDirection(Vector3 position, Vector3 direction)
        {
            var camera = SceneView.currentDrawingSceneView.camera;
            if (!camera)
                return MouseCursor.Arrow;
                    
            var worldCenterPoint1 = SceneHandles.matrix.MultiplyPoint(position);
            var worldCenterPoint2 = SceneHandles.matrix.MultiplyPoint(position + (direction * 10));
            var guiPoint1   = camera.WorldToScreenPoint(worldCenterPoint1);
            var guiPoint2   = camera.WorldToScreenPoint(worldCenterPoint2);
            var delta       = (guiPoint2 - guiPoint1).normalized;

            return GetCursorForDirection(delta);
        }

        static GUIStyle selectionRect = "SelectionRect";

        public static void DrawSelectionRectangle(Rect rect)
        {
            var origMatrix = SceneHandles.matrix;
            SceneHandles.matrix = Matrix4x4.identity;
            if (rect.width >= 0 || rect.height >= 0)
            {
                SceneHandles.BeginGUI();
                selectionRect.Draw(rect, GUIContent.none, false, false, false, false);
                SceneHandles.EndGUI();
            }
            SceneHandles.matrix = origMatrix;
        }

        // Returns the parameter for the projection of the /point/ on the given line
        public static float PointOnLineParameter(Vector3 point, Vector3 lineOrigin, Vector3 lineDirection)
        {
            return Vector3.Dot(lineDirection, (point - lineOrigin)) / lineDirection.sqrMagnitude;
        }

        // Helper function for doing arrows.
        public static float CalcLineTranslation(Vector2 start, Vector2 end, Vector3 origin, Vector3 direction)
        {
            // Apply handle matrix
            //origin		= Handles.matrix.MultiplyPoint(origin);
            //direction	= Handles.matrix.MultiplyVector(direction);


            // The constrained direction is facing towards the camera, THATS BAD when the handle is close to the camera
            // The origin goes through to the other side of the camera
            float invert = 1.0F;
            Vector3 cameraForward = Camera.current.transform.forward;
            if (Vector3.Dot(direction, cameraForward) < 0.0F)
                invert = -1.0F;

            // Ok - Get the parametrization of the line
            // p1 = start position, p2 = p1 + ConstraintDir.
            // we then parametrize the perpendicular position of end into the line (p1-p2)
            Vector3 cd = direction;
            cd.y = -cd.y;
            Camera cam = Camera.current;
            Vector2 p1 = EditorGUIUtility.PixelsToPoints(cam.WorldToScreenPoint(origin));
            Vector2 p2 = EditorGUIUtility.PixelsToPoints(cam.WorldToScreenPoint(origin + direction * invert));
            Vector2 p3 = end;
            Vector2 p4 = start;

            if (p1 == p2)
                return 0;

            p3.y = -p3.y;
            p4.y = -p4.y;

            var lineDirection = p2 - p1;
            float t0 = PointOnLineParameter(p4, p1, lineDirection);
            float t1 = PointOnLineParameter(p3, p1, lineDirection);

            float output = (t1 - t0) * invert;
            return output;
        }
        
        //*
        public static Vector3 ProjectPointLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 relativePoint = point - lineStart;
            Vector3 lineDirection = lineEnd - lineStart;
            float length = lineDirection.magnitude;
            Vector3 normalizedLineDirection = lineDirection;
            if (length > .000001f)
                normalizedLineDirection /= length;

            float dot = Vector3.Dot(normalizedLineDirection, relativePoint);
            dot = Mathf.Clamp(dot, 0.0F, length);

            return lineStart + normalizedLineDirection * dot;
        }
        //*/
        
        public static Vector3 ProjectPointInfiniteLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 relativePoint = point - lineStart;
            Vector3 lineDirection = lineEnd - lineStart;
            float length = lineDirection.magnitude;
            Vector3 normalizedLineDirection = lineDirection;
            if (length > .000001f)
                normalizedLineDirection /= length;

            float dot = Vector3.Dot(normalizedLineDirection, relativePoint);
            //dot = Mathf.Clamp(dot, 0.0F, length);

            return lineStart + normalizedLineDirection * dot;
        }
        
        public static Vector3 ProjectPointRay(Vector3 point, Vector3 lineStart, Vector3 lineDirection)
        {
            Vector3 relativePoint = point - lineStart;
            return lineStart + lineDirection * Vector3.Dot(lineDirection, relativePoint);
        }
        //*/
    }
}
