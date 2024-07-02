using System;
using UnityEngine;
using UnityEditor;
using Chisel.Core;

namespace Chisel.Editors
{
    // TODO: show angle
    [CustomPropertyDrawer(typeof(EulerValueAttribute))]
    public class EulerValueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Float)
                return EditorGUIUtility.singleLineHeight;
             return EditorGUIUtility.singleLineHeight * (EditorGUIUtility.wideMode ? 1f : 2f);
        }

        void ResetValues(object userdata)
        {
            var property = (SerializedProperty)userdata;
            property.quaternionValue = Quaternion.identity;
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
                
                var euler = property.quaternionValue.eulerAngles;
                EditorGUI.BeginChangeCheck();
                {
                    euler = EditorGUI.Vector3Field(position, label, euler);
                }
                if (EditorGUI.EndChangeCheck())
                    property.quaternionValue = Quaternion.Euler(euler);
            }
            catch (ExitGUIException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            EditorGUI.EndProperty();
        }
    }
}
