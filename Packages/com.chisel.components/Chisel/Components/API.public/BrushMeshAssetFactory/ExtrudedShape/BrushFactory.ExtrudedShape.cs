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
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Components
{
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateExtrudedShape(ChiselGeneratedBrushes generatedBrushes, Curve2D shape, Path path, int curveSegments, ChiselBrushMaterial[] brushMaterials, ref SurfaceDescription[] surfaceDescriptions)
        {
            var brushMeshes = generatedBrushes.BrushMeshes;
            if (!BrushMeshFactory.GenerateExtrudedShape(ref brushMeshes, shape, path, curveSegments, brushMaterials, ref surfaceDescriptions))
            {
                generatedBrushes.Clear();
                return false;
            }

            generatedBrushes.SetSubMeshes(brushMeshes);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
    }
}