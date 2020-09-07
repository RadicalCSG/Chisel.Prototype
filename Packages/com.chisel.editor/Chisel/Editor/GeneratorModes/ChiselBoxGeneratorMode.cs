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
    [ChiselPlacementTool(name: ChiselBox.kNodeTypeName, group: "Basic Primitives")]
    public sealed class ChiselBoxPlacementTool : ScriptableObject, IChiselBoundsPlacementTool<ChiselBoxDefinition>
    {
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
}
