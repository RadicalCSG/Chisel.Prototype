using Chisel.Components;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.EditorTools;
using UnitySceneExtensions;
#if !UNITY_2020_2_OR_NEWER
using ToolManager = UnityEditor.EditorTools;
#endif

namespace Chisel.Editors
{
    public sealed class ChiselEditorHandles : IChiselHandles
    {
        public void Start(ChiselGeneratorComponent generator, SceneView sceneView, Matrix4x4 transformation)
        {
            this.focusControl = UnitySceneExtensions.SceneHandleUtility.focusControl;
            this.disabled = UnitySceneExtensions.SceneHandles.disabled;
            this.generator = generator;
            this.matrix = transformation;
            this.modified = false;
            this.lastHandleHadFocus = false;
            if (generator)
                this.generatorTransform = this.generator.transform;
            else
                this.generatorTransform = null;
            this.sceneView = sceneView;
            if (sceneView)
                this.camera = sceneView.camera;
            else
                this.camera = null;
        }

        public void End()
        {
            this.focusControl = 0;
            this.disabled = true;
            this.generator = null;
            this.matrix = Matrix4x4.identity;
            this.modified = false;
            this.lastHandleHadFocus = false;
            this.generatorTransform = null;
            this.sceneView = null;
            this.camera = null;
        }


        public bool IsIn2DMode
        {
            get
            {
                return sceneView != null && sceneView.isRotationLocked && camera.orthographic;
            }
        }

        int focusControl;

        ChiselGeneratorComponent generator;
        Transform generatorTransform;
        public bool modified { get; private set; }

        public Matrix4x4 matrix { get; set; }
        public SceneView sceneView { get; private set; }
        public Camera camera { get; private set; }

        public bool lastHandleHadFocus { get; private set; }
        public bool disabled { get; private set; }
        public bool backfaced { get; set; }
        public Color color
        {
            get { return Handles.color; }
            set { Handles.color = value; }
        }

        public Vector3 moveSnappingSteps
        {
            get
            {
                return UnitySceneExtensions.Snapping.MoveSnappingSteps;
            }
        }

        public bool IsSufaceBackFaced(Vector3 point, Vector3 normal)
        {
            // TODO: make matrix and Handles.matrix/Handles.inverseMatrix work together in a reasonable way
            var inverseMatrix           = UnityEditor.Handles.inverseMatrix;
            var cameraLocalPos          = inverseMatrix.MultiplyPoint(camera.transform.position);
            var cameraLocalForward      = inverseMatrix.MultiplyVector(camera.transform.forward);
            var isCameraOrthographic    = camera.orthographic;

            var cosV = isCameraOrthographic ? Vector3.Dot(normal, -cameraLocalForward) :
                                              Vector3.Dot(normal, (cameraLocalPos - point));

            return (cosV < -0.0001f);
        }

        public void RenderBox(Bounds bounds) { HandleRendering.RenderBox(matrix, bounds); }
        public void RenderBoxMeasurements(Bounds bounds) { HandleRendering.RenderBoxMeasurements(matrix, bounds); }
        public void RenderCylinder(Bounds bounds, int segments) { HandleRendering.RenderCylinder(matrix, bounds, segments); }
        public void RenderShape(Curve2D shape, float height) { HandleRendering.RenderShape(matrix, shape, height); }

        public Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced)
        {
            var nonSelectedColor = baseColor;
            if (isBackfaced) nonSelectedColor.a *= UnitySceneExtensions.SceneHandles.backfaceAlphaMultiplier;
            var focusColor = (hasFocus) ? UnitySceneExtensions.SceneHandles.selectedColor : nonSelectedColor;
            return disabled ? Color.Lerp(focusColor, UnitySceneExtensions.SceneHandles.staticColor, UnitySceneExtensions.SceneHandles.staticBlend) : focusColor;
        }

        public void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLine(from, to, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawContinuousLines(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, startIndex, length, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            ChiselOutlineRenderer.DrawLineLoop(points, 0, points.Length, lineMode, thickness, dashSize);
        }

        void RecordUndo(string undoMessage, params UnityEngine.Object[] targets)
        {
            if (targets == null ||
                targets.Length == 0)
                return;
            if (targets.Length == 1)
            {
                Undo.RecordObject(targets[0], undoMessage);
            } else
                Undo.RecordObjects(targets, undoMessage);
        }

        void RecordUndo(string undoMessage)
        {
            RecordUndo(undoMessage ?? $"Modified {generator.NodeTypeName}", generator);
        }

        public bool DoBoundsHandle(ref Bounds bounds, Vector3? snappingSteps = null, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(bounds, Quaternion.identity, snappingSteps: snappingSteps);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            bounds = newBounds;
            this.modified = true;
            return true;
        }

        public bool DoShapeHandle(ref Curve2D shape, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newShape = UnitySceneExtensions.SceneHandles.Curve2DHandle(Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90, Vector3.right), Vector3.one), shape);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            shape = newShape;
            this.modified = true;
            return true;
        }

        public bool DoShapeHandle(ref Curve2D shape, Matrix4x4 transformation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newShape = UnitySceneExtensions.SceneHandles.Curve2DHandle(transformation, shape);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            shape = newShape;
            this.modified = true;
            return true;
        }

        public bool DoRadiusHandle(ref float outerRadius, Vector3 normal, Vector3 position, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newOuterRadius = UnitySceneExtensions.SceneHandles.RadiusHandle(normal, position, outerRadius, renderDisc: !renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            outerRadius = newOuterRadius;
            this.modified = true;
            return true;
        }

        public bool DoRadius2DHandle(ref Vector3 radius, Vector3 center, Vector3 up, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius = radius;
            this.lastHandleHadFocus = UnitySceneExtensions.SceneHandles.Radius2DHandle(center, up, ref newRadius, ref newRadius, minRadius, minRadius, maxRadius, maxRadius, renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            radius = newRadius;
            this.modified = true;
            return true;
        }

        public bool DoRadius2DHandle(ref Vector3 radius1, ref Vector3 radius2, Vector3 center, Vector3 up, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newRadius1 = radius1;
            var newRadius2 = radius2;
            this.lastHandleHadFocus = UnitySceneExtensions.SceneHandles.Radius2DHandle(center, up, ref newRadius1, ref newRadius2, minRadius1, minRadius2, maxRadius1, maxRadius2, renderDisc);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            radius1 = newRadius1;
            radius2 = newRadius2;
            this.modified = true;
            return true;
        }

        internal static int s_DoDirectionHandleHash = "DoDirectionHandle".GetHashCode();
        public bool DoDirectionHandle(ref Vector3 position, Vector3 direction, float snappingStep = 0, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoDirectionHandleHash, FocusType.Passive);
            this.lastHandleHadFocus = focusControl == id;
            var prevColor = color;
            color = GetStateColor(color, lastHandleHadFocus, backfaced);
            var newPosition = UnitySceneExtensions.SceneHandles.DirectionHandle(position, direction, snappingStep: snappingStep);
            color = prevColor;
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            position = newPosition;
            this.modified = true;
            return true;
        }

        internal static int s_DoRotatableLineHandleHash = "DoDirectionHandle".GetHashCode();
        public bool DoRotatableLineHandle(ref float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var id = GUIUtility.GetControlID(s_DoDirectionHandleHash, FocusType.Passive);
            this.lastHandleHadFocus = focusControl == id;
            var prevColor = color;
            color = GetStateColor(color, lastHandleHadFocus, backfaced);
            var newAngle = RotatableLineHandle.DoHandle(angle, origin, diameter, handleDir, slideDir1, slideDir2);
            color = prevColor;
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            angle = newAngle;
            this.modified = true;
            return true;
        }

        public bool DoTurnHandle(ref Bounds bounds, string undoMessage = null)
        {
            var min = new Vector3(Mathf.Min(bounds.min.x, bounds.max.x), Mathf.Min(bounds.min.y, bounds.max.y), Mathf.Min(bounds.min.z, bounds.max.z));
            var max = new Vector3(Mathf.Max(bounds.min.x, bounds.max.x), Mathf.Max(bounds.min.y, bounds.max.y), Mathf.Max(bounds.min.z, bounds.max.z));

            var center = (max + min) * 0.5f;

            switch (TurnHandle.DoHandle(bounds))
            {
                case TurnState.ClockWise:
                {
                    RecordUndo(undoMessage ?? "Rotated transform", generatorTransform, generator);
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
                    GUI.changed = true;
                    this.modified = true;
                    generatorTransform.RotateAround(generatorTransform.TransformPoint(center + generator.PivotOffset), generatorTransform.up, 90);
                    return true;
                }
                case TurnState.AntiClockWise:
                {
                    RecordUndo(undoMessage ?? "Rotated transform", generatorTransform, generator);
                    var newSize = bounds.size;
                    var t = newSize.x; newSize.x = newSize.z; newSize.z = t;
                    bounds.size = newSize;
                    GUI.changed = true;
                    this.modified = true;
                    generatorTransform.RotateAround(generatorTransform.TransformPoint(center + generator.PivotOffset), generatorTransform.up, -90);
                    return true;
                }
            }
            return false;
        }

        public bool DoEdgeHandle1D(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            offset = SceneHandles.Edge1DHandle(axis, from, to, snappingStep, handleSize);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoEdgeHandle1DOffset(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (renderLine)
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, snappingStep, handleSize);
            else
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, snappingStep, handleSize, capFunction: null);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoEdgeHandle1DOffset(out Vector3 offset, Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (renderLine)
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, direction, snappingStep, handleSize);
            else
                offset = SceneHandles.Edge1DHandleOffset(axis, from, to, direction, snappingStep, handleSize, capFunction: null);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            this.modified = true;
            return true;
        }

        public bool DoPathPointHandle(ref ChiselPathPoint pathPoint, string undoMessage = null)
        {
            switch (Tools.current)
            {
                case Tool.Move:     return DoPositionHandle(ref pathPoint.position, pathPoint.rotation, undoMessage);
                case Tool.Rotate:   return DoRotationHandle(ref pathPoint.rotation, pathPoint.position, undoMessage);
                case Tool.Scale:    return DoPlanarScaleHandle(ref pathPoint.scale, pathPoint.position, pathPoint.rotation, undoMessage);
                default:
                {
                    // TODO: implement
                    return false;
                }
            }
        }

        public bool DoPositionHandle(ref Vector3 position, Quaternion rotation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            var newPosition = UnitySceneExtensions.SceneHandles.PositionHandle(position, rotation);
            if (!EditorGUI.EndChangeCheck())
                return false;
            
            RecordUndo(undoMessage);
            position = newPosition;
            this.modified = true;
            return true;
        }

        public bool DoRotationHandle(ref Quaternion rotation, Vector3 position, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            if (Event.current.type == EventType.Repaint)
                UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap(0, position, rotation, UnityEditor.HandleUtility.GetHandleSize(position) * 0.05f, Event.current.type);
            var newRotation = Handles.RotationHandle(rotation, position);
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            rotation = newRotation;
            this.modified = true;
            return true;
        }

        public bool DoPlanarScaleHandle(ref Vector2 scale2D, Vector3 position, Quaternion rotation, string undoMessage = null)
        {
            EditorGUI.BeginChangeCheck();
            // TODO: create a 2D planar/bounds scale handle
            var scale3D = UnityEditor.Handles.ScaleHandle(new Vector3(scale2D.x, 1, scale2D.y), position, rotation, UnityEditor.HandleUtility.GetHandleSize(position));
            if (!EditorGUI.EndChangeCheck())
                return false;

            RecordUndo(undoMessage);
            scale2D = new Vector2(scale3D.x, scale3D.z);
            this.modified = true;
            return true;
        }
    }

    public sealed class ChiselEditorMessages : IChiselMessages
    {
        public void Warning(string message, Action buttonAction, string buttonText)
        {
            // TODO: prevent duplicates
            // TODO: show button to fix the problem
            EditorGUILayout.HelpBox(message, MessageType.Warning, true);
        }

        public void Warning(string message)
        {
            // TODO: prevent duplicates
            EditorGUILayout.HelpBox(message, MessageType.Warning, true);
        }
    }
}
