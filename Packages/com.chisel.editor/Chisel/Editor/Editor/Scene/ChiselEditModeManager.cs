using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Chisel.Editors
{
    [Serializable]
    public class ChiselEditModeData : ISingletonData
    {
        public IChiselToolMode currentTool;

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}
    }

    public class ChiselEditModeManager : SingletonManager<ChiselEditModeData, ChiselEditModeManager>
    {
        internal sealed class CSGEditModeItem
        {
            public CSGEditModeItem(IChiselToolMode value, Type type)
            {
                this.instance   = value;
                this.type       = type;
                this.content    = new GUIContent(instance.ToolName);
            }
            public IChiselToolMode  instance;
            public Type             type;
            public GUIContent       content; // TODO: put somewhere else
        }

        internal static CSGEditModeItem[]    editModes;
        internal static CSGEditModeItem[]    generatorModes;


        [InitializeOnLoadMethod]
        static void InitializeEditModes()
        {
            var editModeList        = new List<CSGEditModeItem>();
            var generatorModeList   = new List<CSGEditModeItem>();
            foreach (var type in ReflectionExtensions.AllNonAbstractClasses)
            {
                if (!type.GetInterfaces().Contains(typeof(IChiselToolMode)))
                    continue;

                if (type.BaseType == typeof(ChiselGeneratorToolMode))
                {
                    var instance = (IChiselToolMode)Activator.CreateInstance(type);
                    generatorModeList.Add(new CSGEditModeItem(instance, type));
                } else
                {
                    var instance = (IChiselToolMode)Activator.CreateInstance(type);
                    editModeList.Add(new CSGEditModeItem(instance, type));
                }
            }
            editModes       = editModeList.ToArray();
            generatorModes  = generatorModeList.ToArray();
        }


        // TODO: create proper delegate for this, with named parameters for clarity
        public static event Action<IChiselToolMode, IChiselToolMode> EditModeChanged;
        
        public static IChiselToolMode EditMode
        {
            get
            {
                var currentTool = Instance.data.currentTool;
                if (currentTool == null)
                    EditModeIndex = -1;
                return Instance.data.currentTool;
            }
            set
            {
                if (Instance.data.currentTool == value)
                    return;

                RecordUndo("Edit mode changed");

                var prevMode = Instance.data.currentTool;
                Instance.data.currentTool = value;

                if (EditModeChanged != null)
                    EditModeChanged(prevMode, value);
            }
        }

        internal static int EditModeIndex
        {
            get
            {
                var currentEditMode = Instance.data.currentTool;

                var index = Array.IndexOf(editModes, currentEditMode);
                if (index != -1)
                    return -(index + 1);

                index = Array.IndexOf(generatorModes, currentEditMode);
                if (index != -1)
                    return (index + 1);

                return 0;
            }
            set
            {
                if (value < 0)
                {
                    var index = (-value) - 1;
                    if (index >= editModes.Length)
                    {
                        EditMode = null;
                        return;
                    }

                    Instance.data.currentTool = editModes[index].instance;
                    return;
                }

                if (value > 0)
                {
                    var index = (value - 1);
                    if (index >= editModes.Length)
                    {
                        EditMode = null;
                        return;
                    }
                    Instance.data.currentTool = editModes[index].instance;
                    return;
                }

                Instance.data.currentTool = editModes[0].instance;
            }
        }

        public static Type EditModeType
        {
            set
            {
                if (value == null)
                    return;

                foreach(var editMode in editModes)
                {
                    if (editMode.type != value)
                        continue;
                    EditMode = editMode.instance;
                }
                foreach (var generatorMode in generatorModes)
                {
                    if (generatorMode.type != value)
                        continue;
                    EditMode = generatorMode.instance;
                }
            }
        }
    }
}
