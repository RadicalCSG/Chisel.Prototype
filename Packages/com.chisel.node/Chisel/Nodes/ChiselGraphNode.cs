using System;
using UnityEngine;
using XNode;
using Chisel.Core;
using System.Collections.Generic;

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

        public void ParseNode(CSGTreeBranch branch)
        {
            var childrenPort = GetInputPort("children");
            if (childrenPort.IsConnected)
            {
                var chiselNode = childrenPort.Connection.node as ChiselGraphNode;
                chiselNode.ParseNode(branch);
            }

            var node = GetNode();
            if (node.Valid)
            {
                node.Operation = operation;
                branch.Add(node);
            }

            OnParseNode(branch);
        }

        public virtual void OnParseNode(CSGTreeBranch nodes) { }

        public override object GetValue(NodePort port)
        {
            return null;
        }

        void OnValidate()
        {
            chiselGraph.UpdateProperties();
        }

        [Serializable]
        public class CSG { }
    }
}