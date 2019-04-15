using Chisel.Assets;
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
    public sealed class CSGLinearStairsGeneratorMode : ICSGToolMode
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
        
        CSGLinearStairs linearStairs;
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds bounds;
            CSGModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;
            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, false, false, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    linearStairs = BrushMeshAssetFactory.Create<CSGLinearStairs>("Linear Stairs",
                                                                        BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                                        transformation);
                    linearStairs.Operation = forceOperation ?? CSGOperationType.Additive;
                    linearStairs.Bounds = bounds;
                    linearStairs.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    linearStairs.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    linearStairs.definition.Reset();
                    linearStairs.Bounds = bounds;
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = linearStairs.gameObject;

                    // Recenter stairs
                    // TODO: turn into method
                    var transform = linearStairs.transform;
                    Undo.RecordObjects(new UnityEngine.Object[] { linearStairs, transform }, "Modified Linear Stairs");
                    transform.localPosition = transform.localToWorldMatrix.MultiplyPoint(bounds.center);
                    bounds.center = Vector3.zero;
                    linearStairs.Bounds = bounds;

                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }
                case BoxExtrusionState.Cancel:
                {
                    if (linearStairs)
                        UnityEngine.Object.DestroyImmediate(linearStairs.gameObject);
                    Reset();
                    break;
                }
                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }

            }
            HandleRendering.RenderBox(transformation, bounds);
        }
    }
}
