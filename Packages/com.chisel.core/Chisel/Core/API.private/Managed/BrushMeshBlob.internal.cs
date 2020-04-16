using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public struct BrushMeshBlob
    {
        public struct Polygon
        {
            public Int32            firstEdge;
            public Int32            edgeCount;
            public SurfaceLayers    layerDefinition;
            public UVMatrix         UV0;
        }

        // TODO: turn this into AABB
        public Bounds		                    localBounds;

        public BlobArray<float3>	            vertices;
        public BlobArray<BrushMesh.HalfEdge>	halfEdges;
        public BlobArray<int>                   halfEdgePolygonIndices;
        public BlobArray<Polygon>	            polygons;
        public BlobArray<float4>                localPlanes;
        
        public bool IsEmpty()
        {
            return (localPlanes.Length == 0 || polygons.Length == 0 || vertices.Length == 0 || halfEdges.Length == 0);
        }

        public unsafe static BlobAssetReference<BrushMeshBlob> Build(BrushMesh brushMesh)
        {
            if (brushMesh == null ||
                brushMesh.vertices.Length < 4 ||
                brushMesh.polygons.Length < 4 ||
                brushMesh.halfEdges.Length < 12)
                return BlobAssetReference<BrushMeshBlob>.Null;

            var builder = new BlobBuilder(Allocator.Temp);

            var srcVertices = brushMesh.vertices;
            var srcPlanes = brushMesh.planes;


            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = brushMesh.localBounds;
            builder.Construct(ref root.vertices, srcVertices);
            builder.Construct(ref root.halfEdges, brushMesh.halfEdges);
            builder.Construct(ref root.halfEdgePolygonIndices, brushMesh.halfEdgePolygonIndices);
            var polygonArray = builder.Allocate(ref root.polygons, brushMesh.polygons.Length);
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                ref var srcPolygon = ref brushMesh.polygons[p];
                ref var dstPolygon = ref polygonArray[p];
                dstPolygon.firstEdge        = srcPolygon.firstEdge;
                dstPolygon.edgeCount        = srcPolygon.edgeCount;
                dstPolygon.layerDefinition  = srcPolygon.surface.brushMaterial.LayerDefinition;
                dstPolygon.UV0              = srcPolygon.surface.surfaceDescription.UV0;
            }

            builder.Construct(ref root.localPlanes, brushMesh.planes);

            /*
            var vertexIntersectionSegments = stackalloc int2[srcVertices.Length];
            var vertexIntersectionPlanes = stackalloc ushort[srcVertices.Length * (srcPlanes.Length - 1)];
            var vertexIntersectionPlaneCount = 0;
            const float kPlaneDistanceEpsilon = CSGManagerPerformCSG.kPlaneDistanceEpsilon;

            for (int i = 0; i < srcVertices.Length; i++)
            {
                vertexIntersectionSegments[i].x = vertexIntersectionPlaneCount;
                for (int j = 0; j < srcPlanes.Length; j++)
                {
                    var distance = math.dot(srcPlanes[j], new float4(srcVertices[i], 1));
                    if (distance >= -kPlaneDistanceEpsilon && distance <= kPlaneDistanceEpsilon) // Note: this is false on NaN/Infinity, so don't invert
                    {
                        vertexIntersectionPlanes[vertexIntersectionPlaneCount] = (ushort)j;
                        vertexIntersectionPlaneCount++;
                    }
                }
                vertexIntersectionSegments[i].y = vertexIntersectionPlaneCount - vertexIntersectionSegments[i].x;
            }
            builder.Construct(ref root.vertexIntersectionPlanes, vertexIntersectionPlanes, vertexIntersectionPlaneCount);
            builder.Construct(ref root.vertexIntersectionSegments, vertexIntersectionSegments, srcVertices.Length);
            */

            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
