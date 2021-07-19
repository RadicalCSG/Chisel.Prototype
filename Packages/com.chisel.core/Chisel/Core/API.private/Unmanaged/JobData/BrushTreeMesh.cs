using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace Chisel.Core
{
    struct BrushTreeSpaceVerticesBlob
    {
        public ChiselBlobArray<float3> treeSpaceVertices;
    }

    struct BrushTreeSpacePlanes
    {
        public ChiselBlobArray<float4> treeSpacePlanes;
    }
}
