using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    public class FloatPropertyNode : PropertyNode<FloatProperty>
    {
        [Output] public float exit;

        public override object GetValue(NodePort port)
        {
            return property.Value;
        }
    }

    [Serializable]
    public class GraphProperty
    {
        public string Name;
    }

    [Serializable]
    public class FloatProperty : GraphProperty
    {
        public float Value;
    }

    public class PropertyNode<T> : Node where T:GraphProperty
    {
        [HideInInspector]
        public T property;
        public ChiselGraph chiselGraph => graph as ChiselGraph;
    }
}