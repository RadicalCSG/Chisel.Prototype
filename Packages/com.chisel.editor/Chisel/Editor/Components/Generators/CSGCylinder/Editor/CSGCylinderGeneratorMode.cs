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
    public sealed class CSGCylinderGeneratorMode : ICSGToolMode
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
            cylinder = null;
        }
        
        // TODO: Handle forcing operation types
        CSGOperationType?   forceOperation          = null;
        
        // TODO: Ability to modify default settings
        // TODO: Store/retrieve default settings
        bool				generateFromCenterXZ	= true;
        CylinderShapeType	cylinderType		    = CylinderShapeType.Cylinder;
        bool                isSymmetrical           = true;
        int					sides				    = 16;

        CSGCylinder cylinder;

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
                    cylinder = BrushMeshAssetFactory.Create<CSGCylinder>("Cylinder",
                                                                BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor), 
                                                                transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));
                    //cylinder.definition.Reset();
                    cylinder.Operation			= forceOperation ?? CSGOperationType.Additive;
                    cylinder.IsEllipsoid		= !isSymmetrical;
                    cylinder.Type				= cylinderType;
                    cylinder.Height				= height;
                    cylinder.Sides				= sides;
                    cylinder.BottomDiameterX	= bounds.extents[(int)Axis.X] * 2.0f;
                    cylinder.BottomDiameterZ	= bounds.extents[(int)Axis.Z] * 2.0f;
                    cylinder.UpdateGenerator();
                    break;
                }

                case BoxExtrusionState.Modified:
                {
                    cylinder.Operation = forceOperation ?? 
                                    ((height <= 0 && modelBeneathCursor) ? 
                                        CSGOperationType.Subtractive : 
                                        CSGOperationType.Additive);
                    cylinder.Height			    = height;
                    cylinder.BottomDiameterX	= bounds.extents[(int)Axis.X] * 2.0f;
                    cylinder.BottomDiameterZ	= bounds.extents[(int)Axis.Z] * 2.0f;
                    break;
                }
                
                case BoxExtrusionState.Commit:
                {
                    UnityEditor.Selection.activeGameObject = cylinder.gameObject;
                    Reset();
                    CSGEditModeManager.EditMode = CSGEditMode.ShapeEdit;
                    break;
                }

                case BoxExtrusionState.Cancel:
                {
                    Reset();
                    cylinder = null;
                    Undo.RevertAllInCurrentGroup();
                    EditorGUIUtility.ExitGUI();
                    break;
                }
                
                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode:	{ CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }

            }
            
            HandleRendering.RenderCylinder(transformation, bounds, (cylinder) ? cylinder.Sides : sides);
        }
    }
}
