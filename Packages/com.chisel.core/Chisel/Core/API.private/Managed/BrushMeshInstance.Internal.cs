using System;
using UnityEngine;

namespace Chisel.Core
{
    partial struct BrushMeshInstance
    {
#if USE_INTERNAL_IMPLEMENTATION
        private static Int32 CreateBrushMesh(Int32					userID,   
                                             Vector3[]				vertices,
                                             BrushMesh.HalfEdge[]	halfEdges,
                                             BrushMesh.Polygon[]	polygons)
        {
            return BrushMeshManager.CreateBrushMesh(userID, vertices, halfEdges, polygons);
        }

        private static Int32 GetBrushMeshUserID(Int32 brushMeshIndex)
        {
            return BrushMeshManager.GetBrushMeshUserID(brushMeshIndex);
        }

        private static bool UpdateBrushMesh(Int32				 brushMeshID,
                                            Vector3[]            vertices,
                                            BrushMesh.HalfEdge[] halfEdges,
                                            BrushMesh.Polygon[]  polygons)
        {
            return BrushMeshManager.UpdateBrushMesh(brushMeshID, vertices, halfEdges, polygons);
        }
        
        private static bool DestroyBrushMesh(Int32 brushMeshID)
        {
            return BrushMeshManager.DestroyBrushMesh(brushMeshID);
        }

        private static bool IsBrushMeshIDValid(Int32 brushMeshID)
        {
            return BrushMeshManager.IsBrushMeshIDValid(brushMeshID);
        }
#endif
    }
}