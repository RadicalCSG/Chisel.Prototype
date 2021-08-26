using System;
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
using System.Diagnostics;

namespace Chisel.Editors
{
    [Overlay(typeof(SceneView), ChiselGeneratorSelectionOverlay.kOverlayTitle)]
    public class ChiselGeneratorSelectionOverlay : IMGUIOverlay
    {
        const string kOverlayTitle = "Chisel Generator Selection";

        public override void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            {
                ChiselPlacementToolsSelectionWindow.RenderCreationTools();
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }
    }
}
