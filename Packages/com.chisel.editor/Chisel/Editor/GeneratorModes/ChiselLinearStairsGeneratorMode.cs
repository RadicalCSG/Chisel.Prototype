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
    public sealed class ChiselLinearStairsSettings : ScriptableObject
    {
    }

    public sealed class ChiselLinearStairsGeneratorMode : ChiselGeneratorModeWithSettings<ChiselLinearStairsSettings, ChiselLinearStairsDefinition, ChiselLinearStairs>
    {
        const string kToolName = ChiselLinearStairs.kNodeTypeName;
        public override string ToolName => kToolName;
        public override string Group => "Stairs";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.LinearStairsBuilderModeKey, ChiselKeyboardDefaults.LinearStairsBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselLinearStairsGeneratorMode); }
        #endregion

        public override ChiselGeneratorModeFlags Flags 
        { 
            get
            {
                return ChiselGeneratorModeFlags.AlwaysFaceUp | ChiselGeneratorModeFlags.AlwaysFaceCameraXZ;
            } 
        }

        protected override void OnCreate(ChiselLinearStairs generatedComponent)
        {
            generatedComponent.Operation  = forceOperation ?? CSGOperationType.Additive;
        }

        protected override void OnUpdate(ChiselLinearStairs generatedComponent, Bounds bounds)
        {
            generatedComponent.Bounds = bounds;
        }

        protected override void OnPaint(Matrix4x4 transformation, Bounds bounds)
        {
            HandleRendering.RenderBox(transformation, bounds);
        }

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            DoBoxGenerationHandle(dragArea, ToolName);
        }
    }
}
