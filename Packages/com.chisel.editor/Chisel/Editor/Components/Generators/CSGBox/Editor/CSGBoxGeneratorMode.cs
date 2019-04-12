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
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool generateFromCenterXZ = false;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds bounds;
            CSGModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;
            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, isSymmetrical: false, generateFromCenterXZ, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    box = BrushMeshAssetFactory.Create<CSGBox>("Box",
                                                      BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                      transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));
                    box.Operation = forceOperation ?? CSGOperationType.Additive;
                    bounds.center = new Vector3(0, bounds.center.y, 0);
                    box.Bounds = bounds;
                    box.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    box.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    bounds.center = new Vector3(0, bounds.center.y, 0);
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
