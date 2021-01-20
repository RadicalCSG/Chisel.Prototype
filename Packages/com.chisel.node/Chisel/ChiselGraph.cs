using Chisel.Core;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    [CreateAssetMenu(fileName = "New Chisel Graph", menuName = "Chisel Graph")]
    public class ChiselGraph : NodeGraph
    {
        public ChiselGraphNode active;
        public ChiselGraphInstance instance;

        public List<GraphProperty> properties;

        public void SetActiveNode(ChiselGraphNode node)
        {
            active = node;
            UpdateCSG();
        }

        public void UpdateCSG()
        {
            if (instance != null)
                instance.IsDirty = true;

            properties.Clear();
            instance?.properties.Clear();
            foreach (var node in nodes)
                if (node is FloatPropertyNode floatNode)
                {
                    instance?.properties.Add(floatNode.property);
                    properties.Add(floatNode.property);
                }
        }

        public void CollectTreeNode(CSGTree tree)
        {
            var branch = CSGTreeBranch.Create();
            active.ParseNode(branch);
            tree.Add(branch);
        }
    }
}