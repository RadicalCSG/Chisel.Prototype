using System;
using System.Linq;
using UnityEngine;

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


	[Serializable]
	public struct ChiselPathPoint
	{
		public static readonly Vector3 kDefaultDirection = Vector3.up;
		public static readonly Quaternion kDefaultRotation = Quaternion.LookRotation(kDefaultDirection);

		static readonly Vector4 unitX = new(1, 0, 0, 0);
		static readonly Vector4 unitY = new(0, 1, 0, 0);
		static readonly Vector4 unitZ = new(0, 0, 1, 0);
		static readonly Vector4 unitW = new(0, 0, 0, 1);
		static readonly Matrix4x4 swizzleYZ;

		static ChiselPathPoint()
		{
			swizzleYZ.SetColumn(0, unitX);
			swizzleYZ.SetColumn(1, unitZ);
			swizzleYZ.SetColumn(2, unitY);
			swizzleYZ.SetColumn(3, unitW);
		}

		public ChiselPathPoint(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}

		public ChiselPathPoint(Vector3 position)
		{
			this.position = position;
			this.rotation = ChiselPathPoint.kDefaultRotation;
			this.scale = Vector3.one;
		}

		[PositionValue] public Vector3 position;
		[EulerValue] public Quaternion rotation;
		[ScaleValue] public Vector2 scale;

		public readonly Matrix4x4 ToMatrix()
		{
			return ToMatrix(position, rotation, scale);
		}

		static Matrix4x4 ToMatrix(Vector3 position, Quaternion rotation, Vector2 scale)
		{
			return Matrix4x4.TRS(position, Quaternion.Inverse(rotation), new Vector3(scale.x, scale.y, -1));
		}

		public static Matrix4x4 Lerp(ref ChiselPathPoint A, ref ChiselPathPoint B, float t)
		{
			var position = MathExtensions.Lerp(A.position, B.position, t);
			var rotation = MathExtensions.Lerp(A.rotation, B.rotation, t);
			var scale = MathExtensions.Lerp(A.scale, B.scale, t);
			return ToMatrix(position, rotation, scale);
		}
	}
}
