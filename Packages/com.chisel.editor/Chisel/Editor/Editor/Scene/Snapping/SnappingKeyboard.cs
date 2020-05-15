using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    public static class SnappingKeyboard
    {
        const string ShortCutBaseName = "Chisel/Grid/";

        const string HalfGridSizeName = ShortCutBaseName + "Grid Size/Half";
        [Shortcut(HalfGridSizeName, typeof(SceneView), ChiselKeyboardDefaults.HalfGridSizeKey, ChiselKeyboardDefaults.HalfGridSizeModifiers, displayName = HalfGridSizeName)]
        public static void HalfGridSize()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyGridSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        public static float HalfGridSizeRet() { HalfGridSize(); return ChiselEditorSettings.UniformSnapSize; }


        const string DoubleGridSizeName = ShortCutBaseName + "Grid Size/Double";
        [Shortcut(DoubleGridSizeName, typeof(SceneView), ChiselKeyboardDefaults.DoubleGridSizeKey, ChiselKeyboardDefaults.DoubleGridSizeModifiers, displayName = DoubleGridSizeName)]
        public static void DoubleGridSize()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyGridSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }

        public static float DoubleGridSizeRet() { DoubleGridSize(); return ChiselEditorSettings.UniformSnapSize; } 


        public static void MultiplyGridSnapDistance(float modifier)
        {
            ChiselEditorSettings.UniformSnapSize = ChiselEditorSettings.UniformSnapSize * modifier;
            ChiselEditorSettings.Save();
        }



        const string HalfRotateSnapName = ShortCutBaseName + "Rotate Snap/Half";
        [Shortcut(HalfRotateSnapName, typeof(SceneView), displayName = HalfRotateSnapName)]
        public static void HalfRotateSnap()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyRotateSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        public static float HalfRotateSnapRet() { HalfRotateSnap(); return ChiselEditorSettings.RotateSnap; }


        const string DoubleRotateSnapName = ShortCutBaseName + "Rotate Snap/Double";
        [Shortcut(DoubleRotateSnapName, typeof(SceneView), displayName = DoubleRotateSnapName)]
        public static void DoubleRotateSnap()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyRotateSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        public static float DoubleRotateSnapRet() { DoubleRotateSnap(); return ChiselEditorSettings.RotateSnap; }

        public static void MultiplyRotateSnapDistance(float modifier)
        {
            ChiselEditorSettings.RotateSnap = ChiselEditorSettings.RotateSnap * modifier;
            ChiselEditorSettings.Save();
        }
        

        const string HalfScaleSnapName = ShortCutBaseName + "Scale Snap/Half";
        [Shortcut(HalfScaleSnapName, typeof(SceneView), displayName = HalfScaleSnapName)]
        public static void HalfScaleSnap()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyScaleSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        public static float HalfScaleSnapRet() { HalfScaleSnap(); return ChiselEditorSettings.ScaleSnap; }


        const string DoubleScaleSnapName = ShortCutBaseName + "Scale Snap/Double";
        [Shortcut(DoubleScaleSnapName, typeof(SceneView), displayName = DoubleScaleSnapName)]
        public static void DoubleScaleSnap()
        {
            GUIUtility.keyboardControl = 0;
            MultiplyScaleSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        public static float DoubleScaleSnapRet() { DoubleScaleSnap(); return ChiselEditorSettings.ScaleSnap; }

        public static void MultiplyScaleSnapDistance(float modifier)
        {
            ChiselEditorSettings.ScaleSnap = ChiselEditorSettings.ScaleSnap * modifier;
            ChiselEditorSettings.Save();
        }




        const string TogglePivotSnappingName = ShortCutBaseName + "Toggle Snapping/Pivot";
        [Shortcut(TogglePivotSnappingName, typeof(SceneView), ChiselKeyboardDefaults.TogglePivotSnappingKey, ChiselKeyboardDefaults.TogglePivotSnappingModifiers, displayName = TogglePivotSnappingName)]
        public static void TogglePivotSnapping()
        {
            ChiselEditorSettings.PivotSnapping = !ChiselEditorSettings.PivotSnapping;
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string ToggleBoundsSnappingName = ShortCutBaseName + "Toggle Snapping/Bounds";
        [Shortcut(ToggleBoundsSnappingName, typeof(SceneView), ChiselKeyboardDefaults.ToggleBoundsSnappingKey, ChiselKeyboardDefaults.ToggleBoundsSnappingModifiers, displayName = ToggleBoundsSnappingName)]
        public static void ToggleBoundsSnapping()
        {
            ChiselEditorSettings.BoundsSnapping = !ChiselEditorSettings.BoundsSnapping;
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string ToggleShowGridName = ShortCutBaseName + "Grid/Toggle Grid Visibility";
        [Shortcut(ToggleShowGridName, typeof(SceneView), ChiselKeyboardDefaults.ToggleShowGridKey, ChiselKeyboardDefaults.ToggleShowGridModifiers, displayName = ToggleShowGridName)]
        public static void ToggleGridVisibility()
        {
            ChiselEditorSettings.ShowGrid = !ChiselEditorSettings.ShowGrid;
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
    }
}
