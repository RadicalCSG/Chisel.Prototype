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
    public sealed class ChiselLinearStairsGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselLinearStairs.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.LinearStairsBuilderModeKey, ChiselKeyboardDefaults.LinearStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselLinearStairsGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
            linearStairs = null;
        }
        
        ChiselLinearStairs linearStairs;
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);

            var flags = BoxExtrusionFlags.AlwaysFaceUp;

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    linearStairs = ChiselComponentFactory.Create<ChiselLinearStairs>("Linear Stairs",
                                                                        ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                        transformation);
                    linearStairs.definition.Reset();
                    linearStairs.Operation  = forceOperation ?? CSGOperationType.Additive;
                    linearStairs.Bounds     = bounds;
                    linearStairs.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    linearStairs.Operation  = forceOperation ?? 
                                              ((height < 0 && modelBeneathCursor) ? 
                                                CSGOperationType.Subtractive : 
                                                CSGOperationType.Additive);
                    linearStairs.Bounds     = bounds;
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(linearStairs.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderBox(transformation, bounds);
        }
    }
}
