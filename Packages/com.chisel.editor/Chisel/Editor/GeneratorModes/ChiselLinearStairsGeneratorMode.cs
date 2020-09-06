using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    public sealed class ChiselLinearStairsSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselLinearStairsDefinition>
    {
        const string    kToolName   = ChiselLinearStairs.kNodeTypeName;
        public string   ToolName    => kToolName;
        public string   Group       => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.LinearStairsBuilderModeKey, ChiselKeyboardDefaults.LinearStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselLinearStairsGeneratorMode); }
        #endregion

        [ToggleFlags(includeFlags: (int)Editors.PlacementFlags.SameLengthXZ)]
        public PlacementFlags placement = Editors.PlacementFlags.AlwaysFaceUp | Editors.PlacementFlags.AlwaysFaceCameraXZ;        
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselLinearStairsDefinition definition) {}

        public void OnUpdate(ref ChiselLinearStairsDefinition definition, Bounds bounds)
        {
            definition.bounds = bounds;
        }
        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }

    public sealed class ChiselLinearStairsGeneratorMode : ChiselBoundsPlacementTool<ChiselLinearStairsSettings, ChiselLinearStairsDefinition, ChiselLinearStairs>
    {
    }
}
