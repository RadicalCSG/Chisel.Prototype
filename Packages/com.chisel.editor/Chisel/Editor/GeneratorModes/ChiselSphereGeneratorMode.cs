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
    public sealed class ChiselSphereSettings
    {
        public int  horizontalSegments      = ChiselSphereDefinition.kDefaultHorizontalSegments;
        public int  verticalSegments        = ChiselSphereDefinition.kDefaultVerticalSegments;
        
        public bool isSymmetrical           = true;
        public bool generateFromCenterY     = ChiselSphereDefinition.kDefaultGenerateFromCenter;
        public bool generateFromCenterXZ    = true;
    }

    public sealed class ChiselSphereGeneratorMode : ChiselGeneratorModeWithSettings<ChiselSphereSettings, ChiselSphere>
    {
        const string kToolName = ChiselSphere.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SphereBuilderModeKey, ChiselKeyboardDefaults.SphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselSphereGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // TODO: add support for XYZ symmetrical mode (symmetric along all 3 axi)
            var flags = (settings.isSymmetrical          ? BoxExtrusionFlags.IsSymmetricalXZ      : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterY    ? BoxExtrusionFlags.GenerateFromCenterY  : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterXZ   ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselSphere>(ChiselSphere.kNodeTypeName,
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);

                    generatedComponent.definition.Reset();
                    generatedComponent.Operation            = forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.VerticalSegments     = settings.verticalSegments;
                    generatedComponent.HorizontalSegments   = settings.horizontalSegments;
                    generatedComponent.GenerateFromCenter   = settings.generateFromCenterY;
                    generatedComponent.DiameterXYZ          = bounds.size;
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    generatedComponent.Operation    = forceOperation ??
                                          ((height < 0 && modelBeneathCursor) ?
                                            CSGOperationType.Subtractive :
                                            CSGOperationType.Additive);
                    generatedComponent.DiameterXYZ  = bounds.size;
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(generatedComponent.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: Make a RenderSphere method
            HandleRendering.RenderCylinder(transformation, bounds, settings.horizontalSegments);
        }
    }
}
