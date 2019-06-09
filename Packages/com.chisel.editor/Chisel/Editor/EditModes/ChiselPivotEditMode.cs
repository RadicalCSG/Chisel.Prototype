using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    // TODO: should only show pivot when there are pivots to modify
    public sealed class ChiselPivotEditMode : IChiselToolMode
    {
        const string kToolName = "Pivot";
        public string ToolName => kToolName;

        public bool EnableComponentEditors  { get { return false; } }
        public bool CanSelectSurfaces       { get { return false; } }
        public bool ShowCompleteOutline     { get { return true; } }

        #region Keyboard Shortcut
        const string kEditModeShotcutName = ChiselKeyboardDefaults.ShortCutEditModeBase + kToolName + " Mode";
        [Shortcut(kEditModeShotcutName, ChiselKeyboardDefaults.SwitchToPivotEditMode, displayName = kEditModeShotcutName)]
        public static void SwitchToPivotEditMode() { ChiselEditModeManager.EditModeType = typeof(ChiselPivotEditMode); }
        #endregion

        // TODO: put somewhere else
        #region Pivot rendering

        static Vector2[] circlePoints = null;

        static void SetupCirclePoints()
        {
            const int steps = 16;
            circlePoints = new Vector2[steps];
            for (int i = 0; i < steps; i++)
            {
                circlePoints[i] = new Vector2(
                        (float)Mathf.Cos((i / (float)steps) * Mathf.PI * 2),
                        (float)Mathf.Sin((i / (float)steps) * Mathf.PI * 2)
                    );
            }
        }

        public static void DrawCameraAlignedCircle(Vector3 position, float size, Color innerColor, Color outerColor)
        {
            var camera = Camera.current;
            var right = camera.transform.right;
            var up = camera.transform.up;

            if (circlePoints == null)
                SetupCirclePoints();

            var points = new Vector3[circlePoints.Length];
            for (int i = 0; i < circlePoints.Length; i++)
            {
                var circle = circlePoints[i];
                points[i] = position + (((right * circle.x) + (up * circle.y)) * size);
            }

            position = UnityEditor.Handles.matrix.MultiplyPoint(position);

            {
                Color c = outerColor * new Color(1, 1, 1, .5f) + (UnityEditor.Handles.lighting ? new Color(0, 0, 0, .5f) : new Color(0, 0, 0, 0)) * new Color(1, 1, 1, 0.99f);

                UnityEditor.Handles.color = c;
                for (int i = points.Length - 1, j = 0; j < points.Length; i = j, j++)
                {
                    UnityEditor.Handles.DrawAAPolyLine(6.0f, points[i], points[j]);
                }
            }

            {
                Color c = innerColor * new Color(1, 1, 1, .5f) + (UnityEditor.Handles.lighting ? new Color(0, 0, 0, .5f) : new Color(0, 0, 0, 0)) * new Color(1, 1, 1, 0.99f);

                UnityEditor.Handles.color = c;
                for (int i = points.Length - 1, j = 0; j < points.Length; i = j, j++)
                {
                    UnityEditor.Handles.DrawAAPolyLine(2.0f, points[i], points[j]);
                }
            }
        }

        public static void DrawFilledCameraAlignedCircle(Vector3 position, float size)
        {
            var camera = Camera.current;
            var right = camera.transform.right;
            var up = camera.transform.up;

            if (circlePoints == null)
                SetupCirclePoints();

            var points = new Vector3[circlePoints.Length];
            for (int i = 0; i < circlePoints.Length; i++)
            {
                var circle = circlePoints[i];
                points[i] = position + (((right * circle.x) + (up * circle.y)) * size);
            }

            position = UnityEditor.Handles.matrix.MultiplyPoint(position);

            Color c = UnityEditor.Handles.color * new Color(1, 1, 1, .5f) + (UnityEditor.Handles.lighting ? new Color(0, 0, 0, .5f) : new Color(0, 0, 0, 0)) * new Color(1, 1, 1, 0.99f);

            var material = UnitySceneExtensions.SceneHandleMaterialManager.CustomDotMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.TRIANGLES);
                {
                    GL.Color(c);
                    for (int i = 1; i < points.Length - 1; i++)
                    {
                        GL.Vertex(points[0]);
                        GL.Vertex(points[i]);
                        GL.Vertex(points[i + 1]);
                    }
                }
                GL.End();
            }

            material = UnitySceneExtensions.SceneHandleMaterialManager.SurfaceNoDepthMaterial;
            if (material && material.SetPass(0))
            {
                GL.Begin(GL.LINES);
                {
                    GL.Color(Color.black);
                    GL.Vertex(points[0]);
                    for (int i = 1; i < points.Length; i++)
                    {
                        GL.Vertex(points[i]);
                        GL.Vertex(points[i]);
                    }
                    GL.Vertex(points[0]);
                }
                GL.End();
            }
        }
        #endregion

        public void OnEnable()
        {
            ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;
            // TODO: shouldn't just always set this param
            Tools.hidden = true; 
        }

        public void OnDisable()
        {
        }

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var position = Tools.handlePosition;
            var rotation = Tools.handleRotation;
            if (Event.current.type == EventType.Repaint)
            {
                var handleSize = UnityEditor.HandleUtility.GetHandleSize(position);

                DrawFilledCameraAlignedCircle(position, handleSize * 0.1f);
                DrawCameraAlignedCircle(position, handleSize * 0.1f, Color.white, Color.black);
            }

            EditorGUI.BeginChangeCheck();
            var newPosition = UnitySceneExtensions.SceneHandles.PositionHandle(position, rotation);
            if (EditorGUI.EndChangeCheck())
            {
                var objs = Selection.objects;
                if (objs != null && objs.Length > 0)
                {
                    var nodes = new List<ChiselNode>();
                    for (int i = 0; i < objs.Length; i++)
                    {
                        var gameObject = objs[i] as GameObject;
                        if (gameObject)
                        {
                            var node = gameObject.GetComponent<ChiselNode>();
                            if (node) nodes.Add(node);
                        }

                        var component = objs[i] as Component;
                        if (component)
                        {
                            var node = component.GetComponent<ChiselNode>();
                            if (node) nodes.Add(node);
                        }
                    }

                    if (nodes != null && nodes.Count > 0)					
                        MovePivotTo(nodes, newPosition);
                }
            }
        }

        public static void MovePivotTo(List<ChiselNode> nodes, Vector3 newPosition)
        {
            var nodesWithChildren = new HashSet<UnityEngine.Object>();
            foreach (var node in nodes)
            {
                var children = node.GetComponentsInChildren<ChiselNode>();
                foreach (var child in children)
                {
                    nodesWithChildren.Add(child);
                    nodesWithChildren.Add(child.hierarchyItem.Transform);
                }
            }

            Undo.RecordObjects(nodesWithChildren.ToArray(), "Move Pivot");
            foreach (var node in nodes)
                node.SetPivot(newPosition);
        }
    }
}
