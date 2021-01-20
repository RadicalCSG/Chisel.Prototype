using System;
using XNode;

namespace Chisel.Nodes
{
    public class FloatPropertyNode : PropertyNode<FloatProperty>
    {
        [Output] public float exit;

        public override object GetValue(NodePort port)
        {
            var overridden = chiselGraph.GetOverriddenProperty<FloatProperty>(property.Name);
            if (overridden != null)
                return overridden.Value;

            return property.Value;
        }
    }

    [Serializable]
    public class FloatProperty : GraphProperty
    {
        public float Value;
    }
}