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

        class HierarchyMessageHandler : IChiselMessageHandler
        {
            static System.Text.StringBuilder warningStringBuilder = new System.Text.StringBuilder();

            // TODO: how to handle these kind of message in the hierarchy? cannot show buttons, 
            //       but still want to show a coherent message
            public void Warning(string message, Action buttonAction, string buttonText)
            {
                throw new NotImplementedException();
            }

            public void Warning(string message)
            {
                if (warningStringBuilder.Length > 0)
                    warningStringBuilder.AppendLine();
                warningStringBuilder.Append(message);
            }

            public void Clear() { warningStringBuilder.Clear(); }
            public int Length { get { return warningStringBuilder.Length; } }
            public override string ToString() { return warningStringBuilder.ToString(); }
        }

        static HierarchyMessageHandler hierarchyMessageHandler = new HierarchyMessageHandler();

        public static GUIContent GetHierarchyIcon(ChiselNode node, out bool hasValidState)
        {
            hierarchyMessageHandler.Clear();
            node.GetWarningMessages(hierarchyMessageHandler);
            string nodeMessage;
            if (hierarchyMessageHandler.Length != 0)
            {
                hasValidState = false;
                nodeMessage = hierarchyMessageHandler.ToString();
            } else
            {
                hasValidState = true;
                nodeMessage = string.Empty;
            }
            var hierarchyIcon = GetHierarchyIcon(node);
            hierarchyIcon.tooltip = nodeMessage;
            return hierarchyIcon;
        }
    }
}
