using System;
using System.Linq;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using Chisel.Core;

namespace Chisel.Components
{
    public sealed partial class BrushMeshAssetFactory
    {
        public static bool GenerateSphereAsset(ChiselGeneratedBrushes brushMeshAsset, CSGSphereDefinition definition)
        {
            var subMeshes = new[] { new ChiselGeneratedBrushes.ChiselGeneratedBrush() };
            if (!BrushMeshFactory.GenerateSphere(ref subMeshes[0].brushMesh, definition))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SubMeshes = subMeshes;
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }
    }
}