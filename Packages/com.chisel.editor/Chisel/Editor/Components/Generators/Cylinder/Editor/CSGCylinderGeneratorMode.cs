using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class CSGCylinderGeneratorMode : ICSGToolMode
    {
        public void OnEnable()
        {
            // TODO: shouldn't just always set this param
            Tools.hidden = true; 
            Reset();
        }

        public void OnDisable()
        {
            Reset();
        }

        void Reset()
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

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds    bounds;
            ChiselModel  modelBeneathCursor;
            Matrix4x4 transformation;
            float     height;

            var flags = (isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                        (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    cylinder = ChiselModelManager.Create<ChiselCylinder>("Cylinder",
                                                                ChiselModelManager.GetModelForNode(modelBeneathCursor),
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
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = cylinder.gameObject;
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    Reset();
                    break;
                }

                case BoxExtrusionState.Cancel:
                {
                    Reset();
                    Undo.RevertAllInCurrentGroup();
                    EditorGUIUtility.ExitGUI();
                    break;
                }
                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }
            
            HandleRendering.RenderCylinder(transformation, bounds, (cylinder) ? cylinder.Sides : sides);
        }
    }
}
