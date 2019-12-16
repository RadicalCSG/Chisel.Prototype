using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Chisel.Core
{
    sealed partial class ChiselWireframe
    {
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern UInt64 GetBrushOutlineGeneration(Int32 brushNodeID);
        
        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetBrushOutlineSizes(Int32		brushNodeID,
                                                        [Out] out Int32	vertexCount,
                                                        [Out] out Int32	visibleOuterLineCount,
                                                        [Out] out Int32	visibleInnerLineCount,
                                                        [Out] out Int32	invisibleOuterLineCount,
                                                        [Out] out Int32	invisibleInnerLineCount,
                                                        [Out] out Int32	invalidLineCount);

        [DllImport(CSGManager.NativePluginName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GetBrushOutlineValues(Int32 brushNodeID,
                                                         Int32 vertexCount,             [Out] IntPtr vertices,
                                                         Int32 visibleOuterLineCount,   [Out] IntPtr visibleOuterLines,
                                                         Int32 visibleInnerLineCount,   [Out] IntPtr visibleInnerLines,
                                                         Int32 invisibleOuterLineCount, [Out] IntPtr invisibleOuteLines,
                                                         Int32 invisibleInnerLineCount, [Out] IntPtr invisibleInnerLines,
                                                         Int32 invalidLineCount,        [Out] IntPtr invalidLines);

        [DllImport(CSGManager.NativePluginName, CallingConvention=CallingConvention.Cdecl)]
        private static extern bool GetSurfaceOutlineSizes(Int32			brushNodeID,
                                                          Int32			surfaceID,
                                                          [Out] out Int32 vertexCount,
                                                          [Out] out Int32 visibleOuterLineCount,
                                                          [Out] out Int32 visibleInnerLineCount,
                                                          [Out] out Int32 visibleTriangleCount,
                                                          [Out] out Int32 invisibleOuterLineCount,
                                                          [Out] out Int32 invisibleInnerLineCount,
                                                          [Out] out Int32 invalidLineCount);

        [DllImport(CSGManager.NativePluginName, CallingConvention=CallingConvention.Cdecl)]
        private static extern bool GetSurfaceOutlineValues(Int32 brushNodeID,
                                                           Int32 surfaceID,
                                                           Int32 vertexCount,			  [Out] IntPtr vertices,
                                                           Int32 visibleOuterLineCount,	  [Out] IntPtr visibleOuterLines,
                                                           Int32 visibleInnerLineCount,	  [Out] IntPtr visibleInnerLines,
                                                           Int32 visibleTriangleCount,	  [Out] IntPtr visibleTriangles,
                                                           Int32 invisibleOuterLineCount, [Out] IntPtr invisibleOuterLines,
                                                           Int32 invisibleInnerLineCount, [Out] IntPtr invisibleInnerLines,
                                                           Int32 invalidLineCount,		  [Out] IntPtr invalidLines);
        
        private static ChiselWireframe CreateSurfaceWireframe(Int32 brushNodeID, Int32 surfaceID)
        {
            int vertexCount = 0,             visibleOuterLineCount = 0;
            int visibleInnerLineCount = 0,   visibleTriangleCount = 0;
            int invisibleOuterLineCount = 0, invisibleInnerLineCount = 0;
            int invalidLineCount = 0;
            if (!GetSurfaceOutlineSizes(brushNodeID,
                                        surfaceID,
                                        out vertexCount,			 out visibleOuterLineCount,
                                        out visibleInnerLineCount,	 out visibleTriangleCount,
                                        out invisibleOuterLineCount, out invisibleInnerLineCount,
                                        out invalidLineCount))
                return null;
            
            if (vertexCount == 0 ||
                (visibleOuterLineCount == 0 && invisibleOuterLineCount == 0 && 
                 visibleInnerLineCount == 0 && invisibleInnerLineCount == 0 &&
                 visibleTriangleCount == 0 &&
                invalidLineCount == 0))
                return null;


            var wireframe = new ChiselWireframe
            {
                vertices			= new Vector3[vertexCount],
                visibleOuterLines	= new Int32[visibleOuterLineCount],
                visibleInnerLines	= new Int32[visibleInnerLineCount],
                visibleTriangles	= new Int32[visibleTriangleCount],
                invisibleOuterLines = new Int32[invisibleOuterLineCount],
                invisibleInnerLines = new Int32[invisibleInnerLineCount],
                invalidLines		= new Int32[invalidLineCount]
            };

            var verticesHandle				= GCHandle.Alloc(wireframe.vertices, GCHandleType.Pinned);
            var visibleOuterLinesHandle		= GCHandle.Alloc(wireframe.visibleOuterLines, GCHandleType.Pinned);
            var visibleInnerLinesHandle		= GCHandle.Alloc(wireframe.visibleInnerLines, GCHandleType.Pinned);
            var visibleTrianglesHandle		= GCHandle.Alloc(wireframe.visibleTriangles, GCHandleType.Pinned);
            var invisibleOuterLinesHandle	= GCHandle.Alloc(wireframe.invisibleOuterLines, GCHandleType.Pinned);
            var invisibleInnerLinesHandle	= GCHandle.Alloc(wireframe.invisibleInnerLines, GCHandleType.Pinned);
            var invalidLinesHandle			= GCHandle.Alloc(wireframe.invalidLines, GCHandleType.Pinned);

            var verticesPtr				= verticesHandle.AddrOfPinnedObject();
            var visibleOuterLinesPtr	= visibleOuterLinesHandle.AddrOfPinnedObject();
            var visibleInnerLinesPtr	= visibleInnerLinesHandle.AddrOfPinnedObject();
            var visibleTrianglesPtr		= visibleTrianglesHandle.AddrOfPinnedObject();
            var invisibleOuterLinesPtr	= invisibleOuterLinesHandle.AddrOfPinnedObject();
            var invisibleInnerLinesPtr	= invisibleInnerLinesHandle.AddrOfPinnedObject();
            var invalidLinesPtr			= invalidLinesHandle.AddrOfPinnedObject();

            bool success = GetSurfaceOutlineValues(brushNodeID, surfaceID,
                                                   vertexCount,			    verticesPtr,
                                                   visibleOuterLineCount,   visibleOuterLinesPtr,
                                                   visibleInnerLineCount,   visibleInnerLinesPtr,
                                                   visibleTriangleCount,	visibleTrianglesPtr,
                                                   invisibleOuterLineCount, invisibleOuterLinesPtr,
                                                   invisibleInnerLineCount, invisibleInnerLinesPtr,
                                                   invalidLineCount,		invalidLinesPtr);

            verticesHandle.Free();
            visibleOuterLinesHandle.Free();
            visibleInnerLinesHandle.Free();
            visibleTrianglesHandle.Free();
            invisibleOuterLinesHandle.Free();
            invisibleInnerLinesHandle.Free();
            invalidLinesHandle.Free();

            if (!success)
                return null;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(brushNodeID);
            return wireframe;
        }
        
        private static bool UpdateSurfaceWireframe(ChiselWireframe wireframe)
        {
            int vertexCount = 0,             visibleOuterLineCount = 0;
            int visibleInnerLineCount = 0,   visibleTriangleCount = 0;
            int invisibleOuterLineCount = 0, invisibleInnerLineCount = 0;
            int invalidLineCount = 0;
            if (!GetSurfaceOutlineSizes(wireframe.originBrushID, wireframe.originSurfaceID,
                                        out vertexCount,			 out visibleOuterLineCount,
                                        out visibleInnerLineCount,	 out visibleTriangleCount,
                                        out invisibleOuterLineCount, out invisibleInnerLineCount,
                                        out invalidLineCount))
                return false;
            
            if (vertexCount == 0 ||
                (visibleOuterLineCount == 0 && invisibleOuterLineCount == 0 && 
                 visibleInnerLineCount == 0 && invisibleInnerLineCount == 0 &&
                 visibleTriangleCount == 0 &&
                invalidLineCount == 0))
                return false;
            
            if (wireframe.vertices.Length != vertexCount)
                wireframe.vertices = new Vector3[vertexCount];

            if (wireframe.visibleOuterLines.Length != visibleOuterLineCount)
                wireframe.visibleOuterLines = new Int32[visibleOuterLineCount];

            if (wireframe.visibleInnerLines.Length != visibleInnerLineCount)
                wireframe.visibleInnerLines = new Int32[visibleInnerLineCount];
            
            if (wireframe.visibleTriangles.Length != visibleTriangleCount)
                wireframe.visibleTriangles = new Int32[visibleTriangleCount];
            
            if (wireframe.invisibleOuterLines.Length != invisibleOuterLineCount)
                wireframe.invisibleOuterLines = new Int32[invisibleOuterLineCount];

            if (wireframe.invisibleInnerLines.Length != invisibleInnerLineCount)
                wireframe.invisibleInnerLines = new Int32[invisibleInnerLineCount];

            if (wireframe.invalidLines.Length != invalidLineCount)
                wireframe.invalidLines = new Int32[invalidLineCount];
            
            var verticesHandle				= GCHandle.Alloc(wireframe.vertices, GCHandleType.Pinned);
            var visibleOuterLinesHandle		= GCHandle.Alloc(wireframe.visibleOuterLines, GCHandleType.Pinned);
            var visibleInnerLinesHandle		= GCHandle.Alloc(wireframe.visibleInnerLines, GCHandleType.Pinned);
            var visibleTrianglesHandle		= GCHandle.Alloc(wireframe.visibleTriangles, GCHandleType.Pinned);
            var invisibleOuterLinesHandle	= GCHandle.Alloc(wireframe.invisibleOuterLines, GCHandleType.Pinned);
            var invisibleInnerLinesHandle	= GCHandle.Alloc(wireframe.invisibleInnerLines, GCHandleType.Pinned);
            var invalidLinesHandle			= GCHandle.Alloc(wireframe.invalidLines, GCHandleType.Pinned);

            var verticesPtr				= verticesHandle.AddrOfPinnedObject();
            var visibleOuterLinesPtr	= visibleOuterLinesHandle.AddrOfPinnedObject();
            var visibleInnerLinesPtr	= visibleInnerLinesHandle.AddrOfPinnedObject();
            var visibleTrianglesPtr		= visibleTrianglesHandle.AddrOfPinnedObject();
            var invisibleOuterLinesPtr	= invisibleOuterLinesHandle.AddrOfPinnedObject();
            var invisibleInnerLinesPtr	= invisibleInnerLinesHandle.AddrOfPinnedObject();
            var invalidLinesPtr			= invalidLinesHandle.AddrOfPinnedObject();

            bool success = GetSurfaceOutlineValues(wireframe.originBrushID, wireframe.originSurfaceID,
                                                   vertexCount,			    verticesPtr,
                                                   visibleOuterLineCount,   visibleOuterLinesPtr,
                                                   visibleInnerLineCount,   visibleInnerLinesPtr,
                                                   visibleTriangleCount,	visibleTrianglesPtr,
                                                   invisibleOuterLineCount, invisibleOuterLinesPtr,
                                                   invisibleInnerLineCount, invisibleInnerLinesPtr,
                                                   invalidLineCount,		invalidLinesPtr);

            verticesHandle.Free();
            visibleOuterLinesHandle.Free();
            visibleInnerLinesHandle.Free();
            visibleTrianglesHandle.Free();
            invisibleOuterLinesHandle.Free();
            invisibleInnerLinesHandle.Free();
            invalidLinesHandle.Free();

            if (!success)
                return false;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(wireframe.originBrushID);
            return true;
        }
        

        private static ChiselWireframe CreateBrushWireframe(Int32 brushNodeID)
        {
            int vertexCount = 0;
            int visibleOuterLineCount = 0;
            int visibleInnerLineCount = 0;
            int invisibleOuterLineCount = 0;
            int invisibleInnerLineCount = 0;
            int invalidLineCount = 0;
            if (!GetBrushOutlineSizes(brushNodeID,
                                      out vertexCount,             out visibleOuterLineCount,
                                      out visibleInnerLineCount,   out invisibleOuterLineCount,
                                      out invisibleInnerLineCount, out invalidLineCount))
                return null;
            
            if (vertexCount == 0 ||
                (visibleOuterLineCount == 0 && invisibleOuterLineCount == 0 && 
                 visibleInnerLineCount == 0 && invisibleInnerLineCount == 0 && 
                 invalidLineCount == 0))
            {
                return null;
            }

            var wireframe = new ChiselWireframe
            {
                vertices			= new Vector3[vertexCount],
                visibleOuterLines	= new Int32[visibleOuterLineCount],
                visibleInnerLines	= new Int32[visibleInnerLineCount],
                invisibleOuterLines = new Int32[invisibleOuterLineCount],
                invisibleInnerLines = new Int32[invisibleInnerLineCount],
                invalidLines		= new Int32[invalidLineCount],
                originBrushID		= brushNodeID
            };

            var verticesHandle				= GCHandle.Alloc(wireframe.vertices, GCHandleType.Pinned);
            var visibleOuterLinesHandle		= GCHandle.Alloc(wireframe.visibleOuterLines, GCHandleType.Pinned);
            var visibleInnerLinesHandle		= GCHandle.Alloc(wireframe.visibleInnerLines, GCHandleType.Pinned);
            var invisibleOuterLinesHandle	= GCHandle.Alloc(wireframe.invisibleOuterLines, GCHandleType.Pinned);
            var invisibleInnerLinesHandle	= GCHandle.Alloc(wireframe.invisibleInnerLines, GCHandleType.Pinned);
            var invalidLinesHandle			= GCHandle.Alloc(wireframe.invalidLines, GCHandleType.Pinned);

            var verticesPtr				= verticesHandle.AddrOfPinnedObject();
            var visibleOuterLinesPtr	= visibleOuterLinesHandle.AddrOfPinnedObject();
            var visibleInnerLinesPtr	= visibleInnerLinesHandle.AddrOfPinnedObject();
            var invisibleOuterLinesPtr	= invisibleOuterLinesHandle.AddrOfPinnedObject();
            var invisibleInnerLinesPtr	= invisibleInnerLinesHandle.AddrOfPinnedObject();
            var invalidLinesPtr			= invalidLinesHandle.AddrOfPinnedObject();
            
            bool success = GetBrushOutlineValues(brushNodeID,
                                                 vertexCount,             verticesPtr,
                                                 visibleOuterLineCount,   visibleOuterLinesPtr,
                                                 visibleInnerLineCount,   visibleInnerLinesPtr,
                                                 invisibleOuterLineCount, invisibleOuterLinesPtr,
                                                 invisibleInnerLineCount, invisibleInnerLinesPtr,
                                                 invalidLineCount,        invalidLinesPtr);

            verticesHandle.Free();
            visibleOuterLinesHandle.Free();
            visibleInnerLinesHandle.Free();
            invisibleOuterLinesHandle.Free();
            invisibleInnerLinesHandle.Free();
            invalidLinesHandle.Free();

            if (!success)
                return null;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(brushNodeID);
            return wireframe;
        }

        private static bool UpdateBrushWireframe(ChiselWireframe wireframe)
        {
            int vertexCount = 0;
            int visibleOuterLineCount = 0;
            int visibleInnerLineCount = 0;
            int invisibleOuterLineCount = 0;
            int invisibleInnerLineCount = 0;
            int invalidLineCount = 0;

            if (!GetBrushOutlineSizes(wireframe.originBrushID,
                                      out vertexCount,             out visibleOuterLineCount,
                                      out visibleInnerLineCount,   out invisibleOuterLineCount,
                                      out invisibleInnerLineCount, out invalidLineCount))
                return false;
            
            if (vertexCount == 0 ||
                (visibleOuterLineCount == 0 && invisibleOuterLineCount == 0 && 
                 visibleInnerLineCount == 0 && invisibleInnerLineCount == 0 && 
                 invalidLineCount == 0))
                return false;
            
            if (wireframe.vertices.Length != vertexCount)
                wireframe.vertices = new Vector3[vertexCount];

            if (wireframe.visibleOuterLines.Length != visibleOuterLineCount)
                wireframe.visibleOuterLines = new Int32[visibleOuterLineCount];

            if (wireframe.visibleInnerLines.Length != visibleInnerLineCount)
                wireframe.visibleInnerLines = new Int32[visibleInnerLineCount];

            if (wireframe.invisibleOuterLines.Length != invisibleOuterLineCount)
                wireframe.invisibleOuterLines = new Int32[invisibleOuterLineCount];

            if (wireframe.invisibleInnerLines.Length != invisibleInnerLineCount)
                wireframe.invisibleInnerLines = new Int32[invisibleInnerLineCount];

            if (wireframe.invalidLines.Length != invalidLineCount)
                wireframe.invalidLines = new Int32[invalidLineCount];

            var verticesHandle				= GCHandle.Alloc(wireframe.vertices, GCHandleType.Pinned);
            var visibleOuterLinesHandle		= GCHandle.Alloc(wireframe.visibleOuterLines, GCHandleType.Pinned);
            var visibleInnerLinesHandle		= GCHandle.Alloc(wireframe.visibleInnerLines, GCHandleType.Pinned);
            var invisibleOuterLinesHandle	= GCHandle.Alloc(wireframe.invisibleOuterLines, GCHandleType.Pinned);
            var invisibleInnerLinesHandle	= GCHandle.Alloc(wireframe.invisibleInnerLines, GCHandleType.Pinned);
            var invalidLinesHandle			= GCHandle.Alloc(wireframe.invalidLines, GCHandleType.Pinned);

            var verticesPtr				= verticesHandle.AddrOfPinnedObject();
            var visibleOuterLinesPtr	= visibleOuterLinesHandle.AddrOfPinnedObject();
            var visibleInnerLinesPtr	= visibleInnerLinesHandle.AddrOfPinnedObject();
            var invisibleOuterLinesPtr	= invisibleOuterLinesHandle.AddrOfPinnedObject();
            var invisibleInnerLinesPtr	= invisibleInnerLinesHandle.AddrOfPinnedObject();
            var invalidLinesPtr			= invalidLinesHandle.AddrOfPinnedObject();
            
            bool success = GetBrushOutlineValues(wireframe.originBrushID,
                                                 vertexCount,             verticesPtr,
                                                 visibleOuterLineCount,   visibleOuterLinesPtr,
                                                 visibleInnerLineCount,   visibleInnerLinesPtr,
                                                 invisibleOuterLineCount, invisibleOuterLinesPtr,
                                                 invisibleInnerLineCount, invisibleInnerLinesPtr,
                                                 invalidLineCount,        invalidLinesPtr);

            verticesHandle.Free();
            visibleOuterLinesHandle.Free();
            visibleInnerLinesHandle.Free();
            invisibleOuterLinesHandle.Free();
            invisibleInnerLinesHandle.Free();
            invalidLinesHandle.Free();

            if (!success)
                return false;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(wireframe.originBrushID);
            return true;
        }
    }
}
