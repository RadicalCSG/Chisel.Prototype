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
    public sealed class ChiselCapsuleSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselCapsuleDefinition>
    {
        public int      topSegments			 = ChiselCapsuleDefinition.kDefaultTopSegments;
        public int	    bottomSegments	     = ChiselCapsuleDefinition.kDefaultBottomSegments;
        public int      sides				 = ChiselCapsuleDefinition.kDefaultSides;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }


        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        

        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => (SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);

        public void OnCreate(ref ChiselCapsuleDefinition definition) 
        {
            definition.sides           = sides;
            definition.topSegments     = topSegments;
            definition.bottomSegments  = bottomSegments;
        }

        public void OnUpdate(ref ChiselCapsuleDefinition definition, Bounds bounds)
        {
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

            definition.topHeight    = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            definition.bottomHeight = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            definition.diameterX    = bounds.size[(int)Axis.X];
            definition.height       = height;
            definition.diameterZ    = bounds.size[(int)Axis.Z];
        }
        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: render capsule here
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }

    // TODO: maybe just bevel top of cylinder instead of separate capsule generator??
    public sealed class ChiselCapsuleGeneratorMode : ChiselGeneratorModeWithSettings<ChiselCapsuleSettings, ChiselCapsuleDefinition, ChiselCapsule>
    {
        const string kToolName = ChiselCapsule.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CapsuleBuilderModeKey, ChiselKeyboardDefaults.CapsuleBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselCapsuleGeneratorMode); }
        #endregion
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
