using Unity.Mathematics;

using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselLinearStairsDefinition.kNodeTypeName, group: ChiselToolGroups.kStairs)]
    public sealed class ChiselLinearStairsPlacementTool : ChiselBoundsPlacementTool<ChiselLinearStairsDefinition>
    {
        [ToggleFlags(includeFlags: (int)PlacementFlags.SameLengthXZ)]
        public PlacementFlags placement = PlacementFlags.AlwaysFaceUp | PlacementFlags.AlwaysFaceCameraXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnUpdate(ref ChiselLinearStairsDefinition definition, Bounds bounds)
        {
            definition.Reset();
            definition.settings.bounds = bounds.ToAABB();
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
