using System;
using UnityEngine;
using XNode;
using Chisel.Core;

namespace Chisel.Nodes
{
    public abstract class ChiselGraphNode : Node
    {
        [Input] public CSG children;
        [Output] public CSG parent;

        public Action onStateChange;
        public ChiselGraph chiselGraph => graph as ChiselGraph;

        public CSGOperationType operation = CSGOperationType.Additive;

        public void SetActive()
        {
            chiselGraph.SetActiveNode(this);
        }

        public abstract CSGTreeNode GetNode();

        public void ParseNode(CSGTree tree)
        {
            var childrenPort = GetInputPort("children");
            if (childrenPort.IsConnected)
            {
                var chiselNode = childrenPort.Connection.node as ChiselGraphNode;
                chiselNode.ParseNode(tree);
            }

            var node = GetNode();
            node.Operation = operation;
            tree.Add(node);
        }

        public override object GetValue(NodePort port)
        {
            return null;
        }

        void OnValidate()
        {
            chiselGraph.UpdateCSG();
        }

        [Serializable]
        public class CSG { }
    }
}