using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public static class Snapping
    {
        public static event Action SnappingSettingsModified;
        
        internal static bool	SnappingToggled			{ get { return EditorGUI.actionKey; } }

        #region BoundsSnappingEnabled
        private static bool		_boundsSnappingEnabled = true;
        public static bool		BoundsSnappingEnabled
        {
            get
            {
                return _boundsSnappingEnabled;
            }
            set
            {
                if (_boundsSnappingEnabled == value)
                    return;
                _boundsSnappingEnabled = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        public static bool		BoundsSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return BoundsSnappingEnabled;
            }
        }
                
        #region PivotSnappingEnabled
        private static bool		_pivotSnappingEnabled = true;
        public static bool		PivotSnappingEnabled
        {
            get
            {
                return _pivotSnappingEnabled;
            }
            set
            {
                if (_pivotSnappingEnabled == value)
                    return;
                _pivotSnappingEnabled = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        public static bool		PivotSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return PivotSnappingEnabled;
            }
        }
        
        #region RotateSnappingEnabled
        private static bool		_rotateSnappingEnabled = true;		
        public static bool		RotateSnappingEnabled
        {
            get
            {
                return _rotateSnappingEnabled;
            }
            set
            {
                if (_rotateSnappingEnabled == value)
                    return;
                _rotateSnappingEnabled = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        public static bool		RotateSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return RotateSnappingEnabled;
            }
        }
        
        #region ScaleSnappingEnabled
        private static bool		_scaleSnappingEnabled = true;
        public static bool		ScaleSnappingEnabled
        {
            get
            {
                return _scaleSnappingEnabled;
            }
            set
            {
                if (_scaleSnappingEnabled == value)
                    return;
                _scaleSnappingEnabled = value;
                if (SnappingSettingsModified != null)
                    SnappingSettingsModified();
            }
        }
        #endregion
        public static bool		ScaleSnappingActive
        {
            get
            {
                if (SnappingToggled)
                    return !(BoundsSnappingEnabled || PivotSnappingEnabled);
                return _scaleSnappingEnabled;
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
    }

    // TODO: better naming
    public class Snapping1D
    {
        private Vector2			startMousePosition;

        private Extents1D		slideExtents;
        private Vector3			slideOrigin;
        private Vector3			slideOffset;
        private Axis			slideAxis;

        private Vector3			slidePosition;
        private Vector3			snappedPosition;

        private Vector3			slideDirection;
        private float			snappingStep;

        private SnapResult1D	snapResult;
        private float			startOffset;

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

        public void Initialize(Vector2 currentMousePosition, Vector3 slideOrigin, Vector3 slideDirection, float snappingStep, Axis axis)
        {
            this.slideDirection		= slideDirection;
            this.snappingStep		= snappingStep;
            
            this.slideOrigin		= SceneHandleUtility.ProjectPointRay(Grid.ActiveGrid.Center, slideOrigin, slideDirection);
            this.slideExtents.min	= 			
            this.slideExtents.max	= 0;
            
            this.snappedPosition	= this.slideOrigin;
            
            this.slidePosition		= this.slideOrigin;
            this.slideOffset		= slideOrigin - this.slideOrigin;
            this.startOffset		= SnappingUtility.WorldPointToDistance (this.slidePosition - slideOrigin, slideDirection);

            this.startMousePosition = currentMousePosition;
            this.slideAxis			= axis;
            
            this.snapResult			= SnapResult1D.None;
            this.min = slideOrigin + SnappingUtility.DistanceToWorldPoint (slideExtents.min, slideDirection);
            this.max = slideOrigin + SnappingUtility.DistanceToWorldPoint (slideExtents.max, slideDirection);
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
        
        
        public Vector3 SnapExtents1D(Extents1D currentExtents, Vector3 currentPosition, Vector3 slideOrigin, Vector3 slideDirection, float snappingStep, out SnapResult1D snapResult)
        {
            snapResult = SnapResult1D.None;

            var offsetPos			= currentPosition - slideOrigin;
            var offsetDistance		= SnappingUtility.WorldPointToDistance(offsetPos, slideDirection);
            var currDistance		= offsetDistance - this.startOffset;
            var movedExtents		= currentExtents + offsetDistance;
            
            var snappedExtents		= movedExtents;
            snappedExtents.min		= SnappingUtility.SnapValue(movedExtents.min, snappingStep);
            snappedExtents.max		= SnappingUtility.SnapValue(movedExtents.max, snappingStep);

            var snappedExtentsOffset = snappedExtents - movedExtents;
            var snappedPivot		 = SnappingUtility.SnapValue(currDistance, snappingStep) - currDistance;
            
            if (!Snapping.BoundsSnappingActive && !Snapping.PivotSnappingActive)
                return currentPosition;
            var abs_pivot		= Snapping.PivotSnappingActive  ? SnappingUtility.Quantize(Mathf.Abs(snappedPivot            )) : float.PositiveInfinity;
            var abs_min_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedExtentsOffset.min)) : float.PositiveInfinity;
            var abs_max_extents = Snapping.BoundsSnappingActive ? SnappingUtility.Quantize(Mathf.Abs(snappedExtentsOffset.max)) : float.PositiveInfinity;
            var snappedOffsetDistance = (abs_pivot < abs_min_extents && abs_pivot < abs_max_extents) ? snappedPivot : ((abs_min_extents < abs_max_extents) ? snappedExtentsOffset.min : snappedExtentsOffset.max);
            if (abs_min_extents <= abs_max_extents && abs_min_extents <= abs_pivot) snapResult |= SnapResult1D.Min;
            if (abs_max_extents <= abs_min_extents && abs_max_extents <= abs_pivot) snapResult |= SnapResult1D.Max;
            if (abs_pivot       <= abs_min_extents && abs_pivot <= abs_max_extents) snapResult |= SnapResult1D.Pivot;
            
            min = slideOrigin + SnappingUtility.DistanceToWorldPoint (snappedExtents.min, slideDirection);
            max = slideOrigin + SnappingUtility.DistanceToWorldPoint (snappedExtents.max, slideDirection);

            var newOffset = offsetDistance + snappedOffsetDistance;
            if (Mathf.Abs(snappedOffsetDistance) > Mathf.Abs(offsetDistance)) newOffset = 0;

            var snappedDistance = SnappingUtility.DistanceToWorldPoint (newOffset, slideDirection);
            var snappedPosition = (snappedDistance + slideOrigin);
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

            var newSnappedPosition = this.slidePosition;
            
            //if (Snapping.BoundsSnappingActive)
                newSnappedPosition	= SnapExtents1D(this.slideExtents, newSnappedPosition, this.slideOrigin, this.slideDirection, this.snappingStep, out this.snapResult);
            //else
            //	this.snapResult = SnapResult1D.None;

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
        }

        public void CalculateExtents(Vector3[] localPoints)
        {
            this.gridSlideExtents = this.worldSlideGrid.GetGridExtentsOfPointArray(localToWorldMatrix, localPoints);
        }
        
        bool GetIntersectionOnAlternativePlane(Ray worldRay, Vector3 normal, Vector3 origin, out Vector3 worldPlanePosition)
        {
            var rotation    = Quaternion.FromToRotation(normal, worldSlidePlane.normal);
            var alternativePlane = new Plane(normal, origin);
            var dist = 0.0f;
            if (!alternativePlane.SignedRaycast(worldRay, out dist)) { worldPlanePosition = worldSlideOrigin; return false; }

            worldPlanePosition = (rotation * (worldRay.GetPoint(dist) - origin)) + origin;
            return true;
        }


        private bool GetPlaneIntersection(Vector2 guiPosition, out Vector3 worldPlanePosition)
        {
            if (worldSlideGrid == null)
            {
                worldPlanePosition = worldSlideOrigin;
                return false;
            }

            var worldRay = UnityEditor.HandleUtility.GUIPointToWorldRay(guiPosition);
            var dist = 0.0f;

            var camera  = Camera.current;
            var forward = camera.transform.forward;
            if (Mathf.Abs(Vector3.Dot(worldSlidePlane.normal, forward)) < 0.125f)
            {
                var normal = worldSlideGrid.GetClosestAxisVector(forward);
                var origin = worldSlidePlane.ClosestPointOnPlane(worldSlideOrigin);
                return GetIntersectionOnAlternativePlane(worldRay, normal, origin, out worldPlanePosition);
            }

            if (!worldSlidePlane.Raycast(worldRay, out dist)) { dist = float.PositiveInfinity; }
            if (dist > camera.farClipPlane)
            {
                var normal = worldSlideGrid.GetClosestAxisVector(forward);
                var origin = worldSlidePlane.ClosestPointOnPlane(camera.transform.position) + (normal * camera.farClipPlane);
                return GetIntersectionOnAlternativePlane(worldRay, normal, origin, out worldPlanePosition);
            } else
            { 
                if (!worldSlidePlane.SignedRaycast(worldRay, out dist)) { worldPlanePosition = worldSlideOrigin; return false; }
                worldPlanePosition = worldRay.GetPoint(dist);
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
                    this.worldSlideGrid.SnapExtents3D(this.gridSlideExtents, this.worldSlidePosition, this.worldSlideOrigin, out this.snapResult, ignoreStartPoint: (snappingMode == SnappingMode.Always));
                        
            // this doesn't make sense since we're locking axis in world space, but should be grid space, which we're already doing in SnapExtents3D?
            //newWorldPosition = SnappingUtility.PerformAxisLocking(this.worldSlideOrigin, newWorldPosition);

            if ((snappingMode != SnappingMode.Always) && (this.worldSnappedPosition - newWorldPosition).sqrMagnitude == 0)
                return false;
            
            this.worldSnappedPosition = newWorldPosition;
            return true;
        }
    }

}
