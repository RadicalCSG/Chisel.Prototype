using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: ChiselCapsuleDefinition.kNodeTypeName, group: ChiselToolGroups.kBasePrimitives)]
    public sealed class ChiselCapsulePlacementTool : ChiselBoundsPlacementTool<ChiselCapsuleDefinition>
    {
        public int topSegments		= ChiselCapsule.DefaultSettings.topSegments;
        public int bottomSegments	= ChiselCapsule.DefaultSettings.bottomSegments;
        public int sides			= ChiselCapsule.DefaultSettings.sides;


        [ToggleFlags(includeFlags: (int)(PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterY | PlacementFlags.GenerateFromCenterXZ))]
        public PlacementFlags placement = PlacementFlags.SameLengthXZ | PlacementFlags.GenerateFromCenterXZ;
        public override PlacementFlags PlacementFlags => placement;


        public override void OnCreate(ref ChiselCapsuleDefinition definition) 
        {
            ref var settings = ref definition.settings;
            settings.sides           = sides;
            settings.topSegments     = topSegments;
            settings.bottomSegments  = bottomSegments;
        }

        public override void OnUpdate(ref ChiselCapsuleDefinition definition, Bounds bounds)
        {
            ref var settings = ref definition.settings;
            var height              = bounds.size[(int)Axis.Y];
            var hemisphereHeight    = Mathf.Min(bounds.size[(int)Axis.X], bounds.size[(int)Axis.Z]) * ChiselCapsule.kDefaultHemisphereRatio;

            settings.topHeight    = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);
            settings.bottomHeight = Mathf.Min(hemisphereHeight, Mathf.Abs(height) * 0.5f);

            settings.diameterX    = bounds.size[(int)Axis.X];
            settings.height       = height;
            settings.diameterZ    = bounds.size[(int)Axis.Z];
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Bounds bounds)
        {
            // TODO: render capsule here
            renderer.RenderCylinder(bounds, sides);
            renderer.RenderBoxMeasurements(bounds);
        }
    }
}
