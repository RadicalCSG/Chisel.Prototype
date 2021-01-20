using UnityEditor;
using UnityEngine;
using XNodeEditor;

namespace Chisel.Nodes
{
    [CustomEditor(typeof(ChiselGraphInstance))]
    public class ChiselGraphInstanceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var instance = target as ChiselGraphInstance;

            if (GUILayout.Button("Edit", GUI.skin.GetStyle("button")))
            {
                instance.graph.instance = instance;

                NodeEditorWindow.Open(instance.graph);
            }

            if (GUILayout.Button("UpdateCSG", GUI.skin.GetStyle("button")))
            {
                instance.UpdateCSG();
            }

            if (GUILayout.Button("Rebuild", GUI.skin.GetStyle("button")))
            {
                instance.Rebuild();
            }


            EditorGUI.BeginChangeCheck();

            foreach (var property in instance.properties)
                if (property is FloatProperty floatProperty)
                {
                    floatProperty.Value = EditorGUILayout.FloatField(floatProperty.Name, floatProperty.Value);
                }

            if (EditorGUI.EndChangeCheck())
            {
                instance.IsDirty = true;
                instance.UpdateCSG();
            }
        }
    }
}