using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public sealed class ChiselSurfaceArray
    {
        public ChiselSurface[] surfaces;
        
        public ChiselSurface GetSurface(int descriptionIndex) 
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return null;
            return surfaces[descriptionIndex];
        }
        
        public SurfaceDetails GetSurfaceDetails(int descriptionIndex)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return SurfaceDetails.Default;
            return surfaces[descriptionIndex].surfaceDetails;
        }
        
        public void SetSurfaceDetails(int descriptionIndex, SurfaceDetails description)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return;
            surfaces[descriptionIndex].surfaceDetails = description;
        }
        
        public UVMatrix GetSurfaceUV0(int descriptionIndex)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return UVMatrix.identity;
            return surfaces[descriptionIndex].surfaceDetails.UV0; 
        }

        public void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0)
        {
            if (descriptionIndex < 0 || descriptionIndex >= surfaces.Length)
                return;
            surfaces[descriptionIndex].surfaceDetails.UV0 = uv0;
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

            var newSurfaces = new ChiselSurface[expectedSize];
            var prevLength  = (surfaces == null) ? 0 : surfaces.Length;
            if (prevLength > 0)
                Array.Copy(surfaces, newSurfaces, Mathf.Min(newSurfaces.Length, prevLength));
            for (int i = prevLength; i < newSurfaces.Length; i++)
            {
                newSurfaces[i] = ChiselSurface.Create(ChiselDefaultMaterials.DefaultWallMaterial);
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
