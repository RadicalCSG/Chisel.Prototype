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
    public sealed class ChiselSphereSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselSphereDefinition>
    {
        public int      horizontalSegments      = ChiselSphereDefinition.kDefaultHorizontalSegments;
        public int      verticalSegments        = ChiselSphereDefinition.kDefaultVerticalSegments;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     HeightEqualsXZ          { get { return (placement & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ; } set { placement = value ? (placement | PlacementFlags.HeightEqualsXZ) : placement & ~PlacementFlags.HeightEqualsXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        
        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ |
                                            (ChiselSphereDefinition.kDefaultGenerateFromCenter ? PlacementFlags.GenerateFromCenterY : (PlacementFlags)0);

        
        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => (SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                                                               (HeightEqualsXZ       ? ChiselGeneratorModeFlags.HeightEqualsMinXZ    : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);

        public void OnCreate(ref ChiselSphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
            definition.generateFromCenter   = false;
        }

        public void OnUpdate(ref ChiselSphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: Make a RenderSphere method
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }

    public sealed class ChiselSphereGeneratorMode : ChiselGeneratorModeWithSettings<ChiselSphereSettings, ChiselSphereDefinition, ChiselSphere>
    {
        const string kToolName = ChiselSphere.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SphereBuilderModeKey, ChiselKeyboardDefaults.SphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselSphereGeneratorMode); }
        #endregion

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
