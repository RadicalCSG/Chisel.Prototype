using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    public sealed class ChiselExtrudedShapeSettings : ScriptableObject, IChiselShapeGeneratorSettings<ChiselExtrudedShapeDefinition>
    {
        public ChiselGeneratorModeFlags GeneratoreModeFlags => throw new NotImplementedException();

        public void OnCreate(ref ChiselExtrudedShapeDefinition definition, Curve2D shape)
        {
            definition.path = new ChiselPath(new[] {
                        new ChiselPathPoint(Vector3.zero),
                        new ChiselPathPoint(new Vector3(0,1,0))
                    });
            definition.shape = new Curve2D(shape);
        }

        public void OnUpdate(ref ChiselExtrudedShapeDefinition definition, float height)
        {
            definition.path.segments[1].position = new Vector3(0, height, 0);
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Curve2D shape, float height)
        {
            renderer.RenderShape(shape, height);
        }
    }

    public sealed class ChiselExtrudedShapeGeneratorMode : ChiselGeneratorModeWithSettings<ChiselExtrudedShapeSettings, ChiselExtrudedShapeDefinition, ChiselExtrudedShape>
    {
        const string kToolName = "Free Draw";
        public override string ToolName => kToolName;
        public override string Group => "Freeform";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + "Free Drawn Shape";
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.FreeBuilderModeKey, ChiselKeyboardDefaults.FreeBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselExtrudedShapeGeneratorMode); }
        #endregion

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoGenerationHandle(dragArea, Settings);
        }
    }
}
