using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public sealed partial class SceneHandles
    {
        internal static int s_RectHash0 = "RectHash0".GetHashCode();
        internal static int s_RectHash1 = "RectHash1".GetHashCode();
        internal static int s_RectHash2 = "RectHash2".GetHashCode();
        internal static int s_RectHash3 = "RectHash3".GetHashCode();
        internal static int s_RectHash4 = "RectHash4".GetHashCode();
        internal static int s_RectHash5 = "RectHash5".GetHashCode();
        internal static int s_RectHash6 = "RectHash6".GetHashCode();
        internal static int s_RectHash7 = "RectHash7".GetHashCode();
        
        public static Rect RectHandle(Rect rect, Quaternion rotation, CapFunction capFunction)
        {
            var originalMatrix = SceneHandles.matrix;
            SceneHandles.matrix = originalMatrix * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
            var result = RectHandle(rect, capFunction);
            SceneHandles.matrix = originalMatrix;
            return result;
        }
        
        public static Rect RectHandle(Rect rect, CapFunction capFunction)
        {
            var handlesMatrix = SceneHandles.matrix;

            var direction = Vector3.forward;
            var slideDirX = Vector3.right;
            var slideDirY = Vector3.up;

            var point1Id = GUIUtility.GetControlID (s_RectHash0, FocusType.Keyboard);
            var point2Id = GUIUtility.GetControlID (s_RectHash1, FocusType.Keyboard);
            var point3Id = GUIUtility.GetControlID (s_RectHash2, FocusType.Keyboard);
            var point4Id = GUIUtility.GetControlID (s_RectHash3, FocusType.Keyboard);
            
            var edge1Id  = GUIUtility.GetControlID (s_RectHash4, FocusType.Keyboard);
            var edge2Id  = GUIUtility.GetControlID (s_RectHash5, FocusType.Keyboard);
            var edge3Id  = GUIUtility.GetControlID (s_RectHash6, FocusType.Keyboard);
            var edge4Id	 = GUIUtility.GetControlID (s_RectHash7, FocusType.Keyboard);
            
            int currentFocusControl = SceneHandleUtility.focusControl;

            bool highlightEdge1 = (currentFocusControl == edge1Id) || (currentFocusControl == point1Id) || (currentFocusControl == point2Id);
            bool highlightEdge2 = (currentFocusControl == edge2Id) || (currentFocusControl == point3Id) || (currentFocusControl == point4Id);
            bool highlightEdge3 = (currentFocusControl == edge3Id) || (currentFocusControl == point2Id) || (currentFocusControl == point3Id);
            bool highlightEdge4 = (currentFocusControl == edge4Id) || (currentFocusControl == point4Id) || (currentFocusControl == point1Id);
            
            var selectedAxes = ((highlightEdge3 || highlightEdge4) ? Axes.X : Axes.None) |
                               ((highlightEdge1 || highlightEdge2) ? Axes.Y : Axes.None);

            var xMin = rect.xMin;
            var xMax = rect.xMax;
            var yMin = rect.yMin;
            var yMax = rect.yMax;

            var point1 = new Vector3(xMin, yMin, 0);
            var point2 = new Vector3(xMax, yMin, 0);
            var point3 = new Vector3(xMax, yMax, 0);
            var point4 = new Vector3(xMin, yMax, 0);
            
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectExtensions.ContainsStatic(Selection.gameObjects));
            var prevDisabled	= SceneHandles.disabled;
            var prevColor		= SceneHandles.color;

            var xAxisDisabled	= isStatic || prevDisabled || Snapping.AxisLocking[0];
            var yAxisDisabled	= isStatic || prevDisabled || Snapping.AxisLocking[1];
            var xyAxiDisabled	= xAxisDisabled && yAxisDisabled;

            Vector3 position, offset;
            var prevGUIchanged = GUI.changed;
            

            SceneHandles.disabled = yAxisDisabled;
            { 
                GUI.changed = false;
                position = (point1 + point2) * 0.5f;
                SceneHandles.color = SceneHandles.StateColor(SceneHandles.yAxisColor, xAxisDisabled, highlightEdge1);
                offset = Edge1DHandleOffset(edge1Id, Axis.Y, point1, point2, position, slideDirY, Snapping.MoveSnappingSteps.y, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { yMin += offset.y; prevGUIchanged = true; }

                GUI.changed = false;
                position = (point3 + point4) * 0.5f;
                SceneHandles.color = SceneHandles.StateColor(SceneHandles.yAxisColor, xAxisDisabled, highlightEdge2);
                offset = Edge1DHandleOffset(edge2Id, Axis.Y, point3, point4, position, slideDirY, Snapping.MoveSnappingSteps.y, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { yMax += offset.y; prevGUIchanged = true; }
            }
            

            SceneHandles.disabled = xAxisDisabled;
            { 
                GUI.changed = false;
                position = (point2 + point3) * 0.5f;
                SceneHandles.color = SceneHandles.StateColor(SceneHandles.yAxisColor, xAxisDisabled, highlightEdge3);
                offset = Edge1DHandleOffset(edge3Id, Axis.X, point2, point3, position, slideDirX, Snapping.MoveSnappingSteps.x, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { xMax += offset.x; prevGUIchanged = true; }

                GUI.changed = false;
                position = (point4 + point1) * 0.5f;
                SceneHandles.color = SceneHandles.StateColor(SceneHandles.yAxisColor, xAxisDisabled, highlightEdge4);
                offset = Edge1DHandleOffset(edge4Id, Axis.X, point4, point1, position, slideDirX, Snapping.MoveSnappingSteps.x, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, null);
                if (GUI.changed) { xMin += offset.x; prevGUIchanged = true; }
            }
            
            
            SceneHandles.disabled = xyAxiDisabled;
            SceneHandles.color = SceneHandles.StateColor(SceneHandles.yAxisColor, xyAxiDisabled, false);
            { 

                GUI.changed = false;
                point1 = Slider2DHandle(point1Id, point1, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point1) * 0.05f, capFunction, Axes.XZ); 
                if (GUI.changed) { xMin = point1.x; yMin = point1.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point2 = Slider2DHandle(point2Id, point2, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point2) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMax = point2.x; yMin = point2.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point3 = Slider2DHandle(point3Id, point3, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point3) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMax = point3.x; yMax = point3.y; prevGUIchanged = true; }
            
                GUI.changed = false;
                point4 = Slider2DHandle(point4Id, point4, Vector3.zero, direction, slideDirX, slideDirY, UnityEditor.HandleUtility.GetHandleSize(point4) * 0.05f, capFunction, Axes.XZ);
                if (GUI.changed) { xMin = point4.x; yMax = point4.y; prevGUIchanged = true; }
            }
            GUI.changed = prevGUIchanged;
            
            rect.x = xMin; rect.width  = xMax - xMin;
            rect.y = yMin; rect.height = yMax - yMin;

            SceneHandles.disabled = prevDisabled;
            SceneHandles.color = prevColor;

            return rect;
        }
    }
}
