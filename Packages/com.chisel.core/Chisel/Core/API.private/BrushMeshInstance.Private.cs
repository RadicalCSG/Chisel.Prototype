using System;
using System.Runtime.InteropServices;
using UnityEngine.Profiling;

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
                Profiler.BeginSample("CSGManager.NotifyBrushMeshModified");
                CSGManager.NotifyBrushMeshModified(brushMeshID);
                Profiler.EndSample();
            }
            return result;
        }
    }
}