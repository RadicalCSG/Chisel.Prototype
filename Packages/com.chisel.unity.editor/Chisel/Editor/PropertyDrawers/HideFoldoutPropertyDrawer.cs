﻿using Chisel.Core;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(HideFoldoutAttribute))]
    public sealed class HideFoldoutPropertyDrawer : PropertyDrawer
	{
#if !UNITY_2023_1_OR_NEWER
        public override bool CanCacheInspectorGUI(SerializedProperty property) { return true; }
#endif

        public override float GetPropertyHeight(SerializedProperty iterator, GUIContent label)
        {
            float totalHeight = 0;
            int startingDepth = iterator.depth;
            EditorGUIUtility.wideMode = true;
            if (iterator.NextVisible(true))
            {
                do
                {
                    k_TempGUIContent.text = iterator.displayName;
                    totalHeight += EditorGUI.GetPropertyHeight(iterator, k_TempGUIContent, includeChildren: false) + EditorGUIUtility.standardVerticalSpacing;
                }
                while (iterator.NextVisible(iterator.isExpanded) && iterator.depth > startingDepth);
            }
            totalHeight += EditorGUIUtility.standardVerticalSpacing;
            return totalHeight;
        }

        static GUIContent k_TempGUIContent = new GUIContent();

        public override void OnGUI(Rect position, SerializedProperty iterator, GUIContent label)
        {
            var indentLevel = EditorGUI.indentLevel;
            try
            {
                EditorGUIUtility.wideMode = true;
                EditorGUI.BeginChangeCheck();
                float y = position.y + EditorGUIUtility.standardVerticalSpacing;
                if (iterator.NextVisible(true))
                {
                    int startingDepth = iterator.depth;
                    do
                    {
                        EditorGUI.indentLevel = iterator.depth - startingDepth;
                        k_TempGUIContent.text = iterator.displayName;
                        var height = EditorGUI.GetPropertyHeight(iterator, k_TempGUIContent, includeChildren: false);
                        EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), iterator, k_TempGUIContent);
                        y += height + EditorGUIUtility.standardVerticalSpacing;
                    }
                    while (iterator.NextVisible(iterator.isExpanded) && iterator.depth >= startingDepth);
                }
                if (EditorGUI.EndChangeCheck())
                    iterator.serializedObject.ApplyModifiedProperties();
            }
            finally
            {
                EditorGUI.indentLevel = indentLevel;
            }
        }
    }
}
