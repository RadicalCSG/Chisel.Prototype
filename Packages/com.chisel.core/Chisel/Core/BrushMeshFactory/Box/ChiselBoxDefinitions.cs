using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct ChiselBoxDefinition
    {
        public static readonly Bounds   kDefaultBounds = new UnityEngine.Bounds(Vector3.zero, Vector3.one);

        public UnityEngine.Bounds       bounds;
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
    }

}