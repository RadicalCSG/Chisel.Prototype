using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Utilities;
using Chisel.Components;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public sealed class ChiselExtrudedShapeGeneratorMode : IChiselToolMode
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
            ShapeExtrusionHandle.Reset();
            extrudedShape = null;
        }
        
        ChiselExtrudedShape extrudedShape;
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Curve2D shape;
            ChiselModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;
            // TODO: handle snapping against own points
            // TODO: handle ability to 'commit' last point
            switch (ShapeExtrusionHandle.Do(dragArea, out shape, out height, out modelBeneathCursor, out transformation, Axis.Y))
            {
                case ShapeExtrusionState.Create:
                {
                    var center2D = shape.Center;
                    var center3D = new Vector3(center2D.x, 0, center2D.y);
                    extrudedShape = ChiselComponentFactory.Create<ChiselExtrudedShape>("Extruded Shape",
                                                                          ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor), 
                                                                          transformation * Matrix4x4.TRS(center3D, Quaternion.identity, Vector3.one));
                    shape.Center = Vector2.zero;

                    extrudedShape.Operation = forceOperation ?? CSGOperationType.Additive;
                    extrudedShape.Shape = new Curve2D(shape);
                    extrudedShape.Path = new ChiselPath(new[] {
                        new ChiselPathPoint(Vector3.zero),
                        new ChiselPathPoint(new Vector3(0,1,0))
                    });
                    extrudedShape.UpdateGenerator();
                    break;
                }

                case ShapeExtrusionState.Modified:
                {
                    extrudedShape.Operation = forceOperation ?? 
                                              ((height < 0 && modelBeneathCursor) ? 
                                                CSGOperationType.Subtractive : 
                                                CSGOperationType.Additive);
                    extrudedShape.Path.segments[1].position = new Vector3(0, height, 0);
                    extrudedShape.UpdateGenerator();
                    break;
                }
                
                case ShapeExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = extrudedShape.gameObject;
                    Reset();
                    ChiselEditModeManager.EditMode = ChiselEditMode.ShapeEdit;
                    break;
                }

                case ShapeExtrusionState.Cancel:
                {
                    Reset();
                    Undo.RevertAllInCurrentGroup();
                    EditorGUIUtility.ExitGUI();
                    break;
                }
                
                case ShapeExtrusionState.ExtrusionMode:
                case ShapeExtrusionState.ShapeMode:		{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case ShapeExtrusionState.HoverMode:		{ ChiselOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }
            }

            HandleRendering.RenderShape(transformation, shape, height);
        }
    }
}
