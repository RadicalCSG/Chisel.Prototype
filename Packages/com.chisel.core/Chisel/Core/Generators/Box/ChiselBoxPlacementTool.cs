using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselBoxDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselBoxPlacementTool : ChiselBoundsPlacementTool<ChiselBoxDefinition>
    {
        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.None;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnUpdate(ref ChiselBoxDefinition definition, Bounds bounds) 
        { 
            definition.bounds = bounds; 
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderBox(bounds);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
