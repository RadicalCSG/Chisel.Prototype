using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ChiselGUIUtility
    {
        internal const float kTopBarHeight       = 17;

        public static Rect GetRectForEditorWindow(EditorWindow window)
        {
            Rect dragArea = window.position;
            dragArea.x = 0;
            dragArea.y = ChiselSceneGUIStyle.kTopBarHeight;
            dragArea.height -= ChiselSceneGUIStyle.kBottomBarHeight + ChiselSceneGUIStyle.kTopBarHeight;
            return dragArea;
        }

        public static bool LabelHasContent(GUIContent label)
        {
            if (label == null)
            {
                return true;
            }
            // @TODO: find out why checking for GUIContent.none doesn't work
            return label.text != string.Empty || label.image != null;
        }
    }
}
