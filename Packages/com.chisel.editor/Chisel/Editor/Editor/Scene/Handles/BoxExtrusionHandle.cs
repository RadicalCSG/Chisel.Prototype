using Chisel.Assets;
using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public enum BoxExtrusionState
    {
        None,
        HoverMode,
        SquareMode,
        BoxMode,
        Commit,
        Cancel,
        Create,
        Modified
    }

    [Flags]
    public enum BoxExtrusionFlags
    {
        None                    = 0,
        IsSymmetricalXZ         = 1,
        GenerateFromCenterXZ    = 2,
        GenerateFromCenterY     = 4,
        AlwaysFaceUp            = 8
    }


    public static class BoxExtrusionHandle
    {
        static Matrix4x4		s_Transformation = Matrix4x4.identity;
        static CSGModel			s_ModelBeneathCursor;
        static List<Vector3>	s_Points = new List<Vector3>();
        
        // TODO: somehow get rid of this
        public static void Reset()
        {
            s_Points.Clear();
            PointDrawing.Reset();
        }

        static Bounds GetBounds(BoxExtrusionFlags flags, Axis axis)
        {
            if (s_Points.Count == 0) return new Bounds();
            
            var bounds = new Bounds(s_Points[0], Vector3.zero);
            if (s_Points.Count == 1) return bounds;

            if ((flags & BoxExtrusionFlags.IsSymmetricalXZ) == BoxExtrusionFlags.IsSymmetricalXZ)
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
            if ((flags & BoxExtrusionFlags.GenerateFromCenterXZ) == BoxExtrusionFlags.GenerateFromCenterXZ)
            {
                var radius = s_Points[1] - s_Points[0];
                bounds.Encapsulate(s_Points[0] - radius);
                bounds.Encapsulate(s_Points[0] + radius);
            } else
                bounds.Encapsulate(s_Points[1]);

            if (s_Points.Count == 2) return bounds;

            var height = GetHeight(axis);

            var size = bounds.size;
            size[(int)axis] = height;
            bounds.size = size;
            
            if ((flags & BoxExtrusionFlags.GenerateFromCenterY) != BoxExtrusionFlags.GenerateFromCenterY)
            {
                var center = bounds.center;
                center[(int)axis] += height * 0.5f;
                bounds.center = center;
            }
            return bounds;
        }
        static bool Inverted(Axis axis)
        {
            if (s_Points.Count <= 2) return false;
            return (s_Points[2] - s_Points[1])[(int)axis] < 0;
        }

        static float GetHeight(Axis axis)
        {
            if (s_Points.Count <= 2) return 0;
            return (s_Points[2] - s_Points[1])[(int)axis];
        }

        public static BoxExtrusionState Do(Rect dragArea, out Bounds bounds, out float height, out CSGModel modelBeneathCursor, out Matrix4x4 transformation, BoxExtrusionFlags flags, Axis axis, float? snappingSteps = null)
        {
            try
            {
                if (Tools.viewTool != ViewTool.None &&
                    Tools.viewTool != ViewTool.Pan)
                    return BoxExtrusionState.None;

                if (s_Points.Count <= 2)
                {
                    PointDrawing.PointDrawHandle(dragArea, ref s_Points, out s_Transformation, out s_ModelBeneathCursor, UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap);

                    if (s_Points.Count <= 1)
                    {
                        return BoxExtrusionState.HoverMode;
                    }

                    if (s_Points.Count > 2)
                    {
                        s_Points[2] = s_Points[0];
                        return BoxExtrusionState.Create;
                    }

                    return BoxExtrusionState.SquareMode;
                } else
                {
                    var tempPoint = s_Points[2];
                    var oldMatrix = UnityEditor.Handles.matrix;
                    UnityEditor.Handles.matrix = UnityEditor.Handles.matrix * s_Transformation;
                    var extrusionState = ExtrusionHandle.DoHandle(dragArea, ref tempPoint, axis, snappingSteps: snappingSteps);
                    UnityEditor.Handles.matrix = oldMatrix;
                    s_Points[2] = tempPoint;
                
                    switch (extrusionState)
                    {
                        case ExtrusionState.Cancel:		{ return BoxExtrusionState.Cancel; }
                        case ExtrusionState.Commit:		{ return BoxExtrusionState.Commit; }
                        case ExtrusionState.Modified:	{ return BoxExtrusionState.Modified; }
                    }				
                    return BoxExtrusionState.BoxMode;
                }
            }
            finally
            {
                modelBeneathCursor  = s_ModelBeneathCursor;
                bounds			    = GetBounds(flags, axis);
                height			    = GetHeight(axis);

                var center          = bounds.center;
                if ((flags & BoxExtrusionFlags.GenerateFromCenterY) != BoxExtrusionFlags.GenerateFromCenterY)
                    center[(int)axis] -= height * 0.5f;

                transformation      = s_Transformation * Matrix4x4.TRS(center, Quaternion.identity, Vector3.one);

                center = Vector3.zero;
                if ((flags & BoxExtrusionFlags.GenerateFromCenterY) != BoxExtrusionFlags.GenerateFromCenterY)
                    center[(int)axis] = height * 0.5f;
                bounds.center = center;
            }
        }
    }
}
