using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public enum KeyEventType
    { 		
        [KeyDescription("Tools/Object mode",				KeyCode.F1, EventModifiers.Control)]		SwitchToObjectEditMode,
        [KeyDescription("Tools/Pivot mode",					KeyCode.F2, EventModifiers.Control)]		SwitchToPivotEditMode,
        [KeyDescription("Tools/Shape Edit mode",			KeyCode.F3, EventModifiers.Control)]		SwitchToShapeEditMode,
        [KeyDescription("Tools/Surface Edit mode",			KeyCode.F4, EventModifiers.Control)]		SwitchToSurfaceEditMode,


        // TODO: assign reasonable keys
        [KeyDescription("Generate/Free draw",				KeyCode.F5,  EventModifiers.Control)]		FreeBuilderMode,
        [KeyDescription("Generate/Revolved Shape",			KeyCode.F5,  EventModifiers.Control)]		RevolvedShapeBuilderMode,
        [KeyDescription("Generate/Box",						KeyCode.F6,  EventModifiers.Control)]		BoxBuilderMode,
        [KeyDescription("Generate/Cylinder",				KeyCode.F7,  EventModifiers.Control)]		CylinderBuilderMode,
        [KeyDescription("Generate/Hemisphere",				KeyCode.F9,  EventModifiers.Control)]		SphereBuilderMode,
        [KeyDescription("Generate/Sphere",					KeyCode.F10, EventModifiers.Control)]		HemisphereBuilderMode,
        [KeyDescription("Generate/Torus",					KeyCode.F11, EventModifiers.Control)]		TorusBuilderMode,
        [KeyDescription("Generate/Capsule",					KeyCode.F12, EventModifiers.Control)]		CapsuleBuilderMode,
        [KeyDescription("Generate/Stadium",					KeyCode.F12, EventModifiers.Control)]		StadiumBuilderMode,
        
        [KeyDescription("Generate/Pathed Stairs",			KeyCode.F13, EventModifiers.Control)]		PathedStairsBuilderMode,
        [KeyDescription("Generate/Linear Stairs",			KeyCode.F13, EventModifiers.Control)]		LinearStairsBuilderMode,
        [KeyDescription("Generate/Spiral Stairs",			KeyCode.F14, EventModifiers.Control)]		SpiralStairsBuilderMode,


        //[KeyDescription("Object mode/Center pivot",		KeyCode.R, EventModifiers.Control)]			CenterPivot,
        //[KeyDescription("Mesh mode/Merge edge points",	KeyCode.M)]									MergeEdgePoints,

        //[KeyDescription("Surface mode/Smear or copy material", KeyCode.G, KeyOptions.Hold)]				CopyMaterialTexGen,

        //[KeyDescription("Clip mode/Next clip mode",		KeyCode.Tab)]								CycleClipModes,
        //[KeyDescription("Free draw/Insert point",			KeyCode.I)]									InsertPoint,



        [KeyDescription("Grid/Half grid size",				KeyCode.LeftBracket)]						HalfGridSizeKey,
        [KeyDescription("Grid/Double grid size",			KeyCode.RightBracket)]						DoubleGridSizeKey,
        [KeyDescription("Grid/Toggle grid rendering",		KeyCode.G, EventModifiers.Shift)]			ToggleShowGridKey,
        [KeyDescription("Grid/Toggle bounds snapping",		KeyCode.B, EventModifiers.Shift)]			ToggleBoundsSnappingKey,
        [KeyDescription("Grid/Toggle pivot snapping",		KeyCode.P, EventModifiers.Shift)]			TogglePivotSnappingKey,
//		[KeyDescription("Grid/SnapToGrid",					KeyCode.End, EventModifiers.Control)]		SnapToGridKey,

/*
        [KeyDescription("Action/Cancel or Deselect",		KeyCode.Escape)]							CancelActionKey,
        [KeyDescription("Action/Perform",					KeyCode.Return, KeyCode.KeypadEnter)]		PerformActionKey,



        [KeyDescription("Selection/Delete",					KeyCode.Delete, KeyCode.Backspace)]			DeleteSelectionKey,
        
        [KeyDescription("Selection/Clone Drag",				KeyCode.D, 
                                                            KeyOptions.IgnoreModifiers | 
                                                            KeyOptions.Hold)]							CloneDragActivate,

//		[KeyDescription("Selection/QuickHide",				KeyCode.H, EventModifiers.None)]			QuickHideSelectedObjectsKey,
//		[KeyDescription("Selection/QuickUnhide",			KeyCode.H, EventModifiers.Control)]			QuickHideUnselectedObjectsKey,
//		[KeyDescription("Selection/ToggleVisibility",		KeyCode.H, EventModifiers.Shift)]			ToggleSelectedObjectVisibilityKey,
//		[KeyDescription("Selection/UnhideAll",				KeyCode.U, EventModifiers.None)]			UnHideAllObjectsKey,

        [KeyDescription("Selection/Rotate clockwise",		KeyCode.Comma,		EventModifiers.Control)]	RotateSelectionLeft,
        [KeyDescription("Selection/Rotate anti-clockwise",	KeyCode.Period,		EventModifiers.Control)]	RotateSelectionRight,

        [KeyDescription("Selection/Move left",				KeyCode.LeftArrow,	EventModifiers.Control | EventModifiers.Shift)]	MoveSelectionLeft,
        [KeyDescription("Selection/Move right",				KeyCode.RightArrow,	EventModifiers.Control | EventModifiers.Shift)]	MoveSelectionRight,
        [KeyDescription("Selection/Move back",				KeyCode.DownArrow,	EventModifiers.Control | EventModifiers.Shift)] MoveSelectionBack,
        [KeyDescription("Selection/Move forward",			KeyCode.UpArrow,	EventModifiers.Control | EventModifiers.Shift)] MoveSelectionForward,
        [KeyDescription("Selection/Move down",				KeyCode.PageDown,	EventModifiers.Control | EventModifiers.Shift)] MoveSelectionDown,
        [KeyDescription("Selection/Move up",				KeyCode.PageUp,		EventModifiers.Control | EventModifiers.Shift)] MoveSelectionUp,

        [KeyDescription("Selection/Flip on X axis",			KeyCode.X, EventModifiers.Control | EventModifiers.Shift)] FlipSelectionX,
        [KeyDescription("Selection/Flip on Y axis",			KeyCode.Y, EventModifiers.Control | EventModifiers.Shift)] FlipSelectionY,
        [KeyDescription("Selection/Flip on Z axis",			KeyCode.Z, EventModifiers.Control | EventModifiers.Shift)] FlipSelectionZ,

        [KeyDescription("Selection/Set as PassThrough",		KeyCode.Question)]							MakeSelectedPassThroughKey,
        [KeyDescription("Selection/Set as Additive",		KeyCode.Equals, KeyCode.KeypadPlus)]		MakeSelectedAdditiveKey,
        [KeyDescription("Selection/Set as Subtractive",		KeyCode.Minus, KeyCode.KeypadMinus)]		MakeSelectedSubtractiveKey,
        [KeyDescription("Selection/Set as Intersecting",	KeyCode.Backslash, KeyCode.KeypadDivide)]	MakeSelectedIntersectingKey,

        //GroupSelectionKey				= new KeyEvent(KeyCode.G, EventModifiers.Control); // see OperationsUtility / GroupSelectionInOperation
*/
    }
}