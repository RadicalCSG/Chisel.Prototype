using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBoxDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Box";

        public static readonly Bounds   kDefaultBounds = new Bounds(Vector3.zero, Vector3.one);

        public UnityEngine.Bounds       bounds;

        [NamedItems("Top", "Bottom", "Right", "Left", "Front", "Back", fixedSize = 6)]
        public ChiselSurfaceDefinition  surfaceDefinition;
        
        public Vector3                  min		{ get { return bounds.min; } set { bounds.min = value; } }
        public Vector3			        max	    { get { return bounds.max; } set { bounds.max = value; } }
        public Vector3			        size    { get { return bounds.size; } set { bounds.size = value; } }
        public Vector3			        center  { get { return bounds.center; } set { bounds.center = value; } }
        
        public void Reset()
        {
            bounds = kDefaultBounds;
            if (surfaceDefinition != null) surfaceDefinition.Reset();
        }

        public void Validate()
        {
            if (surfaceDefinition == null)
                surfaceDefinition = new ChiselSurfaceDefinition();
            surfaceDefinition.EnsureSize(6);
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GenerateBox(ref brushContainer, ref this);
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoBoundsHandle(ref bounds);
            handles.RenderBoxMeasurements(bounds);
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

        public void OnMessages(IChiselMessages messages)
        {
            if (bounds.size.x == 0 || bounds.size.y == 0 || bounds.size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
    }

}