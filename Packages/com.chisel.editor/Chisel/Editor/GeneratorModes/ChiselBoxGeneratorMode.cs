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
    public sealed class ChiselBoxSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselBoxDefinition>
    {
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     HeightEqualsXZ          { get { return (placement & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ; } set { placement = value ? (placement | PlacementFlags.HeightEqualsXZ) : placement & ~PlacementFlags.HeightEqualsXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        
        [ToggleFlags]
        public PlacementFlags placement = (PlacementFlags)0;

        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => (SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                                                               (HeightEqualsXZ       ? ChiselGeneratorModeFlags.HeightEqualsMinXZ    : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);

        public void OnCreate(ref ChiselBoxDefinition definition) 
        {
        }

        public void OnUpdate(ref ChiselBoxDefinition definition, Bounds bounds)
        {
            definition.bounds = bounds;
        }
        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    } 

    public sealed class ChiselBoxGeneratorMode : ChiselGeneratorModeWithSettings<ChiselBoxSettings, ChiselBoxDefinition, ChiselBox>
    {
        // TODO: move all this to 'settings'? ...
        const string kToolName = ChiselBox.kNodeTypeName;
        public override string  ToolName => kToolName;
        public override string  Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.BoxBuilderModeKey, ChiselKeyboardDefaults.BoxBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselBoxGeneratorMode); }
        #endregion

        // TODO: get rid of this somehow ...
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
