using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSphereDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselSpherePlacementTool : ChiselBoundsPlacementTool<ChiselSphereDefinition>
    {
        public int  horizontalSegments  = ChiselSphere.DefaultValues.horizontalSegments;
        public int  verticalSegments    = ChiselSphere.DefaultValues.verticalSegments;
        
        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ |
                                            (ChiselSphere.DefaultValues.generateFromCenter ? PlacementFlags.GenerateFromCenterY : (PlacementFlags)0);
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselSphereDefinition definition) 
        {
            definition.settings.verticalSegments     = verticalSegments;
            definition.settings.horizontalSegments   = horizontalSegments;
            definition.settings.generateFromCenter   = false;
        }

        public override void OnUpdate(ref ChiselSphereDefinition definition, Bounds bounds)
        {
            definition.settings.diameterXYZ = bounds.size;
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            // TODO: Make a RenderSphere method
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
