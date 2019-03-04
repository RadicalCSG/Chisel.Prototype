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
        bool generateFromCenter = true;
        bool isEllipsoid = false;
        int sides = 16;

        CSGSphere sphere;

        public void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
            Bounds bounds;
            CSGModel modelBeneathCursor;
            Matrix4x4 transformation;
            float height;

            switch (BoxExtrusionHandle.Do(dragArea, out bounds, out height, out modelBeneathCursor, out transformation, !isEllipsoid, generateFromCenter, Axis.Y))
            {
                case BoxExtrusionState.Create:
                    {
                        sphere = BrushMeshAssetFactory.Create<CSGSphere>("Cylinder",
                                                                    BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor),
                                                                    transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));

                        sphere.Operation = forceOperation ?? CSGOperationType.Additive;
                        sphere.DiameterXYZ = new Vector3(bounds.size[(int)Axis.X], Math.Abs(height), bounds.size[(int)Axis.Z]);
                        sphere.UpdateGenerator();
                        break;
                    }

                case BoxExtrusionState.Modified:
                    {
                        sphere.Operation = forceOperation ??
                                        ((height <= 0 && modelBeneathCursor) ?
                                            CSGOperationType.Subtractive :
                                            CSGOperationType.Additive);

                        sphere.DiameterXYZ = new Vector3(bounds.size[(int)Axis.X], Math.Abs(height), bounds.size[(int)Axis.Z]);
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
                        if (sphere)
                            UnityEngine.Object.DestroyImmediate(sphere.gameObject);
                        Reset();
                        break;
                    }

                case BoxExtrusionState.BoxMode:
                case BoxExtrusionState.SquareMode: { CSGOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline; break; }
                case BoxExtrusionState.HoverMode: { CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline; break; }

            }

            // Todo make a RenderSphere method
            var offsetBounds = bounds;
            var yOffset = height > 0 ? bounds.center.y - bounds.extents.y : bounds.center.y + bounds.extents.y;
            offsetBounds.center = new Vector3(bounds.center.x, yOffset, bounds.center.z);
            HandleRendering.RenderCylinder(transformation, offsetBounds, 10);
        }
	}
}
