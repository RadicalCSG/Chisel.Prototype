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
    public sealed class ChiselBoxGeneratorMode : ChiselGeneratorToolMode
    {
        const string kToolName = ChiselBox.kNodeTypeName;
        public override string ToolName => kToolName;

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.BoxBuilderModeKey, ChiselKeyboardDefaults.BoxBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselEditModeManager.EditModeType = typeof(ChiselBoxGeneratorMode); }
        #endregion

        public override void Reset()
        {
            BoxExtrusionHandle.Reset();
            box = null;
        }
        
        ChiselBox box;

        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;
        
        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool generateFromCenterXZ = false;

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            base.OnSceneGUI(sceneView, dragArea);

            var flags = (generateFromCenterXZ ? BoxExtrusionFlags.GenerateFromCenterXZ : BoxExtrusionFlags.None);

            switch (BoxExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    box = ChiselComponentFactory.Create<ChiselBox>("Box",
                                                      ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                      transformation);
                    box.definition.Reset();
                    box.Operation   = forceOperation ?? CSGOperationType.Additive;
                    box.Bounds      = bounds;
                    box.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    box.Operation = forceOperation ??
                                    ((height < 0 && modelBeneathCursor) ?
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    box.Bounds = bounds;
                    break;
                }
                
                case BoxExtrusionState.Commit:      { Commit(box.gameObject); break; }
                case BoxExtrusionState.Cancel:      { Cancel(); break; }                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderBox(transformation, bounds);
        }
    }
}
