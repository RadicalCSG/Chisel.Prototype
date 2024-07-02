using UnityEditor;
using UnityEngine;
 
namespace Chisel.Editors
{
    public interface IChiselToolMode
    {
        string ToolName { get; }
        GUIContent Content { get; }

        void OnActivate();
        void OnDeactivate();

        void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
