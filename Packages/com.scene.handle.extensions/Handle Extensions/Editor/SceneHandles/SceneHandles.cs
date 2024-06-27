using System;
using UnityEngine;

namespace UnitySceneExtensions
{
    public sealed partial class SceneHandles
    {
        public static float		backfaceSizeMultiplier	= 0.95f;
        public static bool		disabled				{ get { return !GUI.enabled; } set { GUI.enabled = !value; } }

        public static Matrix4x4	matrix					{ get { return UnityEditor.Handles.matrix; } set { UnityEditor.Handles.matrix = value; } }
        public static Matrix4x4	inverseMatrix			{ get { return UnityEditor.Handles.inverseMatrix; } }


        public static void DrawDottedLine(Vector3 p1, Vector3 p2, float screenSpaceSize) { UnityEditor.Handles.DrawDottedLine(p1, p2, screenSpaceSize); }		
        public static void DrawDottedLines(Vector3[] lineSegments, float screenSpaceSize) { UnityEditor.Handles.DrawDottedLines(lineSegments, screenSpaceSize); }
        public static void DrawAAPolyLine(Vector3 pointA, Vector3 pointB) 
        {
            linePoints[0] = pointA;
            linePoints[1] = pointB;
            UnityEditor.Handles.DrawAAPolyLine(linePoints); 
        }
        public static void DrawAAPolyLine(params Vector3[] points) { UnityEditor.Handles.DrawAAPolyLine(points); }		
        public static void DrawAAPolyLine(float width, params Vector3[] points) { UnityEditor.Handles.DrawAAPolyLine(width, points); }
        public static void DrawAAPolyLine(float width, int actualNumberOfPoints, params Vector3[] points) { UnityEditor.Handles.DrawAAPolyLine(width, actualNumberOfPoints, points); }
        public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius) { UnityEditor.Handles.DrawWireDisc(center, normal, radius); }
        public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius) { UnityEditor.Handles.DrawWireArc(center, normal, from, angle, radius); }
        public static void DrawAAConvexPolygon(params Vector3[] points) { UnityEditor.Handles.DrawAAConvexPolygon(points); }
        public static void DrawSolidRectangleWithOutline(Rect rectangle, Color faceColor, Color outlineColor) { UnityEditor.Handles.DrawSolidRectangleWithOutline(rectangle, faceColor, outlineColor); }
        public static void DrawSolidRectangleWithOutline(Vector3[] verts, Color faceColor, Color outlineColor) { UnityEditor.Handles.DrawSolidRectangleWithOutline(verts, faceColor, outlineColor); }
        

        public static void BeginGUI() { UnityEditor.Handles.BeginGUI(); }
        public static void EndGUI() { UnityEditor.Handles.EndGUI(); }

        public static void DrawLine(Vector3 p1, Vector3 p2)
        {
            linePoints[0] = p1;
            linePoints[1] = p2;
            UnityEditor.Handles.DrawAAPolyLine(2.0f, linePoints); 
        }
        
        public struct DrawingScope : IDisposable
        {
            bool m_NotDisposed;
            public DrawingScope(Color color) : this(color, UnityEditor.Handles.matrix) { }

            public DrawingScope(Matrix4x4 matrix) : this(UnityEditor.Handles.color, matrix) { }

            public DrawingScope(Color color, Matrix4x4 matrix)
            {
                m_NotDisposed = true;
                originalColor    = UnityEditor.Handles.color;
                originalMatrix   = UnityEditor.Handles.matrix;
                originalDisabled = SceneHandles.disabled;

                UnityEditor.Handles.color = color;
                UnityEditor.Handles.matrix = matrix;
            }

            public Color		originalColor		{ get; private set; }
            public Matrix4x4	originalMatrix		{ get; private set; }
            public bool			originalDisabled	{ get; private set; }

            public void Dispose()
            {
                if (!m_NotDisposed)
                    return; 
                m_NotDisposed = false;
                var prev = SceneHandles.disabled;
                SceneHandles.disabled = originalDisabled;
                UnityEditor.Handles.color = originalColor;
                UnityEditor.Handles.matrix = originalMatrix;
            }
        }
    }
}
