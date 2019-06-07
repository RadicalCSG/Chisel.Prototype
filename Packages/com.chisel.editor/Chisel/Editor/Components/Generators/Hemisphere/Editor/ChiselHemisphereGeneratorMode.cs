using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class ChiselHemisphereGeneratorMode : IChiselToolMode
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

                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = hemisphere.gameObject;
                    ChiselEditModeManager.EditMode = ChiselEditMode.ShapeEdit;
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
                case BoxExtrusionState.SquareMode: { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode: { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: render hemisphere here
            HandleRendering.RenderCylinder(transformation, bounds, (hemisphere) ? hemisphere.HorizontalSegments : horizontalSegments);
        }
    }
}
