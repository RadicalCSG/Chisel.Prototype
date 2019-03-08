using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnitySceneExtensions
{
	public static class PlaneExtensions
	{
#if !UNITY_2017_1_OR_NEWER
		public static Plane Translate(this Plane plane, Vector3 translation)
		{
			var normal	= plane.normal;
			var a			= normal.x;
			var b			= normal.y;
			var c			= normal.z;
			var d			= -plane.distance;
			return new Plane(normal,
				// translated offset = Normal.Dotproduct(translation)
				// normal = A,B,C
								-(d + (a * translation.x) +
									  (b * translation.y) +
									  (c * translation.z)));
		}
#endif
		public static bool UnsignedRaycast(this Plane plane, Ray ray, out float enter)
		{
			float vdot = Vector3.Dot(ray.direction, plane.normal);
			float ndot = -Vector3.Dot(ray.origin, plane.normal) - plane.distance;

			if (Mathf.Approximately(vdot, 0.0f))
			{
				enter = 0.0F;
				return false;
			}

			enter = ndot / vdot;
			return true;
		}
	}
}
