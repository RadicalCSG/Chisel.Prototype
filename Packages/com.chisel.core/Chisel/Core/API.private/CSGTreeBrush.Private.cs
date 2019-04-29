using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct CSGTreeBrush
    {
        private static bool SetBrushMesh(Int32 brushNodeID, BrushMeshInstance brushMesh)
        {
            return SetBrushMeshID(brushNodeID, brushMesh.brushMeshID);
        }

        private static BrushMeshInstance GetBrushMesh(Int32 brushNodeID)
        {
            return new BrushMeshInstance { brushMeshID = GetBrushMeshID(brushNodeID) };
        }


        internal struct AABB
        {
            public AABB(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
            {
                MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY; MinZ = minZ; MaxZ = maxZ;
            }
            public float MinX,MaxX,MinY,MaxY,MinZ,MaxZ;

            public Vector3 Center	{ get { return new Vector3((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f, (MinZ + MaxZ) * 0.5f); } }
            public Vector3 Size		{ get { return new Vector3(MaxX - MinX, MaxY - MinY, MaxZ - MinZ); } }
        }

        private static Bounds GetBrushBounds(Int32 brushNodeID)
        {
#if USE_MANAGED_CSG_IMPLEMENTATION
            var	bounds = new Bounds();
            if (GetBrushBounds(brushNodeID, ref bounds))
                return bounds;
#else
            var	aabb = new AABB();
            if (GetBrushBounds(brushNodeID, ref aabb))
                return new Bounds(aabb.Center, aabb.Size);
#endif
            return new Bounds();
        }
    }
}