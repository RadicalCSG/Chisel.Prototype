using Chisel.Core;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    [CreateAssetMenu(fileName = "New Chisel Graph", menuName = "Chisel Graph")]
    public class ChiselGraph : NodeGraph
    {
        public ChiselGraphNode active;
        public ChiselGraphInstance instance;

        public void SetActiveNode(ChiselGraphNode node)
        {
            active = node;
            UpdateCSG();
        }

        public void UpdateCSG()
        {
            if (instance != null)
                instance.IsDirty = true;
        }

        public void CollectTreeNode(CSGTree tree)
        {
            active.ParseNode(tree);
        }
    }
}