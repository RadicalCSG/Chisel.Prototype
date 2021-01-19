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

            if (GUILayout.Button("Edit", GUI.skin.GetStyle("button")))
            {
                var instance = target as ChiselGraphInstance;
                instance.graph.instance = instance;

                NodeEditorWindow.Open(instance.graph);
            }

            if (GUILayout.Button("Create Tree", GUI.skin.GetStyle("button")))
            {
                var instance = target as ChiselGraphInstance;
                instance.UpdateCSG();
            }

            if (GUILayout.Button("Rebuild", GUI.skin.GetStyle("button")))
            {
                var instance = target as ChiselGraphInstance;
                instance.Rebuild();
            }
        }
    }
}