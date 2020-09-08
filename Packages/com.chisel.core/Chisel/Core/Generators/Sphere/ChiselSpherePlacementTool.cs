using UnityEngine;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselSphereDefinition.kNodeTypeName, group: ChiselGroups.kBasePrimitives)]
    public sealed class ChiselSpherePlacementTool : ChiselBoundsPlacementTool<ChiselSphereDefinition>
    {
        public int  horizontalSegments  = ChiselSphereDefinition.kDefaultHorizontalSegments;
        public int  verticalSegments    = ChiselSphereDefinition.kDefaultVerticalSegments;
        
        [ToggleFlags]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.HeightEqualsXZ | PlacementFlags.GenerateFromCenterXZ |
                                            (ChiselSphereDefinition.kDefaultGenerateFromCenter ? PlacementFlags.GenerateFromCenterY : (PlacementFlags)0);
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselSphereDefinition definition) 
        {
            definition.verticalSegments     = verticalSegments;
            definition.horizontalSegments   = horizontalSegments;
            definition.generateFromCenter   = false;
        }

        public override void OnUpdate(ref ChiselSphereDefinition definition, Bounds bounds)
        {
            definition.diameterXYZ = bounds.size;
        }

        public override void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: Make a RenderSphere method
            renderer.RenderCylinder(bounds, horizontalSegments);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
