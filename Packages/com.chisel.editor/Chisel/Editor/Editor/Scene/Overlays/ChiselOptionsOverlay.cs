using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.EditorTools;

namespace Chisel.Editors
{
    public static class ChiselOptionsOverlay
    {
        public static ChiselOverlay.WindowFunction AdditionalSettings;
        
        const int kPrimaryOrder = int.MaxValue;
        
        const string                    kOverlayTitle   = "Chisel Options";
        static readonly ChiselOverlay   OverlayWindow   = new ChiselOverlay(kOverlayTitle, DisplayControls, kPrimaryOrder);

        static void DisplayControls(SceneView sceneView)
        {
            EditorGUI.BeginChangeCheck();
            {
                AdditionalSettings?.Invoke(sceneView);
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }

        public static void SetTitle(string title) 
        {
            OverlayWindow.Title = title;
        }

        public static void Show()
        {
            if (AdditionalSettings != null)
                OverlayWindow.Show();
        }
    }
}
