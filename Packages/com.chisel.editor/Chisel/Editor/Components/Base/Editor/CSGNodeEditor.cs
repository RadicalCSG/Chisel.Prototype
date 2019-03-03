using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
	[CustomEditor(typeof(CSGNode), isFallback = true)]
	[CanEditMultipleObjects]
	public sealed class CSGNodeEditor : Editor
	{
		static readonly GUIContent  DefaultModelContents = new GUIContent("This node is not a child of a model, and is added to the default model. It is recommended that you explictly add this node to a model.");

		public Bounds OnGetFrameBounds() { return CSGNodeEditor.CalculateBounds(targets); }
		public bool HasFrameBounds() { if (targets == null) return false; return true; }

		public static Bounds CalculateBounds(UnityEngine.Object[] targets)
		{
			var bounds = new Bounds();
			foreach (var target in targets)
			{
				var node = target as CSGNode;
				if (!node)
					continue;

				node.EncapsulateBounds(ref bounds);
			}
			return bounds;
		}

		public static void ForceUpdateNodeContents(SerializedObject serializedObject)
		{
			serializedObject.ApplyModifiedProperties();
			foreach (var target in serializedObject.targetObjects)
			{
				var node = target as CSGNode;
				if (!node)
					continue;
				CSGNodeHierarchyManager.NotifyContentsModified(node);
				node.SetDirty();
			}
		}

		public static bool IsPartOfDefaultModel(UnityEngine.Object[] targetObjects)
		{
			if (targetObjects == null)
				return false;

			for (int i = 0; i < targetObjects.Length; i++)
			{
				CSGNode node = targetObjects[i] as CSGNode;
				if (Equals(node, null))
				{
					var gameObject = targetObjects[i] as GameObject;
					if (gameObject)
						node = gameObject.GetComponent<CSGNode>();
				}
				if (node)
				{
					if (CSGGeneratedComponentManager.IsDefaultModel(node.hierarchyItem.Model))
						return true;
				}
			}
			return false;
		}

		public static void ShowDefaultModelMessage(UnityEngine.Object[] targetObjects)
		{
			if (!IsPartOfDefaultModel(targetObjects))
				return;

			EditorGUILayout.HelpBox(DefaultModelContents.text, MessageType.Warning);
		}

		static HashSet<CSGNode> modifiedNodes = new HashSet<CSGNode>();
		public static void CheckForTransformationChanges(SerializedObject serializedObject)
		{
			if (Event.current.type == EventType.Layout)
			{
				modifiedNodes.Clear();
				foreach (var target in serializedObject.targetObjects)
				{
					var node = target as CSGNode;
					if (!node)
						continue;
					var transform = node.transform;

					// TODO: probably not a good idea to use matrices for this, since it calculates this all the way up the transformation tree
					var curLocalToWorldMatrix = transform.localToWorldMatrix;
					var oldLocalToWorldMatrix = node.hierarchyItem.LocalToWorldMatrix;
					if (curLocalToWorldMatrix.m00 != oldLocalToWorldMatrix.m00 ||
						curLocalToWorldMatrix.m01 != oldLocalToWorldMatrix.m01 ||
						curLocalToWorldMatrix.m02 != oldLocalToWorldMatrix.m02 ||
						curLocalToWorldMatrix.m03 != oldLocalToWorldMatrix.m03 ||

						curLocalToWorldMatrix.m10 != oldLocalToWorldMatrix.m10 ||
						curLocalToWorldMatrix.m11 != oldLocalToWorldMatrix.m11 ||
						curLocalToWorldMatrix.m12 != oldLocalToWorldMatrix.m12 ||
						curLocalToWorldMatrix.m13 != oldLocalToWorldMatrix.m13 ||

						curLocalToWorldMatrix.m20 != oldLocalToWorldMatrix.m20 ||
						curLocalToWorldMatrix.m21 != oldLocalToWorldMatrix.m21 ||
						curLocalToWorldMatrix.m22 != oldLocalToWorldMatrix.m22 ||
						curLocalToWorldMatrix.m23 != oldLocalToWorldMatrix.m23 //||

						//curLocalToWorldMatrix.m30 != oldLocalToWorldMatrix.m30 ||
						//curLocalToWorldMatrix.m31 != oldLocalToWorldMatrix.m31 ||
						//curLocalToWorldMatrix.m32 != oldLocalToWorldMatrix.m32 ||
						//curLocalToWorldMatrix.m33 != oldLocalToWorldMatrix.m33
						)
					{
						node.hierarchyItem.LocalToWorldMatrix = curLocalToWorldMatrix;
						node.hierarchyItem.WorldToLocalMatrix = transform.worldToLocalMatrix;
						modifiedNodes.Add(node);
					}
				}
				if (modifiedNodes.Count > 0)
				{
					CSGNodeHierarchyManager.NotifyTransformationChanged(modifiedNodes);
				}
			}
		}

		static readonly GUIContent convertToBrushesContent  = new GUIContent("Convert to Brushes");
		static readonly GUIContent convertToBrushContent    = new GUIContent("Convert to Brush");


		// TODO: put somewhere else
		internal static void ConvertIntoBrushesButton(SerializedObject serializedObject)
		{
			bool singular = false;
			bool multiple = false;
			foreach (var targetObject in serializedObject.targetObjects)
			{
				var node = targetObject as CSGNode;
				if (!node)
					continue;
				var count = node.GetAllTreeBrushCount();
				singular = (count == 1) || singular;
				multiple = (count > 1) || multiple;
			}
			if (multiple)
			{
				if (!GUILayout.Button(convertToBrushesContent))
					return;
			} else
			if (singular)
			{
				if (!GUILayout.Button(convertToBrushContent))
					return;
			} else
				return;

			bool modified = false;
			foreach (var targetObject in serializedObject.targetObjects)
			{
				var node = targetObject as CSGNode;
				if (!node)
					continue;

				modified = node.ConvertToBrushes() || modified;
			}

			if (modified)
				EditorGUIUtility.ExitGUI();
		}

		class Styles
        {
            const string AdditiveIconTooltip = "Additive CSG Operation";
            const string SubtractiveIconTooltip = "Subtractive CSG Operation";
            const string IntersectingIconTooltip = "Intersecting CSG Operation";

            public readonly GUIContent[] AdditiveIcon;
            public readonly GUIContent[] SubtractiveIcon;
            public readonly GUIContent[] IntersectingIcon;

            public GUIStyle[]	leftButton	= new GUIStyle[2];
			public GUIStyle[]	midButton	= new GUIStyle[2];
			public GUIStyle[]	rightButton = new GUIStyle[2];

			public Styles()
            {
                AdditiveIcon = new[] {
                    CSGDefaults.Style.AdditiveImages[0] == null ? new GUIContent(AdditiveIconTooltip) : new GUIContent(CSGDefaults.Style.AdditiveImages[0], AdditiveIconTooltip),
                    CSGDefaults.Style.AdditiveImages[1] == null ? new GUIContent(AdditiveIconTooltip) : new GUIContent(CSGDefaults.Style.AdditiveImages[1], AdditiveIconTooltip)
                };
                SubtractiveIcon = new[] {
                    CSGDefaults.Style.SubtractiveImages[0] == null ? new GUIContent(SubtractiveIconTooltip) : new GUIContent(CSGDefaults.Style.SubtractiveImages[0], SubtractiveIconTooltip),
                    CSGDefaults.Style.SubtractiveImages[0] == null ? new GUIContent(SubtractiveIconTooltip) : new GUIContent(CSGDefaults.Style.SubtractiveImages[1], SubtractiveIconTooltip)
                };
                IntersectingIcon = new[] {
                    CSGDefaults.Style.IntersectingImages[0] == null ? new GUIContent(IntersectingIconTooltip) : new GUIContent(CSGDefaults.Style.IntersectingImages[0], IntersectingIconTooltip),
                    CSGDefaults.Style.IntersectingImages[0] == null ? new GUIContent(IntersectingIconTooltip) : new GUIContent(CSGDefaults.Style.IntersectingImages[1], IntersectingIconTooltip)
                };

                leftButton[0] = new GUIStyle(EditorStyles.miniButtonLeft) { stretchWidth  = false, stretchHeight = false };
			    leftButton[0].padding.top    += 1;
				leftButton[0].padding.bottom += 2;
				leftButton[1] = new GUIStyle(leftButton[0]);
				leftButton[1].normal.background = leftButton[0].active.background;

				midButton[0] = new GUIStyle(EditorStyles.miniButtonMid) { stretchWidth = false, stretchHeight = false };
				midButton[0].padding.top += 1;
				midButton[0].padding.bottom += 2;
				midButton[1] = new GUIStyle(midButton[0]);
				midButton[1].normal.background = midButton[0].active.background;

				rightButton[0] = new GUIStyle(EditorStyles.miniButtonRight) { stretchWidth = false, stretchHeight = false };
				rightButton[0].padding.top += 1;
				rightButton[0].padding.bottom += 2;
				rightButton[1] = new GUIStyle(rightButton[0]);
				rightButton[1].normal.background = rightButton[0].active.background;
			}
		};

		static Styles styles;

		static bool Toggle(bool selected, GUIContent[] content, GUIStyle[] style)
		{
			return GUILayout.Button(selected ? content[1] : content[0], selected ? style[1] : style[0]);
		}

		// TODO: put somewhere else
		internal static void ShowOperationChoices(SerializedProperty operationProp)
		{
			if (styles == null)
				styles = new Styles();
			EditorGUILayout.BeginHorizontal();
			var operation = operationProp.hasMultipleDifferentValues ? ((CSGOperationType)255) : ((CSGOperationType)operationProp.enumValueIndex);
			if (Toggle((operation == CSGOperationType.Additive)     , styles.AdditiveIcon, styles.leftButton))
				operationProp.enumValueIndex = (int)CSGOperationType.Additive;
			if (Toggle((operation == CSGOperationType.Subtractive)	, styles.SubtractiveIcon , styles.midButton))
				operationProp.enumValueIndex = (int)CSGOperationType.Subtractive;
			if (Toggle((operation == CSGOperationType.Intersecting) , styles.IntersectingIcon, styles.rightButton))
				operationProp.enumValueIndex = (int)CSGOperationType.Intersecting;
			//EditorGUILayout.PropertyField(operationProp);
			EditorGUILayout.EndHorizontal();
		}

		public override void OnInspectorGUI()
		{
			CSGNodeEditor.CheckForTransformationChanges(serializedObject);
			ShowDefaultModelMessage(serializedObject.targetObjects);
		}


		// TODO: put in it's own class
		public static void HierarchyWindowItemOnGUI(CSGNode node, Rect selectionRect)
		{
			// TODO: implement material drag & drop support

			var icon = node.Icon;
			if (icon == null)
				return;

			const float iconSize = 32;
			const float indent	 = 4;
			var max = selectionRect.xMax;
			selectionRect.width  = iconSize;
			selectionRect.height = iconSize;
			selectionRect.x      = max - (iconSize + indent);
			selectionRect.y--;
			GUI.Label(selectionRect, icon);
		}
	}
}
