using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using Chisel.Assets;
using UnitySceneExtensions;

namespace Chisel.Editors
{
	public abstract class GeneratorEditor<T> : Editor
		where T : CSGGeneratorComponent
	{
		public Bounds OnGetFrameBounds()	{ return CSGNodeEditor.CalculateBounds(targets); }
		public bool HasFrameBounds()		{ return true; }

		protected abstract void ResetInspector();
		protected abstract void InitInspector();
		
		protected abstract void OnInspector();
		protected virtual void OnSceneInit(T generator) { }
		protected abstract void OnScene(T generator);
		
		SerializedProperty operationProp;
		void Reset() { operationProp = null; ResetInspector(); }
		void OnDisable() { Reset(); }

		void OnEnable()
		{
			if (!target)
			{
				Reset();
				return;
			}

			operationProp = serializedObject.FindProperty("operation");
			InitInspector();
		}

		public override void OnInspectorGUI()
		{
			if (!target)
				return;

			CSGNodeEditor.CheckForTransformationChanges(serializedObject);
			CSGNodeEditor.ShowDefaultModelMessage(serializedObject.targetObjects);
			try
			{
				EditorGUI.BeginChangeCheck();
				{
					CSGNodeEditor.ShowOperationChoices(operationProp);
					EditorGUILayout.Space();
					CSGNodeEditor.ConvertIntoBrushesButton(serializedObject);

					OnInspector();
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

			using (new UnityEditor.Handles.DrawingScope(UnityEditor.Handles.yAxisColor))
			{
				var generator = target as T;
				if (!generator.isActiveAndEnabled)
					return;

				OnSceneInit(generator);

				var modelMatrix = CSGNodeHierarchyManager.FindModelTransformMatrixOfTransform(generator.hierarchyItem.Transform);
				var brush		= generator.TopNode;
				//foreach (var brush in CSGSyncSelection.GetSelectedVariantsOfBrushOrSelf((CSGTreeBrush)generator.TopNode))
				//foreach (var brush in generator.Node.AllSynchronizedVariants) // <-- this fails when brushes have failed to be created
				{
					//var directSelect = CSGSyncSelection.IsBrushVariantSelected(brush);
					//if (!directSelect)
					//	continue;
					
					UnityEditor.Handles.matrix = modelMatrix * brush.NodeToTreeSpaceMatrix;
					UnityEditor.Handles.color = UnityEditor.Handles.yAxisColor;
				
					OnScene(generator);
				}
			}
		}
	}
}
