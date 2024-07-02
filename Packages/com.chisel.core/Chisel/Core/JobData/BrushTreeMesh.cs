using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;

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
