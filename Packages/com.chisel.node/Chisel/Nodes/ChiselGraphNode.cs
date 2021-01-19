using System;
using UnityEngine;
using XNode;
using Chisel.Core;

namespace Chisel.Nodes
{
    public abstract class ChiselGraphNode : Node
    {
        [Input] public CSG child;
        [Output] public CSG parent;

        public Action onStateChange;
        public ChiselGraph chiselGraph => graph as ChiselGraph;

        public void SetActive()
        {
            var chiselGraph = graph as Nodes.ChiselGraph;
            chiselGraph.active = this;
        }

        public abstract CSGTreeNode GetNode();

        void OnValidate()
        {
            chiselGraph.UpdateCSG();
        }

        [Serializable]
        public class CSG { }
    }
}