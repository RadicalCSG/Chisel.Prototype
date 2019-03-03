using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Chisel.Editors
{
	// TODO: These need to be per SceneView ... ?
	public enum CSGViewOptions
	{
		ShowGrid					= 1,
		ShowInViewHelpers			= 2,
		WireframeMode				= 4,

		ShowVisibleSurfaces			= 8,
		ShowCollidableSurfaces		= 16,
		ShowShadowCastingSurfaces	= 32,
		ShowShadowReceivingSurfaces	= 64
	}

	public static class CSGEditorSettings
	{
		const CSGEditMode				kDefaultEditMode        = CSGEditMode.Object;
		const CSGViewOptions			kDefaultViewOptions		= CSGViewOptions.ShowGrid | CSGViewOptions.ShowVisibleSurfaces | CSGViewOptions.ShowInViewHelpers;

		public static CSGViewOptions	ViewOptions		= kDefaultViewOptions;
		
		public static bool				AxisLockX		{ get { return Snapping.AxisLockX; } set { Snapping.AxisLockX = value; } }
		public static bool				AxisLockY		{ get { return Snapping.AxisLockY; } set { Snapping.AxisLockY = value; } }
		public static bool				AxisLockZ		{ get { return Snapping.AxisLockZ; } set { Snapping.AxisLockZ = value; } }
		
		public static bool				MoveSnapping	{ get { return BoundsSnapping || PivotSnapping; } }
		public static bool				BoundsSnapping  { get { return Snapping.BoundsSnappingEnabled; } set { Snapping.BoundsSnappingEnabled = value; } }
		public static bool				PivotSnapping   { get { return Snapping.PivotSnappingEnabled; } set { Snapping.PivotSnappingEnabled = value; } }
		public static float				MoveSnapX		{ get { return Grid.defaultGrid.SpacingX; } set { Grid.defaultGrid.SpacingX = value; } }
		public static float				MoveSnapY		{ get { return Grid.defaultGrid.SpacingY; } set { Grid.defaultGrid.SpacingY = value; } }
		public static float				MoveSnapZ		{ get { return Grid.defaultGrid.SpacingZ; } set { Grid.defaultGrid.SpacingZ = value; } }
		public static CSGEditMode		EditMode		{ get { return CSGEditModeManager.EditMode; } set { CSGEditModeManager.EditMode = value; } }

		public static bool				ShowAllAxi      = false;
		public static DistanceUnit		DistanceUnit	= DistanceUnit.Meters; 

		public static bool				RotateSnapping  { get { return Snapping.RotateSnappingEnabled; } set { Snapping.RotateSnappingEnabled = value; } }
		public static float				RotateSnap		= 30.0f;

		public static bool				ScaleSnapping   { get { return Snapping.ScaleSnappingEnabled; } set { Snapping.ScaleSnappingEnabled = value; } }
		public static float				ScaleSnap		= 1.0f;
			
		public static void Load()
		{
			AxisLockX		= EditorPrefs.GetBool ("LockAxisX",			false);
			AxisLockY		= EditorPrefs.GetBool ("LockAxisY",			false);
			AxisLockZ		= EditorPrefs.GetBool ("LockAxisZ",			false);

			BoundsSnapping	= EditorPrefs.GetBool ("BoundsSnapping",	true);
			PivotSnapping	= EditorPrefs.GetBool ("PivotSnapping",		false);
			ShowAllAxi		= !EditorPrefs.GetBool("UniformGrid",		true);
			MoveSnapX		= EditorPrefs.GetFloat("MoveSnapX",			1.0f);
			MoveSnapY		= EditorPrefs.GetFloat("MoveSnapY",			1.0f);
			MoveSnapZ		= EditorPrefs.GetFloat("MoveSnapZ",			1.0f);

			DistanceUnit	= (DistanceUnit)EditorPrefs.GetInt  ("DistanceUnit",		(int)DistanceUnit.Meters);

			RotateSnapping	= EditorPrefs.GetBool ("RotateSnapping",	true);
			RotateSnap		= EditorPrefs.GetFloat("RotationSnap",		15.0f);

			ScaleSnapping	= EditorPrefs.GetBool ("ScaleSnapping",		true);
			ScaleSnap		= EditorPrefs.GetFloat("ScaleSnap",			0.1f);

			ViewOptions     = (CSGViewOptions)EditorPrefs.GetInt  ("CSGViewOptions",	(int)kDefaultViewOptions);
			EditMode		= (CSGEditMode)EditorPrefs.GetInt	  ("CSGEditMode",		(int)kDefaultEditMode);
		}

		public static void Save()
		{
			EditorPrefs.SetBool("LockAxisX",		AxisLockX);
			EditorPrefs.SetBool("LockAxisY",		AxisLockY);
			EditorPrefs.SetBool("LockAxisZ",		AxisLockZ);

			EditorPrefs.SetBool("BoundsSnapping",	BoundsSnapping);
			EditorPrefs.SetBool("PivotSnapping",	PivotSnapping);
			EditorPrefs.SetBool("UniformGrid",		!ShowAllAxi);
			EditorPrefs.SetFloat("MoveSnapX",		MoveSnapX);
			EditorPrefs.SetFloat("MoveSnapY",		MoveSnapY);
			EditorPrefs.SetFloat("MoveSnapZ",		MoveSnapZ);

			EditorPrefs.SetInt  ("DistanceUnit",	(int)DistanceUnit);

			EditorPrefs.SetBool("RotateSnapping",	RotateSnapping);
			EditorPrefs.SetFloat("RotationSnap",	RotateSnap);

			EditorPrefs.SetBool("ScaleSnapping",	ScaleSnapping);
			EditorPrefs.SetFloat("ScaleSnap",		ScaleSnap);

			EditorPrefs.SetInt("CSGViewOptions",   (int)ViewOptions);
			EditorPrefs.SetInt("CSGEditMode",	   (int)EditMode);
		}
	};
}
