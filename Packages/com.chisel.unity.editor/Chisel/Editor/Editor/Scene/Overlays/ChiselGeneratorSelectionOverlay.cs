using UnityEditor;
using UnityEditor.Overlays;

namespace Chisel.Editors
{
    [Overlay(typeof(SceneView), kOverlayTitle)]
    public class ChiselGeneratorSelectionOverlay : IMGUIOverlay
    {
        const string kOverlayTitle = "Chisel Active Generator";

        public override void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            {
                ChiselPlacementToolsSelectionWindow.RenderGeneratorTools();
            }
            if (EditorGUI.EndChangeCheck())
                ChiselEditorSettings.Save();
        }
    }
}
