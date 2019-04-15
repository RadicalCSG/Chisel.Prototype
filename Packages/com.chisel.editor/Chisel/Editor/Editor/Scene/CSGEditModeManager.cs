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
    public enum CSGEditMode
    {
        Object,
        Pivot,
        ShapeEdit,
        SurfaceEdit,

        FreeDraw,
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
    public class CSGEditModeData : ISingletonData
    {
        public CSGEditMode editMode;

        public void OnAfterDeserialize() {}
        public void OnBeforeSerialize() {}
    }

    public class CSGEditModeManager : SingletonManager<CSGEditModeData, CSGEditModeManager>
    {
        // TODO: create proper delegate for this, with named parameters for clarity
        public static event Action<CSGEditMode, CSGEditMode> EditModeChanged;
        
        public static CSGEditMode EditMode
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
                case KeyEventType.SwitchToObjectEditMode:	EditMode = CSGEditMode.Object;			break;
                case KeyEventType.SwitchToPivotEditMode:	EditMode = CSGEditMode.Pivot;			break;
                case KeyEventType.SwitchToShapeEditMode:	EditMode = CSGEditMode.ShapeEdit;		break;
                case KeyEventType.SwitchToSurfaceEditMode:	EditMode = CSGEditMode.SurfaceEdit;		break;

                case KeyEventType.FreeBuilderMode:			EditMode = CSGEditMode.FreeDraw;		break;
                case KeyEventType.RevolvedShapeBuilderMode:	EditMode = CSGEditMode.RevolvedShape;	break;

                case KeyEventType.BoxBuilderMode:			EditMode = CSGEditMode.Box;				break;		
                case KeyEventType.CylinderBuilderMode:		EditMode = CSGEditMode.Cylinder;		break;		
                case KeyEventType.TorusBuilderMode:			EditMode = CSGEditMode.Torus;			break;
                case KeyEventType.HemisphereBuilderMode:	EditMode = CSGEditMode.Hemisphere;		break;
                case KeyEventType.SphereBuilderMode:		EditMode = CSGEditMode.Sphere;			break;		
                case KeyEventType.CapsuleBuilderMode:		EditMode = CSGEditMode.Capsule;			break;
                case KeyEventType.StadiumBuilderMode:		EditMode = CSGEditMode.Stadium;			break;
                        
                case KeyEventType.PathedStairsBuilderMode:	EditMode = CSGEditMode.PathedStairs;	break;
                case KeyEventType.LinearStairsBuilderMode:	EditMode = CSGEditMode.LinearStairs;	break;
                case KeyEventType.SpiralStairsBuilderMode:	EditMode = CSGEditMode.SpiralStairs;	break;
            }
        }
    }
}
