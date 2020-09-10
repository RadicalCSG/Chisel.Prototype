using System;
using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    public enum LineMode
    {
        ZTest,
        NoZTest,
    }

    public interface IChiselHandleRenderer
    {
        Matrix4x4 matrix { get; set; }
        Color color { get; set; }
        bool disabled { get; }
        bool backfaced { get; set; }
        bool IsIn2DMode { get; }

        Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced);

        void RenderBox(Bounds bounds);
        void RenderBoxMeasurements(Bounds bounds);
        void RenderCylinder(Bounds bounds, int segments);
        void RenderShape(Curve2D shape, float height);

        void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
    }

    public interface IChiselHandles : IChiselHandleRenderer
    {
        bool modified { get; }
        bool lastHandleHadFocus { get; }

        Vector3 moveSnappingSteps { get; }

        bool IsSufaceBackFaced(Vector3 point, Vector3 normal);

        bool DoBoundsHandle(ref Bounds bounds, Vector3? snappingSteps = null, string undoMessage = null);
        bool DoShapeHandle(ref Curve2D shape, string undoMessage = null);
        bool DoShapeHandle(ref Curve2D shape, Matrix4x4 transformation, string undoMessage = null);
        bool DoRadiusHandle(ref float outerRadius, Vector3 direction, Vector3 position, bool renderDisc = true, string undoMessage = null);
        bool DoRadius2DHandle(ref Vector3 radius1, ref Vector3 radius2, Vector3 center, Vector3 up, float minRadius1 = 0, float minRadius2 = 0, float maxRadius1 = float.PositiveInfinity, float maxRadius2 = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null);
        bool DoRadius2DHandle(ref Vector3 radius, Vector3 center, Vector3 up, float minRadius = 0, float maxRadius = float.PositiveInfinity, bool renderDisc = true, string undoMessage = null);
        bool DoDirectionHandle(ref Vector3 position, Vector3 direction, float snappingStep = 0, string undoMessage = null);
        bool DoRotatableLineHandle(ref float angle, Vector3 origin, float diameter, Vector3 handleDir, Vector3 slideDir1, Vector3 slideDir2, string undoMessage = null);
        bool DoTurnHandle(ref Bounds bounds, string undoMessage = null);

        bool DoEdgeHandle1D(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, string undoMessage = null);
        bool DoEdgeHandle1DOffset(out Vector3 offset, Axis axis, Vector3 from, Vector3 to, Vector3 direction, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null);
        bool DoEdgeHandle1DOffset(out float offset, Axis axis, Vector3 from, Vector3 to, float snappingStep = 0, float handleSize = 0, bool renderLine = true, string undoMessage = null);


        bool DoPathPointHandle(ref ChiselPathPoint pathPoint, string undoMessage = null);
        bool DoPositionHandle(ref Vector3 position, Quaternion rotation, string undoMessage = null);
        bool DoRotationHandle(ref Quaternion rotation, Vector3 position, string undoMessage = null);
        bool DoPlanarScaleHandle(ref Vector2 scale2D, Vector3 position, Quaternion rotation, string undoMessage = null);
    }

    public interface IChiselMessages
    {
        void Warning(string message, Action buttonAction, string buttonText);
        void Warning(string message);
    }
}
