using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

namespace Chisel.Components
{
    // TODO: turn into an asset so we can share it between multiple generators??
    [Serializable]
    public class Path
    {
        public Path() { }
        public Path(Path other)
        {
            this.segments = other.segments.ToArray();
        }
        public Path(PathPoint[] points)
        {
            this.segments = points.ToArray();
        }

        public PathPoint[]  segments;

        public static readonly Path Default = new Path( new[]
        {
            new PathPoint() { position = new Vector3(0,0,0), rotation = Quaternion.identity, scale = Vector2.one },
            new PathPoint() { position = new Vector3(0,1,0), rotation = Quaternion.identity, scale = Vector2.one }
        });
    }
}
