using System;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public abstract class ChiselNode : Node
    {
        public Action onStateChange;
        public abstract bool led { get; }

        public void SetActive()
        {
            var chiselGraph = graph as ChiselGraph;
            chiselGraph.active = this;
        }

        protected abstract void OnInputChanged();

        public override void OnCreateConnection(NodePort from, NodePort to)
        {
            OnInputChanged();
        }
    }
}