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
    // TODO: maybe just bevel top of cylinder instead of separate capsule generator??
    public sealed class ChiselCapsuleGeneratorMode : IChiselToolMode
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
            capsule = null;
        }

        // TODO: Handle forcing operation types
        CSGOperationType?   forceOperation = null;

        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool    generateFromCenterXZ    = true;
        bool    isSymmetrical           = true;
        float   topHeight               = ChiselCapsuleDefinition.kDefaultHemisphereHeight;
        float   bottomHeight            = ChiselCapsuleDefinition.kDefaultHemisphereHeight;
        int	    sides				    = ChiselCapsuleDefinition.kDefaultSides;
        int	    topSegments			    = ChiselCapsuleDefinition.kDefaultTopSegments;
        int	    bottomSegments	        = ChiselCapsuleDefinition.kDefaultBottomSegments;

        ChiselCapsule capsule;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds bounds;
            ChiselModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;

            var flags = (isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                       (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    capsule = ChiselComponentFactory.Create<ChiselCapsule>("Capsule",
                                                                ChiselModelManager.GetModelForNode(modelBeneathCursor),
                                                                transformation);
                    capsule.definition.Reset();
                    capsule.Operation       = forceOperation ?? CSGOperationType.Additive;
                    capsule.Sides           = sides;
                    capsule.TopSegments     = topSegments;
                    capsule.BottomSegments  = bottomSegments;
                    capsule.TopHeight       = topHeight;
                    capsule.BottomHeight    = bottomHeight;
                    capsule.DiameterX       = bounds.size[(int)Axis.X];
                    capsule.Height          = height;
                    capsule.DiameterZ       = bounds.size[(int)Axis.Z];
                    capsule.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    capsule.Operation = forceOperation ??
                                              ((height < 0 && modelBeneathCursor) ?
                                                CSGOperationType.Subtractive :
                                                CSGOperationType.Additive);
                    capsule.TopHeight    = topHeight;
                    capsule.BottomHeight = bottomHeight;
                    capsule.DiameterX    = bounds.size[(int)Axis.X];
                    capsule.Height       = height;
                    capsule.DiameterZ    = bounds.size[(int)Axis.Z];
                    break;
                }

                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = capsule.gameObject;
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

            // TODO: render capsule here
            HandleRendering.RenderCylinder(transformation, bounds, (capsule) ? capsule.Sides : sides);
        }
    }
}
