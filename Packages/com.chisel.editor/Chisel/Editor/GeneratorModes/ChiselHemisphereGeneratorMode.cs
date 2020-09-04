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
    public sealed class ChiselHemisphereSettings : ScriptableObject
    {
        public int      horizontalSegments      = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        public int      verticalSegments        = ChiselHemisphereDefinition.kDefaultVerticalSegments;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     HeightEqualsHalfXZ      { get { return (placement & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ; } set { placement = value ? (placement | PlacementFlags.HeightEqualsXZ) : placement & ~PlacementFlags.HeightEqualsXZ; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ;
    }

    public sealed class ChiselHemisphereGeneratorMode : ChiselGeneratorModeWithSettings<ChiselHemisphereSettings, ChiselHemisphereDefinition, ChiselHemisphere>
    {
        const string kToolName = ChiselHemisphere.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.HemisphereBuilderModeKey, ChiselKeyboardDefaults.HemisphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselHemisphereGeneratorMode); }
        #endregion

        public override ChiselGeneratorModeFlags Flags 
        { 
            get
            {
                return (Settings.SameLengthXZ          ? ChiselGeneratorModeFlags.SameLengthXZ          : ChiselGeneratorModeFlags.None) |
                       (Settings.HeightEqualsHalfXZ ? ChiselGeneratorModeFlags.HeightEqualsHalfMinXZ : ChiselGeneratorModeFlags.None) |
                       (Settings.GenerateFromCenterXZ  ? ChiselGeneratorModeFlags.GenerateFromCenterXZ  : ChiselGeneratorModeFlags.None);
            } 
        }

        protected override void OnCreate(ChiselHemisphere generatedComponent)
        {
            generatedComponent.VerticalSegments     = Settings.verticalSegments;
            generatedComponent.HorizontalSegments   = Settings.horizontalSegments;
        }

        protected override void OnUpdate(ChiselHemisphere generatedComponent, Bounds bounds)
        {
            generatedComponent.DiameterXYZ = bounds.size;
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.HorizontalSegments : Settings.horizontalSegments);
            HandleRendering.RenderBoxMeasurements(transformation, bounds);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
