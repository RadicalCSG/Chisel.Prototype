using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
	public sealed partial class SceneHandles
	{
		internal static int s_xAxisMoveHandleHash	= "xAxisFreeMoveHandleHash".GetHashCode();
		internal static int s_yAxisMoveHandleHash	= "yAxisFreeMoveHandleHash".GetHashCode();
		internal static int s_zAxisMoveHandleHash	= "zAxisFreeMoveHandleHash".GetHashCode();
		internal static int s_xzAxisMoveHandleHash	= "xzAxesFreeMoveHandleHash".GetHashCode();
		internal static int s_xyAxisMoveHandleHash	= "xyAxesFreeMoveHandleHash".GetHashCode();
		internal static int s_yzAxisMoveHandleHash	= "yzAxesFreeMoveHandleHash".GetHashCode();
		internal static int s_centerMoveHandleHash  = "centerFreeMoveHandleHash".GetHashCode();


		public static Vector3[] PositionHandle(Vector3[] points, Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
		{
			GUI.SetNextControlName("xAxis");   var xAxisId   = GUIUtility.GetControlID (s_xAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("yAxis");   var yAxisId   = GUIUtility.GetControlID (s_yAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("zAxis");   var zAxisId   = GUIUtility.GetControlID (s_zAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("xzPlane"); var xzPlaneId = GUIUtility.GetControlID (s_xzAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("xyPlane"); var xyPlaneId = GUIUtility.GetControlID (s_xyAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("yzPlane"); var yzPlaneId = GUIUtility.GetControlID (s_yzAxisMoveHandleHash, FocusType.Passive);
			GUI.SetNextControlName("center");  var centerId  = GUIUtility.GetControlID (s_centerMoveHandleHash, FocusType.Passive);
			
			var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectUtility.ContainsStatic(Selection.gameObjects));
			var prevDisabled	= SceneHandles.disabled;

			var hotControl		= GUIUtility.hotControl;

			var xAxisIsHot		= (xAxisId   == hotControl);
			var yAxisIsHot		= (yAxisId   == hotControl);
			var zAxisIsHot		= (zAxisId   == hotControl);
			var xzAxisIsHot		= (xzPlaneId == hotControl);
			var xyAxisIsHot		= (xyPlaneId == hotControl);
			var yzAxisIsHot		= (yzPlaneId == hotControl);
			var centerIsHot		= (centerId  == hotControl);

			var isControlHot	= xAxisIsHot || yAxisIsHot || zAxisIsHot || xzAxisIsHot || xyAxisIsHot || yzAxisIsHot || centerIsHot;

			var handleSize		= UnityEditor.HandleUtility.GetHandleSize(position);
			var originalColor	= SceneHandles.color;

			var activeAxes		= Snapping.ActiveAxes;
			
			UnityEditor.HandleUtility.AddControl(centerId, UnityEditor.HandleUtility.DistanceToCircle(position, handleSize * 0.055f));


			var evt = Event.current;
			var type = evt.GetTypeForControl(centerId);
			switch (type)
			{
				case EventType.MouseDown:
				{
					if (GUIUtility.hotControl != 0)
						break;

					if ((UnityEditor.HandleUtility.nearestControl != centerId || evt.button != 0) &&
						(GUIUtility.keyboardControl != centerId || evt.button != 2))
						break;

					GUIUtility.hotControl = GUIUtility.keyboardControl = centerId;
					evt.Use();
					EditorGUIUtility.SetWantsMouseJumping(1);
					break;
				}
				case EventType.MouseDrag:
				{
					if (GUIUtility.hotControl != centerId)
						break;
					break;
				}
				case EventType.MouseUp:
				{
					if (GUIUtility.hotControl == centerId && (evt.button == 0 || evt.button == 2))
					{
						GUIUtility.hotControl = 0;
						GUIUtility.keyboardControl = 0;
						evt.Use();
						Snapping.ActiveAxes = Axes.XYZ;
						EditorGUIUtility.SetWantsMouseJumping(0);
						SceneView.RepaintAll();
					}
					break;
				}
			}
			
			//,.,.., look at 2018.1 how the position handle works w/ colors
			
			var xAxisDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.X) == 0) || Snapping.AxisLocking[0] || (isControlHot && !xAxisIsHot && !xzAxisIsHot && !xyAxisIsHot);
			var yAxisDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.Y) == 0) || Snapping.AxisLocking[1] || (isControlHot && !yAxisIsHot && !xyAxisIsHot && !yzAxisIsHot);
			var zAxisDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.Z) == 0) || Snapping.AxisLocking[2] || (isControlHot && !zAxisIsHot && !xzAxisIsHot && !yzAxisIsHot);
			var xzPlaneDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.XZ) != Axes.XZ) || (Snapping.AxisLocking[0] || Snapping.AxisLocking[2]) || (isControlHot && !xzAxisIsHot);
			var xyPlaneDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.XY) != Axes.XY) || (Snapping.AxisLocking[0] || Snapping.AxisLocking[1]) || (isControlHot && !xyAxisIsHot);
			var yzPlaneDisabled	= isStatic || prevDisabled || ((enabledAxes & Axes.YZ) != Axes.YZ) || (Snapping.AxisLocking[1] || Snapping.AxisLocking[2]) || (isControlHot && !yzAxisIsHot);
			
			var currentFocusControl = SceneHandleUtility.focusControl;

			var xAxisIndirectlyFocused = (currentFocusControl == xyPlaneId || currentFocusControl == xzPlaneId);
			var yAxisIndirectlyFocused = (currentFocusControl == xyPlaneId || currentFocusControl == yzPlaneId);
			var zAxisIndirectlyFocused = (currentFocusControl == xzPlaneId || currentFocusControl == yzPlaneId);

			var xAxisIndirectlyActive = activeAxes == Axes.XY || activeAxes == Axes.XZ;
			var yAxisIndirectlyActive = activeAxes == Axes.XY || activeAxes == Axes.YZ;
			var zAxisIndirectlyActive = activeAxes == Axes.XZ || activeAxes == Axes.YZ;

			var xAxisSelected	= xAxisIndirectlyFocused || xAxisIndirectlyActive || activeAxes == Axes.X;
			var yAxisSelected	= yAxisIndirectlyFocused || yAxisIndirectlyActive || activeAxes == Axes.Y;
			var zAxisSelected	= zAxisIndirectlyFocused || zAxisIndirectlyActive || activeAxes == Axes.Z;
			var xzAxiSelected	= activeAxes == Axes.XZ;
			var xyAxiSelected	= activeAxes == Axes.XZ;
			var yzAxiSelected	= activeAxes == Axes.YZ;

			var xAxisColor		= SceneHandles.StateColor(SceneHandles.xAxisColor, xAxisDisabled, xAxisSelected);
			var yAxisColor		= SceneHandles.StateColor(SceneHandles.yAxisColor, yAxisDisabled, yAxisSelected);
			var zAxisColor		= SceneHandles.StateColor(SceneHandles.zAxisColor, zAxisDisabled, zAxisSelected);
			var xzPlaneColor	= SceneHandles.StateColor(SceneHandles.yAxisColor, xzPlaneDisabled, xzAxiSelected);
			var xyPlaneColor	= SceneHandles.StateColor(SceneHandles.zAxisColor, xyPlaneDisabled, xyAxiSelected);
			var yzPlaneColor	= SceneHandles.StateColor(SceneHandles.xAxisColor, yzPlaneDisabled, yzAxiSelected);
			

			SceneHandles.disabled = xAxisDisabled;
			SceneHandles.color = xAxisColor;
			points = Slider1DHandle(xAxisId, Axis.X, points, position, rotation * Vector3.right,   Snapping.MoveSnappingSteps.x, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);
			   
			SceneHandles.disabled = yAxisDisabled;
			SceneHandles.color = yAxisColor;
			points = Slider1DHandle(yAxisId, Axis.Y, points, position, rotation * Vector3.up,      Snapping.MoveSnappingSteps.y, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);
			
			SceneHandles.disabled = zAxisDisabled;
			SceneHandles.color = zAxisColor;
			points = Slider1DHandle(zAxisId, Axis.Z, points, position, rotation * Vector3.forward, Snapping.MoveSnappingSteps.z, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);


			SceneHandles.disabled = xzPlaneDisabled;
			SceneHandles.color = xzPlaneColor;
			points = PlanarHandle(xzPlaneId, PlaneAxes.XZ, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);

			SceneHandles.disabled = xyPlaneDisabled;
			SceneHandles.color = xyPlaneColor;
			points = PlanarHandle(xyPlaneId, PlaneAxes.XY, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);

			SceneHandles.disabled = yzPlaneDisabled;
			SceneHandles.color = yzPlaneColor;
			points = PlanarHandle(yzPlaneId, PlaneAxes.YZ, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);


			switch (type)
			{
				case EventType.Repaint:
				{
					SceneHandles.color = SceneHandles.StateColor(SceneHandles.centerColor, false, centerId == SceneHandleUtility.focusControl);
					SceneHandles.RenderBorderedCircle(position, handleSize * 0.05f);
					break;
				}
			}



			SceneHandles.disabled = prevDisabled;
			SceneHandles.color = originalColor;

			return points;
		}

		public static Vector3 PositionHandle(Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
		{
			return PositionHandle(new[] { position }, position, rotation, enabledAxes)[0];
		}
	}
}
