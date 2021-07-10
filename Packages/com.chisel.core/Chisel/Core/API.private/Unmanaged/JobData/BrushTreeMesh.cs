using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    struct BrushTreeSpaceVerticesBlob
    {
        public BlobArray<float3> treeSpaceVertices;
    }

    struct BrushTreeSpacePlanes
    {
        public BlobArray<float4> treeSpacePlanes;
    }
}
