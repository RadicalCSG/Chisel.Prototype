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
    public sealed class ChiselBoxSettings
    {
        public bool isSymmetrical		    = false;
        public bool generateFromCenterXZ    = false;
    }

    public sealed class ChiselBoxGeneratorMode : ChiselGeneratorModeWithSettings<ChiselBoxSettings, ChiselBox>
    {
        const string kToolName = ChiselBox.kNodeTypeName;
        public override string  ToolName => kToolName;
        public override string  Group => "Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.BoxBuilderModeKey, ChiselKeyboardDefaults.BoxBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselBoxGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var flags = (settings.isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselBox>(ChiselBox.kNodeTypeName,
                                                      ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                      transformation);
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation   = forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.Bounds      = bounds;
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    generatedComponent.Operation = forceOperation ??
                                                    ((height < 0 && modelBeneathCursor) ?
                                                        CSGOperationType.Subtractive : 
                                                        CSGOperationType.Additive);
                    generatedComponent.Bounds = bounds;
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(generatedComponent.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderBox(transformation, bounds);
        }
    }
}
