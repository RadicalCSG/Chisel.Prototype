using System;
using Unity.Mathematics;
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
        Matrix4x4 inverseMatrix { get; }
        Color color { get; set; }
        bool disabled { get; }
        bool backfaced { get; set; }
        bool IsIn2DMode { get; }

        Color GetStateColor(Color baseColor, bool hasFocus, bool isBackfaced);

        void RenderBox(Bounds bounds);
        void RenderBoxMeasurements(Bounds bounds);
        void RenderCylinder(Bounds bounds, int segments);
        void RenderDistanceMeasurement(Vector3 from, Vector3 to);
        void RenderDistanceMeasurement(Vector3 from, Vector3 to, float forceValue);
        void RenderShape(Curve2D shape, float height);

        void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(float3[] points, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
        void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f);
    }

    public static class IChiselHandleRendererExtensions
    {

        public static bool DoBoundsHandle(this IChiselHandles handles, ref ChiselAABB box, Vector3? snappingSteps = null, string undoMessage = null)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(box.Min, box.Max);
            var result = handles.DoBoundsHandle(ref bounds, snappingSteps, undoMessage);
            box.Min = bounds.min;
            box.Max = bounds.max;
            return result;
        }
        public static bool DoTurnHandle(this IChiselHandles handles, ref ChiselAABB box, string undoMessage = null)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(box.Min, box.Max);
            var result = handles.DoTurnHandle(ref box, undoMessage);
            box.Min = bounds.min;
            box.Max = bounds.max;
            return result;
        }

        public static void RenderBox(this IChiselHandleRenderer renderer, ChiselAABB bounds)               { renderer.RenderBox(new Bounds((bounds.Max + bounds.Min) * 0.5f, bounds.Max - bounds.Min)); }
        public static void RenderBoxMeasurements(this IChiselHandleRenderer renderer, ChiselAABB bounds)   { renderer.RenderBoxMeasurements(new Bounds((bounds.Max + bounds.Min) * 0.5f, bounds.Max - bounds.Min)); }
        public static void RenderCylinder(this IChiselHandleRenderer renderer, ChiselAABB bounds, int segments) { renderer.RenderCylinder(new Bounds((bounds.Max + bounds.Min) * 0.5f, bounds.Max - bounds.Min), segments); }
        
        public static void RenderDistanceMeasurement(this IChiselHandleRenderer renderer, float3 from, float3 to) { renderer.RenderDistanceMeasurement((Vector3)from, (Vector3)to); }
        public static void RenderDistanceMeasurement(this IChiselHandleRenderer renderer, float3 from, float3 to, float forceValue) { renderer.RenderDistanceMeasurement((Vector3)from, (Vector3)to, forceValue); }
        
        public static void DrawLine(this IChiselHandleRenderer renderer, float3 from, float3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f) { renderer.DrawLine((Vector3)from, (Vector3)to, lineMode, thickness, dashSize); }

    }

    public interface IChiselHandle
    {
        Color       Color       { get; set; }
        bool TryGetClosestPoint(out Vector3 closestPoint, bool interpolate = true);
    }
    
    public interface IChiselCircleHandle : IChiselHandle
    {
        Vector3     Center      { get; set; }
        Vector3     Normal      { get; set; }
        float       Diameter    { get; set; }
        float       StartAngle  { get; set; }
        float       TotalAngles { get; set; }
        float       DashSize    { get; set; }

        Vector3 GetPointAtDegree(float degree);
    }
    
    public interface IChiselEllipsoidHandle : IChiselHandle
    {
        Vector3     Center      { get; set; }
        Vector3     Normal      { get; set; }
        float       DiameterX   { get; set; }
        float       DiameterZ   { get; set; }
        float       Rotation    { get; set; }
        float       StartAngle  { get; set; }
        float       TotalAngles { get; set; }
        float       DashSize    { get; set; }

        Vector3 GetPointAtDegree(float degree);
    }

    public interface IChiselLineHandle : IChiselHandle
    {
        Vector3     From        { get; set; }
        Vector3     To          { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselPolygonHandle : IChiselHandle
    {
        Vector3[]   Vertices    { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselLineLoopHandle : IChiselHandle
    {
        Vector3[]   Vertices    { get; set; }
        int         Offset      { get; set; }
        int         Count       { get; set; }
        float       DashSize    { get; set; }
    }

    public interface IChiselNormalHandle : IChiselHandle
    {
        Vector3     Origin      { get; set; }
        Vector3     Normal      { get; set; }
        float       DashSize    { get; set; }
    }


    public interface IChiselHandleAllocation
    {
        IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, Color color, float startAngle = 0, float angles = 360, float dashSize = 0);
        IChiselCircleHandle CreateCircleHandle(Vector3 center, Vector3 normal, float diameter, float startAngle = 0, float angles = 360, float dashSize = 0);

        IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, Color color, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0);
        IChiselEllipsoidHandle CreateEllipsoidHandle(Vector3 center, Vector3 normal, float diameterX, float diameterZ, float rotation = 0, float startAngle = 0, float angles = 360, float dashSize = 0);

        IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, Color color, float dashSize = 0, bool highlightOnly = false);
        IChiselLineHandle CreateLineHandle(Vector3 from, Vector3 to, float dashSize = 0, bool highlightOnly = false);

        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, Color color, float dashSize = 0);
        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, float dashSize = 0);

        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, Color color, float dashSize = 0);
        IChiselLineLoopHandle CreateLineLoopHandle(Vector3[] vertices, int offset, int count, float dashSize = 0);

        IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, Color color, float dashSize = 0);
        IChiselNormalHandle CreateNormalHandle(Vector3 origin, Vector3 normal, float dashSize = 0);
    }
        
    public interface IChiselHandles : IChiselHandleRenderer, IChiselHandleAllocation
    {
        bool modified { get; }
        bool lastHandleHadFocus { get; }

        Vector3 moveSnappingSteps { get; }

        object generatorState { get; set; }

        bool TryGetClosestPoint(IChiselHandle[] handles, out Vector3 closestPoint, bool interpolate = true);
        bool TryGetClosestPoint(IChiselHandle handle, out Vector3 closestPoint, bool interpolate = true);
        void DoRenderHandles(IChiselHandle[] handles);

        bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref Vector3 position, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle[] handles, float snappingStep = 0, string undoMessage = null);
        bool DoSlider1DHandle(ref float distance, Vector3 center, Vector3 direction, IChiselHandle handle, float snappingStep = 0, string undoMessage = null);
        bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null);
        bool DoCircleRotationHandle(ref float angle, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null);
        bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle handle, string undoMessage = null);
        bool DoDistanceHandle(ref float distance, Vector3 center, Vector3 normal, IChiselHandle[] handles, string undoMessage = null);


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

    public interface IChiselMessageHandler
    {
        void Warning(string message, Action buttonAction, string buttonText);
        void Warning(string message);
    }
}
