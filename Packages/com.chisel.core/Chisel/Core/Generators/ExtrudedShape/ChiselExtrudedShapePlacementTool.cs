using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [ChiselPlacementTool(name: "Free Draw", group: ChiselToolGroups.kFreeForm)]
    public sealed class ChiselExtrudedShapePlacementTool : ChiselShapePlacementTool<ChiselExtrudedShapeDefinition>
    {
        public override void OnCreate(ref ChiselExtrudedShapeDefinition definition, Curve2D shape)
        {
            definition.path     = new ChiselPath(ChiselPath.Default);
            definition.shape    = new Curve2D(shape);
        }

        public override void OnUpdate(ref ChiselExtrudedShapeDefinition definition, float height)
        {
            definition.path.segments[1].position = ChiselPathPoint.kDefaultDirection * height;
        }

        public override void OnPaint(IChiselHandleRenderer renderer, Curve2D shape, float height)
        {
            renderer.RenderShape(shape, height);
        }
    }
}
