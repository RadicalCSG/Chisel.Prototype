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
    public sealed class ChiselBoxSettings : ScriptableObject, IChiselBoundsPlacementSettings<ChiselBoxDefinition>
    {
        const string    kToolName   = ChiselBox.kNodeTypeName;
        public string   ToolName    => kToolName;
        public string   Group       => "Basic Primitives";

        #region Keyboard Shortcut
        const string kToolShotcutName = ChiselKeyboardDefaults.ShortCutCreateBase + kToolName;
        [Shortcut(kToolShotcutName, ChiselKeyboardDefaults.BoxBuilderModeKey, ChiselKeyboardDefaults.BoxBuilderModeModifiers, displayName = kToolShotcutName)]
        public static void StartGeneratorMode() { ChiselGeneratorManager.GeneratorType = typeof(ChiselBoxGeneratorMode); }
        #endregion

        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.None;
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselBoxDefinition definition) {}
        public void OnUpdate(ref ChiselBoxDefinition definition, Bounds bounds) { definition.bounds = bounds; }
        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    } 

    // TODO: get rid of this, make the code find the definitions, *REGISTER* it, and create a generic GeneratorMode for that definition
    //       make it all data driven so we can support custom generators
    //       *Find* type of component that uses our definition
    public sealed class ChiselBoxGeneratorMode : ChiselBoundsPlacementTool<ChiselBoxSettings, ChiselBoxDefinition, ChiselBox>
    {
    }
}
