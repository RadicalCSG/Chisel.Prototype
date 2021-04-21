using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSpiralStairsDefinition.kNodeTypeName, group: ChiselToolGroups.kStairs)]
    public sealed class ChiselSpiralStairsPlacementTool : ChiselBoundsPlacementTool<ChiselSpiralStairsDefinition>
    {
        // TODO: add more settings
        public float    stepHeight      = ChiselSpiralStairsDefinition.kDefaultStepHeight;
        public int      outerSegments   = ChiselSpiralStairsDefinition.kDefaultOuterSegments;
        
        [ToggleFlags(includeFlags: (int)(PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.GenerateFromCenterXZ | PlacementFlags.AlwaysFaceUp | PlacementFlags.SameLengthXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselSpiralStairsDefinition definition) 
        {
            definition.stepHeight       = stepHeight;
            definition.outerSegments    = outerSegments;
        }

        public override void OnUpdate(ref ChiselSpiralStairsDefinition definition, ref ChiselSurfaceDefinition surfaceDefinition, Bounds bounds)
        {
            definition.height			= bounds.size[(int)Axis.Y];
            definition.outerDiameter	= bounds.size[(int)Axis.X];
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, outerSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
