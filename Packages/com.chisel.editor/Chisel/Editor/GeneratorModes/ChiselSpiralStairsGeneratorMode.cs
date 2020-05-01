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
    public sealed class ChiselSpiralStairsSettings
    {
        // TODO: add more settings
        public float    stepHeight              = ChiselSpiralStairsDefinition.kDefaultStepHeight;
        public int      outerSegments           = ChiselSpiralStairsDefinition.kDefaultOuterSegments;
        public bool     generateFromCenterXZ    = true;
    }

    public sealed class ChiselSpiralStairsGeneratorMode : ChiselGeneratorModeWithSettings<ChiselSpiralStairsSettings, ChiselSpiralStairs>
    {
        const string kToolName = ChiselSpiralStairs.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SpiralStairsBuilderModeKey, ChiselKeyboardDefaults.SpiralStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselSpiralStairsGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var flags = BoxExtrusionFlags.AlwaysFaceUp |
                        BoxExtrusionFlags.IsSymmetricalXZ |
                        (settings.generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y, snappingSteps: settings.stepHeight))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselSpiralStairs>(ChiselSpiralStairs.kNodeTypeName,
                                                                        ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                        transformation);
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation	  = forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.StepHeight     = settings.stepHeight;
                    generatedComponent.Height         = height;
                    generatedComponent.OuterDiameter  = bounds.size[(int)Axis.X];
                    generatedComponent.OuterSegments  = settings.outerSegments;
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    generatedComponent.Operation      = forceOperation ?? 
                                                          ((height < 0 && modelBeneathCursor) ? 
                                                            CSGOperationType.Subtractive : 
                                                            CSGOperationType.Additive);
                    generatedComponent.Height			= bounds.size.y;
                    generatedComponent.OuterDiameter	= bounds.size[(int)Axis.X];
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(generatedComponent.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.OuterSegments : settings.outerSegments);
        }
    }
}
