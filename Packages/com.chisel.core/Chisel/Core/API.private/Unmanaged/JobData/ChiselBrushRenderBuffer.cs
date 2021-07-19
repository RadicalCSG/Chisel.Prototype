using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct ChiselSurfaceRenderBuffer
    {
        public int              surfaceIndex;
        public SurfaceLayers    surfaceLayers;

        public int              vertexCount;
        public int              indexCount;

        public uint             geometryHash;
        public uint             surfaceHash;

        public float3           min, max;  

        public ChiselBlobArray<Int32>		    indices;
        public ChiselBlobArray<RenderVertex>	renderVertices;
        public ChiselBlobArray<float3>	    colliderVertices;
    };

    struct ChiselQuerySurface
    {
        public int      surfaceIndex;
        public int      surfaceParameter;

        public int      vertexCount;
        public int      indexCount;

        public uint     geometryHash;
        public uint     surfaceHash;
    }

    struct ChiselQuerySurfaces
    {
        public CompactNodeID                    brushNodeID;
        public ChiselBlobArray<ChiselQuerySurface>    surfaces;
    }

    struct ChiselBrushRenderBuffer
    {
        public ChiselBlobArray<ChiselSurfaceRenderBuffer> surfaces;
        public ChiselBlobArray<ChiselQuerySurfaces>       querySurfaces;
        public int surfaceOffset;
        public int surfaceCount;
    };

}
