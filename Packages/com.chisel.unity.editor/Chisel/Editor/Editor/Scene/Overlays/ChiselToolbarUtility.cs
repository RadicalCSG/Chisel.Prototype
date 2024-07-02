using UnityEditor;
using UnityEngine;
using Chisel.Components;
using UnityEditor.Toolbars;

namespace Chisel.Editors
{
    static class ChiselToolbarUtility
    {
        public static void SetupToolbarElement(EditorToolbarToggle element, string iconName, string tooltipName)
        {
            element.text   = string.Empty;
            var icons   = ChiselEditorResources.LoadIconImages(iconName);
            if (icons != null && icons[0] != null && icons[1] != null)
            {
                element.onIcon = icons[0];
                element.offIcon = icons[1];
            }
            element.tooltip = tooltipName;
        }

        public static void SetupToolbarElement(EditorToolbarButton element, string iconName, string tooltipName)
        {
            element.text = string.Empty;
            var icons = ChiselEditorResources.LoadIconImages(iconName);
            if (icons != null && icons[0] != null)
                element.icon = icons[0];
            element.tooltip = tooltipName;
        }
        
        // TODO: move somewhere else
        public static bool HaveNodesInSelection()
        {
            if (Selection.count == 0)
                return false;
            if (Selection.count == 1)
            {
                var chiselNode = Selection.activeObject as ChiselNode;
                if (chiselNode != null)
                    return true;

                var gameObject = Selection.activeObject as GameObject;
                if (gameObject != null)
                    if (gameObject.TryGetComponent<ChiselNode>(out _))
                        return true;
            }
            return Selection.GetFiltered<ChiselNode>(SelectionMode.Editable).Length > 0;
        }
    }
}
