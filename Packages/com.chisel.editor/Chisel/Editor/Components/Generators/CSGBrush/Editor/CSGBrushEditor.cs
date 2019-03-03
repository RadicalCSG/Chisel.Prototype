using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
	[CustomEditor(typeof(CSGBrush))]
	[CanEditMultipleObjects]
	public sealed class CSGBrushEditor : Editor
	{
		SerializedProperty operationProp;
		SerializedProperty brushMeshAssetProp;

		internal void OnEnable()
		{
			if (!target)
			{
				operationProp = null;
				brushMeshAssetProp = null;
				return;
			}
			// Fetch the objects from the GameObject script to display in the inspector
			operationProp		= serializedObject.FindProperty("operation");
			brushMeshAssetProp	= serializedObject.FindProperty("brushMeshAsset");
		}

		internal void OnDisable()
		{
			operationProp = null;
			brushMeshAssetProp = null;
		}

		public Bounds OnGetFrameBounds() { return CSGNodeEditor.CalculateBounds(targets); }
		public bool HasFrameBounds() { return true; }


		public override void OnInspectorGUI()
		{
			CSGNodeEditor.CheckForTransformationChanges(serializedObject);
			CSGNodeEditor.ShowDefaultModelMessage(serializedObject.targetObjects);
			try
			{
				EditorGUI.BeginChangeCheck();
				{
					CSGNodeEditor.ShowOperationChoices(operationProp);
					EditorGUILayout.PropertyField(brushMeshAssetProp);
				}
				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
				}
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }
		}

		public void OnSceneGUI()
		{
			if (!target || CSGEditModeManager.EditMode != CSGEditMode.ShapeEdit)
				return;

			var targetBrush				= target as CSGBrush;
			var targetBrushMeshAsset	= targetBrush.BrushMeshAsset;
			if (!targetBrushMeshAsset)
				return;
			
			EditorGUI.BeginChangeCheck();

			var modelMatrix		= CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(targetBrush.hierarchyItem.Transform);
			var vertices		= targetBrushMeshAsset.Vertices;
			var halfEdges		= targetBrushMeshAsset.HalfEdges;

			//HashSet<CSGTreeBrush> foundBrushes = new HashSet<CSGTreeBrush>();
			//targetBrush.GetAllTreeBrushes(foundBrushes, false)
			foreach (var brush in CSGSyncSelection.GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)targetBrush.TopNode))
			{
				var transformation = modelMatrix * brush.NodeToTreeSpaceMatrix;
				for (int e = 0; e < halfEdges.Length; e++)
				{
					var vertexIndex1 = halfEdges[e].vertexIndex;
					var vertexIndex2 = halfEdges[halfEdges[e].twinIndex].vertexIndex;

					var from	= vertices[vertexIndex1];
					var to		= vertices[vertexIndex2];
					CSGOutlineRenderer.DrawLine(transformation, from, to, UnityEditor.Handles.yAxisColor, thickness: 2.5f);
				}
			}

			//var newBounds = CSGHandles.BoundsHandle(originalBounds, Quaternion.identity, CSGHandles.DotHandleCap);

			if (EditorGUI.EndChangeCheck())
			{
				//Undo.RecordObject(target, "Changed shape of Brush");
				//brush.Bounds = newBounds;
			}
		}
	}	
}