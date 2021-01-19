using System;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public abstract class ChiselGraphNode : Node
    {
        [Input] public CSGTree child;
        [Output] public CSGTree parent;

        public Action onStateChange;

        public void SetActive()
        {
            var chiselGraph = graph as ChiselGraph;
            chiselGraph.active = this;
        }

        protected abstract void Generate();

        public override void OnCreateConnection(NodePort from, NodePort to)
        {
            Generate();
        }

        [Serializable]
        public class CSGTree { }
    }

    public abstract class ChiselGraphGeneratorNode : Node
    {
        [Output] public CSGTree parent;

        public Action onStateChange;

        public void SetActive()
        {
            var chiselGraph = graph as ChiselGraph;
            //chiselGraph.active = this;
        }

        protected abstract void Generate();

        public override void OnCreateConnection(NodePort from, NodePort to)
        {
            Generate();
        }

        [Serializable]
        public class CSGTree { }
    }
}