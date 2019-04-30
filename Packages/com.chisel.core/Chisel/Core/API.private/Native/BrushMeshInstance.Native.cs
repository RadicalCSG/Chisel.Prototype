using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    partial struct BrushMeshInstance
    {
#if !USE_MANAGED_CSG_IMPLEMENTATION
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        static extern Int32 CreateBrushMesh(Int32 userID,
                                            Int32 vertexCount,	 [In] IntPtr vertices,												    
                                            Int32 halfEdgeCount, [In] IntPtr halfEdges,
                                            Int32 polygonCount,	 [In] IntPtr polygons);

        private static Int32 CreateBrushMesh(Int32 userID,   
                                             Int32 vertexCount,   Vector3[]            vertices,
                                             Int32 halfEdgeCount, BrushMesh.HalfEdge[] halfEdges,
                                             Int32 polygonCount,  BrushMesh.Polygon[]  polygons)
        {
            var verticesHandle	= GCHandle.Alloc(vertices,  GCHandleType.Pinned);
            var halfEdgesHandle	= GCHandle.Alloc(halfEdges, GCHandleType.Pinned);
            var polygonsHandle	= GCHandle.Alloc(polygons,  GCHandleType.Pinned);
            var brushMeshID = CreateBrushMesh(userID,
                                              vertexCount,   verticesHandle .AddrOfPinnedObject(),
                                              halfEdgeCount, halfEdgesHandle.AddrOfPinnedObject(),
                                              polygonCount,	 polygonsHandle .AddrOfPinnedObject());			
            polygonsHandle.Free();
            halfEdgesHandle.Free();
            verticesHandle.Free();
            return brushMeshID;
        }


        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern Int32 GetBrushMeshUserID(Int32 brushMeshIndex);


        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool UpdateBrushMesh(Int32 brushMeshIndex,
                                                   Int32 vertexCount,    [In] IntPtr vertices,
                                                   Int32 halfEdgeCount,  [In] IntPtr halfEdges,
                                                   Int32 polygonCount,   [In] IntPtr polygons);
        private static bool UpdateBrushMesh(Int32 brushMeshIndex,
                                            Int32 vertexCount,    Vector3[]            vertices,
                                            Int32 halfEdgeCount,  BrushMesh.HalfEdge[] halfEdges,
                                            Int32 polygonCount,   BrushMesh.Polygon[]  polygons)
        {
            if (vertices == null || halfEdges == null || polygons == null) return false;
            if (vertexCount < 0 || halfEdgeCount < 0 || polygonCount < 0) return false;

            var verticesHandle	= GCHandle.Alloc(vertices, GCHandleType.Pinned);
            var halfEdgesHandle	= GCHandle.Alloc(halfEdges, GCHandleType.Pinned);
            var polygonsHandle	= GCHandle.Alloc(polygons, GCHandleType.Pinned);
            var result = UpdateBrushMesh(brushMeshIndex,
                                         vertexCount,    verticesHandle.AddrOfPinnedObject(),
                                         halfEdgeCount,  halfEdgesHandle.AddrOfPinnedObject(),
                                         polygonCount,   polygonsHandle.AddrOfPinnedObject());			
            polygonsHandle.Free();
            halfEdgesHandle.Free();
            verticesHandle.Free();
            return result;
        }
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool DestroyBrushMesh(Int32	brushMeshIndex);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool IsBrushMeshIDValid(Int32	brushMeshIndex);
#endif
    }
}