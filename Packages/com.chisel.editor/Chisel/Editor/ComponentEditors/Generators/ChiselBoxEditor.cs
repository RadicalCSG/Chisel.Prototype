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
        [MenuItem("GameObject/Chisel/Create/" + ChiselBox.kNodeTypeName, false, 0)]
        static void CreateAsGameObject(MenuCommand menuCommand) { CreateAsGameObjectMenuCommand(menuCommand, ChiselBox.kNodeTypeName); }

        protected override void OnScene(SceneView sceneView, ChiselBox generator)
        {
            EditorGUI.BeginChangeCheck();

            var newBounds = generator.Bounds;
            newBounds = UnitySceneExtensions.SceneHandles.BoundsHandle(newBounds, Quaternion.identity);
            HandleRendering.RenderBoxMeasurements(newBounds);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Modified " + generator.NodeTypeName);
                generator.Bounds = newBounds;
            }
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        protected override void OnInspector()
        {
            base.OnInspector();

            // TODO: create an "WarningMessage" method that returns a warningmessage when there's something to warn about, and use that in the inspector
            if (!HasValidState())
            {
                bool zeroSized = false;
                foreach (var target in targets)
                {
                    var generator = target as ChiselBox;
                    if (!generator)
                        continue;

                    if (generator.Size.x == 0 ||
                        generator.Size.y == 0 ||
                        generator.Size.z == 0) zeroSized = true;
                }
                if (zeroSized)
                {
                    EditorGUILayout.HelpBox(kDimensionCannotBeZero, MessageType.Warning, true);
                }
            }
        }
    }
}