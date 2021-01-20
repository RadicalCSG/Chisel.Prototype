using System;
using System.Collections;
using System.Collections.Generic;
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
    public class FloatProperty : GraphProperty
    {
        public float Value;
    }
}