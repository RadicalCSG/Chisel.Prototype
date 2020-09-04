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
    public sealed class ChiselCapsuleSettings : ScriptableObject
    {
        public int      topSegments			 = ChiselCapsuleDefinition.kDefaultTopSegments;
        public int	    bottomSegments	     = ChiselCapsuleDefinition.kDefaultBottomSegments;
        public int      sides				 = ChiselCapsuleDefinition.kDefaultSides;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }


        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
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

        public override ChiselGeneratorModeFlags Flags 
        { 
            get
            {
                return (Settings.SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                       (Settings.GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                       (Settings.GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);
            } 
        }

        protected override void OnCreate(ChiselCapsule generatedComponent)
        {
            generatedComponent.Sides           = Settings.sides;
            generatedComponent.TopSegments     = Settings.topSegments;
            generatedComponent.BottomSegments  = Settings.bottomSegments;
        }

        protected override void OnUpdate(ChiselCapsule generatedComponent, Bounds bounds)
        {
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

            generatedComponent.TopHeight        = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            generatedComponent.BottomHeight     = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            generatedComponent.DiameterX       = bounds.size[(int)Axis.X];
            generatedComponent.Height          = height;
            generatedComponent.DiameterZ       = bounds.size[(int)Axis.Z];
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            // TODO: render capsule here
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.Sides : Settings.sides);
            HandleRendering.RenderBoxMeasurements(transformation, bounds);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
