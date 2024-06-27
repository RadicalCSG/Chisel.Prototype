using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEngine;
using UnitySceneExtensions;
using Grid = UnitySceneExtensions.Grid;

namespace Chisel.Editors
{
    public static class RectangleExtrusionHandle
    {
        static Matrix4x4		s_Transformation = Matrix4x4.identity;
        static ChiselModel		s_ModelBeneathCursor;
        static List<Vector3>	s_Points = new();
        static bool             s_ModifyMode = false;
        static float            s_LastHeight = 1.0f;
        static float            s_NextHeight = 1.0f;
        static float            s_DefaultHeight = 0;
        static Bounds           s_LastBounds;
        
        // TODO: somehow get rid of this
        public static void Reset()
        {
            s_Points.Clear();
            s_Transformation = Matrix4x4.identity;
            s_ModelBeneathCursor = null;
            s_ModifyMode = false;
            s_DefaultHeight = 0;
            s_NextHeight = 0;
            s_LastBounds = new Bounds();
            GUIUtility.hotControl = 0;
            PointDrawing.Reset();
        }

        static Bounds GetBounds(PlacementFlags flags, Axis upAxis)
        {
            if (s_Points.Count == 0) return new Bounds();
            
            var bounds = new Bounds( s_Points[0], Vector3.zero);
            if (s_Points.Count == 1) return bounds;

            if ((flags & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ)
            {
                var pt0 = s_Points[0];
                var pt1 = s_Points[1];
                var xDelta = pt1.x - pt0.x;
                var zDelta = pt1.z - pt0.z;
                var xAbsDelta = Mathf.Abs(xDelta);
                var zAbsDelta = Mathf.Abs(zDelta);
                if (xAbsDelta != zAbsDelta)
                {
                    if (xAbsDelta > zAbsDelta)
                    {
                        zDelta = xAbsDelta * Mathf.Sign(zDelta);
                        pt1.z = pt0.z + zDelta;
                    } else
                    {
                        xDelta = zAbsDelta * Mathf.Sign(xDelta);
                        pt1.x = pt0.x + xDelta;
                    }
                    s_Points[1] = pt1;
                }
            }
            if ((flags & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ)
            {
                var radius = s_Points[1] - s_Points[0];
                bounds.Encapsulate(s_Points[0] - radius);
                bounds.Encapsulate(s_Points[0] + radius);
            } else
                bounds.Encapsulate(s_Points[1]);

            var height = GetHeight(flags, upAxis);
            if (height == 0)
                return bounds;

            var size = bounds.size;
            size[(int)upAxis] = height;
            bounds.size = size;
            
            if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
            {
                var center = bounds.center;
                center[(int)upAxis] += height * 0.5f;
                bounds.center = center;
            }
            return bounds;
        }

        static float GetHeight(PlacementFlags flags, Axis upAxis)
        {
            if (s_Points.Count <= 1) 
                return 0;

            if ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ ||
                (flags & PlacementFlags.HeightEqualsXZ    ) == PlacementFlags.HeightEqualsXZ)
            {
                var heightMultiplier = ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ) ? 0.5f : 1.0f;

                var axis1 = (((int)upAxis) + 1) % 3;
                var axis2 = (((int)upAxis) + 2) % 3;

                if ((flags & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ)
                    heightMultiplier *= 2;

                var length1 = Mathf.Abs((s_Points[0] - s_Points[1])[(int)axis1]);
                var length2 = Mathf.Abs((s_Points[0] - s_Points[1])[(int)axis2]);

                s_DefaultHeight = s_NextHeight = Mathf.Min(length1, length2) * heightMultiplier;
                return s_DefaultHeight;
            }

            if (s_Points.Count <= 2)
            {
                if ((flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
                    s_DefaultHeight = s_NextHeight = s_LastHeight;
                return s_DefaultHeight;
            }

            if ((flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
            {
                s_DefaultHeight = s_NextHeight = s_LastHeight;
            } else
                s_DefaultHeight = s_NextHeight = (s_Points[2] - s_Points[1])[(int)upAxis];
            return s_DefaultHeight;
        }

        public static GeneratorModeState Do(Rect dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, PlacementFlags flags, Axis upAxis, float? snappingSteps = null)
        {
            // TODO: shift should do SameLengthXZ, shift control includes Y
            // TODO: fixed height should be possible to change sign

            // TODO: have some sort of click placement of previously used bounds (alt?)
            //bounds = s_LastBounds;

            bool doCommit = false;
            try
            {
                if (SceneHandles.InCameraOrbitMode)
                    return GeneratorModeState.None;

                height = GetHeight(flags, upAxis);
                if (s_Points.Count <= 2)
                {
                    PointDrawing.PointDrawHandle(dragArea, ref s_Points, out s_Transformation, out s_ModelBeneathCursor, releaseOnMouseUp: false, UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap);
                    if (s_Points.Count <= 1)
                        return GeneratorModeState.None;
                }
                if (s_Points.Count > 2)
                {
                    if (!s_ModifyMode)
                    {
                        PointDrawing.Release();
                        if ((flags & PlacementFlags.HeightEqualsHalfXZ) == PlacementFlags.HeightEqualsHalfXZ ||
                            (flags & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ ||
                            (flags & PlacementFlags.UseLastHeight) == PlacementFlags.UseLastHeight)
                        {
                            if (height > 0)
                            {
                                s_ModifyMode = false;
                                doCommit = true;
                                return GeneratorModeState.Commit;
                            }
                        }
                        
                        s_Points[2] = s_Points[1];
                        s_ModifyMode = true;
                        return GeneratorModeState.Update;
                    }
                    if (s_ModifyMode)
                    {
                        var oldMatrix = UnityEditor.Handles.matrix;
                        UnityEditor.Handles.matrix *= s_Transformation;
                        var tempPoint = s_Points[2];
                        var extrusionState = ExtrusionHandle.DoHandle(ref tempPoint, upAxis, snappingSteps: snappingSteps);
                        s_Points[2] = tempPoint;
                        UnityEditor.Handles.matrix = oldMatrix;

                        switch (extrusionState)
                        {
                            case ExtrusionState.Cancel:     { s_ModifyMode = false; return GeneratorModeState.Cancel; }
                            case ExtrusionState.Commit:     
                            { 
                                s_ModifyMode = false; 
                                doCommit = true;
                                return GeneratorModeState.Commit; 
                            }
                        }
                    }
                }
                return GeneratorModeState.Update;
            }
            finally
            {
                modelBeneathCursor  = s_ModelBeneathCursor;
                bounds			    = GetBounds(flags, upAxis);
                height			    = bounds.size[(int)upAxis];

                var center          = bounds.center;
                if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
                    center[(int)upAxis] -= height * 0.5f;

                transformation      = s_Transformation * Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);
#if true
                //if (height > 0)
                {
                    if ((flags & PlacementFlags.AlwaysFaceUp) == PlacementFlags.AlwaysFaceUp)
                    {
                        var currentUp       = transformation.MultiplyVector(Vector3.up);
                        var currentForward  = transformation.MultiplyVector(Vector3.forward);
                        var currentRight    = transformation.MultiplyVector(Vector3.right);

                        var desiredUp       = Grid.ActiveGrid.Up;
                    
                        var dotX = Vector3.Dot(currentRight,    desiredUp);
                        var dotY = Vector3.Dot(currentUp,       desiredUp);
                        var dotZ = Vector3.Dot(currentForward,  desiredUp);

                        var absDotX = Mathf.Abs(dotX);
                        var absDotY = Mathf.Abs(dotY);
                        var absDotZ = Mathf.Abs(dotZ);

                        if (absDotX > absDotZ)
                        {
                            if (absDotX > absDotY)
                            {
                                var size = bounds.size;
                                var t = size.x; size.x = size.y; size.y = t;
                                bounds.size = size;
                                upAxis = Axis.X;
                                var position = transformation.GetColumn(3);
                                transformation.SetColumn(3, new Vector4(0,0,0,1)); 
                                transformation *= Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                                transformation *= new Matrix4x4(new Vector4(0,1,0,0),
                                                                new Vector4(1,0,0,0),
                                                                new Vector4(0,0,1,0),
                                                                new Vector4(0,0,0,1));
                                transformation.SetColumn(3, position);
                            }
                        } else
                        {
                            if (absDotZ > absDotY)
                            {
                                var size = bounds.size;
                                var t = size.z; size.z = size.y; size.y = t;
                                bounds.size = size;
                                upAxis = Axis.Z;
                                var position = transformation.GetColumn(3);
                                transformation.SetColumn(3, new Vector4(0,0,0,1));
                                transformation *= Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                                transformation *= new Matrix4x4(new Vector4(1,0,0,0),
                                                                new Vector4(0,0,1,0),
                                                                new Vector4(0,1,0,0),
                                                                new Vector4(0,0,0,1));
                                transformation.SetColumn(3, position);
                            }
                        }

                    }

                    if (!s_ModelBeneathCursor &&
                        (flags & PlacementFlags.AlwaysFaceCameraXZ) == PlacementFlags.AlwaysFaceCameraXZ)
                    {
                        // TODO: take grid orientation into account to decide what is "X" and what is "Z"
                        var currentForward  = transformation.MultiplyVector(Vector3.forward);
                        var currentRight    = transformation.MultiplyVector(Vector3.right);
                        var cameraOffset    = Camera.current.transform.forward;
                        var cameraForward   = (new Vector3(cameraOffset.x, 0, cameraOffset.z)).normalized;
                        var dotZ = Vector3.Dot(currentForward, cameraForward);
                        var dotX = Vector3.Dot(currentRight,   cameraForward);

                        var angle = 0;
                        if (Mathf.Abs(dotX) < Mathf.Abs(dotZ))
                        {
                            if (dotZ > 0)
                                angle += 180;
                        } else
                        {
                            if (dotX < 0)
                                angle += 90;
                            else
                                angle -= 90;
                            
                            if (upAxis == Axis.X)
                                upAxis = Axis.Z;
                            var size = bounds.size;
                            var t = size.x; size.x = size.z; size.z = t;
                            bounds.size = size;
                        }
                    
                        var position = transformation.GetColumn(3);
                        transformation.SetColumn(3, new Vector4(0,0,0,1));
                        transformation *= Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                        transformation *= Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, angle, 0), Vector3.one);
                        transformation.SetColumn(3, position);
                    }
                }
#endif

                center = Vector3.zero;
                if ((flags & PlacementFlags.GenerateFromCenterY) != PlacementFlags.GenerateFromCenterY)
                    center[(int)upAxis] = height * 0.5f;
                else
                    center[(int)upAxis] = 0;
                bounds.center = center;
                if (doCommit)
                {
                    s_LastBounds = bounds;
                    s_LastHeight = s_NextHeight;
                }
            }
        }
    }
}
