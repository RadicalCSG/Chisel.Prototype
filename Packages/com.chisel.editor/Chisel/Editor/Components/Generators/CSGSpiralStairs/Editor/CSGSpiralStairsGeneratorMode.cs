using Chisel.Assets;
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
    public sealed class CSGSpiralStairsGeneratorMode : ICSGToolMode
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
            spiralStairs = null;
        }
        
        // TODO: Handle forcing operation types
        CSGOperationType?   forceOperation          = null;
        
        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool				generateFromCenterXZ	= true;
        int                 outerSegments           = CSGSpiralStairsDefinition.kDefaultOuterSegments;
        float               stepHeight              = CSGSpiralStairsDefinition.kDefaultStepHeight;

        CSGSpiralStairs spiralStairs;
        
        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds bounds;
            CSGModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;
            
            var flags = BoxExtrusionFlags.AlwaysFaceUp |
                        BoxExtrusionFlags.IsSymmetricalXZ |
                        (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, flags, Axis.Y, snappingSteps: stepHeight))
            {
                case BoxExtrusionState.Create:
                {
                    spiralStairs = BrushMeshAssetFactory.Create<CSGSpiralStairs>("Spiral Stairs",
                                                                        BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                                        transformation);
                    spiralStairs.definition.Reset();
                    spiralStairs.Operation		= forceOperation ?? CSGOperationType.Additive;
                    spiralStairs.StepHeight     = stepHeight;
                    spiralStairs.Height			= height;
                    spiralStairs.OuterDiameter	= bounds.size[(int)Axis.X];
                    spiralStairs.OuterSegments  = outerSegments;
                    spiralStairs.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    spiralStairs.Operation      = forceOperation ?? 
                                                  ((height < 0 && modelBeneathCursor) ? 
                                                    CSGOperationType.Subtractive : 
                                                    CSGOperationType.Additive);
                    spiralStairs.Height			= height;
                    spiralStairs.OuterDiameter	= bounds.size[(int)Axis.X];
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = spiralStairs.gameObject;
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

            HandleRendering.RenderCylinder(transformation, bounds, (spiralStairs) ? spiralStairs.OuterSegments : outerSegments);
        }
    }
}
