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
    // TODO: maybe just bevel top of cylinder instead of separate capsule generator??
    public sealed class ChiselCapsuleGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselCapsule.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CapsuleBuilderModeKey, ChiselKeyboardDefaults.CapsuleBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselCapsuleGeneratorMode); }
        #endregion

        public override void Reset()
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

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);

            var flags = (isSymmetrical ? BoxExtrusionFlags.IsSymmetricalXZ : BoxExtrusionFlags.None) |
                        (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    capsule = ChiselComponentFactory.Create<ChiselCapsule>("Capsule",
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
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

                    var hemisphereHeight = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

                    capsule.TopHeight    = Mathf.Min(hemisphereHeight, height * 0.5f);
                    capsule.BottomHeight = Mathf.Min(hemisphereHeight, height * 0.5f);
                    capsule.DiameterX    = bounds.size[(int)Axis.X];
                    capsule.Height       = height;
                    capsule.DiameterZ    = bounds.size[(int)Axis.Z];
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(capsule.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }   
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: render capsule here
            HandleRendering.RenderCylinder(transformation, bounds, (capsule) ? capsule.Sides : sides);
        }
    }
}
