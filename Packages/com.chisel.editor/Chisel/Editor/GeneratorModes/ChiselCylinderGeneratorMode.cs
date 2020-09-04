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
    public sealed class ChiselCylinderSettings : ScriptableObject
    {
        public CylinderShapeType    cylinderType		 = CylinderShapeType.Cylinder;
        public int				    sides			     = 16;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        
        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
    }

    public sealed class ChiselCylinderGeneratorMode : ChiselGeneratorModeWithSettings<ChiselCylinderSettings, ChiselCylinderDefinition, ChiselCylinder>
    {
        const string kToolName = ChiselCylinder.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CylinderBuilderModeKey, ChiselKeyboardDefaults.CylinderBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselCylinderGeneratorMode); }
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

        protected override void OnCreate(ChiselCylinder generatedComponent)
        {
            generatedComponent.IsEllipsoid		= !Settings.SameLengthXZ;
            generatedComponent.Type				= Settings.cylinderType;
            generatedComponent.Sides			= Settings.sides;
        }

        protected override void OnUpdate(ChiselCylinder generatedComponent, Bounds bounds)
        {
            var height = bounds.size[(int)Axis.Y];
            generatedComponent.BottomDiameterX    = bounds.size[(int)Axis.X];
            generatedComponent.Height             = height;
            generatedComponent.BottomDiameterZ    = bounds.size[(int)Axis.Z];
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.Sides : Settings.sides);
            HandleRendering.RenderBoxMeasurements(transformation, bounds);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
