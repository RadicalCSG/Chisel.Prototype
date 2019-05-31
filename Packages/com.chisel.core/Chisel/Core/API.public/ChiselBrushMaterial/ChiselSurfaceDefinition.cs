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
            if ((surfaces != null && expectedSize == surfaces.Length) || 
                (surfaces == null && expectedSize == 0))
                return false;

            if (expectedSize == 0)
            {
                surfaces = null;
                return true;
            }

            var defaultRenderMaterial   = CSGMaterialManager.DefaultWallMaterial;
            var defaultPhysicsMaterial  = CSGMaterialManager.DefaultPhysicsMaterial;
            var newSurfaces = new ChiselSurface[expectedSize];
            var prevLength  = (surfaces == null) ? 0 : surfaces.Length;
            if (prevLength > 0)
                Array.Copy(surfaces, newSurfaces, Mathf.Min(newSurfaces.Length, prevLength));
            for (int i = prevLength; i < newSurfaces.Length; i++)
            {
                newSurfaces[i] = new ChiselSurface
                {
                    surfaceDescription  = SurfaceDescription.Default,
                    brushMaterial       = ChiselBrushMaterial.CreateInstance(defaultRenderMaterial, defaultPhysicsMaterial)
                };
            }
            surfaces = newSurfaces;
            return true;
        }
    }
}
