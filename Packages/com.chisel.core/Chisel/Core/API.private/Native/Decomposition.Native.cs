﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;

namespace Chisel.Core
{
    partial class Decomposition
    {
#if !USE_MANAGED_CSG_IMPLEMENTATION
        static Vector2[][] ConvexPartitionInternal(Vector2[] inputVertices2D)
        {
            var polygonCount = DecomposeStart(inputVertices2D);
            if (polygonCount == 0)
                return null;

            var polygonSizes = new Int32[polygonCount];
            if (!DecomposeGetSizes(polygonSizes))
                return null;

            var polygons = new List<Vector2[]>();
            for (int i = 0; i < polygonCount; i++)
            {
                var vertexCount = polygonSizes[i];
                var vertices	= DecomposeGetPolygon(i, vertexCount);
                if (vertices == null)
                    return null;
                polygons.Add(vertices);
            }
            return polygons.ToArray();
        }
        
        [DllImport(CSGManager.NativePluginName, CallingConvention=CallingConvention.Cdecl)]
        private static extern bool DecomposeStart(Int32			vertexCount,
                                                  [In] IntPtr	vertices,		
                                                  out Int32		polygonCount);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention=CallingConvention.Cdecl)]
        private static extern bool DecomposeGetSizes(Int32			polygonCount,
                                                     [Out] IntPtr	polygonSizes);

        [DllImport(CSGManager.NativePluginName, CallingConvention=CallingConvention.Cdecl)]
        private static extern bool DecomposeGetPolygon(Int32		polygonIndex,
                                                       Int32		vertexSize,
                                                       [Out] IntPtr	vertices);
        
        private static int DecomposeStart(Vector2[] points)
        {
            Int32		polygonCount = 0;
            GCHandle	pointsHandle = GCHandle.Alloc(points, GCHandleType.Pinned);
            IntPtr		pointsPtr = pointsHandle.AddrOfPinnedObject();
            var result = DecomposeStart(points.Length, pointsPtr, out polygonCount);
            pointsHandle.Free();
            if (!result)
                return 0;
            return polygonCount;
        }
        
        private static bool DecomposeGetSizes(Int32[] polygonSizes)
        {
            GCHandle	polygonSizesHandle	= GCHandle.Alloc(polygonSizes, GCHandleType.Pinned);
            IntPtr		polygonSizesPtr		= polygonSizesHandle.AddrOfPinnedObject();
            var result = DecomposeGetSizes(polygonSizes.Length, polygonSizesPtr);
            polygonSizesHandle.Free();
            return result;
        }

        private static Vector2[] DecomposeGetPolygon(Int32		polygonIndex,
                                                     Int32		vertexCount)
        {
            var vertices	= new Vector2[vertexCount];
            GCHandle	verticesHandle	= GCHandle.Alloc(vertices, GCHandleType.Pinned);
            IntPtr		verticesPtr		= verticesHandle.AddrOfPinnedObject();
            var result = DecomposeGetPolygon(polygonIndex, vertexCount, verticesPtr);
            verticesHandle.Free();
            if (!result)
                return null;
            return vertices;
        }
#endif
    }
}
