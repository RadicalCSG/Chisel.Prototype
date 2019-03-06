using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Assets;
using Chisel.Utilities;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class CSGBoxGeneratorMode : ICSGToolMode
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
            box = null;
        }
        
        CSGBox box;
        Vector3 offset;
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
                    box = BrushMeshAssetFactory.Create<CSGBox>("Box",
                                                      BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                      transformation);

                    box.Operation = forceOperation ?? CSGOperationType.Additive;
                    offset = bounds.center;
                    box.transform.localPosition += box.transform.localToWorldMatrix.MultiplyVector(offset);
                    bounds.center -= offset;
                    box.Bounds = bounds;
                    box.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    //box.transform.localPosition += box.transform.localToWorldMatrix.MultiplyVector(bounds.center - offset);
                    //offset = bounds.center;
                    box.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    bounds.center -= offset;
                    box.Bounds = bounds;
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = box.gameObject;
                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }
                case BoxExtrusionState.Cancel:
                {
                    if (box)
                        UnityEngine.Object.DestroyImmediate(box.gameObject);
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
