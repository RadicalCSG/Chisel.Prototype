//#define ENABLE_DEBUG_GRID
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    [Flags]
    public enum SnapSettings
    {
        None                    = 0,

        GeometryPivotToGrid     = 1,
        GeometryBoundsToGrid    = 2,
        GeometryVertex          = 4,    // TODO: implement
        GeometryEdge            = 8,    // TODO: implement
        GeometrySurface         = 16,   // TODO: implement

        AllGeometry             = GeometryPivotToGrid | GeometryBoundsToGrid | GeometryVertex | GeometryEdge | GeometrySurface,

        UVGeometryGrid          = 32,
        UVGeometryEdges         = 64,
        UVGeometryVertices      = 128,
        UVGrid                  = 256,  // TODO: implement
        UVBounds                = 512,  // TODO: implement

        AllUV                   = UVGeometryGrid | UVGeometryEdges | UVGeometryVertices | UVGrid | UVBounds,

        All                     = ~0
    }

    [Flags]
    public enum ActiveTransformSnapping
    {
        None                    = 0,

        Scale                   = 1, // TODO: implement
        Rotate                  = 2, // TODO: implement
        Translate               = 4,

        All                     = ~0
    }

    public static class Snapping
    {
        public static event Action SnappingSettingsModified;
        public delegate bool GetCustomSnappingPointsRayDelegate(Vector3 worldRayStart, Vector3 worldRayDirection, int contextIndex, List<Vector3> foundWorldspacePoints);
        public delegate bool GetCustomSnappingPointsDelegate(Vector3 worldRayStart, Plane worldSlidePlane, int contextIndex, List<Vector3> foundWorldspacePoints);
        public delegate void CustomSnappedEventDelegate(int index, int context);

        public static GetCustomSnappingPointsRayDelegate FindCustomSnappingPointsRayMethod;
        public static GetCustomSnappingPointsDelegate FindCustomSnappingPointsMethod;
        public static CustomSnappedEventDelegate CustomSnappedEvent;

        public static bool GetCustomSnappingPoints(Vector3 worldRayStart, Vector3 worldRayDirection, int contextIndex, List<Vector3> foundWorldspacePoints)
        {
            foundWorldspacePoints.Clear();
            return Snapping.FindCustomSnappingPointsRayMethod == null ? false :
                    Snapping.FindCustomSnappingPointsRayMethod(worldRayStart, worldRayDirection, contextIndex, foundWorldspacePoints)
                    && foundWorldspacePoints.Count > 0;
        }

        public static bool GetCustomSnappingPoints(Vector3 worldRayStart, Plane worldSlidePlane, int contextIndex, List<Vector3> foundWorldspacePoints)
        {
            foundWorldspacePoints.Clear();
            return Snapping.FindCustomSnappingPointsMethod == null ? false :
                    Snapping.FindCustomSnappingPointsMethod(worldRayStart, worldSlidePlane, contextIndex, foundWorldspacePoints)
                    && foundWorldspacePoints.Count > 0;
        }


        public static SnapSettings SnapMask { get; set; } = SnapSettings.All;
        public static SnapSettings SnapSettings { get; set; } = SnapSettings.All;

        static bool IsFlagEnabled(SnapSettings flag) { return (SnapSettings & flag) == flag; }
        static void SetFlagEnabled(SnapSettings flag, bool enabled)
        {
            var prevEnabled = IsFlagEnabled(flag);
            if (prevEnabled == enabled)
                return;
            if (enabled)
                SnapSettings |= flag;
            else
                SnapSettings &= ~flag;
            SnappingSettingsModified?.Invoke();
        }
        static bool IsFlagActive(SnapSettings flag) { return ((SnapSettings & SnapMask) & flag) == flag; }
        

        public static ActiveTransformSnapping TransformSettings { get; set; } = ActiveTransformSnapping.All;
        static bool IsFlagEnabled(ActiveTransformSnapping flag) { return (TransformSettings & flag) == flag; }
        static void SetFlagEnabled(ActiveTransformSnapping flag, bool enabled)
        {
            var prevEnabled = IsFlagEnabled(flag);
            if (prevEnabled == enabled)
                return;
            if (enabled)
                TransformSettings |= flag;
            else
                TransformSettings &= ~flag;
        }

        internal static bool	SnappingToggled			{ get { return EditorGUI.actionKey; } }

        #region BoundsSnappingEnabled
        public static bool		BoundsSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.GeometryBoundsToGrid);
            }
            set
            {
                SetFlagEnabled(SnapSettings.GeometryBoundsToGrid, value);
            }
        }
        #endregion
        public static bool		BoundsSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                if (SnappingToggled)
                    return !(IsFlagActive(SnapSettings.GeometryBoundsToGrid) || IsFlagActive(SnapSettings.GeometryPivotToGrid));
                return IsFlagActive(SnapSettings.GeometryBoundsToGrid);
            }
        }

        #region PivotSnappingEnabled
        public static bool		PivotSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.GeometryPivotToGrid);
            }
            set
            {
                SetFlagEnabled(SnapSettings.GeometryPivotToGrid, value);
            }
        }
        #endregion
        public static bool		PivotSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                if (SnappingToggled)
                    return !(IsFlagActive(SnapSettings.GeometryBoundsToGrid) || IsFlagActive(SnapSettings.GeometryPivotToGrid));
                return IsFlagActive(SnapSettings.GeometryPivotToGrid);
            }
        }

        #region VertexSnappingEnabled
        public static bool VertexSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.GeometryVertex);
            }
            set
            {
                SetFlagEnabled(SnapSettings.GeometryVertex, value);
            }
        }
        #endregion
        public static bool VertexSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return IsFlagActive(SnapSettings.GeometryVertex);
            }
        }

        #region EdgeSnappingEnabled
        public static bool  EdgeSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.GeometryEdge);
            }
            set
            {
                SetFlagEnabled(SnapSettings.GeometryEdge, value);
            }
        }
        #endregion
        public static bool  EdgeSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return IsFlagActive(SnapSettings.GeometryEdge);
            }
        }

        #region SurfaceSnappingEnabled
        public static bool  SurfaceSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.GeometrySurface);
            }
            set
            {
                SetFlagEnabled(SnapSettings.GeometrySurface, value);
            }
        }
        #endregion
        public static bool  SurfaceSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return IsFlagActive(SnapSettings.GeometrySurface);
            }
        }



        #region RotateSnappingEnabled
        public static bool	RotateSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(ActiveTransformSnapping.Rotate);
            }
            set
            {
                SetFlagEnabled(ActiveTransformSnapping.Rotate, value);
            }
        }
        #endregion
        public static bool	RotateSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return RotateSnappingEnabled;
            }
        }
        
        #region ScaleSnappingEnabled
        public static bool	ScaleSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(ActiveTransformSnapping.Scale);
            }
            set
            {
                SetFlagEnabled(ActiveTransformSnapping.Scale, value);
            }
        }
        #endregion
        public static bool	ScaleSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return ScaleSnappingEnabled;
            }
        }


        #region TranslateSnappingEnabled
        public static bool TranslateSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(ActiveTransformSnapping.Translate);
            }
            set
            {
                SetFlagEnabled(ActiveTransformSnapping.Translate, value);
            }
        }
        #endregion
        
        #region UVGridSnappingEnabled
        public static bool UVGridSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.UVGeometryGrid);
            }
            set
            {
                SetFlagEnabled(SnapSettings.UVGeometryGrid, value);
            }
        }
        #endregion
        public static bool UVGridSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return UVGridSnappingEnabled;
            }
        }

        #region UVVertexSnappingEnabled
        public static bool UVVertexSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.UVGeometryVertices);
            }
            set
            {
                SetFlagEnabled(SnapSettings.UVGeometryVertices, value);
            }
        }
        #endregion
        public static bool UVVertexSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return UVVertexSnappingEnabled;
            }
        }

        #region UVEdgeSnappingEnabled
        public static bool UVEdgeSnappingEnabled
        {
            get
            {
                return IsFlagEnabled(SnapSettings.UVGeometryEdges);
            }
            set
            {
                SetFlagEnabled(SnapSettings.UVGeometryEdges, value);
            }
        }
        #endregion
        public static bool UVEdgeSnappingActive
        {
            get
            {
                if (!TranslateSnappingEnabled)
                    return false;
                return UVEdgeSnappingEnabled;
            }
        }


        #region MoveSnappingSteps
        public static Vector3	MoveSnappingSteps
        {
            get
            {
                return Grid.defaultGrid.Spacing;
            }
            set
            {
                if (Grid.defaultGrid.Spacing == value)
                    return;
                Grid.defaultGrid.Spacing = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        
        #region RotateSnappingStep
        private static float	_rotateSnappingStep = 15.0f;
        public static float		RotateSnappingStep
        {
            get
            {
                return _rotateSnappingStep;
            }
            set
            {
                if (_rotateSnappingStep == value)
                    return;
                _rotateSnappingStep = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        
        #region ScaleSnappingStep
        private static float	_scaleSnappingStep = 0.1f;
        public static float		ScaleSnappingStep
        {
            get
            {
                return _scaleSnappingStep;
            }
            set
            {
                if (_scaleSnappingStep == value)
                    return;
                _scaleSnappingStep = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        

        #region AxisLocking
        private static readonly bool[] AxisLockingArray = new bool[3] {false, false, false};

        [NotNull]
        public static bool[]	AxisLocking
        {
            get
            {
                return AxisLockingArray;
            }
            set
            {
                if (value.Length != 3)
                    return;
                if (value[0] == AxisLockingArray[0] &&
                    value[1] == AxisLockingArray[1] &&
                    value[2] == AxisLockingArray[2])
                    return;
                Array.Copy(value, AxisLockingArray, 3);
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        
        #region ActiveAxes
        public static Axes ActiveAxes
        {
            get
            {
                if (AxisLockingArray[0])			// -YZ
                {
                    if (AxisLockingArray[1])		// --Z
                    {
                        if (AxisLockingArray[2])	// ---
                            return Axes.None;		
                        return Axes.Z;				
                    } else
                    if (AxisLockingArray[2])		// -Y-
                        return Axes.Y;
                    return Axes.YZ;
                } else
                if (AxisLockingArray[1])			// X-Z
                {
                    if (AxisLockingArray[2])		// X--
                        return Axes.X;
                    return Axes.XZ;
                } else
                if (AxisLockingArray[2])			// XY-
                {
                    return Axes.XY;
                }
                return Axes.XYZ;
            }
            set
            {
                switch (value)
                {
                    case Axes.None:	{ AxisLockingArray[0] = true;  AxisLockingArray[1] = true;  AxisLockingArray[2] = true;  break; }
                    case Axes.X:	{ AxisLockingArray[0] = false; AxisLockingArray[1] = true;  AxisLockingArray[2] = true;  break; }
                    case Axes.Y:	{ AxisLockingArray[0] = true;  AxisLockingArray[1] = false; AxisLockingArray[2] = true;  break; }
                    case Axes.Z:	{ AxisLockingArray[0] = true;  AxisLockingArray[1] = true;  AxisLockingArray[2] = false; break; }
                    case Axes.XY:	{ AxisLockingArray[0] = false; AxisLockingArray[1] = false; AxisLockingArray[2] = true;  break; }
                    case Axes.XZ:	{ AxisLockingArray[0] = false; AxisLockingArray[1] = true;  AxisLockingArray[2] = false; break; }
                    case Axes.YZ:	{ AxisLockingArray[0] = true;  AxisLockingArray[1] = false; AxisLockingArray[2] = false; break; }
                    default:
                    case Axes.XYZ:	{ AxisLockingArray[0] = false; AxisLockingArray[1] = false; AxisLockingArray[2] = false; break; }
                }
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }

        internal static bool AreAxisLocked(Axes axes)
        {
            switch (axes)
            {
                default:
                case Axes.None:	return false;
                case Axes.X:	return AxisLockingArray[0];
                case Axes.Y:	return AxisLockingArray[1];
                case Axes.Z:	return AxisLockingArray[2];
                case Axes.XY:	return AxisLockingArray[0] && AxisLockingArray[1];
                case Axes.XZ:	return AxisLockingArray[0] && AxisLockingArray[2]; 
                case Axes.YZ:	return AxisLockingArray[1] && AxisLockingArray[2]; 
                case Axes.XYZ:	return AxisLockingArray[0] && AxisLockingArray[1] && AxisLockingArray[2];
            }
        }

        internal static bool IsAxisLocked(Axis axis)
        {
            switch (axis)
            {
                default:
                case Axis.None:	return false;
                case Axis.X:	return AxisLockingArray[0];
                case Axis.Y:	return AxisLockingArray[1];
                case Axis.Z:	return AxisLockingArray[2];
            }
        }
        
        public static bool		AxisLockX { get { return AxisLockingArray[0]; } set { AxisLockingArray[0] = value; } }
        public static bool		AxisLockY { get { return AxisLockingArray[1]; } set { AxisLockingArray[1] = value; } }
        public static bool		AxisLockZ { get { return AxisLockingArray[2]; } set { AxisLockingArray[2] = value; } }
        #endregion

        public static Vector3 SnapPoint(Vector3 position, Grid grid, Axes enabledAxes = Axes.XYZ)
        {
            return SnappingUtility.SnapPoint3D(position, Grid.defaultGrid.Spacing, Grid.defaultGrid.Right, Grid.defaultGrid.Up, Grid.defaultGrid.Forward, Grid.defaultGrid.Center, enabledAxes);
        }

        public static Vector3 SnapPoint(Vector3 position, Axes enabledAxes = Axes.XYZ)
        {
            return SnapPoint(position, Grid.defaultGrid, enabledAxes);
        }

        public static (float, float, float, float) SnapBounds(Extents1D currentExtents, float snappingStep)
        {
            if (!Snapping.BoundsSnappingActive)
                return (float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            // Snap current extents against the grid (we're in grid space, so simple snap)
            var snappedExtents          = currentExtents;
            snappedExtents.min		    = SnappingUtility.SnapValue(snappedExtents.min, snappingStep);
            snappedExtents.max		    = SnappingUtility.SnapValue(snappedExtents.max, snappingStep);

            // Determine the offset relative to the current extents
            var snappedExtentsOffset    = currentExtents - snappedExtents;
            var quantized_min_extents   = SnappingUtility.Quantize(snappedExtentsOffset.min);
            var quantized_max_extents   = SnappingUtility.Quantize(snappedExtentsOffset.max);
            var abs_min_extents         = Mathf.Abs(quantized_min_extents);
            var abs_max_extents         = Mathf.Abs(quantized_max_extents);

            // Use the smallest distance as the best snap distance
            if (abs_min_extents < abs_max_extents)
                return (abs_min_extents, snappedExtentsOffset.min, quantized_min_extents, quantized_max_extents);
            else
                return (abs_max_extents, snappedExtentsOffset.max, quantized_min_extents, quantized_max_extents);
        }

        public static (float, float, float) SnapPivot(float currentPivot, float snappingStep)
        {
            if (!Snapping.PivotSnappingActive)
                return (float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            // Snap current pivot position against the grid (we're in grid space, so simple snap)
            var snappedPivot            = SnappingUtility.SnapValue(currentPivot, snappingStep);
            var snappedPivotOffset	    = currentPivot - snappedPivot;

            // Determine the offset relative to the current extents
            var quantized_pivot         = SnappingUtility.Quantize(snappedPivotOffset);
            var abs_pivot               = Mathf.Abs(quantized_pivot);

            return (abs_pivot, snappedPivotOffset, quantized_pivot);
        }

        public static (float, float) SnapCustom(List<Vector3> customSnapPoints, float currentPosition, Vector3 slideDirection, float minPointSnap, List<float> customDistances)
        {
            if (customSnapPoints.Count == 0)
                return (float.PositiveInfinity, float.PositiveInfinity);

            float smallest_abs_distance = float.PositiveInfinity;
            float smallest_distance     = float.PositiveInfinity;

            customDistances.Clear();
            for (int i = 0; i < customSnapPoints.Count; i++)
            {
                var snappedPoint        = SnappingUtility.WorldPointToDistance(customSnapPoints[i], slideDirection);

                // Determine the offset between the current position and the point we want to snap against
                var snappedPointOffset  = currentPosition - snappedPoint;
                var quantized_distance  = SnappingUtility.Quantize(snappedPointOffset);
                var abs_distance        = Mathf.Abs(quantized_distance);

                customDistances.Add(quantized_distance);

                // Use the smallest distance as the best snap distance
                if (smallest_abs_distance > abs_distance)
                {
                    smallest_abs_distance = abs_distance;
                    smallest_distance = snappedPointOffset;
                }
            }

            if (float.IsInfinity(smallest_abs_distance) || smallest_abs_distance > minPointSnap)
                return (float.PositiveInfinity, float.PositiveInfinity);

            return (smallest_abs_distance, smallest_distance);
        }
        
        public static (Vector3, Vector3) SnapCustom(List<Vector3> customSnapPoints, Vector3 currentPosition, Axes enabledAxes, float minPointSnap, List<Vector3> customDistances)
        {
            if (customSnapPoints.Count == 0)
                return (new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                        new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity));

            Vector3 smallest_abs_distance = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 smallest_distance     = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

            if ((enabledAxes & Axes.X) != Axes.X) currentPosition.x = 0;
            if ((enabledAxes & Axes.Y) != Axes.Y) currentPosition.y = 0;
            if ((enabledAxes & Axes.Z) != Axes.Z) currentPosition.z = 0;

            customDistances.Clear();
            for (int i = 0; i < customSnapPoints.Count; i++)
            {
                var snappedPoint        = customSnapPoints[i];
                if ((enabledAxes & Axes.X) != Axes.X) snappedPoint.x = 0;
                if ((enabledAxes & Axes.Y) != Axes.Y) snappedPoint.y = 0;
                if ((enabledAxes & Axes.Z) != Axes.Z) snappedPoint.z = 0;

                // Determine the offset between the current position and the point we want to snap against
                var snappedPointOffset  = currentPosition - snappedPoint;
                var quantized_distance  = SnappingUtility.Quantize(snappedPointOffset);
                var abs_distance        = new Vector3(Mathf.Abs(quantized_distance.x), Mathf.Abs(quantized_distance.y), Mathf.Abs(quantized_distance.z));

                customDistances.Add(quantized_distance);

                // Use the smallest distance as the best snap distance
                if (smallest_abs_distance.sqrMagnitude > abs_distance.sqrMagnitude)
                {
                    smallest_abs_distance = abs_distance;
                    smallest_distance = snappedPointOffset;
                }
            }
            if (float.IsInfinity(smallest_abs_distance.x) || 
                smallest_abs_distance.magnitude > minPointSnap)
                return (new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), 
                        new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity));

            return (smallest_abs_distance, smallest_distance);
        }
        
        public static void SendCustomSnappedEvents(float quantizedDistance, List<float> customDistances, int context)
        {
            for (int i = 0; i< customDistances.Count; i++)
            {
                if (quantizedDistance == customDistances[i])
                    Snapping.CustomSnappedEvent(i, context);
            }
        }

        public static void SendCustomSnappedEvents(Vector3 quantizedDistance, List<Vector3> customDistances, int context)
        {
            for (int i = 0; i < customDistances.Count; i++)
            {
                if (quantizedDistance == customDistances[i])
                    Snapping.CustomSnappedEvent(i, context);
            }
        }
    }

    // TODO: better naming
    public class Snapping1D
    {
        private Vector2			startMousePosition; 
        
        private Vector3			slideOrigin;            // A point on the line that is snapped to the grid
        private float			startOffset;            // Distance from slideOrigin to start world position along the line
        private Vector3			slideOffset;            // Delta from slideOrigin to start world position
        private Extents1D		slideExtents;           // Extents of points along line relative to slideOrigin
        private Axis			slideAxis;              // Axis we're moving on (used for axis locking)

        private Vector3			slidePosition;          // Current unsnapped position on the line, relative to slideOrigin
        private Vector3			snappedPosition;        // Current snapped position on the line, relative to slideOrigin

        private Vector3			slideDirection;         // Direction of the line we're snapping on
        private float			snappingStep;           // Steps of the grid along the line

        private SnapResult1D	snapResult;

        public Vector3			WorldPosition			{ get { return slidePosition + this.slideOffset; } }
        public Vector3			WorldOffset				{ get { return slidePosition - this.slideOrigin; } }
        public float			WorldMagnitude			{ get { return (slidePosition - this.slideOrigin).magnitude; } }
        
        public Vector3			SlideDirection			{ get { return slideDirection; } }
            
        public Vector3			WorldSnappedPosition	{ get { return snappedPosition + this.slideOffset; } }
        public Vector3			WorldSnappedOffset		{ get { return snappedPosition - this.slideOrigin; } }
        public float			WorldSnappedMagnitude	{ get { return (snappedPosition - this.slideOrigin).magnitude; } }

        Vector3 min,max;
        
        public Vector3			Min						{ get { return min; } }
        public Vector3			Max						{ get { return max; } }
        public Extents1D		WorldSnappedExtents		{ get { return slideExtents + WorldSnappedMagnitude; } }

        public SnapResult1D		SnapResult				{ get { return snapResult; } }


        public void Initialize(Vector2 currentMousePosition, Vector3 slideStart, Vector3 slideDirection, float snappingStep, Axis axis)
        {
            this.slideDirection		= slideDirection;
            this.snappingStep		= snappingStep;
            
            this.slideOrigin		= SceneHandleUtility.ProjectPointRay(Grid.ActiveGrid.Center, slideStart, slideDirection);
            this.slideExtents.min	= 			
            this.slideExtents.max	= 0;
            
            this.snappedPosition	= this.slideOrigin;
            
            this.slidePosition		= this.slideOrigin;
            this.slideOffset		= slideStart - this.slideOrigin;
            this.startOffset		= SnappingUtility.WorldPointToDistance(slideStart - this.slidePosition, slideDirection);

            this.startMousePosition = currentMousePosition;
            this.slideAxis			= axis;
            
            this.snapResult			= SnapResult1D.None;
            this.min = slideStart + SnappingUtility.DistanceToWorldPoint (slideExtents.min, slideDirection);
            this.max = slideStart + SnappingUtility.DistanceToWorldPoint (slideExtents.max, slideDirection);
        }
        
        static Extents1D GetExtentsOfPointArray(Matrix4x4 matrix, Vector3[] points, Vector3 slideOrigin, Vector3 slideDirection)
        {
            var transformedDirection = matrix.MultiplyVector(slideDirection);
            var transformedOrigin	 = matrix.MultiplyPoint(slideOrigin);

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var i = 0; i < points.Length; i++)
            {
                var distance = SnappingUtility.WorldPointToDistance(points[i], transformedDirection, transformedOrigin);
                min = Mathf.Min(min, distance);
                max = Mathf.Max(max, distance);
            }
            return new Extents1D(min, max); 
        }

        public void CalculateExtents(Matrix4x4 matrix, Vector3[] points)
        {
            this.slideExtents = GetExtentsOfPointArray(matrix, points, this.slideOrigin, this.slideDirection);
        }
        
        static readonly List<Vector3>       s_CustomSnapPoints    = new List<Vector3>();
        static readonly List<float>         s_CustomDistances     = new List<float>();
        
        public Vector3 SnapExtents1D(Vector3 currentPosition)
        {
            this.snapResult = SnapResult1D.None;

            var boundsActive    = Snapping.BoundsSnappingActive;
            var pivotActive     = Snapping.PivotSnappingActive;

            // Get custom snapping positions along the ray
            var haveCustomSnapping = Snapping.GetCustomSnappingPoints(this.slideOffset + slidePosition, this.slideDirection, 0, s_CustomSnapPoints);
            if (!boundsActive && !pivotActive && !haveCustomSnapping)
                return currentPosition;


            const float kMinPointSnap = 0.25f;
            float minPointSnap = !(boundsActive || pivotActive) ? kMinPointSnap : float.PositiveInfinity;


            // Offset to snapped position relative to the unsnapped position 
            // (used to determine which snap value is closest to unsnapped position)
            // Smallest value is used
            float snappedOffsetDistance     = float.PositiveInfinity;
            float snappedOffsetAbsDistance  = float.PositiveInfinity;


            var deltaToOrigin		= currentPosition - this.slideOrigin;
            var distanceToOrigin	= SnappingUtility.WorldPointToDistance(deltaToOrigin, this.slideDirection);

            var quantized_min_extents     = float.PositiveInfinity;
            var quantized_max_extents     = float.PositiveInfinity;
            var snappedExtents      = Extents1D.empty;
            if (boundsActive)
            {
                (float abs_distance, float snappedOffset, float quantized_min, float quantized_max) = Snapping.SnapBounds(this.slideExtents + distanceToOrigin, this.snappingStep);
                quantized_min_extents = quantized_min;
                quantized_max_extents = quantized_max;
                snappedExtents.min = this.slideExtents.min + distanceToOrigin + Mathf.Abs(quantized_min_extents);
                snappedExtents.max = this.slideExtents.min + distanceToOrigin + Mathf.Abs(quantized_max_extents);
                if (snappedOffsetAbsDistance > abs_distance) { snappedOffsetAbsDistance = abs_distance; snappedOffsetDistance = snappedOffset; }
            }

            var quantized_pivot = float.PositiveInfinity;
            if (pivotActive)
            {
                (float abs_distance, float snappedOffset, float quantized) = Snapping.SnapPivot(this.startOffset + distanceToOrigin, this.snappingStep);
                quantized_pivot = quantized;
                if (snappedOffsetAbsDistance > abs_distance) { snappedOffsetAbsDistance = abs_distance; snappedOffsetDistance = snappedOffset; }
            }

            if (haveCustomSnapping)
            {
                (float abs_distance, float snappedOffset) = Snapping.SnapCustom(s_CustomSnapPoints, this.startOffset + distanceToOrigin, this.slideDirection, minPointSnap, s_CustomDistances);
                if (snappedOffsetAbsDistance > abs_distance) { snappedOffsetAbsDistance = abs_distance; snappedOffsetDistance = snappedOffset; }
            }

            // If we didn't actually snap, just return the actual unsnapped position
            if (float.IsInfinity(snappedOffsetDistance))
                return currentPosition;

            // Snap against drag start position
            if (Mathf.Abs(snappedOffsetDistance) > Mathf.Abs(distanceToOrigin)) 
                snappedOffsetDistance = distanceToOrigin; 

            var quantizedDistance = SnappingUtility.Quantize(snappedOffsetDistance);

            // Figure out what kind of snapping visualization to show, this needs to be done afterwards since 
            // while we're snapping each type of snap can override the next one. 
            // Yet at the same time it's possible to snap with multiple snap-types at the same time.

            if (boundsActive)
            {
                if (quantizedDistance == quantized_min_extents) this.snapResult |= SnapResult1D.Min;
                if (quantizedDistance == quantized_max_extents) this.snapResult |= SnapResult1D.Max;

                min = this.slideOrigin + SnappingUtility.DistanceToWorldPoint(snappedExtents.min, this.slideDirection);
                max = this.slideOrigin + SnappingUtility.DistanceToWorldPoint(snappedExtents.max, this.slideDirection);
            }

            if (pivotActive)
            {
                if (quantizedDistance == quantized_pivot) this.snapResult |= SnapResult1D.Pivot;
            }

            if (haveCustomSnapping)
                Snapping.SendCustomSnappedEvents(quantizedDistance, s_CustomDistances, 0);


            // Calculate the new position based on the snapped offset
            var newOffset = distanceToOrigin - snappedOffsetDistance;
            var snappedDistance = SnappingUtility.DistanceToWorldPoint (newOffset, this.slideDirection);
            var snappedPosition = (snappedDistance + this.slideOrigin);
            return snappedPosition;
        }

        public bool Move(Vector2 currentMousePosition)
        {
            var distanceOnLine	= SceneHandleUtility.CalcLineTranslation(this.startMousePosition, currentMousePosition, this.slideOrigin, this.slideDirection);
            if (distanceOnLine == 0)
                return false;

            var delta = this.slideDirection * distanceOnLine;
            if (delta.sqrMagnitude == 0)
                return false;

            
            this.slidePosition	= this.slideOrigin + delta;

            var newSnappedPosition  = this.slidePosition;            
            newSnappedPosition	    = SnapExtents1D(newSnappedPosition);
            newSnappedPosition		= SnappingUtility.PerformAxisLocking(this.slidePosition, newSnappedPosition, slideAxis);

            if ((this.snappedPosition - newSnappedPosition).sqrMagnitude == 0)
                return false;

            this.snappedPosition = newSnappedPosition;
            return true;
        }
    }

    public enum SnappingMode
    {
        Default,
        Always,
        Never
    }

    // TODO: better naming
    public class Snapping2D
    {
        private Vector3     startWorldPlanePosition;
        private Plane       worldSlidePlane;
        private Grid        worldSlideGrid;

        private Extents3D	gridSlideExtents;
        private Vector3		worldSlideOrigin;

        private Vector3     worldSlidePosition;
        private Vector3     worldSnappedPosition;

        private Matrix4x4   localToWorldMatrix;

        private SnapResult3D  snapResult;
        
        public Grid			WorldSlideGrid			{ get { return worldSlideGrid; } }

        public Vector3		WorldPosition			{ get { return worldSlidePosition; } }
        public Vector3		WorldOffset				{ get { return worldSlidePosition - worldSlideOrigin; } }
        public float		WorldMagnitude			{ get { return (worldSlidePosition - worldSlideOrigin).magnitude; } }

        public Vector3		WorldSnappedPosition	{ get { return worldSnappedPosition; } }
        public Vector3		WorldSnappedDelta		{ get { return worldSnappedPosition - worldSlideOrigin; } }
        public float		WorldSnappedMagnitude	{ get { return (worldSnappedPosition - worldSlideOrigin).magnitude; } }
        
        public Vector3		GridSnappedDelta		{ get { if (worldSlideGrid == null) { return worldSlideOrigin; } return worldSlideGrid.WorldToGridSpace.MultiplyVector(worldSnappedPosition - worldSlideOrigin); } }
        public Vector3		GridSnappedPosition		{ get { if (worldSlideGrid == null) { return worldSlideOrigin; } return worldSlideGrid.WorldToGridSpace.MultiplyPoint(worldSnappedPosition); } }
        
        public Extents3D	WorldSnappedExtents		{ get { return gridSlideExtents + GridSnappedDelta; } }

        public SnapResult3D	SnapResult				{ get { return snapResult; } }

        
        public void Initialize(Grid worldSlideGrid, Vector2 currentGUIPosition, Vector3 localSlideOrigin, Matrix4x4 localToWorldMatrix)
        {
            this.localToWorldMatrix		= localToWorldMatrix;			
            this.worldSlidePosition		= this.worldSlideOrigin;
            this.worldSlideGrid			= worldSlideGrid;
            
            var worldSlideOrigin		= this.localToWorldMatrix.MultiplyPoint(localSlideOrigin);
            var gridSlideOrigin			= this.worldSlideGrid.WorldToGridSpace.MultiplyPoint(worldSlideOrigin);
            this.gridSlideExtents.min	= gridSlideOrigin;
            this.gridSlideExtents.max	= gridSlideOrigin;
            this.worldSlideOrigin		= worldSlideOrigin;
            this.worldSnappedPosition	= worldSlideOrigin;
            
            this.worldSlidePlane		= this.worldSlideGrid.PlaneXZ;
            
            this.snapResult				= SnapResult3D.None;

            GetPlaneIntersection(currentGUIPosition, out startWorldPlanePosition);
        }

        public void Reset()
        {
            startWorldPlanePosition = Vector3.zero;
            worldSlidePlane         = new Plane(Vector3.up, 0);
            worldSlideGrid          = null;

            gridSlideExtents        = new Extents3D(Vector3.zero);
            worldSlideOrigin        = Vector3.zero;

            worldSlidePosition      = Vector3.zero;
            worldSnappedPosition    = Vector3.zero;

            localToWorldMatrix      = Matrix4x4.identity;

            snapResult              = SnapResult3D.None;
#if ENABLE_DEBUG_GRID
            Grid.debugGrid          = null;
#endif
        }

        public void CalculateExtents(Vector3[] localPoints)
        {
            this.gridSlideExtents = this.worldSlideGrid.GetGridExtentsOfPointArray(localToWorldMatrix, localPoints);
        }
        
        bool GetIntersectionOnAlternativePlane(Ray worldRay, Vector3 normal, Vector3 origin, out Vector3 worldPlanePosition)
        {
            var alternativePlane = new Plane(normal, origin);
            var dist = 0.0f;
            if (!alternativePlane.SignedRaycast(worldRay, out dist))
            {
                worldPlanePosition = worldSlideOrigin;
                return false;
            }

            var tangent         = Vector3.Cross(worldSlidePlane.normal, normal);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                worldPlanePosition = worldSlideOrigin;
                return false;
            }
            tangent.Normalize();
            var fromQuaternion  = Quaternion.LookRotation(tangent, normal);
            var toQuaternion    = Quaternion.LookRotation(tangent, worldSlidePlane.normal);
            var rotation        = fromQuaternion * Quaternion.Inverse(toQuaternion);
            var worldAlternativePlanePosition = worldRay.GetPoint(dist) - origin;
            worldPlanePosition = (rotation * (worldAlternativePlanePosition)) + origin;
#if ENABLE_DEBUG_GRID
            {
                var planeOrientation = Quaternion.LookRotation(tangent, alternativePlane.normal);
                Grid.debugGrid = new Grid(Matrix4x4.TRS(origin, planeOrientation, Vector3.one));
            }
#endif
            return true;
        }


        private bool GetPlaneIntersection(Vector2 guiPosition, out Vector3 worldPlanePosition)
        {
#if ENABLE_DEBUG_GRID
            Grid.debugGrid = null;
#endif
            if (worldSlideGrid == null)
            {
                worldPlanePosition = worldSlideOrigin;
                return false;
            }

            var originSnappedPlane = new Plane(worldSlidePlane.normal, worldSlideOrigin);

            var worldRay = UnityEditor.HandleUtility.GUIPointToWorldRay(guiPosition);
            var dist = 0.0f;
            
            var camera  = Camera.current;
            var forward = camera.transform.forward;
            if (Mathf.Abs(Vector3.Dot(originSnappedPlane.normal, forward)) < 0.125f)
            {
                var normal = worldSlideGrid.GetClosestAxisVector(forward);
                var origin = originSnappedPlane.ClosestPointOnPlane(worldSlideOrigin);
                return GetIntersectionOnAlternativePlane(worldRay, normal, origin, out worldPlanePosition);
            }

            if (!originSnappedPlane.Raycast(worldRay, out dist)) { dist = float.PositiveInfinity; }

            float farClipPlaneDistance = camera.farClipPlane * 0.5f;
            if (dist > farClipPlaneDistance)
            {
                var normal = worldSlideGrid.GetClosestAxisVector(forward);
                var origin = originSnappedPlane.ClosestPointOnPlane(camera.transform.position) + (normal * farClipPlaneDistance);
                return GetIntersectionOnAlternativePlane(worldRay, normal, origin, out worldPlanePosition);
            } else
            {
                if (!originSnappedPlane.SignedRaycast(worldRay, out dist)) { worldPlanePosition = worldSlideOrigin; return false; }

                worldPlanePosition = worldRay.GetPoint(Mathf.Abs(dist));
#if ENABLE_DEBUG_GRID
                {
                    var tangent = GeometryUtility.CalculateTangent(worldSlidePlane.normal);
                    var planeOrientation = Quaternion.LookRotation(tangent, worldSlidePlane.normal);
                    Grid.debugGrid = new Grid(Matrix4x4.TRS(worldPlanePosition, planeOrientation, Vector3.one));
                }
#endif

                return true;
            }
        }

        public bool DragTo(Vector2 currentGUIPosition, SnappingMode snappingMode = SnappingMode.Default)
        {
            if (worldSlideGrid == null)
            {
                this.worldSnappedPosition = this.worldSlideOrigin;
                return false;
            }

            Vector3 worldPlanePosition;
            if (!GetPlaneIntersection(currentGUIPosition, out worldPlanePosition))
                return false;

            var worldDelta = worldPlanePosition - this.startWorldPlanePosition;
            if ((snappingMode != SnappingMode.Always) && worldDelta.sqrMagnitude == 0)
                return false;
            
            this.worldSlidePosition = this.worldSlideOrigin + worldDelta;
            var newWorldPosition	= (snappingMode == SnappingMode.Never) ? worldSlidePosition :
                    this.worldSlideGrid.SnapExtents3D(this.gridSlideExtents, this.worldSlidePosition, this.worldSlideOrigin, this.worldSlidePlane, out this.snapResult, ignoreStartPoint: (snappingMode == SnappingMode.Always));
                        
            // this doesn't make sense since we're locking axis in world space, but should be grid space, which we're already doing in SnapExtents3D?
            //newWorldPosition = SnappingUtility.PerformAxisLocking(this.worldSlideOrigin, newWorldPosition);

            if ((snappingMode != SnappingMode.Always) && (this.worldSnappedPosition - newWorldPosition).sqrMagnitude == 0)
                return false;
            
            this.worldSnappedPosition = newWorldPosition;
            return true;
        }
    }

}
