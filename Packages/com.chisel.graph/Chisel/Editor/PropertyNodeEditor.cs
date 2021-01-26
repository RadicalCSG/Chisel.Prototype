using UnityEditor;
using XNodeEditor;

namespace Chisel.Nodes
{
    [CustomNodeEditor(typeof(PropertyNode<FloatProperty>))]
    public class FloatPropertyNodeEditor : NodeEditor
    {
        public override void OnBodyGUI()
        {
            base.OnBodyGUI();
            var node = target as PropertyNode<FloatProperty>;

            EditorGUI.BeginChangeCheck();
            node.property.Name = EditorGUILayout.TextField("Name", node.property.Name);
            node.property.Value = EditorGUILayout.FloatField("Value", node.property.Value);
            if (EditorGUI.EndChangeCheck())
                node.ChiselGraph.UpdateProperties();
        }
    }
}