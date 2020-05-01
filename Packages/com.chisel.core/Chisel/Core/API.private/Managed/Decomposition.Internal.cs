using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;
using Debug = UnityEngine.Debug;
using System.Linq;
using Unity.Mathematics;

namespace Chisel.Core
{
    partial class Decomposition
    {
        static Vector2[][] ConvexPartitionInternal(Vector2[] inputVertices2D)
        {
            var output = BayazitDecomposer.ConvexPartition(inputVertices2D.ToList());
            //for (int i = 0; i < output.Length; i++) output[i].Reverse();
            return output;
        }
    }
}
