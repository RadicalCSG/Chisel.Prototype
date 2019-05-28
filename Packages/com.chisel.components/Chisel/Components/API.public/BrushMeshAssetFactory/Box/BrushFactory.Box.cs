using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateBoxAsset(ChiselGeneratedBrushes generatedBrushes, UnityEngine.Bounds bounds, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            return GenerateBoxAsset(generatedBrushes, bounds.min, bounds.max, brushMaterials, surfaceDescriptions);
        }
        
        public static bool GenerateBoxAsset(ChiselGeneratedBrushes generatedBrushes, UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (!BoundsExtensions.IsValid(min, max))
            {
                generatedBrushes.Clear();
                Debug.LogError("bounds is of an invalid size " + (max - min));
                return false;
            }

            if (surfaceDescriptions == null || surfaceDescriptions.Length != 6)
            {
                generatedBrushes.Clear();
                Debug.LogError("surfaceDescriptions needs to be an array of length 6");
                return false;
            }
            
            if (brushMaterials == null || brushMaterials.Length != 6)
            {
                generatedBrushes.Clear();
                Debug.LogError("brushMaterials needs to be an array of length 6");
                return false;
            }

            var subMeshes = new[] { new ChiselGeneratedBrushes.ChiselGeneratedBrush() };
            BrushMeshFactory.GenerateBoxSubMesh(ref subMeshes[0].brushMesh, min, max, brushMaterials, surfaceDescriptions);
            generatedBrushes.SubMeshes = subMeshes;
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

            var subMeshes = new[] { new ChiselGeneratedBrushes.ChiselGeneratedBrush() };
            BrushMeshFactory.GenerateBoxSubMesh(ref subMeshes[0].brushMesh, min, max, brushMaterials, surfaceFlags);
            generatedBrushes.SubMeshes = subMeshes;
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
        
        public static ChiselGeneratedBrushes CreateBoxAsset(UnityEngine.Vector3 min, UnityEngine.Vector3 max, ChiselBrushMaterial[] brushMaterials, SurfaceDescription[] surfaceDescriptions)
        {
            if (min.x == max.x || min.y == max.y || min.z == max.z)
                return null;

            if (brushMaterials.Length != 6 ||
                surfaceDescriptions.Length != 6)
                return null;
            
            var brushMeshAsset = UnityEngine.ScriptableObject.CreateInstance<ChiselGeneratedBrushes>();
            brushMeshAsset.name = "Box";
            if (!GenerateBoxAsset(brushMeshAsset, min, max, brushMaterials, surfaceDescriptions))
                CSGObjectUtility.SafeDestroy(brushMeshAsset);
            return brushMeshAsset;
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

        /// <summary>
        /// Creates a box <see cref="Chisel.Core.BrushMesh"/> with <paramref name="size"/> and optional <paramref name="material"/>
        /// </summary>
        /// <param name="size">The size of the box</param>
        /// <param name="material">The [UnityEngine.Material](https://docs.unity3d.com/ScriptReference/Material.html) that will be set to all surfaces of the box (optional)</param>
        /// <returns>A <see cref="Chisel.Core.BrushMesh"/> on success, null on failure</returns>
        public static ChiselGeneratedBrushes CreateBoxAsset(UnityEngine.Vector3 size, ChiselBrushMaterial[] brushMaterials, SurfaceFlags surfaceFlags = SurfaceFlags.None)
        {
            var halfSize = size * 0.5f;
            return CreateBoxAsset(-halfSize, halfSize, brushMaterials, surfaceFlags);
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