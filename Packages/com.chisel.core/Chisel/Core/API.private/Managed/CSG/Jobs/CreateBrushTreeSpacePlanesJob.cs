using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    public struct CreateBrushTreeSpacePlanesJob : IJobParallelFor   
    {
        [NoAlias,ReadOnly] public NativeArray<IndexOrder>                                       treeBrushIndexOrders;
        [NoAlias,ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>         brushMeshLookup;
        [NoAlias,ReadOnly] public NativeHashMap<int, BlobAssetReference<NodeTransformations>>   transformations;

        [NoAlias,WriteOnly] public NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>>.ParallelWriter brushTreeSpacePlanes;

        public void Execute(int index)
        {
            var brushIndexOrder = treeBrushIndexOrders[index];
            int brushNodeIndex  = brushIndexOrder.nodeIndex;
            var worldPlanes     = BrushTreeSpacePlanes.Build(brushMeshLookup[brushNodeIndex], 
                                                         transformations[brushNodeIndex].Value.nodeToTree);
            brushTreeSpacePlanes.TryAdd(brushNodeIndex, worldPlanes);
        }
    }
}
