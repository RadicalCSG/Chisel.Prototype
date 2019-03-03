using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	// TODO: make sure this works well with 
	//	- a non-identity Handles.Matrix set
	//	- a non 90 degrees rotated grid
	//	- it works correctly with the right axis enabled/disabled etc. etc.
	//	- it works correctly when handles.disabled = true;

	//	- clicking on points should cycle between tangent modes
	//	- it should be possible to modify tangents
	//	- we should render curves between points when in the correct mode

	//	- we can hover over edges 
	//	- we can move edges
	//	- we can add points by holding shift (seeing the point) and clicking

	//	- we should have the ability to select multiple points
	public sealed partial class SceneHandles
	{
		const int kCurveEdges = 16;
		const float kMinimumEdgeDistance = 6.0f;
		internal static int s_Curve2DDHash = "Curve2DHash".GetHashCode();

		public static Curve2D Curve2DHandle(Quaternion rotation, Vector3 position, Vector3 scale, Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(rotation, position, scale, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(Quaternion rotation, Vector3 position, Vector3 scale, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
		{
			var id = GUIUtility.GetControlID(s_Curve2DDHash, FocusType.Keyboard);
			return Curve2DHandle(id, rotation, position, scale, curve, ref curveSelection, defaultControlID);
		}
		
		public static Curve2D Curve2DHandle(int id, Quaternion rotation, Vector3 position, Vector3 scale, Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(id, rotation, position, scale, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(int id, Matrix4x4 orientation, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
		{
			var originalMatrix = SceneHandles.matrix;
			SceneHandles.matrix = originalMatrix * orientation;
			var result = Curve2DHandleLogic.Do(id, curve, ref curveSelection, defaultControlID);
			SceneHandles.matrix = originalMatrix;
			return result;
		}

		public static Curve2D Curve2DHandle(int id, Quaternion rotation, Vector3 position, Vector3 scale, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
		{
			var orientation = Matrix4x4.TRS(position, rotation, scale);
			return Curve2DHandle(id, orientation, curve, ref curveSelection, defaultControlID);
		}

		public static Curve2D Curve2DHandle(int id, Matrix4x4 transformation, Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(id, transformation, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(Matrix4x4 transformation, Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(transformation, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(Matrix4x4 transformation, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
		{
			var id = GUIUtility.GetControlID(s_Curve2DDHash, FocusType.Keyboard);
			return Curve2DHandle(id, transformation, curve, ref curveSelection, defaultControlID);
		}
		
		public static Curve2D Curve2DHandle(Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(Matrix4x4.identity, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
		{
			var id = GUIUtility.GetControlID(s_Curve2DDHash, FocusType.Keyboard);
			return Curve2DHandle(id, Matrix4x4.identity, curve, ref curveSelection, defaultControlID);
		}

		public static Curve2D Curve2DHandle(int id, Curve2D curve, int defaultControlID = 0) { Curve2DSelection curveSelection = null; return Curve2DHandle(id, Matrix4x4.identity, curve, ref curveSelection, defaultControlID); }
		public static Curve2D Curve2DHandle(int id, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0) { return Curve2DHandle(id, Matrix4x4.identity, curve, ref curveSelection, defaultControlID); }


		internal class Curve2DHandleUtility
		{
			public const float kCurvePointSize      = 0.05f;
			public const float kCurveTangentSize    = kCurvePointSize * 0.75f;
			public const float kCurveLayoutSize     = 0.25f;

			public static Vector3 PointOnBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
			{
				return (1 - t) * (1 - t) * (1 - t) * p0 + 3 * t * (1 - t) * (1 - t) * p1 + 3 * t * t * (1 - t) * p2 + t * t * t * p3;
			}

			public static void CurvedEdges(Curve2D curve, int index, int curveSides, ref Vector3[] points, out int length)
			{
				var controlPoints   = curve.controlPoints;
				var index1          = index;
				var index2          = (index + 1) % controlPoints.Length;
				var p1              = controlPoints[index1].position;
				var p2              = controlPoints[index2].position;

				if (curveSides <= 0 ||
					(controlPoints[index1].GetConstraint(1) == ControlPointConstraint.Straight &&
					 controlPoints[index2].GetConstraint(0) == ControlPointConstraint.Straight))
				{
					if (points == null || points.Length < 2)
						Array.Resize(ref points, 2);
					points[0] = p1;
					points[1] = p2;
					length = 2;
					return;
				}

				Vector2 p0 = p1;
				if (controlPoints[index1].GetConstraint(1) != ControlPointConstraint.Straight)
					p0 -= controlPoints[index1].GetTangent(1);

				Vector2 p3 = p2;
				if (controlPoints[index2].GetConstraint(0) != ControlPointConstraint.Straight)
					p3 -= controlPoints[index2].GetTangent(0);

				if (points == null || points.Length < (curveSides + 1))
					Array.Resize(ref points, curveSides + 1);
				points[0] = p1;
				for (int n = 1; n < curveSides; n++)
					points[n] = PointOnBezier(p1, p0, p3, p2, n / (float)curveSides);
				points[curveSides] = p2;
				length = curveSides + 1;
			}

			public static void SetPointConstraint(Curve2D curve, int pointIndex, ControlPointConstraint state)
			{
				SetPointConstraintSide(curve, pointIndex, 0, state);
				SetPointConstraintSide(curve, pointIndex, 1, state);
			}

			public static void SetPointConstraintSide(Curve2D curve, int pointIndex, int side, ControlPointConstraint state)
			{
				if (curve.controlPoints[pointIndex].GetConstraint(side) == state)
					return;

				curve.controlPoints[pointIndex].SetConstraint(side, state);
				if (state != ControlPointConstraint.Straight &&
					curve.controlPoints[pointIndex].GetConstraint(1 - side) != ControlPointConstraint.Straight)
				{
					if (state == ControlPointConstraint.Broken &&
						curve.controlPoints[pointIndex].GetConstraint(1 - side) == ControlPointConstraint.Mirrored)
						curve.controlPoints[pointIndex].SetConstraint(1 - side, ControlPointConstraint.Broken);

					curve.controlPoints[pointIndex].SetTangent(side, -curve.controlPoints[pointIndex].GetTangent(1 - side));
					return;
				}

				switch (state)
				{
					case ControlPointConstraint.Broken:
					case ControlPointConstraint.Mirrored:
					{
						var count       = curve.controlPoints.Length;
						var prev        = (pointIndex + count - 1) % count;
						var curr        = pointIndex;
						var next        = (pointIndex + count + 1) % count;

						Vector3 tangentA;

						if (curve.closed || (pointIndex > 0 && pointIndex < curve.controlPoints.Length - 1))
						{
							var vertex0 = curve.controlPoints[prev].position;
							var vertex1 = curve.controlPoints[curr].position;
							var vertex2 = curve.controlPoints[next].position;
							var centerA = (vertex0 + vertex1 + vertex2) / 3;
							var deltaA = (vertex1 - centerA);
							var normal = Vector3.Cross(vertex1 - vertex2, vertex1 - vertex0).normalized;
							tangentA = (Vector2)Vector3.Cross(normal, deltaA);
						} else
						if (pointIndex == 0)
						{
							var vertex1 = curve.controlPoints[curr].position;
							var vertex2 = curve.controlPoints[next].position;
							tangentA = (vertex2 - vertex1).normalized;
						} else
						//if (pointIndex < curve.controlPoints.Length - 1)
						{
							var vertex0 = curve.controlPoints[prev].position;
							var vertex1 = curve.controlPoints[curr].position;
							tangentA = (vertex1 - vertex0).normalized;
						}

						//TODO: optimize away
						if (side == 1)
							tangentA = -tangentA;

						curve.controlPoints[pointIndex].SetTangent(side, tangentA);
						break;
					}
				}
			}

			public static Curve2D ToggleContraint(Curve2D curve, int index)
			{
				int length = curve.controlPoints.Length;
				if (curve.closed)
					if (length < 3) return curve;
				else
					if (length < 2) return curve;

				// need to create a new curve, otherwise we'd be modifying the original and 
				// this will make it hard to interact with Undo
				var newCurve            = new Curve2D() { closed = curve.closed };
				var newControlPoints    = new CurveControlPoint2D[curve.controlPoints.Length];
				curve.controlPoints.CopyTo(newControlPoints, 0);
				newCurve.controlPoints = newControlPoints;
				
				if (newCurve.closed || (index > 0 && index < newCurve.controlPoints.Length - 1))
				{ 
					switch (newCurve.controlPoints[index].GetConstraint(0))
					{
						case ControlPointConstraint.Straight: SetPointConstraint(newCurve, index, ControlPointConstraint.Mirrored); break;
						case ControlPointConstraint.Mirrored: SetPointConstraint(newCurve, index, ControlPointConstraint.Broken); break;
						case ControlPointConstraint.Broken:   SetPointConstraint(newCurve, index, ControlPointConstraint.Straight); break;
					}
				} else
				{
					switch (newCurve.controlPoints[index].GetConstraint(0))
					{
						case ControlPointConstraint.Straight: SetPointConstraint(newCurve, index, ControlPointConstraint.Broken); break;
						case ControlPointConstraint.Mirrored: 
						case ControlPointConstraint.Broken:   SetPointConstraint(newCurve, index, ControlPointConstraint.Straight); break;
					}
				}

				return newCurve;
			}

			public static Curve2D MoveCurveTangent(Curve2D curve, CurveControlPoint2D[] localControlPoints, int tangentIndex, Vector2 localDelta)
			{
				var pointIndex  = tangentIndex / 2;
				tangentIndex    = tangentIndex & 1;
				
				// need to create a new curve, otherwise we'd be modifying the original and 
				// this will make it hard to interact with Undo
				var newCurve			= new Curve2D() { closed = curve.closed };
				var newLocalControlPoints	= new CurveControlPoint2D[localControlPoints.Length];
				localControlPoints.CopyTo(newLocalControlPoints, 0);

				newLocalControlPoints[pointIndex].SetTangentPosition(tangentIndex, newLocalControlPoints[pointIndex].GetTangentPosition(tangentIndex) + localDelta);

				newCurve.controlPoints = newLocalControlPoints;
				curve = newCurve;
				return curve;
			}

			public static Curve2D MoveCurvePoint(Curve2D curve, Curve2DSelection curveSelection, CurveControlPoint2D[] localControlPoints, int pointIndex, Vector2 localDelta)
			{
				// need to create a new curve, otherwise we'd be modifying the original and 
				// this will make it hard to interact with Undo
				var newCurve = new Curve2D() { closed = curve.closed };
				var newLocalControlPoints = new CurveControlPoint2D[localControlPoints.Length];
				localControlPoints.CopyTo(newLocalControlPoints, 0);

				if (curveSelection != null)
				{
					for (int i = 0; i < curveSelection.selectedPoints.Length; i++)
					{
						if (!curveSelection.selectedPoints[i])
							continue;
						newLocalControlPoints[i].position += localDelta;
					}
				} else
					newLocalControlPoints[pointIndex].position += localDelta;

				newCurve.controlPoints = newLocalControlPoints;
				curve = newCurve;
				return curve;
			}

			public static Curve2D MoveCurveEdge(Curve2D curve, Curve2DSelection curveSelection, CurveControlPoint2D[] localControlPoints, int edgeIndex, Vector2 localDelta)
			{
				// need to create a new curve, otherwise we'd be modifying the original and 
				// this will make it hard to interact with Undo
				var newCurve				= new Curve2D() { closed = curve.closed };
				var newLocalControlPoints	= new CurveControlPoint2D[localControlPoints.Length];
				localControlPoints.CopyTo(newLocalControlPoints, 0);

				if (curveSelection != null)
				{
					for (int i = 0; i < curveSelection.selectedPoints.Length; i++)
					{
						if (!curveSelection.selectedPoints[i])
							continue;
						newLocalControlPoints[i].position += localDelta;
					}
				} else
				{
					newLocalControlPoints[edgeIndex].position += localDelta;
					newLocalControlPoints[(edgeIndex + 1) % localControlPoints.Length].position += localDelta;
				}

				newCurve.controlPoints = newLocalControlPoints;
				curve = newCurve;
				return curve;
			}

			internal static void PaintConstraintHandles(Curve2D curve, bool haveFocus, int hoverOverPointID, int hoverOverTangentID)
			{
				var color = SceneHandles.color;
				var controlPoints = curve.controlPoints;
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var prevConstraint  = controlPoints[i].constraint2;
					var nextConstraint  = controlPoints[i].constraint1;
					var point           = controlPoints[i].position;
					var prevTangent     = controlPoints[i].tangent2;
					var nextTangent     = controlPoints[i].tangent1;
					var showPointFocus  = haveFocus && (hoverOverPointID == i && hoverOverTangentID == -1);

					if (prevConstraint != ControlPointConstraint.Straight)
					{
						if (curve.closed || (i < controlPoints.Length - 1))
						{
							var tangent1			= point - prevTangent;
							var tangentSize1		= UnityEditor.HandleUtility.GetHandleSize(tangent1) * kCurveTangentSize;
							var showTangentFocus	= haveFocus && (hoverOverTangentID == ((i * 2) + 1));
							SceneHandles.color = SceneHandles.StateColor(color, false, false, (showPointFocus || showTangentFocus));
							SceneHandles.DrawDottedLine(point, tangent1, 4.0f);

							SceneHandles.color = SceneHandles.StateColor(color, false, showTangentFocus, false);
							if (prevConstraint == ControlPointConstraint.Broken) SceneHandles.RenderBordererdTriangle(tangent1, tangentSize1);
							else SceneHandles.RenderBordererdDiamond(tangent1, tangentSize1);
						}
					}

					if (nextConstraint != ControlPointConstraint.Straight) 
					{
						if (curve.closed || (i > 0))
						{ 
							var tangent2			= point - nextTangent;
							var tangentSize2		= UnityEditor.HandleUtility.GetHandleSize(tangent2) * kCurveTangentSize;
							var showTangentFocus	= haveFocus && (hoverOverTangentID == ((i * 2) + 0));
							SceneHandles.color = SceneHandles.StateColor(color, false, (showPointFocus || showTangentFocus));
							SceneHandles.DrawDottedLine(point, tangent2, 4.0f);
							
							SceneHandles.color = SceneHandles.StateColor(color, false, showTangentFocus, false);
							if (nextConstraint == ControlPointConstraint.Broken) SceneHandles.RenderBordererdTriangle(tangent2, tangentSize2);
							else SceneHandles.RenderBordererdDiamond(tangent2, tangentSize2);
						}
					}
				}
			}

			internal static float ClosestConstraintHandleDistance(Curve2D curve, float closestDistance)
			{
				var controlPoints = curve.controlPoints;
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var prevConstraint  = controlPoints[i].constraint2;
					var nextConstraint  = controlPoints[i].constraint1;
					var point           = controlPoints[i].position;
					var prevTangent     = controlPoints[i].tangent2;
					var nextTangent     = controlPoints[i].tangent1;

					if (prevConstraint != ControlPointConstraint.Straight)
					{
						if (curve.closed || i < controlPoints.Length - 1)
						{
							var tangent1 = point - prevTangent;
							var tangentSize1 = UnityEditor.HandleUtility.GetHandleSize(tangent1) * (kCurveTangentSize * kCurveLayoutSize);
							closestDistance = Mathf.Min(closestDistance, UnityEditor.HandleUtility.DistanceToCircle(tangent1, tangentSize1));
						}
					}

					if (nextConstraint != ControlPointConstraint.Straight)
					{
						if (curve.closed || i > 0)
						{
							var tangent2		= point - nextTangent;
							var tangentSize2	= UnityEditor.HandleUtility.GetHandleSize(tangent2) * (kCurveTangentSize * kCurveLayoutSize);
							closestDistance = Mathf.Min(closestDistance, UnityEditor.HandleUtility.DistanceToCircle(tangent2, tangentSize2));
						}
					}
				}

				return closestDistance;
			}
			
			internal static float ClosestCurveDistance(Curve2D curve, float closestDistance)
			{
				var controlPoints	= curve.controlPoints;
				var points			= new Vector3[kCurveEdges];
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var length = 0;
					Curve2DHandleUtility.CurvedEdges(curve, i, kCurveEdges, ref points, out length);
					for (int j = 0; j < length - 1; j++)
					{
						var point1 = points[j    ];
						var point2 = points[j + 1];

						closestDistance = Mathf.Min(closestDistance, UnityEditor.HandleUtility.DistanceToLine(point1, point2));
					}
				}

				return closestDistance;
			}


			internal static void PaintCurveHandles(Curve2D curve, Curve2DSelection curveSelection, bool haveFocus, int hoverOverPointID, int hoverOverTangentID)
			{
				var color = SceneHandles.color;
				var controlPoints = curve.controlPoints;
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var prevConstraint	= controlPoints[i].constraint2;
					var point           = controlPoints[i].position;

					var isSelected		= (curveSelection == null || curveSelection.selectedPoints == null || curveSelection.selectedPoints.Length <= i) ? false : curveSelection.selectedPoints[i];
					var showPointFocus	= haveFocus && (hoverOverPointID == i && hoverOverTangentID == -1);

					color = SceneHandles.StateColor(color, false, isSelected, showPointFocus);

					//var dotConstraint	= (prevConstraint == nextConstraint) ? prevConstraint : ControlPointConstraint.Broken;
					var size = UnityEditor.HandleUtility.GetHandleSize(point) * kCurvePointSize;
					switch (prevConstraint)
					{
						case ControlPointConstraint.Straight:	SceneHandles.RenderBorderedDot(point, size); break;
						case ControlPointConstraint.Broken:		SceneHandles.RenderBorderedCircle(point, size); break;
						case ControlPointConstraint.Mirrored:	SceneHandles.RenderBordererdDiamond(point, size); break;
					}
				}
			}

			internal static float ClosestCurveHandleDistance(Curve2D curve, float closestDistance)
			{
				var controlPoints = curve.controlPoints;
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var point = controlPoints[i].position;
					var size = UnityEditor.HandleUtility.GetHandleSize(point) * (kCurvePointSize * kCurveLayoutSize);
					
					closestDistance = Mathf.Min(closestDistance, UnityEditor.HandleUtility.DistanceToCircle(point, size));
				}
				return closestDistance;
			}
			
			internal static void PaintCurveOutline(Curve2D curve, bool haveFocus, int hoverOverPointID, int hoverOverTangentID, int hoverOverEdgeID)
			{
				var color = SceneHandles.color;
				var controlPoints = curve.controlPoints;

				var outlineColor = Color.black;
				outlineColor.a = color.a;
				
				var points       = new Vector3[kCurveEdges];
				var pointIndex   = (hoverOverPointID   != -1) ? hoverOverPointID : (hoverOverTangentID / 2);
				var tangentIndex = (hoverOverTangentID != -1) ? (hoverOverTangentID & 1) : -1;

				for (int j = controlPoints.Length - 2, i = controlPoints.Length - 1, k = 0; k < controlPoints.Length; j = i, i = k, k++)
				{
					if (!curve.closed)
					{
						if (i == 0)
							continue;
					}
					var length = 0;
					Curve2DHandleUtility.CurvedEdges(curve, j, kCurveEdges, ref points, out length);

					bool renderHighlighted = (j == hoverOverEdgeID);
					if (haveFocus)
					{
						if (hoverOverPointID != -1)
						{
							renderHighlighted = (hoverOverPointID == j || hoverOverPointID == i);
						} else
						if (hoverOverTangentID != -1)
						{
							if (curve.controlPoints[pointIndex].GetConstraint(tangentIndex) == ControlPointConstraint.Mirrored)
							{
								renderHighlighted = (pointIndex == j || pointIndex == i);
							} else
							{
								var leftTangentIndex = (i * 2) + 0;
								var rightTangentIndex = (j * 2) + 1;
								renderHighlighted = (hoverOverTangentID == leftTangentIndex) ||
													(hoverOverTangentID == rightTangentIndex);
							}
						} 
					}

					if (renderHighlighted)
					{
						SceneHandles.color = SceneHandles.StateColor(outlineColor);
						SceneHandles.DrawAAPolyLine(5.0f, length, points);
						SceneHandles.color = SceneHandles.StateColor(color, isSelected: true);
						SceneHandles.DrawAAPolyLine(4.0f, length, points);
						continue;
					}

					SceneHandles.color = SceneHandles.StateColor(outlineColor);
					SceneHandles.DrawAAPolyLine(3.0f, length, points);
					SceneHandles.color = SceneHandles.StateColor(color, isSelected: false);
					SceneHandles.DrawAAPolyLine(2.0f, length, points);
				}
			}

			public static void PaintCurve(Curve2D curve, int id, Curve2DSelection curveSelection, int hoverOverPointID, int hoverOverTangentID, int hoverOverEdgeID)
			{
				var originalDisabled    = SceneHandles.disabled;
				var originalColor       = SceneHandles.color;

				var isStatic            = (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
				SceneHandles.disabled = isStatic || originalDisabled || (Snapping.AxisLocking[0] && Snapping.AxisLocking[1]);
				
				var hotControl			= GUIUtility.hotControl;
				var haveFocus           = ((hotControl == 0) && (id == UnityEditor.HandleUtility.nearestControl)) || (id == hotControl);

				PaintCurveOutline(curve, haveFocus, hoverOverPointID, hoverOverTangentID, hoverOverEdgeID);
				if (!SceneHandles.disabled)
				{
					PaintConstraintHandles(curve, haveFocus, hoverOverPointID, hoverOverTangentID);
					PaintCurveHandles(curve, curveSelection, haveFocus, hoverOverPointID, hoverOverTangentID);
				}

				SceneHandles.disabled = originalDisabled;

				SceneHandles.color = originalColor;
			}

			public static void LayoutCurve(Curve2D curve, int id, out int closestPoint, out int closestTangent, out int closestEdge, out Vector3 pointOnEdge)
			{
				var closestDistance = float.PositiveInfinity;
				var controlPoints   = curve.controlPoints;
				var points			= new Vector3[kCurveEdges];
				closestPoint    = -1;
				closestTangent  = -1;
				closestEdge		= -1;
				pointOnEdge		= Vector3.zero;
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var prevConstraint	= controlPoints[i].constraint2;
					var nextConstraint	= controlPoints[i].constraint1;
					var point			= controlPoints[i].position;
					var prevTangent		= controlPoints[i].tangent1;
					var nextTangent		= controlPoints[i].tangent2;

					var size = UnityEditor.HandleUtility.GetHandleSize(point) * (kCurvePointSize * kCurveLayoutSize);
					var distance = UnityEditor.HandleUtility.DistanceToCircle(point, size);
					if (closestDistance > distance)
					{
						closestDistance = distance;
						closestEdge = -1;
						closestPoint = i;
						closestTangent = -1;
					}

					if (prevConstraint != ControlPointConstraint.Straight)
					{
						var tangent1 = point - prevTangent;
						var tangentSize1 = UnityEditor.HandleUtility.GetHandleSize(tangent1) * (kCurveTangentSize * kCurveLayoutSize);
						distance = UnityEditor.HandleUtility.DistanceToCircle(tangent1, tangentSize1);
						if (closestDistance > distance)
						{
							closestDistance = distance;
							closestEdge = -1;
							closestPoint = -1;
							closestTangent = (i * 2) + 0;
						}
					}

					if (nextConstraint != ControlPointConstraint.Straight)
					{
						var tangent2 = point - nextTangent;
						var tangentSize2 = UnityEditor.HandleUtility.GetHandleSize(tangent2) * (kCurveTangentSize * kCurveLayoutSize);
						distance = UnityEditor.HandleUtility.DistanceToCircle(tangent2, tangentSize2);
						if (closestDistance > distance)
						{
							closestDistance = distance;
							closestEdge = -1;
							closestPoint = -1;
							closestTangent = (i * 2) + 1;
						}
					}
				}
				if (closestDistance > kMinimumEdgeDistance)
				{ 
					var lastPoint = curve.closed ? controlPoints.Length : controlPoints.Length - 1;
					for (int i = 0; i < lastPoint; i++)
					{
						var length = 0;
						Curve2DHandleUtility.CurvedEdges(curve, i, kCurveEdges, ref points, out length);
						for (int j = 0; j < length - 1; j++) 
						{
							var point1 = points[j    ];
							var point2 = points[j + 1];
							var distance = UnityEditor.HandleUtility.DistanceToLine(point1, point2);
							if (closestDistance > distance && distance < kMinimumEdgeDistance)
							{
								pointOnEdge = UnityEditor.HandleUtility.ClosestPointToPolyLine(point1, point2);

								closestDistance = distance;
								closestEdge		= i;
								closestPoint	= -1;
								closestTangent  = -1;
							}
						}
					}
				}

				if (!float.IsInfinity(closestDistance))
					UnityEditor.HandleUtility.AddControl(id, closestDistance);
			}
		}

		public class Curve2DHandleLogic
		{
			class State
			{
				public Snapping2D   s_Snapping2D = new Snapping2D();

				public bool         s_PointHasMoved         = false;
				public Vector2      s_CurrentMousePosition;
				public Vector3      s_LocalStartPosition;

				public int          s_HoverOverPoint        = -1;
				public int          s_HoverOverTangent      = -1;
				public int          s_HoverOverEdge         = -1;
				public Vector3      s_HoverEdgePoint        = Vector3.zero;

				public int          s_ClosestPoint          = -1;
				public int          s_ClosestTangent        = -1;
				public int          s_ClosestEdge           = -1;
				public Vector3		s_ClosestEdgePoint      = Vector3.zero;
				//public Plane		s_CurvePlane;

				public CurveControlPoint2D[]	s_ControlPoints;
				public Curve2DSelection			s_NewCurveSelection;
			}
			
			
			
			static bool InCreateVertexMode()
			{
				return Event.current.shift;
			}

			static void ClickSelect(State state, SelectionType selectionType, bool forceClear = false)
			{
				if (state.s_NewCurveSelection != null)
				{
					if (selectionType == SelectionType.Normal && 
						(forceClear ||
							(
								(state.s_HoverOverPoint == -1 || !state.s_NewCurveSelection.selectedPoints[state.s_HoverOverPoint]) &&
								(state.s_HoverOverEdge  == -1 || !(state.s_NewCurveSelection.selectedPoints[state.s_HoverOverEdge] && state.s_NewCurveSelection.selectedPoints[(state.s_HoverOverEdge + 1) % state.s_ControlPoints.Length]))
							)
						))
					{
						Array.Clear(state.s_NewCurveSelection.selectedPoints, 0, state.s_NewCurveSelection.selectedPoints.Length);
					}
				}
							
				if (state.s_HoverOverTangent != -1)
				{
					var pointIndex = state.s_HoverOverTangent / 2;
					var tangentIndex = state.s_HoverOverTangent & 1;

					if (state.s_NewCurveSelection != null)
					{
						state.s_NewCurveSelection.selectedPoints[pointIndex] = (selectionType == SelectionType.Subtractive) ? false : true;
					}
					state.s_LocalStartPosition = state.s_ControlPoints[pointIndex].GetTangentPosition(tangentIndex);
				} else
				if (state.s_HoverOverPoint != -1)
				{
					if (state.s_NewCurveSelection != null)
					{
						state.s_NewCurveSelection.selectedPoints[state.s_HoverOverPoint] = (selectionType == SelectionType.Subtractive) ? false : true;
					}
					state.s_LocalStartPosition = state.s_ControlPoints[state.s_HoverOverPoint].position;
				} else
				//if (s_HoverOverEdge != -1)
				{
					var pointIndex1 = state.s_HoverOverEdge;
					var pointIndex2 = (state.s_HoverOverEdge + 1) % state.s_ControlPoints.Length;
					if (state.s_NewCurveSelection != null)
					{
						state.s_NewCurveSelection.selectedPoints[pointIndex1] =
						state.s_NewCurveSelection.selectedPoints[pointIndex2] = (selectionType == SelectionType.Subtractive) ? false : true;
					}
					state.s_LocalStartPosition = state.s_ControlPoints[state.s_HoverOverEdge].position;
				}
			}

			public static Curve2D Do(int id, Curve2D curve, ref Curve2DSelection curveSelection, int defaultControlID = 0)
			{
				if (curve == null ||
					curve.controlPoints == null)
					return curve;

				var state = (State)GUIUtility.GetStateObject(typeof(State), id);

				var evt = Event.current;
				switch (evt.GetTypeForControl(id))
				{
					case EventType.Layout:
					{
						Curve2DHandleUtility.LayoutCurve(curve, id, out state.s_ClosestPoint, out state.s_ClosestTangent, out state.s_ClosestEdge, out state.s_ClosestEdgePoint);
						break;
					}
					case EventType.Repaint:
					{
						Curve2DHandleUtility.PaintCurve(curve, id, curveSelection, state.s_HoverOverPoint, state.s_HoverOverTangent, state.s_HoverOverEdge);
						if (state.s_HoverOverEdge != -1 && InCreateVertexMode() && !state.s_PointHasMoved)
						{
							var size = UnityEditor.HandleUtility.GetHandleSize(state.s_HoverEdgePoint) * Curve2DHandleUtility.kCurvePointSize;
							SceneHandles.RenderBorderedDot(state.s_HoverEdgePoint, size);
						}

						if (GUIUtility.hotControl == id)
						{
							var selectedColor = SceneHandles.StateColor(SceneHandles.MultiplyTransparency(SceneHandles.selectedColor, 0.5f));
							using (new SceneHandles.DrawingScope(selectedColor))
								HandleRendering.RenderSnapping3D(state.s_Snapping2D.WorldSlideGrid, state.s_Snapping2D.WorldSnappedExtents, state.s_Snapping2D.GridSnappedPosition, state.s_Snapping2D.SnapResult, true);
						}
						break;
					}
					case EventType.MouseMove:
					{
						state.s_PointHasMoved = false;
						var haveFocus = id == SceneHandleUtility.focusControl;
						if (!haveFocus)
						{
							state.s_ClosestPoint	 = -1;
							state.s_ClosestTangent = -1;
							state.s_ClosestEdge	 = -1;
							state.s_ClosestEdgePoint = Vector3.zero;
						}

						if (state.s_HoverOverPoint		!= state.s_ClosestPoint || 
							state.s_HoverOverTangent	!= state.s_ClosestTangent || 
							state.s_HoverOverEdge		!= state.s_ClosestEdge)
						{
							state.s_HoverOverPoint		= state.s_ClosestPoint;
							state.s_HoverOverTangent	= state.s_ClosestTangent;
							state.s_HoverOverEdge		= state.s_ClosestEdge;
							state.s_HoverEdgePoint		= state.s_ClosestEdgePoint;
							SceneView.RepaintAll();
						} else
						if (state.s_HoverOverEdge	!= -1 &&
							state.s_HoverEdgePoint	!= state.s_ClosestEdgePoint)
						{
							state.s_HoverEdgePoint	= state.s_ClosestEdgePoint;
							SceneView.RepaintAll();
						}
						break;
					}
					case EventType.MouseDown:
					{
						if (SceneHandles.disabled)
							break;

						if (state.s_HoverOverPoint == -1 && state.s_HoverOverTangent == -1 && state.s_HoverOverEdge == -1)
							break;

						if (((UnityEditor.HandleUtility.nearestControl == id && evt.button == 0) ||
							 (GUIUtility.keyboardControl == id && evt.button == 2)) && 
							 GUIUtility.hotControl == 0)
						{
							state.s_PointHasMoved = false;

							if (curveSelection != null)
							{
								state.s_NewCurveSelection = new Curve2DSelection();
								var pointCount = curve.controlPoints.Length;
								if (curveSelection.selectedPoints == null ||
									curveSelection.selectedPoints.Length != pointCount)
								{
									state.s_NewCurveSelection.selectedPoints = new bool[pointCount];
									curveSelection.selectedPoints = state.s_NewCurveSelection.selectedPoints;
								} else
									state.s_NewCurveSelection.selectedPoints = curveSelection.selectedPoints.ToArray();
							}


							var selectionType = SelectionUtility.GetCurrentSelectionType(evt);
							if (state.s_HoverOverEdge != -1 && InCreateVertexMode())
							{
								CurveControlPoint2D newPoint = new CurveControlPoint2D()
								{
									position = state.s_HoverEdgePoint
								};
								// Need to copy this to avoid Undo system getting confused
								state.s_ControlPoints = curve.controlPoints.ToArray();
								state.s_HoverOverPoint = (state.s_HoverOverEdge + 1) % state.s_ControlPoints.Length;
								ArrayUtility.Insert(ref state.s_ControlPoints, state.s_HoverOverPoint, newPoint);
									
								if (state.s_NewCurveSelection != null)
								{
									ArrayUtility.Insert(ref state.s_NewCurveSelection.selectedPoints, state.s_HoverOverPoint, false);
									selectionType = SelectionType.Normal;
								}
								state.s_PointHasMoved = true;
							} else
								state.s_ControlPoints = curve.controlPoints;

							ClickSelect(state, selectionType);
							
							Vector3[] localExtentsPoints;
							if (state.s_HoverOverTangent == -1 && state.s_NewCurveSelection != null)
							{ 
								int selectedCount = 0;
								for (int i = 0; i < state.s_NewCurveSelection.selectedPoints.Length; i++)
									selectedCount += (state.s_NewCurveSelection.selectedPoints[i] ? 1 : 0);

								if (selectedCount == 0)
								{
									state.s_PointHasMoved = false;
									GUIUtility.hotControl = 0;
									GUIUtility.keyboardControl = 0;
									EditorGUIUtility.editingTextField = false;
									state.s_ControlPoints = null;
									evt.Use();
									EditorGUIUtility.SetWantsMouseJumping(0);
									break;
								}

								localExtentsPoints = new Vector3[selectedCount];
								for (int i = 0, n = 0; i < state.s_NewCurveSelection.selectedPoints.Length; i++)
								{
									if (!state.s_NewCurveSelection.selectedPoints[i])
										continue;
									localExtentsPoints[n] = state.s_ControlPoints[i].position;
									n++;
								}
								
								if (selectionType == SelectionType.Normal &&
									curveSelection != null)
								{
									curveSelection.selectedPoints = state.s_NewCurveSelection.selectedPoints;
									state.s_NewCurveSelection = null;
								}
							} else
								localExtentsPoints = new Vector3[] { state.s_LocalStartPosition };
									
							GUIUtility.hotControl = GUIUtility.keyboardControl = id;
							EditorGUIUtility.editingTextField = false;
							evt.Use();
							EditorGUIUtility.SetWantsMouseJumping(1); 
								
							state.s_CurrentMousePosition = evt.mousePosition;

							var activeGrid			= Grid.ActiveGrid;
							var localToWorldMatrix	= SceneHandles.matrix;
							state.s_Snapping2D.Initialize(activeGrid, state.s_CurrentMousePosition, state.s_LocalStartPosition, localToWorldMatrix);
							state.s_Snapping2D.CalculateExtents(localExtentsPoints);
						}
						break;
					}
					case EventType.MouseDrag:
					{
						if (GUIUtility.hotControl != id)
							break;

						if (!state.s_PointHasMoved)
						{
							// TODO: (potentially) Apply selection here, NOT in MouseDown
						}
						
						state.s_PointHasMoved = true;
						evt.Use();
						if (state.s_HoverOverPoint == -1 && state.s_HoverOverTangent == -1 && state.s_HoverOverEdge == -1)
							break;

						// necessary to get accurate mouse cursor position when wrapping around screen due to using EditorGUIUtility.SetWantsMouseJumping
						state.s_CurrentMousePosition += evt.delta;
							
						if (!state.s_Snapping2D.DragTo(state.s_CurrentMousePosition))
							break;
						
						var worldToLocalMatrix	= SceneHandles.inverseMatrix;
						var localSnappedDelta	= worldToLocalMatrix.MultiplyVector(state.s_Snapping2D.WorldSnappedDelta);
						var localQuantizedDelta	= SnappingUtility.Quantize(state.s_LocalStartPosition + localSnappedDelta) - state.s_LocalStartPosition;


						Curve2D prevCurve = curve;
						if      (state.s_HoverOverTangent != -1) curve = Curve2DHandleUtility.MoveCurveTangent(curve,                 state.s_ControlPoints, state.s_HoverOverTangent, localQuantizedDelta);
						else if (state.s_HoverOverPoint   != -1) curve = Curve2DHandleUtility.MoveCurvePoint  (curve, curveSelection, state.s_ControlPoints, state.s_HoverOverPoint,   localQuantizedDelta);
						else if (state.s_HoverOverEdge    != -1) curve = Curve2DHandleUtility.MoveCurveEdge   (curve, curveSelection, state.s_ControlPoints, state.s_HoverOverEdge,    localQuantizedDelta);
						GUI.changed = GUI.changed || (prevCurve != curve);
						break;
					}
					case EventType.MouseUp:
					{
						if (GUIUtility.hotControl != id || (evt.button != 0 && evt.button != 2))
							break;

						if (curveSelection != null &&
							state.s_NewCurveSelection != null) // TODO: should only continue if we actually changed something
						{
							curveSelection.selectedPoints = state.s_NewCurveSelection.selectedPoints;
							GUI.changed = true;
						}
						
						// Check if we clicked instead of dragged
						if (!state.s_PointHasMoved)
						{
							if (state.s_HoverOverTangent != -1)
							{
								// TODO: when clicking on tangent, toggle between curve types (but skip straight)
							}
							if (state.s_HoverOverEdge != -1)
							{
								if (curveSelection != null)
								{
									var selectionType = SelectionUtility.GetCurrentSelectionType(evt);
									if (selectionType == SelectionType.Normal)
									{
										if (state.s_NewCurveSelection == null)
										{
											state.s_NewCurveSelection = new Curve2DSelection();
											state.s_NewCurveSelection.selectedPoints = curveSelection.selectedPoints.ToArray();
										}
										ClickSelect(state, selectionType, forceClear: true);
										curveSelection.selectedPoints = state.s_NewCurveSelection.selectedPoints;
									}
								}			
								
								// TODO: when clicking on edge, toggle between curve types on both points
							}
							if (state.s_HoverOverPoint != -1)
							{ 
								int selectedCount = 0;
								bool pointSelected = true;

								if (curveSelection != null)
								{
									for (int i = 0; i < curveSelection.selectedPoints.Length; i++)
										selectedCount += (curveSelection.selectedPoints[i] ? 1 : 0);
									
									var selectionType = SelectionUtility.GetCurrentSelectionType(evt);
									if (selectionType == SelectionType.Normal)
									{
										if (state.s_NewCurveSelection == null)
										{
											state.s_NewCurveSelection = new Curve2DSelection();
											state.s_NewCurveSelection.selectedPoints = curveSelection.selectedPoints.ToArray();
										}
										ClickSelect(state, selectionType, forceClear: true);
										curveSelection.selectedPoints = state.s_NewCurveSelection.selectedPoints;
									}

									pointSelected = curveSelection.selectedPoints[state.s_HoverOverPoint];
								}

								if (selectedCount <= 1 && pointSelected && evt.modifiers == EventModifiers.None)
								{
									curve = Curve2DHandleUtility.ToggleContraint(curve, state.s_HoverOverPoint);
									GUI.changed = true;
								}
							}
						}

						state.s_NewCurveSelection = null;
						GUIUtility.hotControl = 0;
						GUIUtility.keyboardControl = 0;
						EditorGUIUtility.editingTextField = false;
						state.s_ControlPoints = null;
						evt.Use();
						EditorGUIUtility.SetWantsMouseJumping(0);
						break;
					}
				}
				return curve;
			}
			/*
			private static bool GetPlaneIntersection(State state, Vector2 position, out Vector3 hitpos)
			{
				var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(position);
				var dist = 0.0f;
				if (!state.s_CurvePlane.Raycast(mouseRay, out dist))
				{
					hitpos = Vector3.zero;
					return false;
				}

				hitpos = mouseRay.GetPoint(dist);
				return true;
			}
			*/
		}
	}
}
