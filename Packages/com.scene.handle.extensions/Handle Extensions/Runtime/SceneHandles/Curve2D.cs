using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{

	[Serializable]
	public enum ControlPointConstraint
	{
		Straight,   // Tangents are ignored (straight lines)
		Broken,     // Both tangents can go in different directions and there will be a break in the curvature
		Mirrored    // Both tangents are aligned and mirror each other
	}
	
	[Serializable]
	public struct CurveControlPoint2D
	{
		public CurveControlPoint2D(Vector2 position) { this.position = position; this.tangent1 = Vector3.zero; this.tangent2 = Vector3.zero; constraint1 = ControlPointConstraint.Straight; constraint2 = ControlPointConstraint.Straight; }
		public CurveControlPoint2D(float x, float y) { this.position = new Vector2(x,y); this.tangent1 = Vector3.zero; this.tangent2 = Vector3.zero; constraint1 = ControlPointConstraint.Straight; constraint2 = ControlPointConstraint.Straight; }

		[PositionValue] public Vector2 position;
		[DistanceValue] public Vector2 tangent1;
		[DistanceValue] public Vector2 tangent2;
		public ControlPointConstraint constraint1;
		public ControlPointConstraint constraint2;

		
		public Vector2					GetTangentPosition	(int index) { if (index == 0) return position - tangent1; else return position - tangent2; }
		public void						SetTangentPosition	(int index, Vector2 tangentPosition)
		{
			if (constraint1 != ControlPointConstraint.Mirrored)
			{
				if (index == 0)
					tangent1 = position - tangentPosition;
				else
					tangent2 = position - tangentPosition;
			} else
			{
				if (index == 0)
				{
					tangent1 = position - tangentPosition;
					tangent2 = -tangent1;
				} else
				{
					tangent2 = position - tangentPosition;
					tangent1 = -tangent2;
				}
			}
		}

		public Vector2					GetTangent			(int index) { if (index == 0) return tangent1; else return tangent2; }
		public ControlPointConstraint	GetConstraint		(int index) { if (index == 0) return constraint1; else return constraint2; }
		
		public void SetTangent   (int index, Vector2 value)
		{
			if (index == 0)
			{
				tangent1 = value;
				if (constraint1 == ControlPointConstraint.Mirrored)
					tangent2 = -value;
			} else
			{
				tangent2 = value;
				if (constraint2 == ControlPointConstraint.Mirrored)
					tangent1 = -value;
			}
		}
		public void	SetConstraint(int index, ControlPointConstraint value) { if (index == 0) constraint1 = value; else constraint2 = value; }
	}

	[Serializable]
	public class Curve2D
	{
		public Curve2D() { }
		public Curve2D(bool closed, params CurveControlPoint2D[] controlPoints) { this.closed = closed; this.controlPoints = controlPoints.ToArray(); }
		public Curve2D(params CurveControlPoint2D[] controlPoints) { this.closed = true; this.controlPoints = controlPoints.ToArray(); }
		public Curve2D(Curve2D other) { this.closed = other.closed; this.controlPoints = other.controlPoints.ToArray(); }

		public bool						closed			= false;
		public CurveControlPoint2D[]	controlPoints	= new CurveControlPoint2D[0];

		public Vector2 Center
		{
			get
			{
				var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
				var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				for (int i = 0; i < controlPoints.Length; i++)
				{
					var position = controlPoints[i].position;
					min.x = Mathf.Min(min.x, position.x);
					min.y = Mathf.Min(min.y, position.y);
					
					max.x = Mathf.Max(max.x, position.x);
					max.y = Mathf.Max(max.y, position.y);
				}
				return (max + min) * 0.5f;
			}
			set
			{
				var realCenter = Center;
				if (realCenter == value)
					return;

				for (int i = 0; i < controlPoints.Length; i++)
				{
					var position = controlPoints[i].position;
					position -= realCenter;
					position += value;
					controlPoints[i].position = position;
				}
			}
		}
	}

	[Serializable]
	public class Curve2DSelection
	{
		public void Clear()
		{
			selectedPoints = null;
		}
		
		public void SelectAll(int length)
		{
			selectedPoints = new bool[length];
			for (int i = 0; i < length; i++)
				selectedPoints[i] = true;
		}

		public bool[]					selectedPoints;
	}
}
