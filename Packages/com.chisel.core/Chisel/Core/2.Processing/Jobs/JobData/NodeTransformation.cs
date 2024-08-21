using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct NodeTransformations
    {
        public float4x4 nodeToTree;
        public float4x4 treeToNode;
    };
}
