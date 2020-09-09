using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselCapsuleDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselCapsulePlacementTool : ChiselBoundsPlacementTool<ChiselCapsuleDefinition>
    {
        public int topSegments		= ChiselCapsuleDefinition.kDefaultTopSegments;
        public int bottomSegments	= ChiselCapsuleDefinition.kDefaultBottomSegments;
        public int sides			= ChiselCapsuleDefinition.kDefaultSides;
        

        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;


        public override void OnCreate(ref ChiselCapsuleDefinition definition) 
        {
            definition.sides           = sides;
            definition.topSegments     = topSegments;
            definition.bottomSegments  = bottomSegments;
        }

        public override void OnUpdate(ref ChiselCapsuleDefinition definition, Bounds bounds)
        {
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsuleDefinition.kDefaultHemisphereRatio;

            definition.topHeight    = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            definition.bottomHeight = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            definition.diameterX    = bounds.size[(int)Axis.X];
            definition.height       = height;
            definition.diameterZ    = bounds.size[(int)Axis.Z];
        }

        public override void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds)
        {
            // TODO: render capsule here
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
