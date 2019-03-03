using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

// TODO: use, hidden & non savable, dummy gameobjects for sync'ed brushes to simplifiy selection? 
namespace Chisel.Editors
{
	// TODO: should only use this when there are CSGNodes selected
	public class CSGObjectEditMode : ICSGToolMode
	{
		public void OnEnable()
		{
			CSGOutlineRenderer.VisualizationMode = VisualizationMode.Outline;
			// TODO: shouldn't just always set this param
			Tools.hidden = true; 
		}
		
		public void OnDisable()
		{
		}


		public void OnSceneGUI(SceneView sceneView, Rect dragArea)
		{
			switch (Tools.current)
			{
				case Tool.Move:			Tools.hidden = true; OnMoveTool(); break;
				case Tool.Rotate:		// TODO: implement
				case Tool.Scale:		// TODO: implement
				case Tool.Rect:         // TODO: implement
#if UNITY_2017_3_OR_NEWER
				case Tool.Transform:	// TODO: implement
#endif
				default:
				{
					// TODO: shouldn't just always override this param, but keep prev value and set that
					Tools.hidden = false;
					break;
				}
			}
		}

#if SYNC_SUPPORT
		static readonly HashSet<CSGTreeBrush>	foundTreeBrushes	= new HashSet<CSGTreeBrush>();
#endif

		static void OnMoveTool()
		{
			var position = Tools.handlePosition;
			var rotation = Tools.handleRotation;

#if SYNC_SUPPORT // TODO: finish and fix this
			var selectedNodes = Selection.GetFiltered<CSGNode>(SelectionMode.Editable | SelectionMode.TopLevel);
			if (selectedNodes.Length == 0)
			{
				// TODO: probably need to use our own PositionHandle on non CSG objects, to be able to snap to grid
				return;
			}
			

			var transformation		= Matrix4x4.identity;
			var invTransformation	= Matrix4x4.identity;

			// TODO: figure out how to handle this with selecting multiple variants of the same brush (synchronized brushes)
			if (selectedNodes.Length == 1)
			{
				foundTreeBrushes.Clear();
				selectedNodes[0].GetAllTreeBrushes(foundTreeBrushes, ignoreSynchronizedBrushes: true);
				if (foundTreeBrushes.Count == 1)
				{
					var transform = CSGNodeHierarchyManager.FindModelTransformOfTransform(selectedNodes[0].hierarchyItem.Transform);
					var firstBrush = foundTreeBrushes.First();
					var brush = firstBrush;
					if (!CSGSyncSelection.IsBrushVariantSelected(brush))
					{
						List<CSGTreeBrush> selectedVariants = new List<CSGTreeBrush>();
						if (CSGSyncSelection.GetSelectedVariantsOfBrush(brush, selectedVariants))
							brush = selectedVariants[0];
					}
					if (transform)
						transformation = transform.localToWorldMatrix * brush.NodeToTreeSpaceMatrix;
					else
						transformation = brush.NodeToTreeSpaceMatrix;
					rotation = Quaternion.LookRotation(transformation.GetColumn(2), transformation.GetColumn(1));
					invTransformation = selectedNodes[0].transform.localToWorldMatrix * transformation.inverse;
					if (Tools.pivotRotation == PivotRotation.Global)
						rotation = Quaternion.identity;
					position = transformation.GetColumn(3);
				}
			}
#endif
			
			EditorGUI.BeginChangeCheck();
			// TODO: make this work with bounds!
			var newPosition = UnitySceneExtensions.SceneHandles.PositionHandle(position, rotation);
			if (EditorGUI.EndChangeCheck())
			{
				var delta = newPosition - position;
				var transforms = Selection.transforms;
				if (transforms != null && transforms.Length > 0)
				{				
					MoveTransformsTo(transforms, delta);
				}
			}
		}


		static void MoveTransformsTo(Transform[] transforms, Vector3 delta)
		{
			Undo.RecordObjects(transforms, "Move Transforms");
			foreach (var transform in transforms)
				transform.localPosition += delta;
		}
	}
}
