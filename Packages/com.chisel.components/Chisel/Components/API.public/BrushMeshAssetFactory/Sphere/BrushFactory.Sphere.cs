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
        public static bool GenerateSphere(ChiselGeneratedBrushes brushMeshAsset, ref CSGSphereDefinition definition)
        {
            var brushMesh = new[] { new BrushMesh() };
            if (!BrushMeshFactory.GenerateSphere(ref brushMesh[0], ref definition))
            {
                brushMeshAsset.Clear();
                return false;
            }

            brushMeshAsset.SetSubMeshes(brushMesh);
            brushMeshAsset.CalculatePlanes();
            brushMeshAsset.SetDirty();
            return true;
        }
    }
}