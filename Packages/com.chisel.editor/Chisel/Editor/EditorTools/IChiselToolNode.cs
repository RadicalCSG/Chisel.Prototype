using Chisel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;
 
namespace Chisel.Editors
{
    public interface IChiselToolMode
    {
        string ToolName { get; }
        GUIContent Content { get; }

        void OnActivate();
        void OnDeactivate();

        void OnInSceneOptionsGUI(SceneView sceneView);
        void OnSceneGUI(SceneView sceneView, Rect dragArea);
    }
}
