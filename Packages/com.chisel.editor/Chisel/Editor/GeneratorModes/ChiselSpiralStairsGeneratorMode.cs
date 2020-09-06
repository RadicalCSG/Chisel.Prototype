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
    public sealed class ChiselSpiralStairsSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselSpiralStairsDefinition>
    {
        const string    kToolName   = ChiselSpiralStairs.kNodeTypeName;
        public string   ToolName    => kToolName;
        public string   Group       => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SpiralStairsBuilderModeKey, ChiselKeyboardDefaults.SpiralStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselSpiralStairsGeneratorMode); }
        #endregion

        // TODO: add more settings
        public float    stepHeight              = ChiselSpiralStairsDefinition.kDefaultStepHeight;
        public int      outerSegments           = ChiselSpiralStairsDefinition.kDefaultOuterSegments;
        
        [ToggleFlags(includeFlags: (int)(Editors.PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = Editors.PlacementFlags.GenerateFromCenterXZ | Editors.PlacementFlags.AlwaysFaceUp | Editors.PlacementFlags.SameLengthXZ;
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselSpiralStairsDefinition definition) 
        {
            definition.stepHeight       = stepHeight;
            definition.outerSegments    = outerSegments;
        }

        public void OnUpdate(ref ChiselSpiralStairsDefinition definition, Bounds bounds)
        {
            definition.height			= bounds.size[(int)Axis.Y];
            definition.outerDiameter	= bounds.size[(int)Axis.X];
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, outerSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }

    public sealed class ChiselSpiralStairsGeneratorMode : ChiselBoundsPlacementTool<ChiselSpiralStairsSettings, ChiselSpiralStairsDefinition, ChiselSpiralStairs>
    {
    }
}
