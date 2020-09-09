using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselCylinderDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselCylinderPlacementTool : ChiselBoundsPlacementTool<ChiselCylinderDefinition>
    {
        public CylinderShapeType    cylinderType		 = CylinderShapeType.Cylinder;
        public int				    sides			     = 16;
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;

        public override void OnCreate(ref ChiselCylinderDefinition definition) 
        {
            definition.isEllipsoid		= (placement & PlacementFlags.SameLengthXZ) != PlacementFlags.SameLengthXZ;
            definition.type				= cylinderType;
            definition.sides			= sides;
        }

        public override void OnUpdate(ref ChiselCylinderDefinition definition, Bounds bounds)
        {
            var height = bounds.size[(int)Axis.Y];
            definition.BottomDiameterX  = bounds.size[(int)Axis.X];
            definition.top.height       = height;
            definition.BottomDiameterZ  = bounds.size[(int)Axis.Z];
        }

        public override void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
