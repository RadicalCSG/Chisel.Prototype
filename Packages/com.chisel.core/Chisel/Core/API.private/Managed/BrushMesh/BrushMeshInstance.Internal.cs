using System;
using System.Collections.Generic;
using UnityEngine.Profiling;
using Unity.Mathematics;

namespace Chisel.Core
{
    partial struct BrushMeshInstance
    {
        private static Int32 CreateBrushMesh(int userID, BrushMesh brushMesh)
        {
            if (brushMesh			== null ||
                brushMesh.vertices	== null ||
                brushMesh.halfEdges	== null ||
                brushMesh.polygons	== null)
                return 0;

            var edgeCount = brushMesh.halfEdges.Length;
            if (edgeCount < 12)
                return 0;
            
            var polygonCount = brushMesh.polygons.Length;
            if (polygonCount < 4)
                return 0;
            
            var vertexCount = brushMesh.vertices.Length;
            if (vertexCount < 4)
                return 0;

            var result = CreateBrushMesh(userID,
                                         brushMesh.vertices,
                                         brushMesh.halfEdges,
                                         brushMesh.polygons);
            if (result <= 0)
                result = 0;
            return result;
        }

        private static HashSet<int> s_ModifiedBrushMeshes = new HashSet<int>();

        private static bool UpdateBrushMesh(Int32		brushMeshID,
                                            BrushMesh	brushMesh, 
                                            bool        notifyBrushMeshModified = true)
        {
            if (brushMeshID == 0 ||
                brushMesh.vertices	== null ||
                brushMesh.halfEdges == null ||
                brushMesh.polygons	== null)
                return false;

            var edgeCount = brushMesh.halfEdges.Length;
            if (edgeCount < 12)
                return false;

            var polygonCount = brushMesh.polygons.Length;
            if (polygonCount < 4)
                return false;

            var result = UpdateBrushMesh(brushMeshID,
                                         brushMesh.vertices,
                                         brushMesh.halfEdges,
                                         brushMesh.polygons);
            if (notifyBrushMeshModified)
            {
                s_ModifiedBrushMeshes.Clear();
                s_ModifiedBrushMeshes.Add(brushMeshID);
                Profiler.BeginSample("CSGManager.NotifyBrushMeshModified");
                Chisel.Core.CompactHierarchyManager.NotifyBrushMeshModified(s_ModifiedBrushMeshes);
                s_ModifiedBrushMeshes.Clear();
                Profiler.EndSample();
            }
            return result;
        }

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