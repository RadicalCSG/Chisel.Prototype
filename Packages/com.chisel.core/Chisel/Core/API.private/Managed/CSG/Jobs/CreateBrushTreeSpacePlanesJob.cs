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
        // Read
        [NoAlias,ReadOnly] public NativeArray<IndexOrder>                           treeBrushIndexOrders;
        [NoAlias,ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>    brushMeshLookup;
        [NoAlias,ReadOnly] public NativeArray<NodeTransformations>                  transformations;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias,WriteOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes;

        public void Execute(int index)
        {
            var brushIndexOrder = treeBrushIndexOrders[index];
            int brushNodeOrder  = brushIndexOrder.nodeOrder;
            var worldPlanes     = BrushTreeSpacePlanes.Build(brushMeshLookup[brushNodeOrder], transformations[brushNodeOrder].nodeToTree);
            brushTreeSpacePlanes[brushNodeOrder] = worldPlanes;
        }
    }
}
