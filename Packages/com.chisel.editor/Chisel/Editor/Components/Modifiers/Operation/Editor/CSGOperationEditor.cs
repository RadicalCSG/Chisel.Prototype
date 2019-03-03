using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Components;

namespace Chisel.Editors
{
	[CustomEditor(typeof(CSGOperation))]
	[CanEditMultipleObjects]
	public sealed class CSGOperationEditor : Editor
	{
		SerializedProperty operationProp;
		SerializedProperty passThroughProp;

		internal void OnEnable()
		{
			if (!target)
			{
				operationProp = null;
				passThroughProp = null;
				return;
			}
			// Fetch the objects from the GameObject script to display in the inspector
			operationProp = serializedObject.FindProperty("operation");
			passThroughProp = serializedObject.FindProperty("passThrough");
		}

		internal void OnDisable()
		{
			operationProp = null;
			passThroughProp = null;
		}

		public Bounds OnGetFrameBounds() { return CSGNodeEditor.CalculateBounds(targets); }
		public bool HasFrameBounds() { if (targets == null) return false; return true; }
		
		public override void OnInspectorGUI()
		{
			CSGNodeEditor.CheckForTransformationChanges(serializedObject);
			CSGNodeEditor.ShowDefaultModelMessage(serializedObject.targetObjects);
			try
			{
				bool passThroughChanged = false;
				EditorGUI.BeginChangeCheck();
				{
					EditorGUI.BeginChangeCheck();
					{
						EditorGUILayout.PropertyField(passThroughProp);
					}
					if (EditorGUI.EndChangeCheck()) { passThroughChanged = true; }
					if (!passThroughProp.boolValue)
						EditorGUILayout.PropertyField(operationProp);
				}
				if (EditorGUI.EndChangeCheck())
				{
					serializedObject.ApplyModifiedProperties();
					if (passThroughChanged)
					{
						foreach (var target in serializedObject.targetObjects)
						{
							var operation = target as CSGOperation;
							if (!operation)
								continue;

							CSGNodeHierarchyManager.UpdateAvailability(operation);
						}
					}
				}
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }
		}
	}
}