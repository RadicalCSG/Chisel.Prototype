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
    public sealed class ChiselCapsuleSettings
    {
        public float    topHeight            = ChiselCapsuleDefinition.kDefaultHemisphereHeight;
        public int      topSegments			 = ChiselCapsuleDefinition.kDefaultTopSegments;
        public float    bottomHeight         = ChiselCapsuleDefinition.kDefaultHemisphereHeight;
        public int	    bottomSegments	     = ChiselCapsuleDefinition.kDefaultBottomSegments;
        public int      sides				 = ChiselCapsuleDefinition.kDefaultSides;
        public bool     isSymmetrical        = true;
        public bool     generateFromCenterY  = false;
        public bool     generateFromCenterXZ = true;
    }

    // TODO: maybe just bevel top of cylinder instead of separate capsule generator??
    public sealed class ChiselCapsuleGeneratorMode : ChiselGeneratorModeWithSettings<ChiselCapsuleSettings, ChiselCapsule>
    {
        const string kToolName = ChiselCapsule.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.CapsuleBuilderModeKey, ChiselKeyboardDefaults.CapsuleBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselCapsuleGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            var flags = (settings.isSymmetrical          ? BoxExtrusionFlags.IsSymmetricalXZ      : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterY    ? BoxExtrusionFlags.GenerateFromCenterY  : BoxExtrusionFlags.None) |
                        (settings.generateFromCenterXZ   ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    generatedComponent = ChiselComponentFactory.Create<ChiselCapsule>(ChiselCapsule.kNodeTypeName,
                                                                ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                transformation);
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation       = forceOperation ?? CSGOperationType.Additive;
                    generatedComponent.Sides           = settings.sides;
                    generatedComponent.TopSegments     = settings.topSegments;
                    generatedComponent.BottomSegments  = settings.bottomSegments;
                    generatedComponent.TopHeight       = settings.topHeight;
                    generatedComponent.BottomHeight    = settings.bottomHeight;
                    generatedComponent.DiameterX       = bounds.size[(int)Axis.X];
                    generatedComponent.Height          = height;
                    generatedComponent.DiameterZ       = bounds.size[(int)Axis.Z];
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    generatedComponent.Operation = forceOperation ??
                                              ((height < 0 && modelBeneathCursor) ?
                                                CSGOperationType.Subtractive :
                                                CSGOperationType.Additive);

                    var hemisphereHeight = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

                    generatedComponent.TopHeight    = Mathf.Min(hemisphereHeight, height * 0.5f);
                    generatedComponent.BottomHeight = Mathf.Min(hemisphereHeight, height * 0.5f);
                    generatedComponent.DiameterX    = bounds.size[(int)Axis.X];
                    generatedComponent.Height       = height;
                    generatedComponent.DiameterZ    = bounds.size[(int)Axis.Z];
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(generatedComponent.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }   
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            // TODO: render capsule here
            HandleRendering.RenderCylinder(transformation, bounds, (generatedComponent) ? generatedComponent.Sides : settings.sides);
        }
    }
}
