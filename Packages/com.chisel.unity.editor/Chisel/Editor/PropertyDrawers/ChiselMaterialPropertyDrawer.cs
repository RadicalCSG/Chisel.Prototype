using System;
using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
#if true
	[CustomPropertyDrawer(typeof(ChiselMaterial))]
	public sealed class ChiselMaterialPropertyDrawer : PropertyDrawer
	{
		static readonly int kBrushMaterialEditorHashCode = (nameof(ChiselMaterialPropertyDrawer) + "Material").GetHashCode();
		
		static readonly GUIContent kRenderMaterialContents = new("Material");

#if !UNITY_2023_1_OR_NEWER
        public override bool CanCacheInspectorGUI(SerializedProperty property) { return true; }
#endif

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUIUtility.singleLineHeight * 4;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (ChiselNodeEditorBase.InSceneSettingsContext)
			{
				EditorGUI.BeginProperty(position, label, property);
				EditorGUI.EndProperty();
				return;
			}

			var chiselMaterialProp = property;
			ChiselUVToolCommon.UpdateChiselMaterials(chiselMaterialProp);

			EditorGUI.BeginProperty(position, label, property);
			try
			{
				var originalPosition = position;				
				var previewSize = originalPosition.height;
				var materialPrefixRect = originalPosition;
				materialPrefixRect.height = EditorGUIUtility.singleLineHeight;

				var hasLabel = ChiselGUIUtility.LabelHasContent(label);

				EditorGUI.BeginChangeCheck();
				{
					var materialLabelID = EditorGUIUtility.GetControlID(kBrushMaterialEditorHashCode, FocusType.Keyboard, materialPrefixRect);
					Rect propRect = EditorGUI.PrefixLabel(materialPrefixRect, materialLabelID, !hasLabel ? GUIContent.none : kRenderMaterialContents);
					
					propRect.height = previewSize;
					ChiselUVToolCommon.ShowMaterialProp(propRect, chiselMaterialProp);
				}
				if (EditorGUI.EndChangeCheck())
				{
#if MATERIAL_IS_SCRIPTABLEOBJECT
                    materialObject.ApplyModifiedProperties();
#else
					chiselMaterialProp.serializedObject.ApplyModifiedProperties();
#endif
				}
			}
			catch (ExitGUIException) { }
			catch (Exception ex) { Debug.LogException(ex); }

			EditorGUI.EndProperty();
		}
	}
#endif
}
