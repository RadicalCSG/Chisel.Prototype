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
    public sealed class ChiselCylinderSettings : ScriptableObject, IChiselBoundsGeneratorSettings<ChiselCylinderDefinition>
    {
        public CylinderShapeType    cylinderType		 = CylinderShapeType.Cylinder;
        public int				    sides			     = 16;
        
        public bool     SameLengthXZ		    { get { return (placement & PlacementFlags.SameLengthXZ) == PlacementFlags.SameLengthXZ; } set { placement = value ? (placement | PlacementFlags.SameLengthXZ) : placement & ~PlacementFlags.SameLengthXZ; } }
        public bool     GenerateFromCenterY     { get { return (placement & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterY) : placement & ~PlacementFlags.GenerateFromCenterY; } }
        public bool     GenerateFromCenterXZ    { get { return (placement & PlacementFlags.GenerateFromCenterXZ) == PlacementFlags.GenerateFromCenterXZ; } set { placement = value ? (placement | PlacementFlags.GenerateFromCenterXZ) : placement & ~PlacementFlags.GenerateFromCenterXZ; } }
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;

        // TODO: this could be the placementflags ...
        public ChiselGeneratorModeFlags GeneratoreModeFlags => (SameLengthXZ         ? ChiselGeneratorModeFlags.SameLengthXZ         : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterY  ? ChiselGeneratorModeFlags.GenerateFromCenterY  : ChiselGeneratorModeFlags.None) |
                                                               (GenerateFromCenterXZ ? ChiselGeneratorModeFlags.GenerateFromCenterXZ : ChiselGeneratorModeFlags.None);

        public void OnCreate(ref ChiselCylinderDefinition definition) 
        {
            definition.isEllipsoid		= !SameLengthXZ;
            definition.type				= cylinderType;
            definition.sides			= sides;
        }

        public void OnUpdate(ref ChiselCylinderDefinition definition, Bounds bounds)
        {
            var height = bounds.size[(int)Axis.Y];
            definition.BottomDiameterX  = bounds.size[(int)Axis.X];
            definition.top.height       = height;
            definition.BottomDiameterZ  = bounds.size[(int)Axis.Z];
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
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

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
