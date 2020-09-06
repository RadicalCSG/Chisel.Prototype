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
        public ChiselPlacementTool currentGenerator;

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}
    }

    public class ChiselGeneratorManager : SingletonManager<ChiselEditModeData, ChiselGeneratorManager>
    {
        internal static ChiselPlacementTool[] generatorModes;


        [InitializeOnLoadMethod]
        static void InitializeEditModes()
        {
            var generatorModeList = new List<ChiselPlacementTool>();
            foreach (var type in ReflectionExtensions.AllNonAbstractClasses)
            {
                var baseType = type.BaseType;
                bool found = false;
                int count = 0;
                while (count < 4 && baseType != null)
                {
                    if (baseType == typeof(ChiselPlacementTool))
                    {
                        found = true;
                        break;
                    }
                    baseType = baseType.BaseType;
                    count++;
                }
                if (!found)
                    continue;

                var instance = (ChiselPlacementTool)Activator.CreateInstance(type); 
                generatorModeList.Add(instance);
            }
            generatorModeList.Sort(delegate (ChiselPlacementTool x, ChiselPlacementTool y)
            {
                int difference = x.Group.CompareTo(y.Group);
                if (difference != 0)
                    return difference;
                return x.ToolName.CompareTo(y.ToolName);
            });
            generatorModes  = generatorModeList.ToArray();
        }


        // TODO: create proper delegate for this, with named parameters for clarity
        public static event Action<ChiselPlacementTool, ChiselPlacementTool> GeneratorSelectionChanged;


        public static ChiselPlacementTool GeneratorMode
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

        internal static void ActivateTool(ChiselPlacementTool currentTool)
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
