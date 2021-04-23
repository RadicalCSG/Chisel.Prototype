using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public static class ChiselNodeDetailsManager
    {
        static Dictionary<Type, IChiselNodeDetails> nodeDetailsLookup = new Dictionary<Type, IChiselNodeDetails>();
        static IChiselNodeDetails generatorDefaultDetails = new ChiselDefaultGeneratorDetails();

        [InitializeOnLoadMethod]
        static void InitializeNodeDetails()
        {
            ReflectionExtensions.Initialize();
            foreach (var type in ReflectionExtensions.AllNonAbstractClasses)
            {
                var baseType = type.GetGenericBaseClass(typeof(ChiselNodeDetails<>));
                if (baseType == null)
                    continue;

                var typeParameters = baseType.GetGenericArguments();
                var instance = (IChiselNodeDetails)Activator.CreateInstance(type);
                nodeDetailsLookup.Add(typeParameters[0], instance);
            }
        }



        public static IChiselNodeDetails GetNodeDetails(ChiselNode node)
        {
            if (nodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails nodeDetails))
                return nodeDetails;
            return generatorDefaultDetails;
        }

        public static IChiselNodeDetails GetNodeDetails(Type type)
        {
            if (nodeDetailsLookup.TryGetValue(type, out IChiselNodeDetails nodeDetails))
                return nodeDetails;
            return generatorDefaultDetails;
        }

        public static GUIContent GetHierarchyIcon(ChiselNode node)
        {
            if (nodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails nodeDetails))
            {
                return nodeDetails.GetHierarchyIconForGenericNode(node);
            }
            return generatorDefaultDetails.GetHierarchyIconForGenericNode(node);
        }

        public static GUIContent GetHierarchyIcon(ChiselNode node, out bool hasValidState)
        {
            hasValidState = node.HasValidState();
            return GetHierarchyIcon(node);
        }
    }
}
