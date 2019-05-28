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
    // TODO: clean up
    // TODO: rename
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateCylinderAsset(ChiselGeneratedBrushes brushMeshAsset, CSGCylinderDefinition definition)
        {
            var brushMeshes = new [] { new BrushMesh() };
            if (!BrushMeshFactory.GenerateCylinder(ref brushMeshes[0], definition))
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