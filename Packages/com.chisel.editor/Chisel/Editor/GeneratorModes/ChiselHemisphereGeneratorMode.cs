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
    public sealed class ChiselHemisphereSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselHemisphereDefinition>
    {
        public int      horizontalSegments      = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        public int      verticalSegments        = ChiselHemisphereDefinition.kDefaultVerticalSegments;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     HeightEqualsHalfXZ      { get { return (placement & PlacementFlags.HeightEqualsXZ) == PlacementFlags.HeightEqualsXZ; } set { placement = value ? (placement | PlacementFlags.HeightEqualsXZ) : placement & ~PlacementFlags.HeightEqualsXZ; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ;
        
        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => (SameLengthXZ          ? ChiselGeneratorModeFlags.SameLengthXZ          : ChiselGeneratorModeFlags.None) |
                                                               (HeightEqualsHalfXZ    ? ChiselGeneratorModeFlags.HeightEqualsHalfMinXZ : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterXZ  ? ChiselGeneratorModeFlags.GenerateFromCenterXZ  : ChiselGeneratorModeFlags.None);

        public void OnCreate(ref ChiselHemisphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
        }

        public void OnUpdate(ref ChiselHemisphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
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

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
