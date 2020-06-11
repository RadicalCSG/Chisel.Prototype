using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{    
    public struct BrushTreeSpaceVerticesBlob
    {
        public BlobArray<float3> treeSpaceVertices;

        public unsafe static BlobAssetReference<BrushTreeSpaceVerticesBlob> Build(ref BlobArray<float3> localVertices, float4x4 nodeToTreeSpaceMatrix, Allocator allocator = Allocator.Persistent)
        {
            var totalSize   = localVertices.Length * sizeof(float3);
            var builder     = new BlobBuilder(Allocator.Temp, math.max(4, totalSize));
            ref var root    = ref builder.ConstructRoot<BrushTreeSpaceVerticesBlob>();
            var treeSpaceVertices = builder.Allocate(ref root.treeSpaceVertices, localVertices.Length);
            for (int i = 0; i < localVertices.Length; i++)
                treeSpaceVertices[i] = math.mul(nodeToTreeSpaceMatrix, new float4(localVertices[i], 1)).xyz;
            var result = builder.CreateBlobAssetReference<BrushTreeSpaceVerticesBlob>(allocator);
            builder.Dispose();
            return result;
        }
    }

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

        public BlobArray<float3>	            localVertices;
        public BlobArray<BrushMesh.HalfEdge>	halfEdges;
        public BlobArray<int>                   halfEdgePolygonIndices;
        public BlobArray<Polygon>	            polygons;
        public BlobArray<float4>                localPlanes;
        
        public bool IsEmpty()
        {
            return (localPlanes.Length == 0 || polygons.Length == 0 || localVertices.Length == 0 || halfEdges.Length == 0);
        }

        public unsafe static BlobAssetReference<BrushMeshBlob> Build(BrushMesh brushMesh, Allocator allocator = Allocator.Persistent)
        {
            if (brushMesh == null ||
                brushMesh.vertices.Length < 4 ||
                brushMesh.polygons.Length < 4 ||
                brushMesh.halfEdges.Length < 12)
                return BlobAssetReference<BrushMeshBlob>.Null;


            var srcVertices = brushMesh.vertices;
            //var srcPlanes = brushMesh.planes;
            
            var totalPolygonSize        = 16 + (brushMesh.polygons.Length * UnsafeUtility.SizeOf<Polygon>());
            var totalPlaneSize          = 16 + (brushMesh.planes.Length * UnsafeUtility.SizeOf<float4>());
            var totalPolygonIndicesSize = 16 + (brushMesh.halfEdgePolygonIndices.Length * UnsafeUtility.SizeOf<int>());
            var totalHalfEdgeSize       = 16 + (brushMesh.halfEdges.Length * UnsafeUtility.SizeOf<BrushMesh.HalfEdge>());
            var totalVertexSize         = 16 + (srcVertices.Length * UnsafeUtility.SizeOf<float3>());
            var totalSize               = totalPlaneSize + totalPolygonSize + totalPolygonIndicesSize + totalHalfEdgeSize + totalVertexSize;


            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = brushMesh.localBounds;
            builder.Construct(ref root.localVertices, srcVertices);
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

            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }
    }
}
