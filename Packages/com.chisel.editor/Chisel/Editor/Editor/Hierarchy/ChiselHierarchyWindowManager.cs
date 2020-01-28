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
    public sealed class GUIView
    {
        static PropertyInfo currentProperty;
        static PropertyInfo hasFocusProperty;

        static GUIView()
        {
            var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            currentProperty = type.GetProperty("current");
            hasFocusProperty = type.GetProperty("hasFocus");
        }

        public static bool HasFocus
        {
            get
            {
                var currentGuiView = currentProperty.GetMethod.Invoke(null, null);
                if (currentGuiView == null)
                    return false;
                
                var hasFocusObj = hasFocusProperty.GetMethod.Invoke(currentGuiView, null);
                if (hasFocusObj == null)
                    return false;
                
                return (bool) hasFocusObj;
            }
        }
    }

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
            if (!ChiselSceneGUIStyle.isInitialized)
                return;
            var model = node as ChiselModel;
            if (!ReferenceEquals(model, null))
            {
                if (model == ChiselModelManager.ActiveModel)
                {
                    var content = EditorGUIUtility.TrTempContent(node.name + " (active)");

                    bool selected = GUIView.HasFocus && Selection.Contains(instanceID);
                    GUI.Label(selectionRect, content, selected ? ChiselSceneGUIStyle.inspectorSelectedLabel : ChiselSceneGUIStyle.inspectorLabel);
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
