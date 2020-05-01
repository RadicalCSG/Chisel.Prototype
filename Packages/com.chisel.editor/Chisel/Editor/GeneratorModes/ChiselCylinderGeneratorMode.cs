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
    public sealed class ChiselCylinderSettings
    {
        public CylinderShapeType    cylinderType		 = CylinderShapeType.Cylinder;
        public int				    sides			     = 16;
        public bool                 isSymmetrical		 = true;
        public bool                 generateFromCenterY  = false;
        public bool                 generateFromCenterXZ = true;
    }

    public sealed class ChiselCylinderGeneratorMode : ChiselGeneratorModeWithSettings<ChiselCylinderSettings, ChiselCylinder>
    {
        const string kToolName = ChiselCylinder.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CylinderBuilderModeKey, ChiselKeyboardDefaults.CylinderBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselCylinderGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var flags = (settings.isSymmetrical          ? BoxExtrusionFlags.IsSymmetricalXZ      : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterY    ? BoxExtrusionFlags.GenerateFromCenterY  : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterXZ   ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselCylinder>(ChiselCylinder.kNodeTypeName,
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation		= forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.IsEllipsoid		= !settings.isSymmetrical;
                    generatedComponent.Type				= settings.cylinderType;
                    generatedComponent.Height			= height;
                    generatedComponent.Sides			= settings.sides;
                    generatedComponent.BottomDiameterX	= bounds.size[(int)Axis.X];
                    generatedComponent.Height           = height;
                    generatedComponent.BottomDiameterZ	= bounds.size[(int)Axis.Z];
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    generatedComponent.Operation  = forceOperation ?? 
                                              ((height < 0 && modelBeneathCursor) ? 
                                                CSGOperationType.Subtractive : 
                                                CSGOperationType.Additive);

                    generatedComponent.BottomDiameterX    = bounds.size[(int)Axis.X];
                    generatedComponent.Height             = height;
                    generatedComponent.BottomDiameterZ    = bounds.size[(int)Axis.Z];
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(generatedComponent.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }
            
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.Sides : settings.sides);
        }
    }
}
