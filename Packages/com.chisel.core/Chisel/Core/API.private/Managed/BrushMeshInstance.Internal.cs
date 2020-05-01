using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    partial struct BrushMeshInstance
    {
        private static Int32 CreateBrushMesh(Int32					userID,
                                             float3[]				vertices,
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
                                            float3[]             vertices,
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
    }
}