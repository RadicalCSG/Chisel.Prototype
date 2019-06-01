using UnityEngine;
using System.Collections;
using System;
using Chisel.Core;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Chisel.Components
{
    [Serializable]
    public sealed class SurfaceIntersection
    {
        public SurfaceReference surface;
        public CSGSurfaceIntersection intersection;
    }
}
