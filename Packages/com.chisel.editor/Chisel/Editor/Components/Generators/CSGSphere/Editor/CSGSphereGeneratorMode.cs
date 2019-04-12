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
    public sealed class CSGSphereGeneratorMode : ICSGToolMode
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
            sphere = null;
        }

        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;

        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool    generateFromCenterXZ    = true;
        bool    generateFromCenterY     = CSGSphereDefinition.kDefaultGenerateFromCenter;
        bool    isSymmetrical           = true;
        int		verticalSegments        = CSGSphereDefinition.kDefaultVerticalSegments;
        int		horizontalSegments      = CSGSphereDefinition.kDefaultHorizontalSegments;

        CSGSphere sphere;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            Bounds bounds;
            CSGModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, isSymmetrical, generateFromCenterXZ, Axis.Y))
            {
                case BoxExtrusionState.Create:
                {
                    sphere = BrushMeshAssetFactory.Create<CSGSphere>("Sphere",
                                                                BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                                transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));
                    sphere.definition.Reset();
                    sphere.Operation            = forceOperation ?? CSGOperationType.Additive;
                    sphere.VerticalSegments     = verticalSegments;
                    sphere.HorizontalSegments   = horizontalSegments;
                    sphere.GenerateFromCenter   = generateFromCenterY;
                    sphere.DiameterXYZ          = new Vector3(bounds.size[(int)Axis.X], height, bounds.size[(int)Axis.Z]);
                    sphere.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    sphere.Operation = forceOperation ??
                                    ((height <= 0 && modelBeneathCursor) ?
                                        CSGOperationType.Subtractive :
                                        CSGOperationType.Additive);

                    sphere.DiameterXYZ = new Vector3(bounds.size[(int)Axis.X], height, bounds.size[(int)Axis.Z]);
                    break;
                }

                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = sphere.gameObject;
                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }

                case BoxExtrusionState.Cancel:
                {
                    Reset();
                    sphere = null;
                    Undo.RevertAllInCurrentGroup();
                    EditorGUIUtility.ExitGUI();
                    break;
                }

                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:  { CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:   { CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }

            }

            // TODO: make a RenderSphere method
            if (generateFromCenterY)
            { 
                var offsetBounds = bounds;
                var yOffset = height > 0 ? bounds.center.y - bounds.extents.y : bounds.center.y + bounds.extents.y;
                offsetBounds.center = new Vector3(bounds.center.x, yOffset, bounds.center.z);
                HandleRendering.RenderCylinder(transformation, offsetBounds, horizontalSegments);
            } else
                HandleRendering.RenderCylinder(transformation, bounds, horizontalSegments);
        }
    }
}
