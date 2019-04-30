using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using System.Reflection;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    public static class ChiselHierarchyWindowManager
    {
        public static void RenderIcon(Rect selectionRect, GUIContent icon)
        {
            const float iconSize = 16;
            const float indent   = 0;
            var max = selectionRect.xMax;
            selectionRect.width = iconSize;
            selectionRect.height = iconSize;
            selectionRect.x = max - (iconSize + indent);
            selectionRect.y--;
            GUI.Label(selectionRect, icon);
        }

        internal static void OnHierarchyWindowItemGUI(CSGNode node, Rect selectionRect)
        {
            // TODO: implement material drag & drop support on hierarchy items

            if (Event.current.type != EventType.Repaint)
                return;

            var icon = ChiselNodeDetailsManager.GetHierarchyIcon(node);
            if (icon != null)
                RenderIcon(selectionRect, icon);
        }
    }
}
