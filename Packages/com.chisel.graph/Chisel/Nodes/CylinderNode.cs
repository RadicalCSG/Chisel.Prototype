using Chisel.Core;
using UnityEngine;

namespace Chisel.Nodes
{
    public class CylinderNode : ChiselGraphNode
    {
        [Input] public float diameter = 1;
        [Input] public float height = 1;
        [Input] public int sides = 3;

        public override CSGTreeNode GetNode()
        {
            var cylinder = new ChiselCylinderDefinition();
            cylinder.type = CylinderShapeType.Cylinder;
            cylinder.Diameter = GetInputValue("diameter", diameter);
            cylinder.height = GetInputValue("height", height);
            cylinder.sides = GetInputValue("sides", sides);

            var brushContainer = new ChiselBrushContainer();
            BrushMeshFactory.GenerateCylinder(ref brushContainer, ref cylinder);

            var instance = BrushMeshInstance.Create(brushContainer.brushMeshes[0]);
            var treeNode = CSGTreeBrush.Create(GetInstanceID(), instance);

            return treeNode;
        }
    }
}