using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushTreeSpacePlanes
    {
        public BlobArray<float4> treeSpacePlanes;
    }
}
