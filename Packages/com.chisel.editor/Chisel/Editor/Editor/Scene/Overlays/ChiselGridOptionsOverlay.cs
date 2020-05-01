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
    public static class ChiselGridOptionsOverlay
    {
        const int kPrimaryOrder = 99;

        const string                    kOverlayTitle               = "Grid";
        static readonly ChiselOverlay   kOverlay                    = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);

        static readonly GUIContent      kDoubleSnapDistanceButton   = EditorGUIUtility.TrTextContent("+", "Double the snapping distance.\nHotkey: ]");
        static readonly GUIContent      kHalveSnapDistanceButton    = EditorGUIUtility.TrTextContent("-", "Halve the snapping distance.\nHotkey: [");

        static GUILayoutOption sizeButtonWidth = GUILayout.Width(16);

        static void DisplayControls(SceneView sceneView)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(ChiselOverlay.kMinWidthLayout);

            ChiselEditorSettings.ShowGrid = GUILayout.Toggle(ChiselEditorSettings.ShowGrid, "Show Grid", GUI.skin.button);

            ChiselEditorSettings.UniformSnapSize = EditorGUILayout.FloatField(ChiselEditorSettings.UniformSnapSize);
            if (GUILayout.Button(kHalveSnapDistanceButton, EditorStyles.miniButtonLeft, sizeButtonWidth))
            {
                SnappingKeyboard.HalfGridSize();
            }
            if (GUILayout.Button(kDoubleSnapDistanceButton, EditorStyles.miniButtonRight, sizeButtonWidth))
            {
                SnappingKeyboard.DoubleGridSize();
            }

            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        public static void Show()
        {
            kOverlay.Show();
        }
    }
}
