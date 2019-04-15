using System;
using UnityEngine;

namespace UnitySceneExtensions
{
    [Serializable]
    public sealed class Frustum
    {
        public readonly Plane[] Planes = new Plane[6];
        

        public bool IsInside(Vector3 point, float distanceEpsilon = 0.0001f)
        {
            point = SceneHandles.matrix.MultiplyPoint(point);
            for (var i = 0; i < 6; i++)
                if (Planes[i].GetDistanceToPoint(point) > distanceEpsilon)
                    return false;
            return true;
        }
    }
}
