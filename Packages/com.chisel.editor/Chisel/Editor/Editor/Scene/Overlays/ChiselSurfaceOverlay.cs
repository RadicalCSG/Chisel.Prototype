﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using UnityEditor.EditorTools;
using UnitySceneExtensions;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;

namespace Chisel.Editors
{
    [Overlay(typeof(SceneView), ChiselSurfaceOverlay.kOverlayTitle)]
    public class ChiselSurfaceOverlay : IMGUIOverlay, ITransientOverlay
    {
        const string kOverlayTitle = "Surface Options";
        
        // TODO: CLEAN THIS UP
        public const int kMinWidth = ((242 + 32) - ((32 + 2) * ChiselPlacementToolsSelectionWindow.kToolsWide)) + (ChiselPlacementToolsSelectionWindow.kButtonSize * ChiselPlacementToolsSelectionWindow.kToolsWide);
        public static readonly GUILayoutOption kMinWidthLayout = GUILayout.MinWidth(kMinWidth);

        static bool show = false;
        public bool visible { get { return show && Tools.current == Tool.Custom; } }

        public static void Show() { show = true; }
        public static void Hide() { show = false; }

        public override void OnGUI()
        {
            EditorGUILayout.GetControlRect(false, 0, kMinWidthLayout);
            var sceneView = containerWindow as SceneView;
            ChiselUVToolCommon.Instance.OnSceneSettingsGUI(sceneView);
        }
    }
}
