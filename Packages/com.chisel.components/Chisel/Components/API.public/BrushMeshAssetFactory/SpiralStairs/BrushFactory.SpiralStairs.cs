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
        public static bool GenerateSpiralStairs(ChiselGeneratedBrushes generatedBrushes, ref CSGSpiralStairsDefinition definition)
        {
            var brushMeshes = generatedBrushes.BrushMeshes;
            var operations  = generatedBrushes.Operations;
            if (!BrushMeshFactory.GenerateSpiralStairs(ref brushMeshes, ref operations, ref definition))
            {
                generatedBrushes.Clear();
                return false;
            }
            
            generatedBrushes.SetSubMeshes(brushMeshes, operations);
            generatedBrushes.CalculatePlanes();
            generatedBrushes.SetDirty();
            return true;
        }
    }
}