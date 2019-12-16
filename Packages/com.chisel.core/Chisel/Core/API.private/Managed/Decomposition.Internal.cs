using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;

namespace Chisel.Core
{
    partial class Decomposition
    {
#if USE_MANAGED_CSG_IMPLEMENTATION
        static Vector2[][] ConvexPartitionInternal(Vector2[] inputVertices2D)
        {
            // TODO: convert convex partitioning code from native code to managed code
            throw new NotImplementedException();
        }
#endif
    }
}
