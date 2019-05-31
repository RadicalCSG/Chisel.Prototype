using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chisel.Editors
{
    public static class SnappingKeyboard
    {
        public static void Init()
        {
            KeyboardManager.KeyboardEventCalled -= OnKeyboardEventCalled;
            KeyboardManager.KeyboardEventCalled += OnKeyboardEventCalled;
        }

        private static void OnKeyboardEventCalled(KeyEventType type)
        {
            switch (type)
            {
                case KeyEventType.HalfGridSizeKey:			ChiselSceneBottomGUI.MultiplySnapDistance(0.5f); break;
                case KeyEventType.DoubleGridSizeKey:		ChiselSceneBottomGUI.MultiplySnapDistance(2.0f); break;	

                // TODO: turn this into utility functions that are shared with CSGSceneBottomGUI
                case KeyEventType.ToggleBoundsSnappingKey:	ChiselEditorSettings.PivotSnapping = !ChiselEditorSettings.PivotSnapping; ChiselEditorSettings.Save(); break;
                case KeyEventType.TogglePivotSnappingKey:	ChiselEditorSettings.BoundsSnapping = !ChiselEditorSettings.BoundsSnapping; ChiselEditorSettings.Save(); break;

                case KeyEventType.ToggleShowGridKey:		ChiselEditorSettings.ShowGrid = !ChiselEditorSettings.ShowGrid; ChiselEditorSettings.Save(); break;
            }
        }
    }
}
