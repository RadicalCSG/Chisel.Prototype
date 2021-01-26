using System;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public class PropertyNode<T> : Node, IPropertyNode where T : GraphProperty
    {
        [HideInInspector]
        public T property;
        public GraphProperty Property => property;

        public ChiselGraph ChiselGraph => graph as ChiselGraph;

        protected override void Init()
        {
            ChiselGraph.UpdateProperties();
        }

        void OnDestroy()
        {
            ChiselGraph.UpdateProperties();
        }
    }

    public interface IPropertyNode
    {
        GraphProperty Property { get; }
    }

    [Serializable]
    public abstract class GraphProperty
    {
        public string Name;
        public bool overrideValue;
    }
}