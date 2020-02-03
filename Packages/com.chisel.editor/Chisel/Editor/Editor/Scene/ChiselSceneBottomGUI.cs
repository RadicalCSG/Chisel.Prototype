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
        static readonly int BottomBarGUIHash = typeof(ChiselSceneBottomGUI).Name.GetHashCode();

        static GUILayoutOption floatWidthLayout = GUILayout.Width(ChiselSceneGUIStyle.kFloatFieldWidth);


        // UI Element definitions
        static GUIContent rebuildButton             = new GUIContent("Rebuild");
        static GUIContent doubleSnapDistanceButton  = new GUIContent("+", "Double the snapping distance.\nHotkey: ]");
        static GUIContent halveSnapDistanceButton   = new GUIContent("-", "Halve the snapping distance.\nHotkey: [");

        static readonly int kBottomBarID		    = "ChiselSceneBottomGUI".GetHashCode();


        public static void Rebuild()
        {
            var startTime = EditorApplication.timeSinceStartup;
            ChiselNodeHierarchyManager.Rebuild();
            var csg_endTime = EditorApplication.timeSinceStartup;
            Debug.Log($"Full CSG rebuild done in {((csg_endTime - startTime) * 1000)} ms. ");
        }

        static GUILayoutOption sizeButtonWidth = GUILayout.Width(12);

        static readonly GUI.WindowFunction OnBottomBarUI = OnBottomBarUIFunction;
        static void OnBottomBarUIFunction(int windowID)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();

            // TODO: assign hotkey to rebuild, and possibly move it elsewhere to avoid it seemingly like a necessary action.
            if (GUILayout.Button(rebuildButton, ChiselSceneGUIStyle.buttonStyle)) { 
                Rebuild();
            }

            GUILayout.FlexibleSpace();

            ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", ChiselSceneGUIStyle.toggleStyle);

            ChiselEditorSettings.UniformSnapDistance = EditorGUILayout.FloatField(ChiselEditorSettings.UniformSnapDistance, floatWidthLayout);
            if (GUILayout.Button(halveSnapDistanceButton, EditorStyles.miniButtonLeft, sizeButtonWidth)) {
                SnappingKeyboard.HalfGridSize();
            }
            if (GUILayout.Button(doubleSnapDistanceButton, EditorStyles.miniButtonRight, sizeButtonWidth)) {
                SnappingKeyboard.DoubleGridSize();
            }

            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        public static void OnSceneGUI(SceneView sceneView)
        {
            // Calculate size of bottom bar and draw it
            Rect position = sceneView.position;
            position.x		= -2;
            position.y		= position.height - ChiselSceneGUIStyle.kBottomBarHeight + 1;
            position.width  += 4;
            position.height = ChiselSceneGUIStyle.kBottomBarHeight;

            GUI.Window(kBottomBarID, position, OnBottomBarUI, string.Empty, ChiselSceneGUIStyle.toolbarStyle);

            ChiselEditorUtility.ConsumeUnusedMouseEvents(BottomBarGUIHash, position);
        }
    }
}
