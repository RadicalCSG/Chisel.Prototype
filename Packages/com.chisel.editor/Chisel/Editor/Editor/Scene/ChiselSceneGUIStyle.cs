using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;

namespace Chisel.Editors
{
    public static class ChiselSceneGUIStyle
    {
        public const float kTopBarHeight        = 22;
        public const float kBottomBarHeight     = 22;
        public const float kFloatFieldWidth     = 60;

        public static GUIStyle toolbarStyle;
        public static GUIStyle windowStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle buttonStyle;

        static bool prevSkinType = false;

        public static GUISkin GetSceneSkin()
        {
            if (EditorGUIUtility.isProSkin)
            {
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
            } else
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
        }

        public static void Update()
        {
            var curSkin = EditorGUIUtility.isProSkin;
            if (toolbarStyle != null && prevSkinType == curSkin)
                return;

            windowStyle = new GUIStyle(GUI.skin.window);

            toolbarStyle = new GUIStyle(GUI.skin.window);
            toolbarStyle.fixedHeight = kBottomBarHeight;
            toolbarStyle.padding = new RectOffset(2, 6, 0, 1); 
            toolbarStyle.contentOffset = Vector2.zero;

            toggleStyle = new GUIStyle(EditorStyles.toolbarButton);
            toggleStyle.fixedHeight = kBottomBarHeight - 2;
            toggleStyle.margin = new RectOffset(0, 0, 1, 0);

            buttonStyle = new GUIStyle(EditorStyles.toolbarButton);
            buttonStyle.fixedHeight = kBottomBarHeight - 2;
            buttonStyle.margin = new RectOffset(0, 0, 1, 0);

            prevSkinType = curSkin;
            ChiselEditorSettings.Load(); // <- put somewhere else
        }
    }
}
