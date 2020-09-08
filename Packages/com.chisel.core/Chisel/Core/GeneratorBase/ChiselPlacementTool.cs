using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    public static class ChiselGroups
    {
        public const string kBasePrimitives = "Basic Primitives";
        public const string kFreeForm       = "FreeForm";
        public const string kStairs         = "Stairs";
    }

    public abstract class ChiselBoundsPlacementTool<DefinitionType> : ScriptableObject
        where DefinitionType : IChiselGenerator, new()
    {
        public abstract PlacementFlags PlacementFlags { get; }
        public virtual void OnCreate(ref DefinitionType definition) { }
        public abstract void OnUpdate(ref DefinitionType definition, Bounds bounds);
        public abstract void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds);
    }

    public abstract class ChiselShapePlacementTool<DefinitionType> : ScriptableObject
        where DefinitionType : IChiselGenerator, new()
    {
        public virtual void OnCreate(ref DefinitionType definition, Curve2D shape) { }
        public abstract void OnUpdate(ref DefinitionType definition, float height);
        public abstract void OnPaint(IGeneratorHandleRenderer renderer, Curve2D shape, float height);
    }
}
