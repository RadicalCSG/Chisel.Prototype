using UnityEngine;
using XNodeEditor;

namespace Chisel.Nodes
{
    [CustomNodeEditor(typeof(ChiselGraphNode))]
    public class ChiselGraphNodeEditor : NodeEditor
    {
        public override void OnHeaderGUI()
        {
            GUI.color = Color.white;
            var node = target as ChiselGraphNode;
            var graph = node.graph as ChiselGraph;
            if (graph.active == node) GUI.color = Color.green;

            string title = target.name;
            GUILayout.Label(title, NodeEditorResources.styles.nodeHeader, GUILayout.Height(30));
            GUI.color = Color.white;
        }

        public override void OnBodyGUI()
        {

            var input = target.GetPort("input");
            var output = target.GetPort("output");

            GUILayout.BeginHorizontal();
            if (input != null) NodeEditorGUILayout.PortField(GUIContent.none, input, GUILayout.MinWidth(0));
            if (output != null) NodeEditorGUILayout.PortField(GUIContent.none, output, GUILayout.MinWidth(0));
            GUILayout.EndHorizontal();

            base.OnBodyGUI();
            var node = target as ChiselGraphNode;
            if (node.chiselGraph.active != node)
            {
                if (GUILayout.Button("Preview"))
                    node.SetActive();
            }
        }
    }
}