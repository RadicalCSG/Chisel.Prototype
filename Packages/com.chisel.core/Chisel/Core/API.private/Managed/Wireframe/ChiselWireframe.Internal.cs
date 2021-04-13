using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    sealed partial class ChiselWireframe
    {
        // TODO: generate outlines somewhere in managed code


        private static uint GetBrushOutlineHash(CSGTreeBrush brush)
        {
            return brush.Outline.hash;
        }

        private static bool GetBrushOutlineValues(CSGTreeBrush      brush,
                                                  ref float3[]      vertices,
                                                  ref Int32[]       visibleOuterLines)
        {
            ref var brushOutline = ref brush.Outline;
            if (brushOutline.vertices.Length < 3)
                return false;

            // TODO: once we switch to managed code, remove need to copy outlines
            vertices            = brushOutline.vertices.ToArray();
            visibleOuterLines   = brushOutline.visibleOuterLines.ToArray();
            return true;
        }

        private static bool GetSurfaceOutlineValues(CSGTreeBrush    brush,
                                                    Int32           surfaceIndex,
                                                    ref float3[]    vertices,
                                                    ref Int32[]     visibleOuterLines)
        {
            ref var brushOutline = ref brush.Outline;
            if (brushOutline.vertices.Length < 3)
                return false;

            var surfaceOutlineRanges = brushOutline.surfaceVisibleOuterLineRanges;
            if (!surfaceOutlineRanges.IsCreated || surfaceIndex < 0 || surfaceIndex >= surfaceOutlineRanges.Length)
                return false;

            var startIndex  = surfaceIndex == 0 ? 0 : surfaceOutlineRanges[surfaceIndex - 1];
            var lastIndex   = surfaceOutlineRanges[surfaceIndex];
            var count       = lastIndex - startIndex;
            if (count <= 0)
                return false;

            var surfaceOutlines = brushOutline.surfaceVisibleOuterLines;
            if (startIndex < 0 || lastIndex > surfaceOutlines.Length)
                return false;

            visibleOuterLines = new int[count];
            for (int i = startIndex; i < lastIndex; i++)
                visibleOuterLines[i - startIndex] = surfaceOutlines[i];

            vertices = brushOutline.vertices.ToArray();
            return true;
        }

        private static ChiselWireframe CreateSurfaceWireframe(CSGTreeBrush brush, Int32 surfaceID)
        {
            var wireframe = new ChiselWireframe() { originBrush = brush, originSurfaceID = surfaceID };
            bool success = GetSurfaceOutlineValues(brush, surfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines);

            if (!success)
                return null;

            wireframe.outlineHash = GetBrushOutlineHash(brush);
            return wireframe;
        }

        private static bool UpdateSurfaceWireframe(ChiselWireframe wireframe)
        {
            bool success = GetSurfaceOutlineValues(wireframe.originBrush, 
                                                   wireframe.originSurfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines);

            if (!success)
                return false;

            wireframe.outlineHash = GetBrushOutlineHash(wireframe.originBrush);
            return true;
        }

        private static ChiselWireframe CreateBrushWireframe(CSGTreeBrush brush)
        {
            var wireframe = new ChiselWireframe { originBrush = brush };
            bool success = GetBrushOutlineValues(brush,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines);

            if (!success)
                return null;

            wireframe.outlineHash = GetBrushOutlineHash(brush);
            return wireframe;
        }

        private static bool UpdateBrushWireframe(ChiselWireframe wireframe)
        {
            bool success = GetBrushOutlineValues(wireframe.originBrush,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines);

            if (!success)
                return false;

            wireframe.outlineHash = GetBrushOutlineHash(wireframe.originBrush);
            return true;
        }
    }
}
