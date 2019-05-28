using System;
using System.Linq;
using Chisel.Core;
using UnityEngine;
using System.Collections.Generic;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateBox(ChiselGeneratedBrushes generatedBrushes, ref CSGBoxDefinition definition)
        {
            var brushMeshes = new[] { new BrushMesh() };
            if (!BrushMeshFactory.GenerateBox(ref brushMeshes[0], ref definition))
            {
                generatedBrushes.Clear();
                return false;
            }
            generatedBrushes.SetSubMeshes(brushMeshes);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
        
        public static bool GenerateBoxAsset(ChiselGeneratedBrushes generatedBrushes, UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial[] brushMaterials, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            if (!BoundsExtensions.IsValid(min, max))
            {
                generatedBrushes.Clear();
                return false;
            }

            if (brushMaterials.Length != 6)
            {
                generatedBrushes.Clear();
                return false;
            }

            var brushMeshes = new[] { new BrushMesh() };
            BrushMeshFactory.GenerateBox(ref brushMeshes[0], min, max, brushMaterials, surfaceFlags);
            generatedBrushes.SetSubMeshes(brushMeshes);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
        
        public static ChiselGeneratedBrushes CreateBoxAsset(UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial[] brushMaterials, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            if (min.x == max.x || min.y == max.y || min.z == max.z)
                return null;

            if (brushMaterials.Length != 6)
                return null;

            var brushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            brushMeshAsset.name = "Box";
            if (!GenerateBoxAsset(brushMeshAsset, min, max, brushMaterials, surfaceFlags))
                CSGObjectUtility.SafeDestroy(brushMeshAsset);
            return brushMeshAsset;
        }
        
        public static ChiselGeneratedBrushes CreateBoxAsset(UnityEngine.Vector3 size, ChiselBrushMaterial brushMaterial, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            var halfSize = size * 0.5f;
            return CreateBoxAsset(-halfSize, halfSize, brushMaterial, surfaceFlags);
        }

        public static ChiselGeneratedBrushes CreateBoxAsset(UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial brushMaterial, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            return CreateBoxAsset(min, max, new ChiselBrushMaterial[] { brushMaterial, brushMaterial, brushMaterial, brushMaterial, brushMaterial, brushMaterial }, surfaceFlags);
        }
    }
}