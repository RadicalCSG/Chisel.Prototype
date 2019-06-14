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
    public sealed class ChiselHemisphereGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselHemisphere.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.HemisphereBuilderModeKey, ChiselKeyboardDefaults.HemisphereBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselHemisphereGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
            hemisphere = null;
        }

        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool    generateFromCenterXZ    = true;
        bool    isSymmetrical           = true;
        int	    horizontalSegments      = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        int	    verticalSegments        = ChiselHemisphereDefinition.kDefaultVerticalSegments;

        ChiselHemisphere hemisphere;
        
        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);

            var flags = (isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                       (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    hemisphere = ChiselComponentFactory.Create<ChiselHemisphere>("Hemisphere",
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);
                    hemisphere.definition.Reset();
                    hemisphere.Operation            = forceOperation ?? CSGOperationType.Additive;
                    hemisphere.VerticalSegments     = verticalSegments;
                    hemisphere.HorizontalSegments   = horizontalSegments;
                    hemisphere.DiameterXYZ          = bounds.size;
                    hemisphere.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    hemisphere.Operation    = forceOperation ??
                                              ((height < 0 && modelBeneathCursor) ?
                                                CSGOperationType.Subtractive :
                                                CSGOperationType.Additive);
                    hemisphere.DiameterXYZ  = bounds.size;
                    break;
                }
                
                
                case BoxExtrusionState.Commit:      { Commit(hemisphere.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: render hemisphere here
            HandleRendering.RenderCylinder(transformation, bounds, (hemisphere) ? hemisphere.HorizontalSegments : horizontalSegments);
        }
    }
}
