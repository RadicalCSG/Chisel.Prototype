using Chisel.Core;
using UnityEngine;

namespace Chisel.Nodes
{
    public class BoxNode : ChiselGraphNode
    {
        [Input] public Vector3 center;
        [Input] public Vector3 size = Vector3.one;

        public override CSGTreeNode GetNode()
        {
            var box = new ChiselBoxDefinition();
            box.center = center;
            box.size = size;

            var brushContainer = new ChiselBrushContainer();
            BrushMeshFactory.GenerateBox(ref brushContainer, ref box);

            var instance = BrushMeshInstance.Create(brushContainer.brushMeshes[0]);
            var treeNode = CSGTreeBrush.Create(0, instance);

            treeNode.Operation = operation;

            return treeNode;
        }
    }
}