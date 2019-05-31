using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
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

                Init();

                if (EditModeChanged != null)
                    EditModeChanged(prevMode, value);
            }
        }

        internal static void Init()
        {
            KeyboardManager.KeyboardEventCalled -= OnKeyboardEventCalled;
            KeyboardManager.KeyboardEventCalled += OnKeyboardEventCalled;
        }

        private static void OnKeyboardEventCalled(KeyEventType type)
        { 
            switch (type)
            {
                case KeyEventType.SwitchToObjectEditMode:	EditMode = ChiselEditMode.Object;			break;
                case KeyEventType.SwitchToPivotEditMode:	EditMode = ChiselEditMode.Pivot;			break;
                case KeyEventType.SwitchToShapeEditMode:	EditMode = ChiselEditMode.ShapeEdit;		break;
                case KeyEventType.SwitchToSurfaceEditMode:	EditMode = ChiselEditMode.SurfaceEdit;		break;

                case KeyEventType.FreeBuilderMode:			EditMode = ChiselEditMode.FreeDraw;		break;
                case KeyEventType.RevolvedShapeBuilderMode:	EditMode = ChiselEditMode.RevolvedShape;	break;

                case KeyEventType.BoxBuilderMode:			EditMode = ChiselEditMode.Box;				break;		
                case KeyEventType.CylinderBuilderMode:		EditMode = ChiselEditMode.Cylinder;		break;		
                case KeyEventType.TorusBuilderMode:			EditMode = ChiselEditMode.Torus;			break;
                case KeyEventType.HemisphereBuilderMode:	EditMode = ChiselEditMode.Hemisphere;		break;
                case KeyEventType.SphereBuilderMode:		EditMode = ChiselEditMode.Sphere;			break;		
                case KeyEventType.CapsuleBuilderMode:		EditMode = ChiselEditMode.Capsule;			break;
                case KeyEventType.StadiumBuilderMode:		EditMode = ChiselEditMode.Stadium;			break;
                        
                case KeyEventType.PathedStairsBuilderMode:	EditMode = ChiselEditMode.PathedStairs;	break;
                case KeyEventType.LinearStairsBuilderMode:	EditMode = ChiselEditMode.LinearStairs;	break;
                case KeyEventType.SpiralStairsBuilderMode:	EditMode = ChiselEditMode.SpiralStairs;	break;
            }
        }
    }
}
