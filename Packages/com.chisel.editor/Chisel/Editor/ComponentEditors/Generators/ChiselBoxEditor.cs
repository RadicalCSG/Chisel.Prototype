using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Chisel.Editors
{
    // TODO: why did resetting this generator not work?
    [CustomEditor(typeof(ChiselBox))]
    [CanEditMultipleObjects]
    public sealed class ChiselBoxEditor : ChiselGeneratorEditor<ChiselBox>
    {
        [MenuItem("GameObject/Chisel/" + ChiselBox.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselBox.kNodeTypeName); }

        protected override void OnScene(SceneView sceneView, ChiselBox generator)
        {
            EditorGUI.BeginChangeCheck();

            var newBounds = generator.Bounds;
            newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(newBounds, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Bounds = newBounds;
            }
        }
    }
}