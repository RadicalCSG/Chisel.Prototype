using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Chisel.Core
{
    sealed partial class ChiselWireframe
    {
        // TODO: generate outlines somewhere in managed code

        // temporary solution to be able to see a brush wireframe
        static BrushOutline CreateOutline(BrushMesh brushMesh)
        {
            var brushOutline = new BrushOutline();
            brushOutline.Reset();
            if (brushMesh == null)
                return brushOutline;
            brushOutline.surfaceOutlines = new Outline[brushMesh.planes.Length];
            brushOutline.vertices = brushMesh.vertices.ToArray();

            var surfaceOutlines = brushOutline.surfaceOutlines;
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                surfaceOutlines[p] = new Outline();
                surfaceOutlines[p].Reset();
            }

            var brushVisibleOuterLines   = new List<int>();
            var surfaceVisibleOuterLines = new List<int>();
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                var surfaceOutline = surfaceOutlines[p];
                surfaceVisibleOuterLines.Clear();
                var firstEdge = brushMesh.polygons[p].firstEdge;
                var edgeCount = brushMesh.polygons[p].edgeCount;
                var lastEdge  = firstEdge + edgeCount;

                var vertexIndex0 = brushMesh.halfEdges[lastEdge - 1].vertexIndex;
                var vertexIndex1 = 0;
                for (int h = firstEdge; h < lastEdge; vertexIndex0 = vertexIndex1, h++)
                {
                    vertexIndex1 = brushMesh.halfEdges[h].vertexIndex;
                    if (vertexIndex0 > vertexIndex1) // avoid duplicate edges
                    {
                        brushVisibleOuterLines.Add(vertexIndex0);
                        brushVisibleOuterLines.Add(vertexIndex1);
                    }
                    surfaceVisibleOuterLines.Add(vertexIndex0);
                    surfaceVisibleOuterLines.Add(vertexIndex1);
                }

                surfaceOutline.visibleOuterLines = surfaceVisibleOuterLines.ToArray();
            }
            brushOutline.brushOutline.visibleOuterLines = brushVisibleOuterLines.ToArray();
            return brushOutline;
        }



        private static UInt64 GetBrushOutlineGeneration(Int32 brushNodeID)
        {
            var brushInfo = CSGManager.GetBrushOutlineState(brushNodeID);
            return brushInfo.brushOutlineGeneration;
        }

        internal static void UpdateOutline(Int32 brushNodeID)
        {
            var brushInfo = CSGManager.GetBrushOutlineState(brushNodeID);
            var brushMeshInstanceID = CSGManager.GetBrushMeshID(brushNodeID);
            if (BrushMeshManager.IsBrushMeshIDValid(brushMeshInstanceID))
            {
                var brushMesh = BrushMeshManager.GetBrushMesh(brushMeshInstanceID);
                brushInfo.brushOutline = CreateOutline(brushMesh);
            } else
            {
                if (brushInfo.brushOutline == null)
                    brushInfo.brushOutline = new BrushOutline();
                brushInfo.brushOutline.Reset();
            }
        }

        private static bool GetBrushOutlineValues(Int32             brushNodeID,
                                                  ref float3[]      vertices,
                                                  ref Int32[]       visibleOuterLines,
                                                  ref Int32[]       visibleInnerLines,
                                                  ref Int32[]       invisibleOuteLines,
                                                  ref Int32[]       invisibleInnerLines,
                                                  ref Int32[]       invalidLines)
        {
            var brushInfo = CSGManager.GetBrushOutlineState(brushNodeID);
            if (brushInfo == null)
                return false;

            if (brushInfo.brushOutlineDirty)
            {
                ChiselWireframe.UpdateOutline(brushNodeID);
                brushInfo.brushOutlineDirty = false;
            }

            var brushOutline = brushInfo.brushOutline;
            if (brushOutline == null ||
                brushOutline.vertices == null ||
                brushOutline.vertices.Length < 3)
                return false;

            // TODO: once we switch to managed code, remove need to copy outlines
            vertices            = brushOutline.vertices.ToArray();
            visibleOuterLines   = brushOutline.brushOutline.visibleOuterLines.ToArray();
            visibleInnerLines   = brushOutline.brushOutline.visibleInnerLines.ToArray();
            invisibleOuteLines  = brushOutline.brushOutline.invisibleOuterLines.ToArray();
            invisibleInnerLines = brushOutline.brushOutline.invisibleInnerLines.ToArray();
            invalidLines        = brushOutline.brushOutline.invalidLines.ToArray();
            return true;
        }

        private static bool GetSurfaceOutlineValues(Int32           brushNodeID,
                                                    Int32           surfaceID,
                                                    ref float3[]    vertices,
                                                    ref Int32[]     visibleOuterLines,
                                                    ref Int32[]     visibleInnerLines,
                                                    ref Int32[]     visibleTriangles,
                                                    ref Int32[]     invisibleOuterLines,
                                                    ref Int32[]     invisibleInnerLines,
                                                    ref Int32[]     invalidLines)
        {
            var brushInfo = CSGManager.GetBrushOutlineState(brushNodeID);
            if (brushInfo == null)
                return false;

            if (brushInfo.brushOutlineDirty)
            {
                ChiselWireframe.UpdateOutline(brushNodeID);
                brushInfo.brushOutlineDirty = false;
            }

            var brushOutline = brushInfo.brushOutline;
            if (brushOutline == null ||
                brushOutline.vertices == null ||
                brushOutline.vertices.Length < 3)
                return false;

            var surfaceOutlines = brushOutline.surfaceOutlines;
            if (surfaceOutlines == null ||
                surfaceID < 0 ||
                surfaceID >= surfaceOutlines.Length)
                return false;

            var surfaceOutline = surfaceOutlines[surfaceID];

            // TODO: once we switch to managed code, remove need to copy outlines
            vertices            = brushOutline.vertices.ToArray();
            visibleOuterLines   = surfaceOutline.visibleOuterLines.ToArray();
            visibleInnerLines   = surfaceOutline.visibleInnerLines.ToArray();
            visibleTriangles    = surfaceOutline.visibleTriangles.ToArray();
            invisibleOuterLines = surfaceOutline.invisibleOuterLines.ToArray();
            invisibleInnerLines = surfaceOutline.invisibleInnerLines.ToArray();
            invalidLines        = surfaceOutline.invalidLines.ToArray();
            return true;
        }

        private static ChiselWireframe CreateSurfaceWireframe(Int32 brushNodeID, Int32 surfaceID)
        {
            var wireframe = new ChiselWireframe() { originBrushID = brushNodeID, originSurfaceID = surfaceID };
            bool success = GetSurfaceOutlineValues(brushNodeID, surfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines,
                                                   ref wireframe.visibleInnerLines,
                                                   ref wireframe.visibleTriangles,
                                                   ref wireframe.invisibleOuterLines,
                                                   ref wireframe.invisibleInnerLines,
                                                   ref wireframe.invalidLines);

            if (!success)
                return null;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(brushNodeID);
            return wireframe;
        }

        private static bool UpdateSurfaceWireframe(ChiselWireframe wireframe)
        {
            bool success = GetSurfaceOutlineValues(wireframe.originBrushID, 
                                                   wireframe.originSurfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines,
                                                   ref wireframe.visibleInnerLines,
                                                   ref wireframe.visibleTriangles,
                                                   ref wireframe.invisibleOuterLines,
                                                   ref wireframe.invisibleInnerLines,
                                                   ref wireframe.invalidLines);

            if (!success)
                return false;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(wireframe.originBrushID);
            return true;
        }

        private static ChiselWireframe CreateBrushWireframe(Int32 brushNodeID)
        {
            var wireframe = new ChiselWireframe { originBrushID = brushNodeID };
            bool success = GetBrushOutlineValues(brushNodeID,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines,
                                                 ref wireframe.visibleInnerLines,
                                                 ref wireframe.invisibleOuterLines,
                                                 ref wireframe.invisibleInnerLines,
                                                 ref wireframe.invalidLines);

            if (!success)
                return null;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(brushNodeID);
            return wireframe;
        }

        private static bool UpdateBrushWireframe(ChiselWireframe wireframe)
        {
            bool success = GetBrushOutlineValues(wireframe.originBrushID,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines,
                                                 ref wireframe.visibleInnerLines,
                                                 ref wireframe.invisibleOuterLines,
                                                 ref wireframe.invisibleInnerLines,
                                                 ref wireframe.invalidLines);

            if (!success)
                return false;

            wireframe.outlineGeneration = GetBrushOutlineGeneration(wireframe.originBrushID);
            return true;
        }
    }
}
