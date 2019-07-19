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
        
        static void RenderHierarchyItem(int instanceID, ChiselNode node, Rect selectionRect)
        {
            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                if (model == ChiselModelManager.ActiveModel)
                {
                    const float kIconSize     = 16;
                    const float kOffsetToText = 0.0f;
                    //const float kOffsetToText = 24.0f;  // this used to be a random '24' in another version of unity?

                    var rect = selectionRect;
                    rect.xMin += kIconSize + kOffsetToText;

                    var content     = EditorGUIUtility.TrTempContent(node.name + " (active)");

                    // TODO: figure out correct color depending on selection and proSkin

                    GUI.Label(rect, content);
                    rect.xMin += 0.5f;
                    GUI.Label(rect, content);
                }
            }

            var icon = ChiselNodeDetailsManager.GetHierarchyIcon(node);
            if (icon != null)
                RenderIcon(selectionRect, icon);
        }

        internal static void OnHierarchyWindowItemGUI(int instanceID, ChiselNode node, Rect selectionRect)
        {
            // TODO: implement material drag & drop support on hierarchy items

            if (Event.current.type == EventType.Repaint)
                RenderHierarchyItem(instanceID, node, selectionRect);
        }
    }
}
