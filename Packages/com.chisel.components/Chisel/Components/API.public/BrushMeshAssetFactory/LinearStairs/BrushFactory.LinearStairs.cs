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
        public static bool GenerateLinearStairsAsset(ChiselGeneratedBrushes brushMeshAsset, CSGLinearStairsDefinition definition)
        {
            var brushMeshes = brushMeshAsset.BrushMeshes;
            if (!BrushMeshFactory.GenerateLinearStairsSubMeshes(ref brushMeshes, definition, definition.leftSide, definition.rightSide, 0))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SetSubMeshes(brushMeshes);
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }

    }
}