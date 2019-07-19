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
    public sealed class ChiselCylinderGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselCylinder.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CylinderBuilderModeKey, ChiselKeyboardDefaults.CylinderBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselCylinderGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }
        
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;
        
        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool			    generateFromCenterXZ    = true;
        CylinderShapeType   cylinderType		    = CylinderShapeType.Cylinder;
        bool			    isSymmetrical		    = false;
        int				    sides			        = 16;
        
        ChiselCylinder cylinder;

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);
            
            var flags = (isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                        (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    cylinder = ChiselComponentFactory.Create<ChiselCylinder>("Cylinder",
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);
                    cylinder.definition.Reset();
                    cylinder.Operation			= forceOperation ?? CSGOperationType.Additive;
                    cylinder.IsEllipsoid		= !isSymmetrical;
                    cylinder.Type				= cylinderType;
                    cylinder.Height				= height;
                    cylinder.Sides				= sides;
                    cylinder.BottomDiameterX	= bounds.size[(int)Axis.X];
                    cylinder.Height             = height;
                    cylinder.BottomDiameterZ	= bounds.size[(int)Axis.Z];
                    cylinder.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    cylinder.Operation  = forceOperation ?? 
                                              ((height < 0 && modelBeneathCursor) ? 
                                                CSGOperationType.Subtractive : 
                                                CSGOperationType.Additive);

                    cylinder.BottomDiameterX    = bounds.size[(int)Axis.X];
                    cylinder.Height             = height;
                    cylinder.BottomDiameterZ    = bounds.size[(int)Axis.Z];
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(cylinder.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }
            
            HandleRendering.RenderCylinder(transformation, bounds, (cylinder) ? cylinder.Sides : sides);
        }
    }
}
