using Chisel.Components;
using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    [Serializable]
    public class ChiselEditModeData : ISingletonData
    {
        public ChiselPlacementToolInstance currentGenerator;

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}
    }

    public class ChiselGeneratorManager : SingletonManager<ChiselEditModeData, ChiselGeneratorManager>
    {
        const string kDefaultGroupName = "Default";

        internal static ChiselPlacementToolInstance[] generatorModes;
        internal static Dictionary<Type, Type> generatorComponentLookup;

        [InitializeOnLoadMethod]
        static void InitializeEditModes()
        {
            // First, for every generator definition we find the component type
            generatorComponentLookup = new Dictionary<Type, Type>();
            foreach (var type in ReflectionExtensions.AllNonAbstractClasses)
            {
                if (!ReflectionExtensions.HasBaseClass<ChiselGeneratorComponent>(type))
                    continue;

                // Our generator component needs to inherit from ChiselDefinedGeneratorComponent<DefinitionType>
                //  in the type definition we pass along the definition type we belong to
                var baseType = type.GetGenericBaseClass(typeof(ChiselDefinedGeneratorComponent<>));
                if (baseType == null)
                    continue;

                // We get the generic parameters from our type, and get our definition type.
                // we store both in a dictionary so we can find the component type back based on the definition
                var typeParameters = baseType.GetGenericArguments();
                var definitionType = typeParameters[0];
                generatorComponentLookup[definitionType] = type;
            }

            var generatorModeList = new List<ChiselPlacementToolInstance>();
            // Now, we find all BOUNDS placement tools, create a generic class for it, instantiate it, and register it
            foreach (var placementToolType in ReflectionExtensions.AllNonAbstractClasses)
            {
                var baseType = placementToolType.GetGenericBaseClass(typeof(ChiselBoundsPlacementTool<>));
                if (baseType == null)
                    continue;

                // Our definitionType is part of the generic type definition, so we retrieve it
                var typeParameters = baseType.GetGenericArguments();
                var definitionType = typeParameters[0];

                // Using the definition type, we can find the generator Component it belongs to
                if (!generatorComponentLookup.TryGetValue(definitionType, out var componentType))
                    continue;

                string group, toolName;
                var attributes = placementToolType.GetCustomAttributes(typeof(ChiselPlacementToolAttribute), true);
                if (attributes != null &&
                    attributes.Length > 0)
                {
                    var attribute = attributes[0] as ChiselPlacementToolAttribute;
                    group       = attribute.Group;
                    toolName    = attribute.ToolName;
                } else
                {
                    group       = kDefaultGroupName;
                    toolName    = definitionType.Name;
                }

                // Now that we have our placement tool Type, our definition type and our generator component type, we can create a tool instance
                var placementTool = typeof(ChiselBoundsPlacementToolInstance<,,>).MakeGenericType(placementToolType, definitionType, componentType);
                var placementToolInstance = (ChiselPlacementToolInstance)Activator.CreateInstance(placementTool, toolName, group);
                if (placementToolInstance == null)
                    continue;

                generatorModeList.Add(placementToolInstance);
            }

            // Now, we find all SHAPE placement tools, create a generic class for it, instantiate it, and register it
            foreach (var placementToolType in ReflectionExtensions.AllNonAbstractClasses)
            {
                var baseType = placementToolType.GetGenericBaseClass(typeof(ChiselShapePlacementTool<>));
                if (baseType == null)
                    continue;

                // Our definitionType is part of the generic type definition, so we retrieve it
                var typeParameters = baseType.GetGenericArguments();
                var definitionType = typeParameters[0];

                // Using the definition type, we can find the generator Component it belongs to
                if (!generatorComponentLookup.TryGetValue(definitionType, out var componentType))
                    continue;

                string group, toolName;
                var attributes = placementToolType.GetCustomAttributes(typeof(ChiselPlacementToolAttribute), true);
                if (attributes != null &&
                    attributes.Length > 0)
                {
                    var attribute = attributes[0] as ChiselPlacementToolAttribute;
                    group       = attribute.Group;
                    toolName    = attribute.ToolName;
                } else
                {
                    group       = kDefaultGroupName;
                    toolName    = definitionType.Name;
                }

                // Now that we have our placement tool Type, our definition type and our generator component type, we can create a tool instance
                var placementTool = typeof(ChiselShapePlacementToolInstance<,,>).MakeGenericType(placementToolType, definitionType, componentType);
                var placementToolInstance = (ChiselPlacementToolInstance)Activator.CreateInstance(placementTool, toolName, group);
                if (placementToolInstance == null)
                    continue;

                generatorModeList.Add(placementToolInstance);
            }

            generatorModeList.Sort(delegate (ChiselPlacementToolInstance x, ChiselPlacementToolInstance y)
            {
                int difference = x.Group.CompareTo(y.Group);
                if (difference != 0)
                    return difference;
                return x.ToolName.CompareTo(y.ToolName);
            }); 
            generatorModes = generatorModeList.ToArray();
        }


        // TODO: create proper delegate for this, with named parameters for clarity
        public static event Action<ChiselPlacementToolInstance, ChiselPlacementToolInstance> GeneratorSelectionChanged;


        public static ChiselPlacementToolInstance GeneratorMode
        {
            get
            {
                if (generatorModes == null ||
                    generatorModes.Length == 0)
                    InitializeEditModes();
                var currentTool = Instance.data.currentGenerator;
                if (currentTool == null)
                    GeneratorIndex = 1;
                return Instance.data.currentGenerator;
            }
            set
            {
                if (generatorModes == null ||
                    generatorModes.Length == 0)
                    InitializeEditModes();
                if (Instance.data.currentGenerator == value)
                    return;

                RecordUndo("Generator selection changed");

                ActivateTool(value);
            }
        }

        internal static void ActivateTool(ChiselPlacementToolInstance currentTool)
        {
            if (currentTool != null && Tools.hidden)
                Tools.hidden = false;
            if (currentTool == Instance.data.currentGenerator)
                return;
            if (currentTool != null && Tools.hidden)
                Tools.hidden = false;

            var prevTool = Instance.data.currentGenerator;
            if (prevTool != null)
                prevTool.OnDeactivate();
            Instance.data.currentGenerator = null;
            if (currentTool != null)
                currentTool.OnActivate();
            Instance.data.currentGenerator = currentTool;

            ChiselToolsOverlay.UpdateCreateToolIcon();

            GeneratorSelectionChanged?.Invoke(prevTool, currentTool);
        }

        internal static int GeneratorIndex
        {
            get
            {
                if (generatorModes == null ||
                    generatorModes.Length == 0)
                    InitializeEditModes();
                var currentGenerator = Instance.data.currentGenerator;
                for (int j = 0; j < generatorModes.Length; j++)
                {
                    if (generatorModes[j] == currentGenerator)
                        return (j + 1);
                }
                return 0;
            }
            set
            {
                if (generatorModes == null ||
                    generatorModes.Length == 0)
                    InitializeEditModes();
                if (value > 0)
                {
                    var index = (value - 1);
                    if (index >= generatorModes.Length)
                    {
                        GeneratorMode = null;
                        return;
                    }
                    GeneratorMode = generatorModes[index];
                    return;
                }

                GeneratorMode = generatorModes[0];
            }
        }

        public static Type GeneratorType
        {
            set
            {
                if (value == null)
                    return;

                foreach (var generatorMode in generatorModes)
                {
                    if (generatorMode.GetType() != value)
                        continue;
                    GeneratorMode = generatorMode;
                }
            }
        }
    }
}
