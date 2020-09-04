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
    public sealed class ChiselSpiralStairsSettings : ScriptableObject
    {
        // TODO: add more settings
        public float    stepHeight              = ChiselSpiralStairsDefinition.kDefaultStepHeight;
        public int      outerSegments           = ChiselSpiralStairsDefinition.kDefaultOuterSegments;
        
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }

        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ;
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
 
        public override ChiselGeneratorModeFlags Flags 
        { 
            get
            {
                return ChiselGeneratorModeFlags.AlwaysFaceUp |
                       ChiselGeneratorModeFlags.SameLengthXZ |
                       (Settings.GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);
            } 
        }

        protected override void OnCreate(ChiselSpiralStairs generatedComponent)
        {
            generatedComponent.Operation        = forceOperation ?? CSGOperationType.Additive;
            generatedComponent.StepHeight       = Settings.stepHeight;
            generatedComponent.OuterSegments    = Settings.outerSegments;
        }

        protected override void OnUpdate(ChiselSpiralStairs generatedComponent, Bounds bounds)
        {
            generatedComponent.Height			= bounds.size[(int)Axis.Y];
            generatedComponent.OuterDiameter	= bounds.size[(int)Axis.X];
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.OuterSegments : Settings.outerSegments);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
