using UnitySceneExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Chisel.Editors
{
    public static class ChiselEditorSettings
    {
        // View Options
        public static bool				ShowGrid		= true;
        
        public static bool				AxisLockX		{ get { return Snapping.AxisLockX; } set { Snapping.AxisLockX = value; } }
        public static bool				AxisLockY		{ get { return Snapping.AxisLockY; } set { Snapping.AxisLockY = value; } }
        public static bool				AxisLockZ		{ get { return Snapping.AxisLockZ; } set { Snapping.AxisLockZ = value; } }

        internal static bool IsInToolBox(string toolName, bool defaultValue)
        {
            if (!inToolBoxSettings.TryGetValue(toolName, out bool found))
                return defaultValue;
            return found;
        }

        internal static void SetInToolBox(string toolName, bool value)
        {
            inToolBoxSettings[toolName] = value;
        }

        public static bool				MoveSnapping	{ get { return BoundsSnapping || PivotSnapping; } }
        public static bool				BoundsSnapping  { get { return Snapping.BoundsSnappingEnabled; } set { Snapping.BoundsSnappingEnabled = value; } }
        public static bool				PivotSnapping   { get { return Snapping.PivotSnappingEnabled; } set { Snapping.PivotSnappingEnabled = value; } }
        public static float				UniformSnapSize { get { return ChiselGridSettings.Size.Value.x; } set { Grid.defaultGrid.Spacing = ChiselGridSettings.Size.Value = new UnityEngine.Vector3(value, value, value); } } 

        public static bool				ShowAllAxi      = false;
        public static DistanceUnit		DistanceUnit	= DistanceUnit.Meters; 

        public static bool				RotateSnapping  { get { return Snapping.RotateSnappingEnabled; } set { Snapping.RotateSnappingEnabled = value; } }
        public static float				RotateSnap		= 30.0f;

        public static bool				ScaleSnapping   { get { return Snapping.ScaleSnappingEnabled; } set { Snapping.ScaleSnappingEnabled = value; } }
        public static float				ScaleSnap		= 1.0f;

        static Dictionary<string, bool> inToolBoxSettings = new System.Collections.Generic.Dictionary<string, bool>();

        public static void Load()
        {
            AxisLockX		= EditorPrefs.GetBool ("LockAxisX",			false);
            AxisLockY		= EditorPrefs.GetBool ("LockAxisY",			false);
            AxisLockZ		= EditorPrefs.GetBool ("LockAxisZ",			false);

            BoundsSnapping	= EditorPrefs.GetBool ("BoundsSnapping",	true);
            PivotSnapping	= EditorPrefs.GetBool ("PivotSnapping",		false);
            ShowAllAxi		= !EditorPrefs.GetBool("UniformGrid",		true);
            
            DistanceUnit	= (DistanceUnit)EditorPrefs.GetInt("DistanceUnit", (int)DistanceUnit.Meters);

            RotateSnapping	= EditorPrefs.GetBool ("RotateSnapping",	true);
            RotateSnap		= EditorPrefs.GetFloat("RotationSnap",		15.0f);

            ScaleSnapping	= EditorPrefs.GetBool ("ScaleSnapping",		true);
            ScaleSnap		= EditorPrefs.GetFloat("ScaleSnap",			0.1f);

            ShowGrid 		= EditorPrefs.GetBool ("ShowGrid",			true);
            Snapping.SnapSettings = (SnapSettings)EditorPrefs.GetInt("SnapSettings", (int)SnapSettings.All);
            Snapping.TransformSettings = (ActiveTransformSnapping)EditorPrefs.GetInt("TransformSettings", (int)ActiveTransformSnapping.All);

            var toolBoxValues = EditorPrefs.GetString("InToolBox").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            inToolBoxSettings.Clear();
            if (toolBoxValues.Length > 0)
            {
                foreach (var item in toolBoxValues)
                {
                    var itemState = item.Split('=');
                    inToolBoxSettings[itemState[0]] = (itemState[1][0] == '1');
                }
            }

            ChiselGeneratorManager.GeneratorIndex = EditorPrefs.GetInt("GeneratorIndex", (int)1);
        }

        public static void Save()
        {
            EditorPrefs.SetBool("LockAxisX",		AxisLockX);
            EditorPrefs.SetBool("LockAxisY",		AxisLockY);
            EditorPrefs.SetBool("LockAxisZ",		AxisLockZ);

            EditorPrefs.SetBool("BoundsSnapping",	BoundsSnapping);
            EditorPrefs.SetBool("PivotSnapping",	PivotSnapping);
            EditorPrefs.SetBool("UniformGrid",		!ShowAllAxi);

            EditorPrefs.SetInt  ("DistanceUnit",	(int)DistanceUnit);

            EditorPrefs.SetBool("RotateSnapping",	RotateSnapping);
            EditorPrefs.SetFloat("RotationSnap",	RotateSnap);

            EditorPrefs.SetBool("ScaleSnapping",	ScaleSnapping);
            EditorPrefs.SetFloat("ScaleSnap",		ScaleSnap);

            EditorPrefs.SetInt("SnapSettings",	    (int)Snapping.SnapSettings);
            EditorPrefs.SetInt("TransformSettings",	(int)Snapping.TransformSettings);

            EditorPrefs.SetBool("ShowGrid",   		ShowGrid);

            var values = new StringBuilder();
            foreach (var pair in inToolBoxSettings)
            {
                if (values.Length > 0)
                    values.Append(',');
                values.Append(pair.Key);
                values.Append('=');
                if (pair.Value) values.Append('1');
                else values.Append('0');
            }
            EditorPrefs.SetString("InToolBox", values.ToString());

            EditorPrefs.SetInt("GeneratorIndex", ChiselGeneratorManager.GeneratorIndex);
        }
    };
}
