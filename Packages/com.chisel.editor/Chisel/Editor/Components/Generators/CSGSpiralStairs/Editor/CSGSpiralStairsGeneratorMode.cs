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
    public sealed class CSGSpiralStairsGeneratorMode : ICSGToolMode
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
            spiralStairs = null;
        }
        
        CSGSpiralStairs spiralStairs;
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
                    spiralStairs = BrushMeshAssetFactory.Create<CSGSpiralStairs>("Spiral Stairs",
                                                                        BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                                        transformation);
                    spiralStairs.Operation		= forceOperation ?? CSGOperationType.Additive;
                    spiralStairs.Origin			= bounds.center;
                    spiralStairs.Height			= bounds.size.y;
                    spiralStairs.OuterDiameter	= bounds.size.x * 0.5f;
                    spiralStairs.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    spiralStairs.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    spiralStairs.definition.Reset();
                    spiralStairs.Origin			= bounds.center;
                    spiralStairs.Height			= bounds.size.y;
                    spiralStairs.OuterDiameter	= bounds.size.x * 0.5f;
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = spiralStairs.gameObject;

                    // Recenter stairs
                    // TODO: turn into method
                    var transform = spiralStairs.transform;
                    Undo.RecordObjects(new UnityEngine.Object[] { spiralStairs, transform }, "Modified Spiral Stairs");

                    var center = bounds.center;
                    center.y = bounds.min.y;

                    transform.localPosition = transform.localToWorldMatrix.MultiplyPoint(center);
                    bounds.center = Vector3.zero;
                    spiralStairs.Origin			= Vector3.zero;
                    spiralStairs.Height			= bounds.size.y;
                    spiralStairs.OuterDiameter	= bounds.size.x * 0.5f;

                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }
                case BoxExtrusionState.Cancel:
                {
                    if (spiralStairs)
                        UnityEngine.Object.DestroyImmediate(spiralStairs.gameObject);
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
