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
        const int kLatestVersion = 1;
        [UnityEngine.SerializeField] int version = 0;


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
            new ChiselPathPoint(Vector3.zero),
            new ChiselPathPoint(ChiselPathPoint.kDefaultDirection)
        });

        public void UpgradeIfNecessary()
        {
            if (version == kLatestVersion)
                return;

            version = kLatestVersion;
            if (this.segments == null ||
                this.segments.Length == 0)
                return;

            for (int i = 0; i < this.segments.Length; i++)
                this.segments[i].rotation = ChiselPathPoint.kDefaultRotation;
        }
    }
}
