using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed partial class SceneHandles
    {
        public delegate void CapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);
        
        public static void RenderBorderedDot(Vector3 position, float size)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Only apply matrix to the position because its camera facing
            position = SceneHandles.matrix.MultiplyPoint(position);

            var camera		= Camera.current;
            var transform	= camera.transform;
            var sideways	= transform.right * size;
            var up			= transform.up * size;

            var p0 = position + (sideways + up);
            var p1 = position + (sideways - up);
            var p2 = position + (-sideways - up);
            var p3 = position + (-sideways + up);

            Color col = SceneHandles.color;

            var material = SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.QUADS);
                {
                    GL.Color(col);
                    GL.Vertex(p0);
                    GL.Vertex(p1);
                    GL.Vertex(p2);
                    GL.Vertex(p3);
                }
                GL.End();
            }

            material = SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    col.r = 0.0f;
                    col.g = 0.0f;
                    col.b = 0.0f;
                    GL.Color(col);
                    GL.Vertex(p0); GL.Vertex(p1);
                    GL.Vertex(p1); GL.Vertex(p2);
                    GL.Vertex(p2); GL.Vertex(p3);
                    GL.Vertex(p3); GL.Vertex(p0);
                }
                GL.End();
            }
        }

        public static void RenderBordererdDiamond(Vector3 position, float size)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Only apply matrix to the position because its camera facing
            position = SceneHandles.matrix.MultiplyPoint(position);

            var camera		= Camera.current;
            var transform	= camera.transform;
            var forward		= transform.forward;
            const float kSqrt2 = 1.414213562373095f; // Sqrt(2) to make it roughly equal size to the other dots

            var sideways	= (transform.right * size * kSqrt2);
            var up			= (transform.up    * size * kSqrt2);
            
            var p0 = position + (-sideways);
            var p1 = position + (-up      );
            var p2 = position + ( sideways);
            var p3 = position + ( up      );

            Color col = SceneHandles.color;

            var material = SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.QUADS);
                {
                    GL.Color(col);
                    GL.Vertex(p0);
                    GL.Vertex(p1);
                    GL.Vertex(p2);
                    GL.Vertex(p3);
                }
                GL.End();
            }
            
            material = SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    col.r = 0.0f;
                    col.g = 0.0f;
                    col.b = 0.0f;
                    GL.Color(col);
                    GL.Vertex(p0); GL.Vertex(p1);
                    GL.Vertex(p1); GL.Vertex(p2);
                    GL.Vertex(p2); GL.Vertex(p3);
                    GL.Vertex(p3); GL.Vertex(p0);
                }
                GL.End();
            }
        }

        public static void RenderBordererdTriangle(Vector3 position, float size)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            // Only apply matrix to the position because its camera facing
            position = SceneHandles.matrix.MultiplyPoint(position);

            var camera		= Camera.current;
            var transform	= camera.transform;
            var forward		= transform.forward;
            const float kSqrt2 = 1.414213562373095f; // Sqrt(2) to make it roughly equal size to the other dots

            var sideways	= (transform.right * size * kSqrt2);
            var up			= (transform.up    * size * kSqrt2);
            
            var p0 = position + (-sideways + up);
            var p1 = position + (-up           );
            var p2 = position + ( sideways + up);

            Color col = SceneHandles.color;

            var material = SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.TRIANGLES);
                {
                    GL.Color(col);
                    GL.Vertex(p0);
                    GL.Vertex(p1);
                    GL.Vertex(p2);
                }
                GL.End();
            }

            material = SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    col.r = 0.0f;
                    col.g = 0.0f;
                    col.b = 0.0f;
                    GL.Color(col);
                    GL.Vertex(p0); GL.Vertex(p1);
                    GL.Vertex(p1); GL.Vertex(p2);
                    GL.Vertex(p2); GL.Vertex(p0);
                }
                GL.End();
            }
        }

        static Vector2[] circlePoints = null;
        static Vector3[] circleRotatedPoints = null;

        public static void RenderBorderedCircle(Vector3 position, float size)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            
            if (circlePoints == null ||
                circleRotatedPoints == null ||
                circlePoints.Length + 1 != circleRotatedPoints.Length)
            {
                const int kCircleSteps = 12;
                
                circlePoints = new Vector2[kCircleSteps];
                for (int i = 0; i < kCircleSteps; i++)
                {
                    circlePoints[i] = new Vector2(
                            (float)Mathf.Cos((i / (float)kCircleSteps) * Mathf.PI * 2),
                            (float)Mathf.Sin((i / (float)kCircleSteps) * Mathf.PI * 2)
                        );
                }
                circleRotatedPoints = new Vector3[kCircleSteps + 1];
            }


            // Only apply matrix to the position because its camera facing
            position = SceneHandles.matrix.MultiplyPoint(position);

            var camera		= Camera.current;
            var transform	= camera.transform;
            var sideways	= transform.right;
            var up			= transform.up;
            
            for (int i = 0; i < circlePoints.Length; i++)
            {
                const float kCircleSize = 1.2f; // to make it roughly equal size to the other dots
                var circle = circlePoints[i];
                var sizex = circle.x * size;
                var sizey = circle.y * size;
                circleRotatedPoints[i] = position + (((sideways * sizex) + (up * sizey)) * kCircleSize);
            }
            circleRotatedPoints[circlePoints.Length] = circleRotatedPoints[0];

            Color col = SceneHandles.color;

            var material = SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.TRIANGLES);
                {
                    GL.Color(col);
                    for (int i = 1; i < circleRotatedPoints.Length - 1; i++)
                    {
                        GL.Vertex(circleRotatedPoints[0]);
                        GL.Vertex(circleRotatedPoints[i]);
                        GL.Vertex(circleRotatedPoints[i + 1]);
                    }
                }
                GL.End();
            }

            material = SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    col.r = 0.0f;
                    col.g = 0.0f;
                    col.b = 0.0f;
                    GL.Color(col);
                    GL.Vertex(circleRotatedPoints[0]);
                    for (int i = 1; i < circleRotatedPoints.Length; i++)
                    {
                        GL.Vertex(circleRotatedPoints[i]);
                        GL.Vertex(circleRotatedPoints[i]);
                    }
                    GL.Vertex(circleRotatedPoints[0]);
                }
                GL.End();
            }
        }

        public readonly static CapFunction NormalHandleCap = NormalHandleCapFunction;
        public static void NormalHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    if (controlID == -1)
                        break;
                    UnityEditor.HandleUtility.AddControl(controlID, UnityEditor.HandleUtility.DistanceToCircle(position, size));
                    UnityEditor.HandleUtility.AddControl(controlID, UnityEditor.HandleUtility.DistanceToLine(position, position + (rotation * Vector3.forward * size * 10)));
                    break;
                }
                case EventType.Repaint:
                {
                    RenderBorderedCircle(position, size);
                    var prevColor = SceneHandles.color;
                    var color = prevColor;
                    color.a = 1.0f;

                    var currentFocusControl = SceneHandleUtility.focusControl;
                    if (currentFocusControl == controlID)
                    {
                        using (new SceneHandles.DrawingScope(color))
                        {
                            // Matrices with an uneven axi being scaled negatively, will invert the normals of the cone, 
                            //  and this will always render it as black. To avoid this situation, we decuce the direction 
                            // and origin that we would've had if we used the matrix and set the matrix to identity
                            var matrix = Handles.matrix;
                            rotation = Quaternion.LookRotation(matrix.MultiplyVector(rotation * Vector3.forward));
                            position = matrix.MultiplyPoint(position);
                            Handles.matrix = Matrix4x4.identity;
                            ArrowHandleCap(controlID, position, rotation, size * 20, Event.current.type);
                        }
                    } else
                    {
                        SceneHandles.color = color;
                        var normal = rotation * Vector3.forward;
                        linePoints[0] = position;
                        linePoints[1] = position + (normal * size * 10);
                        DrawAAPolyLine(3.5f, linePoints);

                        SceneHandles.color = prevColor;
                    }
                    break;
                }
            }
        }

        public static void RenderBorderedCircle(Vector3 position, Quaternion rotation, float size)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            
            if (circlePoints == null ||
                circleRotatedPoints == null ||
                circlePoints.Length != circleRotatedPoints.Length + 1)
            {
                const int kCircleSteps = 12;
                
                circlePoints = new Vector2[kCircleSteps];
                for (int i = 0; i < kCircleSteps; i++)
                {
                    circlePoints[i] = new Vector2(
                            (float)Mathf.Cos((i / (float)kCircleSteps) * Mathf.PI * 2),
                            (float)Mathf.Sin((i / (float)kCircleSteps) * Mathf.PI * 2)
                        );
                }
                circleRotatedPoints = new Vector3[kCircleSteps + 1];
            }


            // Only apply matrix to the position because its camera facing
            position = SceneHandles.matrix.MultiplyPoint(position);


            var sideways	= rotation * Vector3.right;
            var up			= rotation * Vector3.up;
            
            for (int i = 0; i < circlePoints.Length; i++)
            {
                const float kCircleSize = 1.2f; // to make it roughly equal size to the other dots
                var circle = circlePoints[i];
                var sizex = circle.x * size;
                var sizey = circle.y * size;
                circleRotatedPoints[i] = position + (((sideways * sizex) + (up * sizey)) * kCircleSize);
            }
            circleRotatedPoints[circlePoints.Length] = circleRotatedPoints[0];

            Color col = SceneHandles.color;

            var material = SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.TRIANGLES);
                {
                    GL.Color(col);
                    for (int i = 1; i < circleRotatedPoints.Length - 1; i++)
                    {
                        GL.Vertex(circleRotatedPoints[0]);
                        GL.Vertex(circleRotatedPoints[i]);
                        GL.Vertex(circleRotatedPoints[i + 1]);
                    }
                }
                GL.End();
            }

            material = SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    col.r = 0.0f;
                    col.g = 0.0f;
                    col.b = 0.0f;
                    GL.Color(col);
                    GL.Vertex(circleRotatedPoints[0]);
                    for (int i = 1; i < circleRotatedPoints.Length; i++)
                    {
                        GL.Vertex(circleRotatedPoints[i]);
                        GL.Vertex(circleRotatedPoints[i]);
                    }
                    GL.Vertex(circleRotatedPoints[0]);
                }
                GL.End();
            }
        }

        public readonly static CapFunction OutlinedCircleHandleCap = OutlinedCircleHandleCapFunction;
        public static void OutlinedCircleHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    if (controlID == -1)
                        break;
                    UnityEditor.HandleUtility.AddControl(controlID, UnityEditor.HandleUtility.DistanceToCircle(position, size));
                    break;
                }
                case EventType.Repaint:
                { 
                    RenderBorderedCircle(position, size);
                    break;
                }
            }
        }

        public readonly static CapFunction OutlinedDotHandleCap = OutlinedDotHandleCapFunction;
        public static void OutlinedDotHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                {
                    if (SceneHandles.InCameraOrbitMode)
                        break;
                    if (controlID == -1)
                        break;
                    UnityEditor.HandleUtility.AddControl(controlID, UnityEditor.HandleUtility.DistanceToCircle(position, size));
                    break;
                }
                case EventType.Repaint:
                { 
                    RenderBorderedDot(position, size);
                    break;
                }
            }
        }

#if UNITY_5_6_OR_NEWER
        public readonly static CapFunction DotHandleCap = DotHandleCapFunction;
        public static void DotHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            UnityEditor.Handles.DotHandleCap(controlID, position, rotation, size, eventType);
        }
#else
        public static void DotHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                {
                    if (controlID == -1)
                        break;
                    UnityEngine.HandleUtility.AddControl(controlID, UnityEngine.HandleUtility.DistanceToCircle(position, size));
                    break;
                }
                case EventType.Repaint:
                { 
                    var direction = rotation * Vector3.forward;
                    UnityEngine.Handles.DotCap(controlID, position, Quaternion.LookRotation(direction), size * .2f);
                    break;
                }
            }
        }
#endif

#if UNITY_5_6_OR_NEWER
        public readonly static CapFunction CubeHandleCap = CubeHandleCapFunction;
        public static void CubeHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            UnityEditor.Handles.CubeHandleCap(controlID, position, rotation, size, eventType);
        }
#else
        public static void CubeHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                {
                    if (controlID == -1)
                        break;
                    UnityEngine.HandleUtility.AddControl(controlID, UnityEngine.HandleUtility.DistanceToCircle(position, size));
                    break;
                }
                case EventType.Repaint:
                { 
                    var direction = rotation * Vector3.forward;
                    UnityEngine.Handles.CubeCap(controlID, position, Quaternion.LookRotation(direction), size * .2f);
                    break;
                }
            }
        }
#endif

        internal static bool IsHovering(int controlID, Event evt)
        {
            return controlID == HandleUtility.nearestControl && GUIUtility.hotControl == 0 && !viewToolActive;
        }

        internal static bool viewToolActive
        {
            get
            {
                if (GUIUtility.hotControl != 0)
                    return false;

                Event evt = Event.current;
                bool viewShortcut = evt.type != EventType.Used && (evt.alt || evt.button == 1 || evt.button == 2);
                return Tools.current == Tool.View || viewShortcut;
            }
        }

#if UNITY_2020_1_OR_NEWER
        public readonly static CapFunction ArrowHandleCap = ArrowHandleCapFunction;
        public static void ArrowHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            UnityEditor.Handles.ArrowHandleCap(controlID, position, rotation, size, eventType);
        }
#else
        public static void ArrowHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            var hoverExtraScale     = 1.05f;
            var coneOffset          = Vector3.zero;
            switch (eventType)
            {
                case EventType.Layout:
                case EventType.MouseMove:
                {
                    Vector3 direction = rotation * Vector3.forward;
                    var distance = HandleUtility.DistanceToLine(position, position + (direction + coneOffset) * (size * 1.1f)) * 0.9f;
                    HandleUtility.AddControl(controlID, distance);
                    //HandleUtility.AddControl(controlID, HandleUtility.DistanceToCone(position + (direction + coneOffset) * size, rotation, size * .2f));
                    break;
                }
                case EventType.Repaint:
                {
                    Vector3 direction = rotation * Vector3.forward;
                    float coneSize = size * .2f;
                    if (IsHovering(controlID, Event.current))
                        coneSize *= hoverExtraScale;
                    ConeHandleCap(controlID, position + (direction + coneOffset) * size, rotation, coneSize, eventType);
                    Handles.DrawLine(position, position + (direction + coneOffset) * (size * .9f));
                    break;
                }
            }
        }
#endif

#if UNITY_5_6_OR_NEWER
        public readonly static CapFunction ConeHandleCap = ConeHandleCapFunction;
        public static void ConeHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            UnityEditor.Handles.ConeHandleCap(controlID, position, rotation, size, eventType);
        }
#else
        public static void ConeHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) 
        {
            switch (eventType)
            {
                case (EventType.Layout):
                {
                    if (controlID == -1)
                        break;
                    UnityEngine.HandleUtility.AddControl(controlID, UnityEngine.HandleUtility.DistanceToCircle(position, size));
                    break;
                }
                case (EventType.Repaint):
                {
                    UnityEngine.Handles.ConeCap(controlID, position, rotation, size);
                    break;
                }
            }
        }
#endif

#if UNITY_5_6_OR_NEWER
        public readonly static CapFunction RectangleHandleCap = RectangleHandleCapFunction;
        public static void RectangleHandleCapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            UnityEditor.Handles.RectangleHandleCap(controlID, position, rotation, size, eventType);
        }
#else
        static Vector3[] s_RectangleHandlePointsCache = new Vector3[5];

        public static void RectangleHandleCap (int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) 
        {
            RectangleHandleCap(controlID, position, rotation, new Vector2(size, size), eventType);
        }

        internal static void RectangleHandleCap (int controlID, Vector3 position, Quaternion rotation, Vector2 size, EventType eventType) 
        {
            switch (eventType)
            {
                case (EventType.Layout):
                {
                    if (controlID == -1)
                        break;
                    UnityEngine.HandleUtility.AddControl (controlID, UnityEngine.HandleUtility.DistanceToRectangle (position, rotation, size));
                    break;
                }
                case (EventType.Repaint):
                {
                    var sideways = rotation * new Vector3 (size.x, 0, 0);
                    var up = rotation * new Vector3 (0, size.y, 0);
                    s_RectangleHandlePointsCache[0] = position + sideways + up;
                    s_RectangleHandlePointsCache[1] = position + sideways - up;
                    s_RectangleHandlePointsCache[2] = position - sideways - up;
                    s_RectangleHandlePointsCache[3] = position - sideways + up;
                    s_RectangleHandlePointsCache[4] = position + sideways + up;
                    UnityEngine.Handles.DrawPolyLine (s_RectangleHandlePointsCache);
                    break;
                }
            }
        }
#endif
    }
}
