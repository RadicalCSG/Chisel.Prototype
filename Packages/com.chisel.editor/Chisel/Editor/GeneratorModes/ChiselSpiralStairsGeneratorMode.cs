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
    public sealed class ChiselSpiralStairsSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselSpiralStairsDefinition>
    {
        // TODO: add more settings
        public float    stepHeight              = ChiselSpiralStairsDefinition.kDefaultStepHeight;
        public int      outerSegments           = ChiselSpiralStairsDefinition.kDefaultOuterSegments;
        
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }

        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ;
        
        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => ChiselGeneratorModeFlags.AlwaysFaceUp | ChiselGeneratorModeFlags.SameLengthXZ |
                                                               (GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);

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

    public sealed class ChiselSpiralStairsGeneratorMode : ChiselGeneratorModeWithSettings<ChiselSpiralStairsSettings, ChiselSpiralStairsDefinition, ChiselSpiralStairs>
    {
        const string kToolName = ChiselSpiralStairs.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SpiralStairsBuilderModeKey, ChiselKeyboardDefaults.SpiralStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselSpiralStairsGeneratorMode); }
        #endregion

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
