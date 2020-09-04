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
    public sealed class ChiselLinearStairsSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselLinearStairsDefinition>
    {
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        
        [ToggleFlags(includeFlags: (int)PlacementFlags.SameLengthXZ)]
        public PlacementFlags placement = (PlacementFlags)0;

        
        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => ChiselGeneratorModeFlags.AlwaysFaceUp | ChiselGeneratorModeFlags.AlwaysFaceCameraXZ |
                                                               (SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None);

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

    public sealed class ChiselLinearStairsGeneratorMode : ChiselGeneratorModeWithSettings<ChiselLinearStairsSettings, ChiselLinearStairsDefinition, ChiselLinearStairs>
    {
        const string kToolName = ChiselLinearStairs.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.LinearStairsBuilderModeKey, ChiselKeyboardDefaults.LinearStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselLinearStairsGeneratorMode); }
        #endregion

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
