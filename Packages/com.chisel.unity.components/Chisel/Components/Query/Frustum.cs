using System;
using UnityEngine;

namespace Chisel.Components
{
    [Serializable]
    public sealed class Frustum
    {
        public readonly Plane[] Planes = new Plane[6];
    }
}
