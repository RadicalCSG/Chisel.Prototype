using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public enum ExtrusionState
    {
        None,
        Cancel,
        Commit,
        Modified
    }

    public static class ExtrusionHandle
    {
        internal static int s_HeightSizingHash = "HeightSizingHash".GetHashCode();
        public static ExtrusionState DoHandle(Rect dragArea, ref Vector3 position, Axis axis, UnitySceneExtensions.SceneHandles.CapFunction capFunction = null, float? snappingSteps = null)
        {
            var id = GUIUtility.GetControlID(s_HeightSizingHash, FocusType.Keyboard);
            return ExtrusionHandle.Do(id, dragArea, ref position, axis, capFunction, snappingSteps);
        }

        static Vector3 s_StartPosition;
        static Vector2 s_StartMousePosition;
        static Vector2 s_CurrentMousePosition;
        static Snapping1D s_Snapping1D = new Snapping1D();
            
        
        static void TakeControl(int id)
        {
            GUIUtility.hotControl = id;
            GUIUtility.keyboardControl = id;
            EditorGUIUtility.SetWantsMouseJumping(1);
        }

        static void Finish(Event evt)
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            evt.Use();
            
            EditorGUIUtility.SetWantsMouseJumping(0);
        }

        static ExtrusionState Cancel(Event evt)
        {
            Finish(evt);
            return ExtrusionState.Cancel;
        }

        static ExtrusionState Commit(Event evt)
        {
            Finish(evt);
            return ExtrusionState.Commit;
        }
            
        public static ExtrusionState Do(int id, Rect dragArea, ref Vector3 position, Axis axis, UnitySceneExtensions.SceneHandles.CapFunction capFunction, float? snappingSteps = null)
        {
            if (!snappingSteps.HasValue)
                snappingSteps = UnitySceneExtensions.Snapping.MoveSnappingSteps[(int)axis];
            return Do(id, dragArea, ref position, axis, snappingSteps.Value, capFunction);
        }

        static ExtrusionState Do(int id, Rect dragArea, ref Vector3 position, Axis axis, float snappingSteps, UnitySceneExtensions.SceneHandles.CapFunction capFunction)
        {
            var transformation  = UnityEditor.Handles.matrix;
            var state           = ExtrusionState.None;
            var evt             = Event.current;
            var type            = evt.GetTypeForControl(id);
            switch (type)
            {
                case EventType.ValidateCommand: { if (evt.commandName == PointDrawing.kSoftDeleteCommand) { evt.Use(); break; } break; }
                case EventType.ExecuteCommand:	{ if (evt.commandName == PointDrawing.kSoftDeleteCommand) { state = Cancel(evt); break; } break; }
                case EventType.KeyDown:			{ if (evt.keyCode == PointDrawing.kCancelKey || 
                                                      evt.keyCode == PointDrawing.kCommitKey) { evt.Use(); break; } break; }
                case EventType.KeyUp:			{ if (evt.keyCode == PointDrawing.kCancelKey) { state = Cancel(evt); break; } else
                                                  if (evt.keyCode == PointDrawing.kCommitKey) { state = Commit(evt); break; } break; }
                    
                case EventType.MouseDrag:
                case EventType.MouseMove:
                {
                    // If we can, make current control hot
                    if (GUIUtility.hotControl == 0)
                    {
                        TakeControl(id);
                        s_StartPosition = transformation.MultiplyPoint(position); 
                        s_StartMousePosition = evt.mousePosition - evt.delta;
                        s_CurrentMousePosition = s_StartMousePosition;
                        s_Snapping1D.Initialize(evt.mousePosition,
                                                s_StartPosition, 
                                                ((Vector3)transformation.GetColumn((int)axis)).normalized,
                                                snappingSteps, axis);
                    }

                    // If another control is hot, don't do anything
                    if (GUIUtility.hotControl != id)
                        break;
                        
                    // necessary to get accurate mouse cursor position when wrapping around screen due to using EditorGUIUtility.SetWantsMouseJumping
                    s_CurrentMousePosition += evt.delta;

                    if (!s_Snapping1D.Move(s_CurrentMousePosition))
                        break;

                    position = transformation.inverse.MultiplyPoint(SnappingUtility.Quantize(s_Snapping1D.WorldSnappedPosition));
                    state = ExtrusionState.Modified;
                    break;
                }
                case EventType.Layout:
                {
                    UnityEditor.HandleUtility.AddControl(id, 0.0f);
                    break;
                }
                case EventType.MouseDown:
                {
                    if (GUIUtility.hotControl != id)
                        break;

                    evt.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id || evt.button != 0)
                        break;
                        
                    state = Commit(evt);
                    break;
                }
            }
            return state;
        }
    }
}
