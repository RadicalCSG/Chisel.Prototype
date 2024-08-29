using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ChiselSceneGUIStyle
    {
        public const float kTopBarHeight = 22;
        public const float kBottomBarHeight = 22;
        public const float kFloatFieldWidth = 60;

        const int kIconSize = 16;
        const int kOffsetToText = 3;

        public static GUIStyle toolbarStyle;
        public static GUIStyle windowStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle buttonStyle;


        public static GUIStyle inspectorLabel;
        public static GUIStyle inspectorSelectedLabel;

        public static bool isInitialized { get; private set; }
        static bool isProSkin = true;
        static bool prevIsProSkin = false;

        public static GUISkin GetSceneSkin()
        {
            isProSkin = EditorGUIUtility.isProSkin;
            if (isProSkin)
            {
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);
            } else
                return EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
        }

        public static void Update()
        {
            isProSkin = EditorGUIUtility.isProSkin;
            if (toolbarStyle != null && prevIsProSkin == isProSkin)
                return;

            isInitialized = true;
            inspectorLabel = new GUIStyle(GUI.skin.label);
            inspectorLabel.padding = new RectOffset(kIconSize + kOffsetToText, 0, 0, 0);

            inspectorSelectedLabel = new GUIStyle(inspectorLabel);
            if (!isProSkin)
            {
                inspectorSelectedLabel.normal.textColor = Color.white;
                inspectorSelectedLabel.onNormal.textColor = Color.white;
            }

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

            prevIsProSkin = isProSkin;
            ChiselEditorSettings.Load(); // <- put somewhere else
        }
    }
}
