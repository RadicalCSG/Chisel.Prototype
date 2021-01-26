using Chisel.Core;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Chisel.Nodes
{
    [CreateAssetMenu(fileName = "New Chisel Graph", menuName = "Chisel Graph")]
    public class ChiselGraph : NodeGraph
    {
        public ChiselGraphNode active;
        public ChiselGraphInstance instance;

        public List<GraphProperty> properties;

        public void SetActiveNode(ChiselGraphNode node)
        {
            active = node;
            UpdateProperties();
        }

        public void UpdateProperties()
        {
            properties = new List<GraphProperty>();
            foreach (var node in nodes)
                if (node is IPropertyNode propertyNode)
                    properties.Add(propertyNode.Property);
            UpdateCSG();
        }

        public void UpdateCSG()
        {
            if (instance != null)
                instance.IsDirty = true;
        }

        public void CollectTreeNode(CSGTree tree)
        {
            var branch = CSGTreeBranch.Create(GetInstanceID());
            active.ParseNode(branch);
            tree.Add(branch);
        }

        public T GetOverriddenProperty<T>(string key) where T : GraphProperty
        {
            if (instance == null) return null;

            if (instance.overriddenProperties == null)
                instance.UpdateProperties();

            if (instance.overriddenProperties.ContainsKey(key))
                return instance.overriddenProperties[key] as T;
            return null;
        }

        void OnValidate()
        {
            UpdateProperties();
        }
    }
}