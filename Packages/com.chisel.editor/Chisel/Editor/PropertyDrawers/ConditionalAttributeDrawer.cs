using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    [CustomPropertyDrawer(typeof(ConditionalNameAttribute))]
    [CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
    public class ConditionalFieldAttributeDrawer : PropertyDrawer
	{
        static Dictionary<int, string> pathLookup = new Dictionary<int, string>();
        static SerializedProperty GetSiblingByName(SerializedProperty property, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || property == null)
                return null;
            
            var hash = (property.propertyPath.GetHashCode() * 31) + name.GetHashCode();
            if (!pathLookup.TryGetValue(hash, out var siblingPath))
            {
                siblingPath = property.propertyPath.Remove(property.propertyPath.LastIndexOf('.') + 1) + name;
                pathLookup[hash] = siblingPath;
            }
            return property.serializedObject.FindProperty(siblingPath);
        }

        public override bool CanCacheInspectorGUI(SerializedProperty property) { return false; }


        static Dictionary<string, string> foundParts = new Dictionary<string, string>();
        bool TryGetName(SerializedProperty property, out string name)
        {
            name = null;
            if (property == null)
                return false;

            var conditionalNames = fieldInfo.GetCustomAttributes(typeof(ConditionalNameAttribute), true);
            if (conditionalNames == null ||
                conditionalNames.Length == 0)
                return false;

            if (conditionalNames.Length > 1)
            {
                Debug.LogWarning("A field can only have one ConditionalName");
                return false;
            }

            var conditionalNameParts = fieldInfo.GetCustomAttributes(typeof(ConditionalNamePartAttribute), true);
            if (conditionalNameParts == null ||
                conditionalNameParts.Length == 0)
            {
                Debug.LogWarning("Could not find any ConditionalNameParts but ConditionalName has been specified");
                return false;
            }

            foundParts.Clear();
            foreach (var attribute in conditionalNameParts)
            {
                var conditionalNamePart = attribute as ConditionalNamePartAttribute;
                if (IsVisible(property, conditionalNamePart.Condition)) foundParts[conditionalNamePart.Pattern] = conditionalNamePart.TrueName;
                else                                                    foundParts[conditionalNamePart.Pattern] = conditionalNamePart.FalseName;
            }

            var conditionalName = conditionalNames[0] as ConditionalNameAttribute;
            if (conditionalName == null)
                return false;

            name = conditionalName.Name;
            foreach (var part in foundParts)
                name = name.Replace(part.Key, part.Value);
            return true;
        }

        bool IsVisible(SerializedProperty property)
        {
            if (property == null)
                return true;

            var conditionalFlags = fieldInfo.GetCustomAttributes(typeof(ConditionalHideAttribute), true);
            foreach (var attribute in conditionalFlags)
            {
                var conditionalFlag = attribute as ConditionalHideAttribute;
                if (!IsVisible(property, conditionalFlag.Condition))
                    return false;
            }
            return true;
        }

        static bool IsVisible(SerializedProperty property, in Condition condition)
        {
            if (property == null)
                return true;
            var fieldToCheckProperty = GetSiblingByName(property, condition.FieldToCheck);
            if (fieldToCheckProperty.propertyType == SerializedPropertyType.Boolean)
                return fieldToCheckProperty.boolValue;
            if (fieldToCheckProperty.propertyType == SerializedPropertyType.Enum)
            {
                if (condition.ValuesToCompareWith == null ||
                    condition.ValuesToCompareWith.Length == 0)
                {
                    Debug.LogWarning("Nothing to compare to");
                    return true;
                }

                try
                {
                    for (int i = 0; i < condition.ValuesToCompareWith.Length; i++)
                    {
                        if (fieldToCheckProperty.intValue == Convert.ToInt32(condition.ValuesToCompareWith[i]))
                            return false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            return true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!IsVisible(property))
                return -EditorGUIUtility.standardVerticalSpacing;
            return base.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsVisible(property))
                return;

            if (TryGetName(property, out var name))
                label = EditorGUIUtility.TrTextContent(name, label.tooltip, label.image);

            EditorGUI.PropertyField(position, property, label);
        }
    }
}
