using UnityEngine;
using UnitySceneExtensions;

namespace Chisel.Core
{
    /// Default group names for ChiselPlacementTools such as <see cref="Chisel.Core.ChiselBoundsPlacementTool"/> 
    /// and <see cref="Chisel.Core.ChiselShapePlacementTool"/>
    public static class ChiselToolGroups
    {
        public const string kBasePrimitives = "Basic Primitives";
        public const string kFreeForm       = "FreeForm";
        public const string kStairs         = "Stairs";

        // When a generator doesn't have a group (no attribute set), it'll use this name
        public const string kDefault        = "Default";
    }


    /// A placement tool that is placed by dragging a box like shape, and fitting the generator inside it
    /// Use <see cref="Chisel.Core.ChiselPlacementToolAttribute"/> to give it a name and group
    public abstract class ChiselBoundsPlacementTool<PlacementToolType> : ScriptableObject
        where PlacementToolType : IChiselGenerator, new()
    {
        public abstract PlacementFlags PlacementFlags { get; }
        public virtual void OnCreate(ref PlacementToolType definition) { }
        public abstract void OnUpdate(ref PlacementToolType definition, Bounds bounds);
        public abstract void OnPaint(IGeneratorHandleRenderer renderer, Bounds bounds);
    }


    /// A placement tool that is placed by drawing a 2D shape and extruding it
    /// Use <see cref="Chisel.Core.ChiselPlacementToolAttribute"/> to give it a name and group
    public abstract class ChiselShapePlacementTool<PlacementToolType> : ScriptableObject
        where PlacementToolType : IChiselGenerator, new()
    {
        public virtual void OnCreate(ref PlacementToolType definition, Curve2D shape) { }
        public abstract void OnUpdate(ref PlacementToolType definition, float height);
        public abstract void OnPaint(IGeneratorHandleRenderer renderer, Curve2D shape, float height);
    }
}
