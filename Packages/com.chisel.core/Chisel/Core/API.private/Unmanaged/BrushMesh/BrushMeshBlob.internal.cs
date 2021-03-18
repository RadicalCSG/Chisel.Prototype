using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    struct BrushMeshBlob
    {
        public struct Polygon
        {
            public Int32            firstEdge;
            public Int32            edgeCount;
            public SurfaceLayers    layerDefinition;
            public UVMatrix         UV0;
        }

        public MinMaxAABB		                localBounds;

        public BlobArray<float3>	            localVertices;
        public BlobArray<BrushMesh.HalfEdge>	halfEdges;
        public BlobArray<int>                   halfEdgePolygonIndices;
        public BlobArray<Polygon>	            polygons;
        public BlobArray<float4>                localPlanes;
        
        public bool IsEmpty()
        {
            return (localPlanes.Length == 0 || polygons.Length == 0 || localVertices.Length == 0 || halfEdges.Length == 0);
        }
    }
}
