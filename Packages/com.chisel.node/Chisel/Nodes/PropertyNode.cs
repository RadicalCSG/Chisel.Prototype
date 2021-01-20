using System;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public class PropertyNode<T> : Node where T:GraphProperty
    {
        [HideInInspector]
        public T property;
        public ChiselGraph chiselGraph => graph as ChiselGraph;
    }

    [Serializable]
    public abstract class GraphProperty
    {
        public string Name;
        public bool overrideValue;
    }
}