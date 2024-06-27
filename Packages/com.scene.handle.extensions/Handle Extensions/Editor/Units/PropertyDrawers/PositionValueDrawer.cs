using System;
using UnityEngine;
using UnityEditor;

namespace UnitySceneExtensions
{
    [CustomPropertyDrawer(typeof(PositionValueAttribute))]
    public class PositionValuesDrawer : PropertyDrawer
    {
        // TODO: use UnityTypes
        UnitType Type { get { return ((PositionValueAttribute)attribute).Type; } }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                return EditorGUIUtility.singleLineHeight;
             return EditorGUIUtility.singleLineHeight * (EditorGUIUtility.wideMode ? 1f : 2f);
        }

        void ResetValues(object userdata)
        {
            var property = (SerializedProperty)userdata;
            if (property.propertyType == SerializedPropertyType.Float) property.floatValue = 0;
            if (property.propertyType == SerializedPropertyType.Vector2) property.vector2Value = Vector2.zero;
            if (property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = Vector3.zero;
            property.serializedObject.ApplyModifiedProperties();
        }

        static GUIContent ResetValuesContent = new GUIContent ("Reset Values");
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                Event e = Event.current;
     
                if (e.type == EventType.MouseDown && e.button == 1 && position.Contains(e.mousePosition)) {
         
                    var context = new GenericMenu ();
         
                    context.AddItem (ResetValuesContent, false, ResetValues, property);
         
                    context.ShowAsContext ();
                }
                
                EditorGUI.PropertyField(position, property, label);
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            EditorGUI.EndProperty();
        }
    }
}
