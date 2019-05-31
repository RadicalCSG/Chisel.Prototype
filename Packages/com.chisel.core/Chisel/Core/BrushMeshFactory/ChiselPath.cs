using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

namespace Chisel.Core
{
    // TODO: turn into an asset so we can share it between multiple generators??
    [Serializable]
    public class ChiselPath
    {
        public ChiselPath() { }
        public ChiselPath(ChiselPath other)
        {
            this.segments = other.segments.ToArray();
        }
        public ChiselPath(ChiselPathPoint[] points)
        {
            this.segments = points.ToArray();
        }

        public ChiselPathPoint[]  segments;

        public static readonly ChiselPath Default = new ChiselPath( new[]
        {
            new ChiselPathPoint() { position = new Vector3(0,0,0), rotation = Quaternion.identity, scale = Vector2.one },
            new ChiselPathPoint() { position = new Vector3(0,1,0), rotation = Quaternion.identity, scale = Vector2.one }
        });
    }
}
