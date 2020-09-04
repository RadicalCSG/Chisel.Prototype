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
    [Flags]
    public enum PlacementFlags
    {
        [ToggleFlag("SizeFromBottom",   "Extrude from the bottom",
                    "SizeFromCenter",   "Extrude it from the center")]
        GenerateFromCenterY     = 1,
        [ToggleFlag("DragToHeight",     "Drag to extrude distance",
                    "AutoHeight",       "Extrude distance is determined by base size")]
        HeightEqualsXZ          = 2,
        [ToggleFlag("RectangularBase",  "Base width and depth can be sized independently", 
                    "SquareBase",       "Base width and depth are identical in size")]
        SameLengthXZ            = 4,
        [ToggleFlag("SizeBaseFromCorner", "Base is sized from corner",
                    "SizeBaseFromCenter", "Base is sized from center")]
        GenerateFromCenterXZ    = 8
    }


    public sealed class ChiselBoxSettings : ScriptableObject
    {
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     HeightEqualsXZ          { get { return (placement & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ; } set { placement = value ? (placement | PlacementFlags.HeightEqualsXZ) : placement & ~PlacementFlags.HeightEqualsXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        
        [ToggleFlags]
        public PlacementFlags placement = (PlacementFlags)0;
    } 

    public sealed class ChiselBoxGeneratorMode : ChiselGeneratorModeWithSettings<ChiselBoxSettings, ChiselBoxDefinition, ChiselBox>
    {
        const string kToolName = ChiselBox.kNodeTypeName;
        public override string  ToolName => kToolName;
        public override string  Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.BoxBuilderModeKey, ChiselKeyboardDefaults.BoxBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselBoxGeneratorMode); }
        #endregion

        public override ChiselGeneratorModeFlags Flags 
        { 
            get
            {
                return (Settings.SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                       (Settings.HeightEqualsXZ       ? ChiselGeneratorModeFlags.HeightEqualsMinXZ    : ChiselGeneratorModeFlags.None) |
                       (Settings.GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                       (Settings.GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);
            } 
        }

        protected override void OnCreate(ChiselBox generatedComponent) {}

        protected override void OnUpdate(ChiselBox generatedComponent, Bounds bounds)
        {
            generatedComponent.Bounds = bounds;
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            HandleRendering.RenderBox(transformation, bounds);
            HandleRendering.RenderBoxMeasurements(transformation, bounds);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
