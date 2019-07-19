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
    // TODO: add tooltips
    public static class ChiselSceneBottomGUI
    {
        const float kBottomBarHeight    = 20;
        const float kTopBarHeight       = 17;
        const float kFloatFieldWidth    = 60;

        static readonly int BottomBarGUIHash = typeof(ChiselSceneBottomGUI).Name.GetHashCode();

        static GUILayoutOption floatWidthLayout;

        static GUIStyle toolbarStyle;
        static GUIStyle toggleStyle;
        static GUIStyle buttonStyle;
        static bool     prevSkin = false;

        // UI Element definitions
        static GUIContent rebuildButton = new GUIContent("Rebuild");
        static GUIContent doubleSnapDistanceButton = new GUIContent("+", "Double the snapping distance.\nHotkey: ]");
        static GUIContent halveSnapDistanceButton = new GUIContent("-", "Halve the snapping distance.\nHotkey: [");

        static readonly int bottomBarGuiId					= 21001;


        public static void Rebuild()
        {
            CSGNodeHierarchyManager.Rebuild();
        }

        static void OnBottomBarUI(int windowID)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();

            // TODO: assign hotkey to rebuild, and possibly move it elsewhere to avoid it seemingly like a necessary action.
            if (GUILayout.Button(rebuildButton, buttonStyle)) { 
                Rebuild();
            }

            GUILayout.FlexibleSpace();

            ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", toggleStyle);

            ChiselEditorSettings.UniformSnapDistance = EditorGUILayout.FloatField(ChiselEditorSettings.UniformSnapDistance, floatWidthLayout);
            if (GUILayout.Button(halveSnapDistanceButton, EditorStyles.miniButtonLeft)) {
                SnappingKeyboard.HalfGridSize();
            }
            if (GUILayout.Button(doubleSnapDistanceButton, EditorStyles.miniButtonRight)) {
                SnappingKeyboard.DoubleGridSize();
            }

            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }


        public static Rect OnSceneGUI(SceneView sceneView)
        {
            // TODO: put somewhere else
            var curSkin = EditorGUIUtility.isProSkin;
            if (toolbarStyle == null ||
                prevSkin != curSkin)
            {

                toolbarStyle = new GUIStyle(EditorStyles.toolbar);
                toolbarStyle.fixedHeight = kBottomBarHeight;

                toggleStyle = new GUIStyle(EditorStyles.toolbarButton);
                toggleStyle.fixedHeight = kBottomBarHeight;

                buttonStyle = new GUIStyle(EditorStyles.toolbarButton);
                buttonStyle.fixedHeight = kBottomBarHeight;

                floatWidthLayout = GUILayout.Width(kFloatFieldWidth);

                prevSkin = curSkin;
                ChiselEditorSettings.Load();
            }


            // Calculate size of bottom bar and draw it
            Rect position = sceneView.position;
            position.x		= 0;
            position.y		= position.height - kBottomBarHeight;
            position.height = kBottomBarHeight; 

            GUILayout.Window(bottomBarGuiId, position, OnBottomBarUI, "", toolbarStyle);
            CSGEditorUtility.ConsumeUnusedMouseEvents(BottomBarGUIHash, position);

            Rect dragArea = sceneView.position;
            dragArea.x = 0;
            dragArea.y = kTopBarHeight;
            dragArea.height -= kBottomBarHeight + kTopBarHeight;
            return dragArea;
        }
    }
}
