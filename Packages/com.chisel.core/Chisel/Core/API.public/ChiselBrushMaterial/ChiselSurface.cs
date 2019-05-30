using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Chisel.Core
{
    [Serializable]
    public sealed class ChiselSurface
    {
        public ChiselBrushMaterial brushMaterial;
        public SurfaceDescription  surfaceDescription;
    }
}
