using System;
using Chisel.Core;

namespace Chisel.Components
{
    [Serializable]
    public sealed class SurfaceIntersection
    {
        public SurfaceReference surface;
        public ChiselSurfaceIntersection intersection;
    }
}
