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
    public sealed class CSGHemisphereGeneratorMode : ICSGToolMode
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
            hemisphere = null;
        }
        
        // TODO: Handle forcing operation types
        CSGOperationType? forceOperation = null;
        
        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool	generateFromCenterXZ    = true;
        bool    isSymmetrical           = true;
        int		horizontalSegments      = CSGHemisphereDefinition.kDefaultHorizontalSegments;
        int		verticalSegments        = CSGHemisphereDefinition.kDefaultVerticalSegments;

        CSGHemisphere hemisphere;

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
                    hemisphere = BrushMeshAssetFactory.Create<CSGHemisphere>("Hemisphere",
                                                                BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor), 
                                                                transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));
                    hemisphere.definition.Reset();
                    hemisphere.Operation			= forceOperation ?? CSGOperationType.Additive;
                    hemisphere.VerticalSegments     = verticalSegments;
                    hemisphere.HorizontalSegments   = horizontalSegments;
                    hemisphere.DiameterXYZ          = new Vector3(bounds.size[(int)Axis.X], height, bounds.size[(int)Axis.Z]);
                    hemisphere.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    hemisphere.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    hemisphere.DiameterXYZ  = new Vector3(bounds.size[(int)Axis.X], height, bounds.size[(int)Axis.Z]);
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = hemisphere.gameObject;
                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }

                case BoxExtrusionState.Cancel:
                {
                    Reset();
                    hemisphere = null;
                    Undo.RevertAllInCurrentGroup();
                    EditorGUIUtility.ExitGUI();
                    break;
                }
                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }

            }
            
            // TODO: render hemisphere here
            HandleRendering.RenderCylinder(transformation, bounds, (hemisphere) ? hemisphere.HorizontalSegments : horizontalSegments);
        }
    }
}
