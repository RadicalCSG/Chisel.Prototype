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
    public sealed class ChiselSphereGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselSphere.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.SphereBuilderModeKey, ChiselKeyboardDefaults.SphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselSphereGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
            sphere = null;
        }

        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool generateFromCenterXZ   = true;
        bool generateFromCenterY    = ChiselSphereDefinition.kDefaultGenerateFromCenter;
        bool isSymmetrical          = true;
        int verticalSegments        = ChiselSphereDefinition.kDefaultVerticalSegments;
        int horizontalSegments      = ChiselSphereDefinition.kDefaultHorizontalSegments;


        ChiselSphere sphere;

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);

            var flags = (generateFromCenterY  ? BoxExtrusionFlags.GenerateFromCenterY  : BoxExtrusionFlags.None) |
                        (isSymmetrical        ? BoxExtrusionFlags.IsSymmetricalXZ      : BoxExtrusionFlags.None) |
                        (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    sphere = ChiselComponentFactory.Create<ChiselSphere>("Sphere",
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);

                    sphere.definition.Reset();
                    sphere.Operation            = forceOperation ?? CSGOperationType.Additive;
                    sphere.VerticalSegments     = verticalSegments;
                    sphere.HorizontalSegments   = horizontalSegments;
                    sphere.GenerateFromCenter   = generateFromCenterY;
                    sphere.DiameterXYZ          = bounds.size;
                    sphere.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    sphere.Operation    = forceOperation ??
                                          ((height < 0 && modelBeneathCursor) ?
                                            CSGOperationType.Subtractive :
                                            CSGOperationType.Additive);
                    sphere.DiameterXYZ  = bounds.size;
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(sphere.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: Make a RenderSphere method
            HandleRendering.RenderCylinder(transformation, bounds, horizontalSegments);
        }
    }
}
