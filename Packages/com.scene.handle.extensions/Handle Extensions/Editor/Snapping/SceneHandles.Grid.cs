using System;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public enum SnapResult3D
	{
		None	= 0,

		MinX	= 1,
		MaxX	= 2,

		MinY	= 4,
		MaxY	= 8,

		MinZ	= 16,
		MaxZ	= 32,

		PivotX	= 64,
		PivotY	= 128,
		PivotZ	= 256
	}
	
	public enum SnapResult1D
	{
		None	= 0,
		Min		= 1,
		Max		= 2,
		Pivot	= 4
	}

	public class Grid
	{
		const float kMinSpacing = (1 / 8192.0f);

		public Grid() { }
		public Grid(Matrix4x4 gridToWorldSpace, Vector3 spacing) { this.GridToWorldSpace = gridToWorldSpace; this.Spacing = spacing; }
		public Grid(Matrix4x4 gridToWorldSpace) { this.GridToWorldSpace = gridToWorldSpace; this.Spacing = defaultGrid.Spacing; }

		public static readonly Grid defaultGrid = new Grid();
		public static Grid currentGrid = null;

		public static Grid HoverGrid { get; set; }

		public static Grid ActiveGrid
		{
			get
			{
				if (currentGrid == null)
					return defaultGrid;
				return currentGrid;
			}
		}

		private static bool m_enabled = false;
		public static bool Enabled
		{
			get
			{
				return m_enabled;
			}

			set
			{
				m_enabled = value;
			}
		}

		public bool Hide { get; internal set; }

		Vector3 _spacing = Vector3.one;
		public Vector3 Spacing
		{
			get
			{
				return _spacing;
			}
			set
			{
				_spacing.x = UnityEngine.Mathf.Max(kMinSpacing, value.x);
				_spacing.y = UnityEngine.Mathf.Max(kMinSpacing, value.y);
				_spacing.z = UnityEngine.Mathf.Max(kMinSpacing, value.z);
			}
		}
		public float SpacingX { get { return _spacing.x; } set { _spacing.x = UnityEngine.Mathf.Max(kMinSpacing, value);} }
		public float SpacingY { get { return _spacing.y; } set { _spacing.y = UnityEngine.Mathf.Max(kMinSpacing, value); } }
		public float SpacingZ { get { return _spacing.z; } set { _spacing.z = UnityEngine.Mathf.Max(kMinSpacing, value); } }

		Matrix4x4 _gridToWorldSpace = Matrix4x4.identity;
		Matrix4x4 _worldToGridSpace = Matrix4x4.identity;

		public Matrix4x4 GridToWorldSpace
		{
			get
			{
				return _gridToWorldSpace;
			}
			set
			{
				_gridToWorldSpace = value;
				_worldToGridSpace = Matrix4x4.Inverse(_gridToWorldSpace);
			}
		}

		public Matrix4x4 WorldToGridSpace
		{
			get
			{
				return _worldToGridSpace;
			}
			set
			{
				_worldToGridSpace = value;
				_gridToWorldSpace = Matrix4x4.Inverse(_worldToGridSpace);
			}
		}
		
		public Vector3	Center
		{
			get
			{
				var center = (Vector3)_gridToWorldSpace.GetColumn(3);
				return center;
			}
		}
		
		public Vector3 Up
		{
			get
			{
				return (Vector3)_gridToWorldSpace.GetColumn(1);
			}
		}

		public Vector3 Right
		{
			get
			{
				return (Vector3)_gridToWorldSpace.GetColumn(0);
			}
		}

		public Vector3 Forward
		{
			get
			{
				return (Vector3)_gridToWorldSpace.GetColumn(2);
			}
		}

		public Plane PlaneXZ
		{
			get
			{
				var up		= (Vector3)_gridToWorldSpace.GetColumn(1);
				var center	= (Vector3)_gridToWorldSpace.GetColumn(3);
				return new Plane(up, center);
			}
		}

		public Vector3 GetAxisVector(Axis axis)
		{
			switch (axis)
			{
				default:
				case Axis.X: return (Vector3)_gridToWorldSpace.GetColumn(0);
				case Axis.Y: return (Vector3)_gridToWorldSpace.GetColumn(1);
				case Axis.Z: return (Vector3)_gridToWorldSpace.GetColumn(2);
			}
		}

		public float GetAxisSnapping(Axis axis)
		{
			switch (axis)
			{
				default:
				case Axis.X: return _spacing.x;
				case Axis.Y: return _spacing.y;
				case Axis.Z: return _spacing.z;
			}
		}

		public Grid Transform(Matrix4x4 matrix)
		{
			var activeGrid = ActiveGrid;
			return new Grid(activeGrid.GridToWorldSpace * matrix, activeGrid.Spacing);
		}

		public Extents3D GetGridExtentsOfPointArray(Matrix4x4 localToWorldMatrix, Vector3[] points)
		{
			var toMatrix = _worldToGridSpace * localToWorldMatrix;

			var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
			for (var i = 0; i < points.Length; i++)
			{
				var distance = toMatrix.MultiplyPoint(points[i]);
				min.x = Mathf.Min(min.x, distance.x);
				min.y = Mathf.Min(min.y, distance.y);
				min.z = Mathf.Min(min.z, distance.z);

				max.x = Mathf.Max(max.x, distance.x);
				max.y = Mathf.Max(max.y, distance.y);
				max.z = Mathf.Max(max.z, distance.z);
			}
			return new Extents3D(min, max);
		}
		
		public Vector3 SnapExtents3D(Extents3D extentsInGridSpace, Vector3 worldCurrentPosition, Vector3 worldStartPosition, Axes enabledAxes = Axes.XYZ)
		{
			SnapResult3D result;
			return SnapExtents3D(extentsInGridSpace, worldCurrentPosition, worldStartPosition, out result, enabledAxes);
		}

		public Vector3 SnapExtents3D(Extents3D extentsInGridSpace, Vector3 worldCurrentPosition, Vector3 worldStartPosition, out SnapResult3D snapResult, Axes enabledAxes = Axes.XYZ)
		{
			snapResult = SnapResult3D.None;
			if (!Snapping.BoundsSnappingActive && !Snapping.PivotSnappingActive)
				return worldCurrentPosition;

			var offsetInWorldSpace		= worldCurrentPosition - worldStartPosition;			
			var offsetInGridSpace		= _worldToGridSpace.MultiplyVector(offsetInWorldSpace);
			var pivotInGridSpace		= _worldToGridSpace.MultiplyVector(worldCurrentPosition - Center);
			
			// Snap our extents in grid space
			var movedExtentsInGridspace	= extentsInGridSpace + offsetInGridSpace;
			
			if ((enabledAxes & Axes.X) > 0)
			{
				var snappedPivot		= SnappingUtility.SnapValue(pivotInGridSpace.x, _spacing.x) - pivotInGridSpace.x;
				var snappedMinExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.min.x, _spacing.x) - movedExtentsInGridspace.min.x;
				var snappedMaxExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.max.x, _spacing.x) - movedExtentsInGridspace.max.x;

				// Figure out on which side of the extents is closest to the grid, use that offset for each axis
				var abs_pivot		= Snapping.PivotSnappingActive  ? SnappingUtility.Quantize(Mathf.Abs(snappedPivot     )) : float.PositiveInfinity;
				var abs_min_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMinExtents)) : float.PositiveInfinity;
				var abs_max_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMaxExtents)) : float.PositiveInfinity;
				offsetInGridSpace.x += (abs_pivot < abs_min_extents && abs_pivot < abs_max_extents) ? snappedPivot : ((abs_min_extents < abs_max_extents) ? snappedMinExtents : snappedMaxExtents);
				if (abs_min_extents <= abs_max_extents && abs_min_extents <= abs_pivot) snapResult |= SnapResult3D.MinX;
				if (abs_max_extents <= abs_min_extents && abs_max_extents <= abs_pivot) snapResult |= SnapResult3D.MaxX;
				if (abs_pivot       <= abs_min_extents && abs_pivot <= abs_max_extents) snapResult |= SnapResult3D.PivotX;
			}

			if ((enabledAxes & Axes.Y) > 0)
			{
				var snappedPivot		= SnappingUtility.SnapValue(pivotInGridSpace.y, _spacing.y) - pivotInGridSpace.y;
				var snappedMinExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.min.y, _spacing.y) - movedExtentsInGridspace.min.y;
				var snappedMaxExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.max.y, _spacing.y) - movedExtentsInGridspace.max.y;

				// Figure out on which side of the extents is closest to the grid, use that offset for each axis
				var abs_pivot		= Snapping.PivotSnappingActive  ? SnappingUtility.Quantize(Mathf.Abs(snappedPivot     )) : float.PositiveInfinity;
				var abs_min_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMinExtents)) : float.PositiveInfinity;
				var abs_max_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMaxExtents)) : float.PositiveInfinity;
				offsetInGridSpace.y += (abs_pivot < abs_min_extents && abs_pivot < abs_max_extents) ? snappedPivot : ((abs_min_extents < abs_max_extents) ? snappedMinExtents : snappedMaxExtents);
				if (abs_min_extents <= abs_max_extents && abs_min_extents <= abs_pivot) snapResult |= SnapResult3D.MinY;
				if (abs_max_extents <= abs_min_extents && abs_max_extents <= abs_pivot) snapResult |= SnapResult3D.MaxY;
				if (abs_pivot       <= abs_min_extents && abs_pivot <= abs_max_extents) snapResult |= SnapResult3D.PivotY;
			}

			if ((enabledAxes & Axes.Z) > 0)
			{
				var snappedPivot		= SnappingUtility.SnapValue(pivotInGridSpace.z, _spacing.z) - pivotInGridSpace.z;
				var snappedMinExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.min.z, _spacing.z) - movedExtentsInGridspace.min.z;
				var snappedMaxExtents	= SnappingUtility.SnapValue(movedExtentsInGridspace.max.z, _spacing.z) - movedExtentsInGridspace.max.z;

				// Figure out on which side of the extents is closest to the grid, use that offset for each axis
				var abs_pivot		= Snapping.PivotSnappingActive  ? SnappingUtility.Quantize(Mathf.Abs(snappedPivot     )) : float.PositiveInfinity;
				var abs_min_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMinExtents)) : float.PositiveInfinity;
				var abs_max_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedMaxExtents)) : float.PositiveInfinity;
				offsetInGridSpace.z += (abs_pivot < abs_min_extents && abs_pivot < abs_max_extents) ? snappedPivot : ((abs_min_extents < abs_max_extents) ? snappedMinExtents : snappedMaxExtents);
				if (abs_min_extents <= abs_max_extents && abs_min_extents <= abs_pivot) snapResult |= SnapResult3D.MinZ;
				if (abs_max_extents <= abs_min_extents && abs_max_extents <= abs_pivot) snapResult |= SnapResult3D.MaxZ;
				if (abs_pivot       <= abs_min_extents && abs_pivot <= abs_max_extents) snapResult |= SnapResult3D.PivotZ;
			}


			
			var snappedOffsetInWorldSpace	=_gridToWorldSpace.MultiplyVector(offsetInGridSpace);
			var snappedPositionInWorldSpace	= (worldStartPosition + snappedOffsetInWorldSpace);
			return snappedPositionInWorldSpace;
		}
	}
}
