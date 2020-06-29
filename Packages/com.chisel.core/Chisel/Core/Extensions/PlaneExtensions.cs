using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static class PlaneExtensions
    {
        public static readonly Vector3 NanVector = new Vector3(float.NaN, float.NaN, float.NaN);
        public static readonly float3 NanFloat3 = new float3(float.NaN, float.NaN, float.NaN);

        public static double3 Intersection(float4 inPlane1, float4 inPlane2, float4 inPlane3)
        {
#if false
            var N0 = inPlane2.wzyx * inPlane3.yxwz - inPlane2.yxwz * inPlane3.wzyx;
            var Nx = inPlane2.yyww * inPlane3.xzzx - inPlane2.xzzx * inPlane3.yyww;

            var tx = inPlane1 * N0;
            var ty = inPlane1.wxyz * Nx;
            var tz = inPlane1.yzwx * -Nx.wxyz;

            var E = tx + ty + tz;
            return E.zwx / E.y;
#else
            var N0 = inPlane2.wzyx * inPlane3.yxwz - inPlane2.yxwz * inPlane3.wzyx;
            var Nx = inPlane2.yyww * inPlane3.xzzx - inPlane2.xzzx * inPlane3.yyww;

            var tx = inPlane1 * N0;
            var ty = inPlane1.wxyz * Nx;
            var tz = inPlane1.yzwx * -Nx.wxyz;

            var E = tx + ty + tz;
            if (math.isnan(E.y) || E.y > -CSGConstants.kDivideMinimumEpsilon && E.y < CSGConstants.kDivideMinimumEpsilon)
                return NanFloat3;
            return (E.zwx / E.y);
#endif
        }

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
            var xf = (float)(-(c2 * b1 * d3 - c2 * b3 * d1 + b3 * c1 * d2 + c3 * b2 * d1 - b1 * c3 * d2 - c1 * b2 * d3) /
                              (-c2 * b3 * a1 + c3 * b2 * a1 - b1 * c3 * a2 - c1 * b2 * a3 + b3 * c1 * a2 + c2 * b1 * a3));
            var yf = (float)((c3 * a2 * d1 - c3 * a1 * d2 - c2 * a3 * d1 + d2 * c1 * a3 - a2 * c1 * d3 + c2 * d3 * a1) /
                              (-c2 * b3 * a1 + c3 * b2 * a1 - b1 * c3 * a2 - c1 * b2 * a3 + b3 * c1 * a2 + c2 * b1 * a3));
            var zf = (float)(-(-a2 * b1 * d3 + a2 * b3 * d1 - a3 * b2 * d1 + d3 * b2 * a1 - d2 * b3 * a1 + d2 * b1 * a3) /
                              (-c2 * b3 * a1 + c3 * b2 * a1 - b1 * c3 * a2 - c1 * b2 * a3 + b3 * c1 * a2 + c2 * b1 * a3));

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


        public static void PlanePlaneIntersection(out double3 linePoint, out double3 lineVec,
                                                  double4 plane1, double4 plane2)
        {
            var plane1Normal = plane1.xyz; var plane1Position = plane1Normal * plane1.w;
            var plane2Normal = plane2.xyz; var plane2Position = plane2Normal * plane2.w;

            lineVec = math.cross(plane1Normal, plane2Normal);
            var ldir = math.cross(plane2Normal, lineVec);

            var denominator = math.dot(plane1Normal, ldir);

            var plane1ToPlane2 = plane1Position - plane2Position;
            var t = math.dot(plane1Normal, plane1ToPlane2) / denominator;
            linePoint = plane2Position + t * ldir;
        }

        // TODO: use these two methods to first calculate all valid pairs

        public static void IntersectionFirst(double4 inPlane2, double4 inPlane3, out double4 outN0, out double4 outN1)
        {
#if false
            outN0 = inPlane2.wzyx * inPlane3.yxwz - inPlane2.yxwz * inPlane3.wzyx;
            outN1 = inPlane2.yyww * inPlane3.xzzx - inPlane2.xzzx * inPlane3.yyww;
#else
            outN0 = inPlane2.wzyx * inPlane3.yxwz - inPlane2.yxwz * inPlane3.wzyx;
            outN1 = inPlane2.ywwy * inPlane3.zzxx - inPlane2.zzxx * inPlane3.ywwy;
#endif
        }

        public static float3 IntersectionSecond(double4 inPlane1, double4 N0, double4 N1)
        {
#if false
            var tx = inPlane1 * N0;
            var ty = inPlane1.wxyz * N1;
            var tz = inPlane1.yzwx * -N1.wxyz;

            var E = tx + ty + tz;
            return (float3)(E.zwx / E.y); 
#else
            /*
            var m = new float4x4(0, N1.y, N0.z, -N1.x,
                                 -N1.y, 0, N1.z, N0.w,
                                  N0.x, -N1.z, 0, N1.w,
                                  N1.x, N0.y, -N1.w, 0);*/

            double4 E;
            E.x =                       N1.y * inPlane1.y +  N0.z * inPlane1.z + -N1.x * inPlane1.w;
            E.y = -N1.y * inPlane1.x +                       N1.z * inPlane1.z +  N0.w * inPlane1.w;
            E.z =  N0.x * inPlane1.x + -N1.z * inPlane1.y +                       N1.w * inPlane1.w;
            E.w =  N1.x * inPlane1.x +  N0.y * inPlane1.y + -N1.w * inPlane1.z                     ;

/*
            var N2 = -N1.yzwx;

            // TODO: it should be possible to put all these steps in a single 4x4 matrix
            //       figure out what this matrix would look like and then track back
            //       to input to simplify
            var tx = inPlane1 * N0;
            var ty = inPlane1 * N1;
            var tz = inPlane1 * N2;

            var E = (tx.zwxy + ty.yzwx + tz.wxyz);*/
            return (float3)(E.xyz / E.w);
#endif
        }
    }

}
