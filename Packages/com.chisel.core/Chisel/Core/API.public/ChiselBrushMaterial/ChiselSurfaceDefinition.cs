using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Chisel.Core
{
    [Serializable]
    public sealed class ChiselSurfaceDefinition
    {
        public ChiselSurface[] surfaces;

        public void Reset() { surfaces = null; }

        public bool EnsureSize(int expectedSize)
        {
            if (surfaces == null ||
                surfaces.Length != expectedSize)
            {
                var defaultRenderMaterial = CSGMaterialManager.DefaultWallMaterial;
                var defaultPhysicsMaterial = CSGMaterialManager.DefaultPhysicsMaterial;
                surfaces = new ChiselSurface[expectedSize];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    surfaces[i] = new ChiselSurface
                    {
                        surfaceDescription  = SurfaceDescription.Default,
                        brushMaterial       = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial)
                    };
                }
                return true;
            }
            return false;
        }
    }
}
