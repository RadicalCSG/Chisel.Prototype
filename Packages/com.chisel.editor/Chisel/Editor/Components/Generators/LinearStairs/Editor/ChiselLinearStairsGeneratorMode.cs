using Chisel.Core;
using Chisel.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Utilities;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class ChiselLinearStairsGeneratorMode : IChiselToolMode
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
            linearStairs = null;
        }
        
        ChiselLinearStairs linearStairs;
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds    bounds;
            ChiselModel  modelBeneathCursor;
            Matrix4x4 transformation;
            float     height;

            var flags = BoxExtrusionFlags.AlwaysFaceUp;

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, flags, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    linearStairs = ChiselModelManager.Create<ChiselLinearStairs>("Linear Stairs",
                                                                        ChiselModelManager.GetModelForNode(modelBeneathCursor),
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
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = linearStairs.gameObject;
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
                case BoxExtrusionState.SquareMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderBox(transformation, bounds);
        }
    }
}
