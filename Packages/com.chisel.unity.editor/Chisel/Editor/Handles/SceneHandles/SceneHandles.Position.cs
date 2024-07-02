using System;
using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    [Flags]
    public enum ControlState : byte
    {
        None            = 0,
        Disabled        = 1,
        Hot             = 4,
        Focused         = 8,
        Locked          = 16,
        Active          = 32,
        Selected = Active | Focused
    }

    public sealed partial class SceneHandles
    {
        internal static int s_xAxisMoveHandleHash	= "xAxisFreeMoveHandleHash".GetHashCode();
        internal static int s_yAxisMoveHandleHash	= "yAxisFreeMoveHandleHash".GetHashCode();
        internal static int s_zAxisMoveHandleHash	= "zAxisFreeMoveHandleHash".GetHashCode();
        internal static int s_xzAxisMoveHandleHash	= "xzAxesFreeMoveHandleHash".GetHashCode();
        internal static int s_xyAxisMoveHandleHash	= "xyAxesFreeMoveHandleHash".GetHashCode();
        internal static int s_yzAxisMoveHandleHash	= "yzAxesFreeMoveHandleHash".GetHashCode();
        internal static int s_centerMoveHandleHash  = "centerFreeMoveHandleHash".GetHashCode();

        public struct PositionHandleIDs
        {
            public int xAxisId;
            public int yAxisId;
            public int zAxisId;
            public int xzPlaneId;
            public int xyPlaneId;
            public int yzPlaneId;
            public int centerId;

            public Vector3 originalPosition;

            public bool Contains(int id)
            {
                return  id == xAxisId   || id == yAxisId   || id == zAxisId ||
                        id == xzPlaneId || id == xyPlaneId || id == yzPlaneId ||
                        id == centerId;
            }


            public Axes HotAxes
            {
                get
                {
                    var hotControl = GUIUtility.hotControl;
                    return  (xAxisId == hotControl) ? Axes.X :
                            (yAxisId == hotControl) ? Axes.Y :
                            (zAxisId == hotControl) ? Axes.Z :
                            (xzPlaneId == hotControl) ? Axes.XZ :
                            (xyPlaneId == hotControl) ? Axes.XY :
                            (yzPlaneId == hotControl) ? Axes.YZ :
                            Axes.None;
                }
            }

            public ControlState xAxisState;
            public ControlState yAxisState;
            public ControlState zAxisState;
            public ControlState xzPlaneState;
            public ControlState xyPlaneState;
            public ControlState yzPlaneState;
            public ControlState centerState;

            public ControlState xAxisIndirectState  { get { return (ControlState)(((int)xAxisState) | ((int)xyPlaneState) | ((int)xzPlaneState)); } }
            public ControlState yAxisIndirectState  { get { return (ControlState)(((int)yAxisState) | ((int)xyPlaneState) | ((int)yzPlaneState)); } }
            public ControlState zAxisIndirectState  { get { return (ControlState)(((int)zAxisState) | ((int)yzPlaneState) | ((int)xzPlaneState)); } }
            public ControlState combinedState       { get { return (ControlState)(((int)xAxisState) | ((int)yAxisState) | ((int)zAxisState) | ((int)xyPlaneState) | ((int)yzPlaneState) | ((int)xzPlaneState) | ((int)centerState)); } }
        }

        public static void Initialize(ref PositionHandleIDs handleIDs)
        {
            GUI.SetNextControlName("xAxis");   handleIDs.xAxisId   = GUIUtility.GetControlID (s_xAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("yAxis");   handleIDs.yAxisId   = GUIUtility.GetControlID (s_yAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("zAxis");   handleIDs.zAxisId   = GUIUtility.GetControlID (s_zAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("xzPlane"); handleIDs.xzPlaneId = GUIUtility.GetControlID (s_xzAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("xyPlane"); handleIDs.xyPlaneId = GUIUtility.GetControlID (s_xyAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("yzPlane"); handleIDs.yzPlaneId = GUIUtility.GetControlID (s_yzAxisMoveHandleHash, FocusType.Passive);
            GUI.SetNextControlName("center");  handleIDs.centerId  = GUIUtility.GetControlID (s_centerMoveHandleHash, FocusType.Passive);

            handleIDs.xAxisState    = ControlState.None;
            handleIDs.yAxisState    = ControlState.None;
            handleIDs.zAxisState    = ControlState.None;
            handleIDs.xzPlaneState  = ControlState.None;
            handleIDs.xyPlaneState  = ControlState.None;
            handleIDs.yzPlaneState  = ControlState.None;
            handleIDs.centerState   = ControlState.None;

            var hotControl		= GUIUtility.hotControl;
            if (handleIDs.xAxisId   == hotControl) handleIDs.xAxisState   |= ControlState.Hot;
            if (handleIDs.yAxisId   == hotControl) handleIDs.yAxisState   |= ControlState.Hot;
            if (handleIDs.zAxisId   == hotControl) handleIDs.zAxisState   |= ControlState.Hot;
            if (handleIDs.xzPlaneId == hotControl) handleIDs.xzPlaneState |= ControlState.Hot;
            if (handleIDs.xyPlaneId == hotControl) handleIDs.xyPlaneState |= ControlState.Hot;
            if (handleIDs.yzPlaneId == hotControl) handleIDs.yzPlaneState |= ControlState.Hot;
            if (handleIDs.centerId  == hotControl) handleIDs.centerState  |= ControlState.Hot;
            var xzPlaneIsHot	    = (handleIDs.xzPlaneState & ControlState.Hot) == ControlState.Hot;
            var xyPlaneIsHot	    = (handleIDs.xyPlaneState & ControlState.Hot) == ControlState.Hot;
            var yzPlaneIsHot	    = (handleIDs.yzPlaneState & ControlState.Hot) == ControlState.Hot;
            var xAxisIndirectlyHot  = (handleIDs.xAxisIndirectState & ControlState.Hot) == ControlState.Hot;
            var yAxisIndirectlyHot  = (handleIDs.yAxisIndirectState & ControlState.Hot) == ControlState.Hot;
            var zAxisIndirectlyHot  = (handleIDs.zAxisIndirectState & ControlState.Hot) == ControlState.Hot;
            var centerIsHot         = (handleIDs.centerState & ControlState.Hot) == ControlState.Hot;
            var isAnyHot	        = (handleIDs.combinedState & ControlState.Hot) == ControlState.Hot;
            
            var focusControl = SceneHandleUtility.focusControl;            
            if (handleIDs.xAxisId   == focusControl) handleIDs.xAxisState   |= ControlState.Focused;
            if (handleIDs.yAxisId   == focusControl) handleIDs.yAxisState   |= ControlState.Focused;
            if (handleIDs.zAxisId   == focusControl) handleIDs.zAxisState   |= ControlState.Focused;
            if (handleIDs.xzPlaneId == focusControl) handleIDs.xzPlaneState |= ControlState.Focused;
            if (handleIDs.xyPlaneId == focusControl) handleIDs.xyPlaneState |= ControlState.Focused;
            if (handleIDs.yzPlaneId == focusControl) handleIDs.yzPlaneState |= ControlState.Focused;
            if (handleIDs.centerId  == focusControl) handleIDs.centerState  |= ControlState.Focused;
            
            var xAxisLocked	    = Snapping.AxisLocking[0];
            var yAxisLocked     = Snapping.AxisLocking[1];
            var zAxisLocked     = Snapping.AxisLocking[2];
            var xzPlaneLocked   = xAxisLocked && zAxisLocked;
            var xyPlaneLocked   = xAxisLocked && yAxisLocked;
            var yzPlaneLocked   = yAxisLocked && zAxisLocked;
            if (xAxisLocked  ) handleIDs.xAxisState   |= ControlState.Locked;
            if (yAxisLocked  ) handleIDs.yAxisState   |= ControlState.Locked;
            if (zAxisLocked  ) handleIDs.zAxisState   |= ControlState.Locked;
            if (xzPlaneLocked) handleIDs.xzPlaneState |= ControlState.Locked;
            if (xyPlaneLocked) handleIDs.xyPlaneState |= ControlState.Locked;
            if (yzPlaneLocked) handleIDs.yzPlaneState |= ControlState.Locked;

            var activeAxes      = Snapping.ActiveAxes;
            if (activeAxes == Axes.X ) handleIDs.xAxisState   |= ControlState.Active;
            if (activeAxes == Axes.Y ) handleIDs.yAxisState   |= ControlState.Active;
            if (activeAxes == Axes.Z ) handleIDs.zAxisState   |= ControlState.Active;
            if (activeAxes == Axes.XZ) handleIDs.xzPlaneState |= ControlState.Active;
            if (activeAxes == Axes.XY) handleIDs.xyPlaneState |= ControlState.Active;
            if (activeAxes == Axes.YZ) handleIDs.yzPlaneState |= ControlState.Active;
            
            var isStatic		= (!Tools.hidden && EditorApplication.isPlaying && GameObjectExtensions.ContainsStatic(Selection.gameObjects));
            var isDisabled	    = SceneHandles.disabled;

            var xAxisDisabled	= isStatic || isDisabled || xAxisLocked   || (isAnyHot && !xAxisIndirectlyHot);
            var yAxisDisabled	= isStatic || isDisabled || yAxisLocked   || (isAnyHot && !yAxisIndirectlyHot);
            var zAxisDisabled	= isStatic || isDisabled || zAxisLocked   || (isAnyHot && !zAxisIndirectlyHot);
            var xzPlaneDisabled	= isStatic || isDisabled || xzPlaneLocked || (isAnyHot && !xzPlaneIsHot);
            var xyPlaneDisabled	= isStatic || isDisabled || xyPlaneLocked || (isAnyHot && !xyPlaneIsHot);
            var yzPlaneDisabled	= isStatic || isDisabled || yzPlaneLocked || (isAnyHot && !yzPlaneIsHot);
            var centerDisabled	= isStatic || isDisabled || (isAnyHot && !centerIsHot);
            if (xAxisDisabled  ) handleIDs.xAxisState   |= ControlState.Disabled;
            if (yAxisDisabled  ) handleIDs.yAxisState   |= ControlState.Disabled;
            if (zAxisDisabled  ) handleIDs.zAxisState   |= ControlState.Disabled;
            if (xzPlaneDisabled) handleIDs.xzPlaneState |= ControlState.Disabled;
            if (xyPlaneDisabled) handleIDs.xyPlaneState |= ControlState.Disabled;
            if (yzPlaneDisabled) handleIDs.yzPlaneState |= ControlState.Disabled;
            if (centerDisabled ) handleIDs.centerState  |= ControlState.Disabled;
        }

        public static Vector3[] PositionHandle(Vector3[] points, Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
        {
            var handleIDs = new PositionHandleIDs();
            Initialize(ref handleIDs);
            return PositionHandle(ref handleIDs, points, position, rotation, enabledAxes);
        }

        public static Vector3[] PositionHandle(ref PositionHandleIDs handleIDs, Vector3[] points, Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
        {
            var xAxisId   = handleIDs.xAxisId;
            var yAxisId   = handleIDs.yAxisId;
            var zAxisId   = handleIDs.zAxisId;
            var xzPlaneId = handleIDs.xzPlaneId;
            var xyPlaneId = handleIDs.xyPlaneId;
            var yzPlaneId = handleIDs.yzPlaneId;
            var centerId  = handleIDs.centerId;
 
            var originalColor	= SceneHandles.color;
            
            var handleSize		= UnityEditor.HandleUtility.GetHandleSize(position);
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

                    handleIDs.originalPosition = position;
                    GUIUtility.hotControl = GUIUtility.keyboardControl = centerId;
                    evt.Use();
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    break;
                }
                case EventType.MouseMove:
                {
                    handleIDs.originalPosition = position;
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
                        handleIDs.originalPosition = position;
                    }
                    break;
                }
            }

            //,.,.., look at 2018.1 how the position handle works w/ colors
            
            var xAxisLocked	    = (handleIDs.xAxisState   & ControlState.Locked) == ControlState.Locked;
            var yAxisLocked     = (handleIDs.yAxisState   & ControlState.Locked) == ControlState.Locked;
            var zAxisLocked     = (handleIDs.zAxisState   & ControlState.Locked) == ControlState.Locked;
            var xzPlaneLocked   = (handleIDs.xzPlaneState & ControlState.Locked) == ControlState.Locked;
            var xyPlaneLocked   = (handleIDs.xyPlaneState & ControlState.Locked) == ControlState.Locked;
            var yzPlaneLocked   = (handleIDs.yzPlaneState & ControlState.Locked) == ControlState.Locked;

            var xAxisDisabled   = ((enabledAxes & Axes.X ) != Axes.X ) || (handleIDs.xAxisState   & ControlState.Disabled) == ControlState.Disabled;
            var yAxisDisabled   = ((enabledAxes & Axes.Y ) != Axes.Y ) || (handleIDs.yAxisState   & ControlState.Disabled) == ControlState.Disabled;
            var zAxisDisabled   = ((enabledAxes & Axes.Z ) != Axes.Z ) || (handleIDs.zAxisState   & ControlState.Disabled) == ControlState.Disabled;
            var xyPlaneDisabled = ((enabledAxes & Axes.XY) != Axes.XY) || (handleIDs.xyPlaneState & ControlState.Disabled) == ControlState.Disabled;
            var yzPlaneDisabled = ((enabledAxes & Axes.YZ) != Axes.YZ) || (handleIDs.yzPlaneState & ControlState.Disabled) == ControlState.Disabled;
            var xzPlaneDisabled = ((enabledAxes & Axes.XZ) != Axes.XZ) || (handleIDs.xzPlaneState & ControlState.Disabled) == ControlState.Disabled;

            var xAxisSelected	= (handleIDs.xAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None;
            var yAxisSelected	= (handleIDs.yAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None;
            var zAxisSelected	= (handleIDs.zAxisIndirectState & (ControlState.Focused | ControlState.Active)) != ControlState.None;
            var xzPlaneSelected	= (handleIDs.xzPlaneState & (ControlState.Focused | ControlState.Active)) != ControlState.None;
            var xyPlaneSelected	= (handleIDs.xyPlaneState & (ControlState.Focused | ControlState.Active)) != ControlState.None;
            var yzPlaneSelected	= (handleIDs.yzPlaneState & (ControlState.Focused | ControlState.Active)) != ControlState.None;

            var xAxisColor		= SceneHandles.StateColor(SceneHandles.xAxisColor, xAxisDisabled,   xAxisSelected);
            var yAxisColor		= SceneHandles.StateColor(SceneHandles.yAxisColor, yAxisDisabled,   yAxisSelected);
            var zAxisColor		= SceneHandles.StateColor(SceneHandles.zAxisColor, zAxisDisabled,   zAxisSelected);
            var xzPlaneColor	= SceneHandles.StateColor(SceneHandles.yAxisColor, xzPlaneDisabled, xzPlaneSelected);
            var xyPlaneColor	= SceneHandles.StateColor(SceneHandles.zAxisColor, xyPlaneDisabled, xyPlaneSelected);
            var yzPlaneColor	= SceneHandles.StateColor(SceneHandles.xAxisColor, yzPlaneDisabled, yzPlaneSelected);


            var prevDisabled = SceneHandles.disabled;

            if (!xAxisLocked)
            {
                SceneHandles.disabled = xAxisDisabled;
                SceneHandles.color = xAxisColor;
                points = Slider1DHandle(xAxisId, Axis.X, points, position, rotation * Vector3.right, Snapping.MoveSnappingSteps.x, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);
            }

            if (!yAxisLocked)
            {
                SceneHandles.disabled = yAxisDisabled;
                SceneHandles.color = yAxisColor;
                points = Slider1DHandle(yAxisId, Axis.Y, points, position, rotation * Vector3.up, Snapping.MoveSnappingSteps.y, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);
            }

            if (!zAxisLocked)
            {
                SceneHandles.disabled = zAxisDisabled;
                SceneHandles.color = zAxisColor;
                points = Slider1DHandle(zAxisId, Axis.Z, points, position, rotation * Vector3.forward, Snapping.MoveSnappingSteps.z, handleSize, ArrowHandleCap, selectLockingAxisOnClick: true);
            }

            if (!xzPlaneLocked)
            {
                SceneHandles.disabled = xzPlaneDisabled;
                SceneHandles.color = xzPlaneColor;
                points = PlanarHandle(xzPlaneId, PlaneAxes.XZ, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);
            }

            if (!xyPlaneLocked)
            {
                SceneHandles.disabled = xyPlaneDisabled;
                SceneHandles.color = xyPlaneColor;
                points = PlanarHandle(xyPlaneId, PlaneAxes.XY, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);
            }

            if (!yzPlaneLocked)
            {
                SceneHandles.disabled = yzPlaneDisabled;
                SceneHandles.color = yzPlaneColor;
                points = PlanarHandle(yzPlaneId, PlaneAxes.YZ, points, position, rotation, handleSize * 0.3f, selectLockingAxisOnClick: true);
            }


            if ((handleIDs.centerState & ControlState.Disabled) != ControlState.Disabled)
            {
                switch (type)
                {
                    case EventType.Repaint:
                    {
                        var focused = (handleIDs.centerState & ControlState.Focused) == ControlState.Focused;
                        SceneHandles.color = SceneHandles.StateColor(SceneHandles.centerColor, false, focused);
                        SceneHandles.RenderBorderedCircle(position, handleSize * 0.05f);
                        break;
                    }
                }
            }


            SceneHandles.disabled = prevDisabled;
            SceneHandles.color = originalColor;

            return points;
        }


        static readonly Vector3[] s_PositionHandleArray = new Vector3[1];
        public static Vector3 PositionHandle(Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
        {
            s_PositionHandleArray[0] = position;
            return PositionHandle(s_PositionHandleArray, position, rotation, enabledAxes)[0];
        }

        public static Vector3 PositionHandle(Vector3 position, Axes enabledAxes = Axes.XYZ)
        {
            var activeGrid  = Grid.ActiveGrid;
            var rotation    = Quaternion.LookRotation(activeGrid.Forward, activeGrid.Up);
            s_PositionHandleArray[0] = position;
            return PositionHandle(s_PositionHandleArray, position, rotation, enabledAxes)[0];
        }

        public static Vector3 PositionHandle(ref PositionHandleIDs handleIDs, Vector3 position, Axes enabledAxes = Axes.XYZ)
        {
            var activeGrid = Grid.ActiveGrid;
            var rotation = Quaternion.LookRotation(activeGrid.Forward, activeGrid.Up);
            s_PositionHandleArray[0] = position;
            return PositionHandle(ref handleIDs, s_PositionHandleArray, position, rotation, enabledAxes)[0];
        }

        public static Vector3 PositionHandle(ref PositionHandleIDs handleIDs, Vector3 position, Quaternion rotation, Axes enabledAxes = Axes.XYZ)
        {
            s_PositionHandleArray[0] = position;
            return PositionHandle(ref handleIDs, s_PositionHandleArray, position, rotation, enabledAxes)[0];
        }

        public static Vector3 PositionHandleOffset(Vector3 position, Axes enabledAxes = Axes.XYZ)
        {
            return PositionHandle(position, enabledAxes) - position;
        }

        public static Vector3 PositionHandleOffset(ref PositionHandleIDs handleIDs, Vector3 position, Axes enabledAxes = Axes.XYZ)
        {
            return PositionHandle(ref handleIDs, position, enabledAxes) - position;
        }
    }
}
