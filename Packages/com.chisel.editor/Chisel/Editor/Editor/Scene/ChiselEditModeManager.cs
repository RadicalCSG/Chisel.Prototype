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
    public enum ChiselEditMode
    {
        // Edit modes
        Object,
        Pivot,
        ShapeEdit,
        SurfaceEdit,

        // Generators
        FirstGenerator,
        FreeDraw = FirstGenerator,
        RevolvedShape,
        Box,
        Cylinder,
        Torus,
        Hemisphere,
        Sphere,
        Capsule,
        Stadium,

        PathedStairs,
        LinearStairs,
        SpiralStairs
    }

    [Serializable]
    public class ChiselEditModeData : ISingletonData
    {
        public ChiselEditMode editMode;

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}
    }

    public class ChiselEditModeManager : SingletonManager<ChiselEditModeData, ChiselEditModeManager>
    {
        // TODO: create proper delegate for this, with named parameters for clarity
        public static event Action<ChiselEditMode, ChiselEditMode> EditModeChanged;
        
        public static ChiselEditMode EditMode
        {
            get { return Instance.data.editMode; }
            set
            {
                if (Instance.data.editMode == value)
                    return;

                RecordUndo("Edit mode changed");

                var prevMode = Instance.data.editMode;
                Instance.data.editMode = value;

                if (EditModeChanged != null)
                    EditModeChanged(prevMode, value);
            }
        }
    }
}
