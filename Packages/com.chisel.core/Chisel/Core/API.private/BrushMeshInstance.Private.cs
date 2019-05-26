using System;

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

#if USE_MANAGED_CSG_IMPLEMENTATION
            var result = CreateBrushMesh(userID,
                                         brushMesh.vertices,
                                         brushMesh.halfEdges,
                                         brushMesh.polygons);
#else
            var result = CreateBrushMesh(userID,
                                         vertexCount,  brushMesh.vertices,
                                         edgeCount,    brushMesh.halfEdges,
                                         polygonCount, brushMesh.polygons);
#endif
            if (result <= 0)
                result = 0;
            return result;
        }
        
        private static bool UpdateBrushMesh(Int32		brushMeshIndex,
                                            BrushMesh	brushMesh)
        {
            if (brushMeshIndex == 0 ||
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

            var vertexCount = brushMesh.vertices.Length;
#if USE_MANAGED_CSG_IMPLEMENTATION
            var result = UpdateBrushMesh(brushMeshIndex,
                                         brushMesh.vertices,
                                         brushMesh.halfEdges,
                                         brushMesh.polygons);
#else
            var result = UpdateBrushMesh(brushMeshIndex,
                                         vertexCount,  brushMesh.vertices,
                                         edgeCount,    brushMesh.halfEdges,
                                         polygonCount, brushMesh.polygons);
#endif
            return result;
        }
    }
}