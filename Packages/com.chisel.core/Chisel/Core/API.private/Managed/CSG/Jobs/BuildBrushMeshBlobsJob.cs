using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    /*
    struct BuildBrushMeshBlobsJob : IJobParallelFor
    {
        [ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>> brushMeshBlobs;
        [ReadOnly] public NativeList<int> brushMeshUpdateList;
        [ReadOnly] public NativeList<BrushMesh> brushMeshes;

        public void Execute(int index)
        {
            var brushMeshBlobs = ChiselMeshLookup.Value.brushMeshBlobs;
            foreach (var brushMeshIndex in brushMeshUpdateList)
            {
                var brushMeshID = brushMeshIndex + 1;
                var brushMesh = brushMeshes[brushMeshIndex];
                if (brushMesh == null)
                    brushMeshBlobs[brushMeshIndex] = BlobAssetReference<BrushMeshBlob>.Null;
                else
                    brushMeshBlobs[brushMeshIndex] = BrushMeshBlob.Build(brushMesh);
            }
        }
}*/
}
