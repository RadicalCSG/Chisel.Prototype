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
            MultiplySnapDistance(0.5f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }


        const string DoubleGridSizeName = ShortCutBaseName + "Grid Size/Double";
        [Shortcut(DoubleGridSizeName, typeof(SceneView), ChiselKeyboardDefaults.DoubleGridSizeKey, ChiselKeyboardDefaults.DoubleGridSizeModifiers, displayName = DoubleGridSizeName)]
        public static void DoubleGridSize()
        {
            MultiplySnapDistance(2.0f);
            ChiselEditorSettings.Save();
            SceneView.RepaintAll();
        }
        
        public static void MultiplySnapDistance(float modifier)
        {
            ChiselEditorSettings.UniformSnapSize = ChiselEditorSettings.UniformSnapSize * modifier;
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
