using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselHemisphereDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselHemispherePlacementTool : ChiselBoundsPlacementTool<ChiselHemisphereDefinition>
    {
        public int horizontalSegments   = ChiselHemisphereDefinition.kDefaultHorizontalSegments;
        public int verticalSegments     = ChiselHemisphereDefinition.kDefaultVerticalSegments;
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsHalfXZ | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsHalfXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselHemisphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
        }

        public override void OnUpdate(ref ChiselHemisphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
