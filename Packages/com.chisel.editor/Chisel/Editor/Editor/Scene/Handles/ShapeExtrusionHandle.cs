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
	public enum ShapeExtrusionState
	{
		HoverMode,
		ShapeMode,
		ExtrusionMode,
		Commit,
		Cancel,
		Create,
		Modified
	}

	public static class ShapeExtrusionHandle
	{
		static Matrix4x4		s_Transformation = Matrix4x4.identity;
		static CSGModel			s_ModelBeneathCursor;
		static List<Vector3>	s_Points = new List<Vector3>();
		static Curve2D          s_Curve2D = null;
		static bool             s_ExtrusionMode = false;
		
		// TODO: somehow get rid of this
		public static void Reset()
		{
			s_ExtrusionMode = false;
			s_Curve2D = null;
			s_Points.Clear();
			PointDrawing.Reset();
		}

		static Curve2D GetShape()
		{
			if (s_Points.Count < 2)
			{
				s_Curve2D = null;
				return null;
			}

			if (s_Curve2D == null)
			{
				s_Curve2D = new Curve2D();
				s_Curve2D.closed = true;
			}

			var pointCount = s_ExtrusionMode ? s_Points.Count - 1 : s_Points.Count;

			if ((s_Points[pointCount - 1] - s_Points[0]).sqrMagnitude < kDistanceEpsilon)
				pointCount--;

			if (s_Curve2D.controlPoints.Length != pointCount)
				s_Curve2D.controlPoints = new CurveControlPoint2D[pointCount];
			
			for (int i = 0; i < pointCount; i++)
				s_Curve2D.controlPoints[i].position = new Vector2(s_Points[i].x, s_Points[i].z);

			return s_Curve2D;
		}

		static float GetHeight(Axis axis)
		{
			if (!s_ExtrusionMode) return 0;
			return (s_Points[s_Points.Count - 1] - s_Points[1])[(int)axis];
		}

		const float kDistanceEpsilon = 0.0001f;

		public static ShapeExtrusionState Do(Rect dragArea, out Curve2D shape, out float height, out CSGModel modelBeneathCursor, out Matrix4x4 transformation, Axis axis)
		{
			try
			{ 
				if (!s_ExtrusionMode)
				{
					// TODO: handle snapping against own points
					// TODO: handle ability to 'commit'
					PointDrawing.PointDrawHandle(dragArea, ref s_Points, out s_Transformation, out s_ModelBeneathCursor, UnitySceneExtensions.SceneHandles.OutlinedDotHandleCap);

					if (s_Points.Count <= 1)
						return ShapeExtrusionState.HoverMode;

					if (s_Points.Count <= 3)
						return ShapeExtrusionState.ShapeMode;

					if ((s_Points[s_Points.Count - 2] - s_Points[0]).sqrMagnitude < kDistanceEpsilon)
					{
						s_ExtrusionMode = true;
						s_Points[s_Points.Count - 1] = s_Points[0];
						return ShapeExtrusionState.Create;
					}
					return ShapeExtrusionState.ShapeMode;
				} else
				{
					var tempPoint = s_Points[s_Points.Count-1];
					var oldMatrix = UnityEditor.Handles.matrix;
					UnityEditor.Handles.matrix = UnityEditor.Handles.matrix * s_Transformation;
					var extrusionState = ExtrusionHandle.DoHandle(dragArea, ref tempPoint, axis);
					UnityEditor.Handles.matrix = oldMatrix;
					s_Points[s_Points.Count - 1] = tempPoint;
				
					switch (extrusionState)
					{
						case ExtrusionState.Cancel:		{ s_ExtrusionMode = false; return ShapeExtrusionState.Cancel; }
						case ExtrusionState.Commit:		{ s_ExtrusionMode = false; return ShapeExtrusionState.Commit; }
						case ExtrusionState.Modified:	{ return ShapeExtrusionState.Modified; }
					}				
					return ShapeExtrusionState.ExtrusionMode;
				}
			}
			finally
			{
				modelBeneathCursor = s_ModelBeneathCursor;
				transformation = s_Transformation;
				shape = GetShape();
				height = GetHeight(axis);
			}
		}
	}
}
