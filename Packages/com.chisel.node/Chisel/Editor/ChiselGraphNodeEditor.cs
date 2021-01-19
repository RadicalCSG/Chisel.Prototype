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
            if (graph.active == node) GUI.color = Color.cyan;

            string title = target.name;
            GUILayout.Label(title, NodeEditorResources.styles.nodeHeader, GUILayout.Height(30));
            GUI.color = Color.white;
        }

        public override void OnBodyGUI()
        {
            base.OnBodyGUI();
            var node = target as ChiselGraphNode;
            if (GUILayout.Button("Preview")) node.SetActive();
        }
    }
}