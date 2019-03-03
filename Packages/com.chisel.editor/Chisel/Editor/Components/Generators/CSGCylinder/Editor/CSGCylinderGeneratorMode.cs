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
		
		CSGCylinder cylinder;
		
		// TODO: Handle forcing operation types
		CSGOperationType? forceOperation = null;
		
		// TODO: Ability to modify default settings
		// TODO: Store/retrieve default settings
		bool				generateFromCenter	= true;
		CylinderShapeType	cylinderType		= CylinderShapeType.Cylinder;
		bool				isEllipsoid			= false;
		int					sides				= 16;

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
					cylinder = BrushMeshAssetFactory.Create<CSGCylinder>("Cylinder",
																BrushMeshAssetFactory.GetModelForNode(modelBeneathCursor), 
																transformation * Matrix4x4.TRS(bounds.center, Quaternion.identity, Vector3.one));
					cylinder.Operation		= forceOperation ?? CSGOperationType.Additive;
					cylinder.IsEllipsoid	= isEllipsoid;
					cylinder.Type			= cylinderType;
					cylinder.Height			= height;
					cylinder.Sides			= sides;
					cylinder.BottomDiameterX	= bounds.extents[(int)Axis.X];
					cylinder.BottomDiameterZ	= bounds.extents[(int)Axis.Z];
					cylinder.UpdateGenerator();
					break;
				}

				case BoxExtrusionState.Modified:
				{
					cylinder.Operation = forceOperation ?? 
									((height <= 0 && modelBeneathCursor) ? 
										CSGOperationType.Subtractive : 
										CSGOperationType.Additive);
					cylinder.Height			= height;
					cylinder.BottomDiameterX	= bounds.extents[(int)Axis.X];
					cylinder.BottomDiameterZ	= bounds.extents[(int)Axis.Z];
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
					if (cylinder)
						UnityEngine.Object.DestroyImmediate(cylinder.gameObject);
					Reset();
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
