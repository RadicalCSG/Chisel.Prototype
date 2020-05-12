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
            MultiplyGridSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string DoubleGridSizeName = ShortCutBaseName + "Grid Size/Double";
        [Shortcut(DoubleGridSizeName, typeof(SceneView), ChiselKeyboardDefaults.DoubleGridSizeKey, ChiselKeyboardDefaults.DoubleGridSizeModifiers, displayName = DoubleGridSizeName)]
        public static void DoubleGridSize()
        {
            MultiplyGridSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        
        public static void MultiplyGridSnapDistance(float modifier)
        {
            ChiselEditorSettings.UniformSnapSize = ChiselEditorSettings.UniformSnapSize * modifier;
            ChiselEditorSettings.Save();
        }



        const string HalfRotateSnapName = ShortCutBaseName + "Rotate Snap/Half";
        [Shortcut(HalfRotateSnapName, typeof(SceneView), displayName = HalfRotateSnapName)]
        public static void HalfRotateSnap()
        {
            MultiplyRotateSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string DoubleRotateSnapName = ShortCutBaseName + "Rotate Snap/Double";
        [Shortcut(DoubleRotateSnapName, typeof(SceneView), displayName = DoubleRotateSnapName)]
        public static void DoubleRotateSnap()
        {
            MultiplyRotateSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }

        public static void MultiplyRotateSnapDistance(float modifier)
        {
            ChiselEditorSettings.RotateSnap = ChiselEditorSettings.RotateSnap * modifier;
            ChiselEditorSettings.Save();
        }
        

        const string HalfScaleSnapName = ShortCutBaseName + "Scale Snap/Half";
        [Shortcut(HalfScaleSnapName, typeof(SceneView), displayName = HalfScaleSnapName)]
        public static void HalfScaleSnap()
        {
            MultiplyScaleSnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string DoubleScaleSnapName = ShortCutBaseName + "Scale Snap/Double";
        [Shortcut(DoubleScaleSnapName, typeof(SceneView), displayName = DoubleScaleSnapName)]
        public static void DoubleScaleSnap()
        {
            MultiplyScaleSnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }

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
