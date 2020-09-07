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
    [ChiselPlacementTool(name: ChiselLinearStairs.kNodeTypeName, group: "Stairs")]
    public sealed class ChiselLinearStairsPlacementTool : ScriptableObject, IChiselBoundsPlacementTool<ChiselLinearStairsDefinition>
    {
        [ToggleFlags(includeFlags: (int)Editors.PlacementFlags.SameLengthXZ)]
        public PlacementFlags placement = Editors.PlacementFlags.AlwaysFaceUp | Editors.PlacementFlags.AlwaysFaceCameraXZ;        
        public PlacementFlags PlacementFlags => placement;

        public void OnCreate(ref ChiselLinearStairsDefinition definition) {}

        public void OnUpdate(ref ChiselLinearStairsDefinition definition, Bounds bounds)
        {
            definition.Reset();
            definition.bounds = bounds;
        }

        public void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
