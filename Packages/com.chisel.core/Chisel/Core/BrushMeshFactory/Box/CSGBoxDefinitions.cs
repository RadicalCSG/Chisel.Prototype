using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: beveled edges?
    [Serializable]
    public struct CSGBoxDefinition
    {
        public static readonly Bounds   kDefaultBounds = new UnityEngine.Bounds(Vector3.zero, Vector3.one);

        public UnityEngine.Bounds       bounds;
        public ChiselBrushMaterial[]    brushMaterials;
        public SurfaceDescription[]     surfaceDescriptions;
        
        public Vector3                  min		{ get { return bounds.min; } set { bounds.min = value; } }
        public Vector3			        max	    { get { return bounds.max; } set { bounds.max = value; } }
        public Vector3			        size    { get { return bounds.size; } set { bounds.size = value; } }
        public Vector3			        center  { get { return bounds.center; } set { bounds.center = value; } }

        public void Reset()
        {
            bounds              = kDefaultBounds;

            brushMaterials		= null;
            surfaceDescriptions = null;
        }

        public void Validate()
        {
            if (brushMaterials == null ||
                brushMaterials.Length != 6)
            {
                var defaultRenderMaterial  = CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial;
                brushMaterials = new []
                {
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),

                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                    ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial),
                };
            }

            if (surfaceDescriptions == null ||
                surfaceDescriptions.Length != 6)
            {
                surfaceDescriptions = new[]
                {
                    SurfaceDescription.Default, SurfaceDescription.Default, SurfaceDescription.Default,
                    SurfaceDescription.Default, SurfaceDescription.Default, SurfaceDescription.Default
                };
            }
        }
    }

}