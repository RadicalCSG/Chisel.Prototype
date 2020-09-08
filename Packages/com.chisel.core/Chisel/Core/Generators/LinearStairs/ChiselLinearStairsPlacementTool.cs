using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselLinearStairsDefinition.kNodeTypeName, group: ChiselGroups.kStairs)]
    public sealed class ChiselLinearStairsPlacementTool : ChiselBoundsPlacementTool<ChiselLinearStairsDefinition>
    {
        [ToggleFlags(includeFlags: (int)PlacementFlags.SameLengthXZ)]
        public PlacementFlags placement = PlacementFlags.AlwaysFaceUp | PlacementFlags.AlwaysFaceCameraXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnUpdate(ref ChiselLinearStairsDefinition definition, Bounds bounds)
        {
            definition.Reset();
            definition.bounds = bounds;
        }

        public override void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
