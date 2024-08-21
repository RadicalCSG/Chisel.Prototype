using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSpiralStairsDefinition.kNodeTypeName, group: ChiselToolGroups.kStairs)]
    public sealed class ChiselSpiralStairsPlacementTool : ChiselBoundsPlacementTool<ChiselSpiralStairsDefinition>
    {
        // TODO: add more settings
        public float    stepHeight      = ChiselSpiralStairs.DefaultValues.stepHeight;
        public int      outerSegments   = ChiselSpiralStairs.DefaultValues.outerSegments;
        
        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ | PlacementFlags.AlwaysFaceUp | PlacementFlags.SameLengthXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselSpiralStairsDefinition definition) 
        {
            definition.settings.stepHeight       = stepHeight;
            definition.settings.outerSegments    = outerSegments;
        }

        public override void OnUpdate(ref ChiselSpiralStairsDefinition definition, Bounds bounds)
        {
            definition.settings.height			= bounds.size[(int)Axis.Y];
            definition.settings.outerDiameter	= bounds.size[(int)Axis.X];
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, outerSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
