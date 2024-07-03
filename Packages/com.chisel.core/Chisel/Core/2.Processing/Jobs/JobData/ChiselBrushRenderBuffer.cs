using System;

using Unity.Entities;
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

        public BlobArray<Int32>		    indices;
        public BlobArray<RenderVertex>	renderVertices;
        public BlobArray<float3>	    colliderVertices;
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
        public BlobArray<ChiselQuerySurface>    surfaces;
    }

    struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
        public BlobArray<ChiselQuerySurfaces>       querySurfaces;
        public int surfaceOffset;
        public int surfaceCount;
    };

}
