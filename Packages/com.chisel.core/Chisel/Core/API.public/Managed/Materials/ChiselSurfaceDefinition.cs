using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    public struct NativeChiselSurfaceDefinition
    {
        public BlobArray<NativeChiselSurface> surfaces;
    }

    [Serializable]
    public sealed class ChiselSurfaceDefinition
    {
        public ChiselSurface[] surfaces;
        

        public ChiselBrushMaterial GetBrushMaterial(int descriptionIndex) 
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return null;
            return surfaces[descriptionIndex].brushMaterial;
        }
        
        public SurfaceDescription GetSurfaceDescription(int descriptionIndex)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return SurfaceDescription.Default;
            return surfaces[descriptionIndex].surfaceDescription;
        }
        
        public void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return;
            surfaces[descriptionIndex].surfaceDescription = description;
        }
        
        public UVMatrix GetSurfaceUV0(int descriptionIndex)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return UVMatrix.identity;
            return surfaces[descriptionIndex].surfaceDescription.UV0; 
        }

        public void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return;
            surfaces[descriptionIndex].surfaceDescription.UV0 = uv0;
        }

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

            var defaultRenderMaterial   = ChiselMaterialManager.DefaultWallMaterial;
            var defaultPhysicsMaterial  = ChiselMaterialManager.DefaultPhysicsMaterial;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                if (surfaces == null || surfaces.Length == 0)
                    return 0;

                uint hash = (uint)surfaces[0].GetHashCode();
                for (int i = 1; i < surfaces.Length; i++)
                {
                    hash = math.hash(new uint2(hash, (uint)surfaces[i].GetHashCode()));
                }
                return (int)hash;
            }
        }
    }
}
