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
		HoverMode,
		SquareMode,
		BoxMode,
		Commit,
		Cancel,
		Create,
		Modified
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

		static Bounds GetBounds(bool isSquare, bool generateFromCenter)
		{
			if (s_Points.Count == 0) return new Bounds();
			
			var bounds = new Bounds(s_Points[0], Vector3.zero);
			if (s_Points.Count == 1) return bounds;
			
			if (isSquare)
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
			if (generateFromCenter)
			{
				var radius = s_Points[1] - s_Points[0];
				bounds.Encapsulate(s_Points[0] - radius);
				bounds.Encapsulate(s_Points[0] + radius);
			} else
				bounds.Encapsulate(s_Points[1]);

			if (s_Points.Count == 2) return bounds;
			
			bounds.Encapsulate(s_Points[0] + (GetHeight(Axis.Y) * Vector3.up));
			return bounds;
		}

		static float GetHeight(Axis axis)
		{
			if (s_Points.Count <= 2) return 0;
			return (s_Points[2] - s_Points[1])[(int)axis];
		}

		public static BoxExtrusionState Do(Rect dragArea, out Bounds bounds, out float height, out CSGModel modelBeneathCursor, out Matrix4x4 transformation, bool isSymmetrical, bool generateFromCenter, Axis axis, float? snappingSteps = null)
		{
			try
			{
                if (Tools.viewTool != ViewTool.None &&
                    Tools.viewTool != ViewTool.Pan)
                    return BoxExtrusionState.HoverMode; 

				if (s_Points.Count <= 2)
				{
					PointDrawing.PointDrawHandle(dragArea, ref s_Points, out s_Transformation, out s_ModelBeneathCursor, UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap);

                    if (s_Points.Count <= 1)
                        return BoxExtrusionState.HoverMode;
                    
					if (s_Points.Count > 2){ s_Points[2] = s_Points[0]; return BoxExtrusionState.Create; }
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
				modelBeneathCursor	= s_ModelBeneathCursor;
				bounds				= GetBounds(isSymmetrical, generateFromCenter);
				transformation		= s_Transformation;
				height				= GetHeight(axis);
			}
		}
	}
}
