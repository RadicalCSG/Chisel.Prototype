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
				case KeyEventType.HalfGridSizeKey:			CSGSceneBottomGUI.DecreaseMoveSnapAll(); break;
				case KeyEventType.DoubleGridSizeKey:		CSGSceneBottomGUI.IncreaseMoveSnapAll(); break;	

				// TODO: turn this into utility functions that are shared with CSGSceneBottomGUI
				case KeyEventType.ToggleBoundsSnappingKey:	CSGEditorSettings.PivotSnapping = !CSGEditorSettings.PivotSnapping; CSGEditorSettings.Save(); break;
				case KeyEventType.TogglePivotSnappingKey:	CSGEditorSettings.BoundsSnapping = !CSGEditorSettings.BoundsSnapping; CSGEditorSettings.Save(); break;

				case KeyEventType.ToggleShowGridKey:		CSGEditorSettings.ShowGrid = !CSGEditorSettings.ShowGrid; CSGEditorSettings.Save(); break;
			}
		}
	}
}
