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
    public sealed class ChiselHemisphereSettings
    {
        public int  horizontalSegments      = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        public int  verticalSegments        = ChiselHemisphereDefinition.kDefaultVerticalSegments;
        public bool isSymmetrical           = true;
        public bool generateFromCenterXZ    = true;
    }

    public sealed class ChiselHemisphereGeneratorMode : ChiselGeneratorModeWithSettings<ChiselHemisphereSettings, ChiselHemisphere>
    {
        const string kToolName = ChiselHemisphere.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.HemisphereBuilderModeKey, ChiselKeyboardDefaults.HemisphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselHemisphereGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // TODO: add support for XYZ symmetrical mode (symmetric along all 3 axi, except Y is half sized)
            var flags = (settings.isSymmetrical        ? BoxExtrusionFlags.IsSymmetricalXZ      : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselHemisphere>(ChiselHemisphere.kNodeTypeName,
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation            = forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.VerticalSegments     = settings.verticalSegments;
                    generatedComponent.HorizontalSegments   = settings.horizontalSegments;
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

            // TODO: render hemisphere here
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.HorizontalSegments : settings.horizontalSegments);
        }
    }
}
