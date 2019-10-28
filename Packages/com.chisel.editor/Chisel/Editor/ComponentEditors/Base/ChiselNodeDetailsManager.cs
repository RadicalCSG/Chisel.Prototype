using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
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
            if (nodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails someInterface))
                return someInterface;
            return generatorDefaultDetails;
        }

        public static IChiselNodeDetails GetNodeDetails(Type type)
        {
            if (nodeDetailsLookup.TryGetValue(type, out IChiselNodeDetails someInterface))
                return someInterface;
            return generatorDefaultDetails;
        }


        public static GUIContent GetHierarchyIcon(ChiselNode node)
        {
            if (nodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails someInterface))
                return someInterface.GetHierarchyIconForGenericNode(node);
            return generatorDefaultDetails.GetHierarchyIconForGenericNode(node);
        }
    }
}
