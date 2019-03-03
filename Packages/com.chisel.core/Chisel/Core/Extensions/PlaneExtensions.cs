using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Chisel.Core
{
	public static class PlaneExtensions
	{	
		public static readonly Vector3 NanVector = new Vector3(float.NaN, float.NaN, float.NaN);
		public static Vector3 Intersection(Plane inPlane1,
										   Plane inPlane2,
										   Plane inPlane3)
		{
			//const double kEpsilon = 0.0006f;
			//  {
			//      x = -( c2*b1*d3-c2*b3*d1+b3*c1*d2+c3*b2*d1-b1*c3*d2-c1*b2*d3)/
			//           (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3), 
			//      y =  ( c3*a2*d1-c3*a1*d2-c2*a3*d1+d2*c1*a3-a2*c1*d3+c2*d3*a1)/
			//           (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3), 
			//      z = -(-a2*b1*d3+a2*b3*d1-a3*b2*d1+d3*b2*a1-d2*b3*a1+d2*b1*a3)/
			//           (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3)
			//  }

			var a1 = (double)(inPlane1.normal.x);
			var b1 = (double)(inPlane1.normal.y);
			var c1 = (double)(inPlane1.normal.z);

			var a2 = (double)(inPlane2.normal.x);
			var b2 = (double)(inPlane2.normal.y);
			var c2 = (double)(inPlane2.normal.z);

			var a3 = (double)(inPlane3.normal.x);
			var b3 = (double)(inPlane3.normal.y);
			var c3 = (double)(inPlane3.normal.z);
			/*
			var bc1 = (b1 * c3) - (b3 * c1);
			var bc2 = (b2 * c1) - (b1 * c2);
			var bc3 = (b3 * c2) - (b2 * c3);

			var w = -((a1 * bc3) + (a2 * bc1) + (a3 * bc2));

			// better to have detectable invalid values than to have reaaaaaaally big values
			if (w > -kEpsilon && w < kEpsilon)
				return NanVector;
			*/
			var d1 = (double)(inPlane1.distance);
			var d2 = (double)(inPlane2.distance);
			var d3 = (double)(inPlane3.distance);
			/*
			var ad1 = (a1 * d3) - (a3 * d1);
			var ad2 = (a2 * d1) - (a1 * d2);
			var ad3 = (a3 * d2) - (a2 * d3);

			var x = -((d1 * bc3) + (d2 * bc1) + (d3 * bc2));
			var y = -((c1 * ad3) + (c2 * ad1) + (c3 * ad2));
			var z = +((b1 * ad3) + (b2 * ad1) + (b3 * ad2));
			*/
			var xf = (float)(-( c2*b1*d3-c2*b3*d1+b3*c1*d2+c3*b2*d1-b1*c3*d2-c1*b2*d3) /
							  (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3)); 
			var yf = (float)( ( c3*a2*d1-c3*a1*d2-c2*a3*d1+d2*c1*a3-a2*c1*d3+c2*d3*a1) /
							  (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3)); 
			var zf = (float)(-(-a2*b1*d3+a2*b3*d1-a3*b2*d1+d3*b2*a1-d2*b3*a1+d2*b1*a3) /
							  (-c2*b3*a1+c3*b2*a1-b1*c3*a2-c1*b2*a3+b3*c1*a2+c2*b1*a3));
        
			//var xf = (float)(x / w);
			if (float.IsInfinity(xf) || float.IsNaN(xf))
				return NanVector;

			//var yf = (float)(y / w);
			if (float.IsInfinity(yf) || float.IsNaN(yf))
				return NanVector;

			//var zf = (float)(z / w);
			if (float.IsInfinity(zf) || float.IsNaN(zf))
				return NanVector;
        
			return new Vector3(xf, yf, zf);
		}
	}

}
