using System;
using System.Linq;
using Chisel.Core;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        //
        //
        //            
        //
        //  0          3
        //  *---------*
        //  | \____  / \
        //  \      \/   \
        //   *--__ /_\___\ 
        //  1 \_  /  \___\\
        //       *---------*
        //      2           4
        //
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> CreateSquarePyramidAssetPolygons(float3 vertex0,
                                                                                         float3 vertex1,
                                                                                         float3 vertex2,
                                                                                         float3 vertex3,
                                                                                         float3 vertex4,
                                                                                         in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition, 
                                                                                         Allocator allocator)
        {
            if (surfaceDefinition == BlobAssetReference<NativeChiselSurfaceDefinition>.Null)
                return BlobAssetReference<BrushMeshBlob>.Null;

            ref var surfaces = ref surfaceDefinition.Value.surfaces;
            if (surfaces.Length < 5)
                return BlobAssetReference<BrushMeshBlob>.Null;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,           5);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              16);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, 16);
                var polygons                = builder.Allocate(ref root.polygons,                5);
                var localPlanes             = builder.Allocate(ref root.localPlanes,             5);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                const int vertIndex4 = 4;

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;
                localVertices[vertIndex4] = vertex4;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;
                const int polygon4 = 4;

                const int edge0_0 = (polygon0 * 4) + 0;
                const int edge0_1 = (polygon0 * 4) + 1;
                const int edge0_2 = (polygon0 * 4) + 2;
                const int edge0_3 = (polygon0 * 4) + 3;

                const int edge1_0 = (polygon1 * 4) + 0;
                const int edge1_1 = (polygon1 * 4) + 1;
                const int edge1_2 = (polygon1 * 4) + 2; 
                
                const int edge2_0 = (polygon2 * 4) + 0;
                const int edge2_1 = (polygon2 * 4) + 1;
                const int edge2_2 = (polygon2 * 4) + 2;

                const int edge3_0 = (polygon3 * 4) + 0;
                const int edge3_1 = (polygon3 * 4) + 1;
                const int edge3_2 = (polygon3 * 4) + 2;

                const int edge4_0 = (polygon4 * 4) + 0;
                const int edge4_1 = (polygon4 * 4) + 1;
                const int edge4_2 = (polygon4 * 4) + 2;


                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex =  8, vertexIndex = 0 }; // 0  (0-3)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = 11, vertexIndex = 1 }; // 1  (1-0)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = 14, vertexIndex = 2 }; // 2  (2-1)
                halfEdges[edge0_3] = new BrushMeshBlob.HalfEdge { twinIndex =  5, vertexIndex = 3 }; // 3  (3-2)

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex =  9, vertexIndex = 3 }; // 4  (3-4)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex =  3, vertexIndex = 2 }; // 5  (2-3)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = 13, vertexIndex = 4 }; // 6  (4-2)

                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = 12, vertexIndex = 0 }; // 7  (0-4)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex =  0, vertexIndex = 3 }; // 8  (3-0)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex =  4, vertexIndex = 4 }; // 9  (4-3)

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = 15, vertexIndex = 1 }; // 10 (1-4)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex =  1, vertexIndex = 0 }; // 11 (0-1)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex =  7, vertexIndex = 4 }; // 12 (4-0)

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex =  6, vertexIndex = 2 }; // 13 (2-4)
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex =  2, vertexIndex = 1 }; // 14 (1-2)
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex = 10, vertexIndex = 4 }; // 15 (4-1)

                halfEdgePolygonIndices[edge0_0] = polygon0;
                halfEdgePolygonIndices[edge0_1] = polygon0;
                halfEdgePolygonIndices[edge0_2] = polygon0;
                halfEdgePolygonIndices[edge0_3] = polygon0;

                halfEdgePolygonIndices[edge1_0] = polygon1;
                halfEdgePolygonIndices[edge1_1] = polygon1;
                halfEdgePolygonIndices[edge1_2] = polygon1;

                halfEdgePolygonIndices[edge2_0] = polygon2;
                halfEdgePolygonIndices[edge2_1] = polygon2;
                halfEdgePolygonIndices[edge2_2] = polygon2;

                halfEdgePolygonIndices[edge3_0] = polygon3;
                halfEdgePolygonIndices[edge3_1] = polygon3;
                halfEdgePolygonIndices[edge3_2] = polygon3;

                halfEdgePolygonIndices[edge4_0] = polygon4;
                halfEdgePolygonIndices[edge4_1] = polygon4;
                halfEdgePolygonIndices[edge4_2] = polygon4;

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge =  0, edgeCount = 4, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge =  4, edgeCount = 3, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge =  7, edgeCount = 3, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = 10, edgeCount = 3, descriptionIndex = 3, surface = surfaces[3] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = 13, edgeCount = 3, descriptionIndex = 4, surface = surfaces[3] };// TODO: figure out if this should be [4]

                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                root.localBounds = CalculateBounds(in localVertices);
                return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            }
        }

        static readonly BrushMesh.HalfEdge[] squarePyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 0 },	// 0  (0-3)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 1 },	// 1  (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 },	// 2  (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 3  (3-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 },	// 4  (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 2 },	// 5  (2-3)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 4 },	// 6  (4-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 0 },	// 7  (0-4)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 3 },	// 8  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 9  (4-3)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 1 },	// 10 (1-4)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	// 11 (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 4 },	// 12 (4-0)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 13 (2-4)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 14 (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 4 },	// 15 (4-1)
        };

        static readonly BrushMesh.HalfEdge[] invertedSquarePyramidHalfEdges = squarePyramidHalfEdges;

        //
        //
        //            
        //
        //  1          3
        //  *---------*
        //   \\____  / \
        //    \    \/   \
        //     \   / \___\ 
        //      \ /      \\
        //       *---------*
        //      2           0
        //
        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> CreateTriangularPyramidAssetPolygons(float3 vertex0,
                                                                                             float3 vertex1,
                                                                                             float3 vertex2,
                                                                                             float3 vertex3,
                                                                                             in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition, 
                                                                                             Allocator allocator)
        {
            if (surfaceDefinition == BlobAssetReference<NativeChiselSurfaceDefinition>.Null)
                return BlobAssetReference<BrushMeshBlob>.Null;

            ref var surfaces = ref surfaceDefinition.Value.surfaces;
            if (surfaces.Length < 5)
                return BlobAssetReference<BrushMeshBlob>.Null;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,           4);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              12);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, 12);
                var polygons                = builder.Allocate(ref root.polygons,                4);
                var localPlanes             = builder.Allocate(ref root.localPlanes,             4);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;

                const int edge0_0 = (polygon0 * 4) + 0;
                const int edge0_1 = (polygon0 * 4) + 1;
                const int edge0_2 = (polygon0 * 4) + 2;

                const int edge1_0 = (polygon1 * 4) + 0;
                const int edge1_1 = (polygon1 * 4) + 1;
                const int edge1_2 = (polygon1 * 4) + 2; 
                
                const int edge2_0 = (polygon2 * 4) + 0;
                const int edge2_1 = (polygon2 * 4) + 1;
                const int edge2_2 = (polygon2 * 4) + 2;

                const int edge3_0 = (polygon3 * 4) + 0;
                const int edge3_1 = (polygon3 * 4) + 1;
                const int edge3_2 = (polygon3 * 4) + 2;

                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = 11, vertexIndex = 3 }; // 0  (3-1)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex =  5, vertexIndex = 2 }; // 1  (2-3)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex =  8, vertexIndex = 1 }; // 2  (1-2)
            
                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = 10, vertexIndex = 0 }; // 3  (0-3)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex =  6, vertexIndex = 2 }; // 4  (2-0)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex =  1, vertexIndex = 3 }; // 5  (3-2)
            
                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex =  4, vertexIndex = 0 }; // 6  (0-2)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex =  9, vertexIndex = 1 }; // 7  (1-0)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex =  2, vertexIndex = 2 }; // 8  (2-1)
            
                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex =  7, vertexIndex = 0 }; // 9  (0-1)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex =  3, vertexIndex = 3 }; // 10 (3-0)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex =  0, vertexIndex = 1 }; // 11 (1-3)

                halfEdgePolygonIndices[edge0_0] = polygon0;
                halfEdgePolygonIndices[edge0_1] = polygon0;
                halfEdgePolygonIndices[edge0_2] = polygon0;

                halfEdgePolygonIndices[edge1_0] = polygon1;
                halfEdgePolygonIndices[edge1_1] = polygon1;
                halfEdgePolygonIndices[edge1_2] = polygon1;

                halfEdgePolygonIndices[edge2_0] = polygon2;
                halfEdgePolygonIndices[edge2_1] = polygon2;
                halfEdgePolygonIndices[edge2_2] = polygon2;

                halfEdgePolygonIndices[edge3_0] = polygon3;
                halfEdgePolygonIndices[edge3_1] = polygon3;
                halfEdgePolygonIndices[edge3_2] = polygon3;

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge =  0, edgeCount = 3, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge =  3, edgeCount = 3, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge =  6, edgeCount = 3, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge =  9, edgeCount = 3, descriptionIndex = 3, surface = surfaces[3] };

                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                root.localBounds = CalculateBounds(in localVertices);
                return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            }
        }

        static readonly BrushMesh.HalfEdge[] triangularPyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 3 },	// 0  (3-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 2 },	// 1  (2-3)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 1 },	// 2  (1-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 0 },	// 3  (0-3)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 4  (2-0)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 3 },	// 5  (3-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 0 },	// 6  (0-2)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 1 },	// 7  (1-0)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 8  (2-1)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 0 },	// 9  (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 3 },	// 10 (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 1 },	// 11 (1-3)
        };

        static readonly BrushMesh.HalfEdge[] invertedTriangularPyramidHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 1 },	// 0  (1-3)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 2 },	// 1  (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 3 },	// 2  (3-2)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 3 },	// 3  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 4  (2-3)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 0 },	// 5  (0-2)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 2 },	// 6  (2-0)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 1 },	// 7  (1-2)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 0 },	// 8  (0-1)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 1 },	// 9  (1-0)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 3 },	// 10 (3-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 0 },	// 11 (0-3)
        };
        

        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> CreateWedgeAssetPolygons(float3 vertex0,
                                                                                 float3 vertex1,
                                                                                 float3 vertex2,
                                                                                 float3 vertex3,
                                                                                 float3 vertex4,
                                                                                 float3 vertex5,
                                                                                 in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition, 
                                                                                 Allocator allocator)
        {
            if (surfaceDefinition == BlobAssetReference<NativeChiselSurfaceDefinition>.Null)
                return BlobAssetReference<BrushMeshBlob>.Null;

            ref var surfaces = ref surfaceDefinition.Value.surfaces;
            if (surfaces.Length < 5)
                return BlobAssetReference<BrushMeshBlob>.Null;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,           6);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              18);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, 18);
                var polygons                = builder.Allocate(ref root.polygons,                5);
                var localPlanes             = builder.Allocate(ref root.localPlanes,             5);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                const int vertIndex4 = 4;
                const int vertIndex5 = 5;

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;
                localVertices[vertIndex4] = vertex4;
                localVertices[vertIndex5] = vertex5;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;
                const int polygon4 = 4;

                const int edge0_0 = (polygon0 * 4) + 0;
                const int edge0_1 = (polygon0 * 4) + 1;
                const int edge0_2 = (polygon0 * 4) + 2;

                const int edge1_0 = (polygon1 * 4) + 0;
                const int edge1_1 = (polygon1 * 4) + 1;
                const int edge1_2 = (polygon1 * 4) + 2;

                const int edge2_0 = (polygon2 * 4) + 0;
                const int edge2_1 = (polygon2 * 4) + 1;
                const int edge2_2 = (polygon2 * 4) + 2;
                const int edge2_3 = (polygon2 * 4) + 3;

                const int edge3_0 = (polygon3 * 4) + 0;
                const int edge3_1 = (polygon3 * 4) + 1;
                const int edge3_2 = (polygon3 * 4) + 2;
                const int edge3_3 = (polygon3 * 4) + 3;

                const int edge4_0 = (polygon4 * 4) + 0;
                const int edge4_1 = (polygon4 * 4) + 1;
                const int edge4_2 = (polygon4 * 4) + 2;
                const int edge4_3 = (polygon4 * 4) + 3;

                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex =  6, vertexIndex = 0 }; // 0  (0-2)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = 10, vertexIndex = 1 }; // 1  (1-0)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = 14, vertexIndex = 2 }; // 2  (2-1)

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = 16, vertexIndex = 4 }; // 3  (4-5)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = 12, vertexIndex = 3 }; // 4  (3-4)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex =  8, vertexIndex = 5 }; // 5  (5-3)
            
                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex =  0, vertexIndex = 2 }; // 6  (2-0)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = 17, vertexIndex = 5 }; // 7  (5-2)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex =  5, vertexIndex = 3 }; // 8  (3-5)
                halfEdges[edge2_3] = new BrushMeshBlob.HalfEdge { twinIndex = 11, vertexIndex = 0 }; // 9  (0-3)

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex =  1, vertexIndex = 0 }; // 10 (0-1)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex =  9, vertexIndex = 3 }; // 11 (3-0)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex =  4, vertexIndex = 4 }; // 12 (4-3)
                halfEdges[edge3_3] = new BrushMeshBlob.HalfEdge { twinIndex = 15, vertexIndex = 1 }; // 13 (1-4)

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex =  2, vertexIndex = 1 }; // 14 (1-2)
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex = 13, vertexIndex = 4 }; // 15 (4-1)
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex =  3, vertexIndex = 5 }; // 16 (5-4)
                halfEdges[edge4_3] = new BrushMeshBlob.HalfEdge { twinIndex =  7, vertexIndex = 2 }; // 17 (2-5)

                halfEdgePolygonIndices[edge0_0] = polygon0;
                halfEdgePolygonIndices[edge0_1] = polygon0;
                halfEdgePolygonIndices[edge0_2] = polygon0;
                
                halfEdgePolygonIndices[edge1_0] = polygon1;
                halfEdgePolygonIndices[edge1_1] = polygon1;
                halfEdgePolygonIndices[edge1_2] = polygon1;
                
                halfEdgePolygonIndices[edge2_0] = polygon2;
                halfEdgePolygonIndices[edge2_1] = polygon2;
                halfEdgePolygonIndices[edge2_2] = polygon2;
                halfEdgePolygonIndices[edge2_3] = polygon2;

                halfEdgePolygonIndices[edge3_0] = polygon3;
                halfEdgePolygonIndices[edge3_1] = polygon3;
                halfEdgePolygonIndices[edge3_2] = polygon3;
                halfEdgePolygonIndices[edge3_3] = polygon3;

                halfEdgePolygonIndices[edge4_0] = polygon4;
                halfEdgePolygonIndices[edge4_1] = polygon4;
                halfEdgePolygonIndices[edge4_2] = polygon4;
                halfEdgePolygonIndices[edge4_3] = polygon4;

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge =  0, edgeCount = 3, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge =  3, edgeCount = 3, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge =  6, edgeCount = 3, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = 10, edgeCount = 3, descriptionIndex = 3, surface = surfaces[3] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = 14, edgeCount = 3, descriptionIndex = 4, surface = surfaces[4] };

                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                root.localBounds = CalculateBounds(in localVertices);
                return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            }
        }


        static readonly BrushMesh.HalfEdge[] wedgeHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 0 },	// 0  (0-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 1 },	// 1  (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 },	// 2  (2-1)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 4 },	// 3  (4-5)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 3 },	// 4  (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 5 },	// 5  (5-3)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 2 },	// 6  (2-0)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 5 },	// 7  (5-2)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 8  (3-5)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 0 },	// 9  (0-3)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	// 10 (0-1)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 },	// 11 (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 12 (4-3)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 1 },	// 13 (1-4)
            
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 14 (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 4 },	// 15 (4-1)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 5 },	// 16 (5-4)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 2 }	// 17 (2-5)
        };

        static readonly BrushMesh.HalfEdge[] invertedWedgeHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 2 },	// 0  (2-0)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 1 },	// 1  (1-2)
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 0 },	// 2  (0-1)
            
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 5 },	// 3  (5-4)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 3 },	// 4  (3-5)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 4 },	// 5  (4-3)
            
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 0 },	// 6  (0-2)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 3 },	// 7  (3-0)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 5 },	// 8  (5-3)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 2 },	// 9  (2-5)
            
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 1 },	// 10 (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 4 },	// 11 (4-1)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 3 },	// 12 (3-4)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 0 },	// 13 (0-3)
            
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 2 },	// 14 (2-1)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 5 },	// 15 (5-2)
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 4 },	// 16 (4-5)
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 1 }	// 17 (1-4)
        };

        static readonly BrushMesh.HalfEdge[] boxHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 0 },	//  0
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 3 },	//  1
            new BrushMesh.HalfEdge{ twinIndex = 20, vertexIndex = 2 },	//  2
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 1 },	//  3
                
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 4 },	//  4
            new BrushMesh.HalfEdge{ twinIndex = 19, vertexIndex = 5 },	//  5
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 6 },	//  6
            new BrushMesh.HalfEdge{ twinIndex = 22, vertexIndex = 7 },	//  7
                    
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 0 },	//  8
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 4 },	//  9
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 7 },	// 10
            new BrushMesh.HalfEdge{ twinIndex = 21, vertexIndex = 3 },	// 11
                    
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 18, vertexIndex = 1 },	// 12
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 2 },	// 13
            new BrushMesh.HalfEdge{ twinIndex = 23, vertexIndex = 6 },	// 14
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 5 },	// 15
                     
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 0 },	// 16
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 1 },	// 17
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 5 },	// 18
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 4 },	// 19
                    
            // polygon 5
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 3 },	// 20
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 7 },	// 21
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 6 },	// 22
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 2 }	// 23
        };
        
        static readonly BrushMesh.HalfEdge[] invertedBoxHalfEdges = new[]
        {
            // polygon 0
            new BrushMesh.HalfEdge{ twinIndex = 19, vertexIndex = 1 },	//  0 (1-0)
            new BrushMesh.HalfEdge{ twinIndex = 15, vertexIndex = 2 },	//  1 (2-1)
            new BrushMesh.HalfEdge{ twinIndex = 20, vertexIndex = 3 },	//  2 (3-2)
            new BrushMesh.HalfEdge{ twinIndex =  8, vertexIndex = 0 },	//  3 (0-3)
                
            // polygon 1
            new BrushMesh.HalfEdge{ twinIndex = 10, vertexIndex = 7 },	//  4 (7-4)
            new BrushMesh.HalfEdge{ twinIndex = 22, vertexIndex = 6 },	//  5 (6-7)
            new BrushMesh.HalfEdge{ twinIndex = 13, vertexIndex = 5 },	//  6 (5-6)
            new BrushMesh.HalfEdge{ twinIndex = 17, vertexIndex = 4 },	//  7 (4-5)
                    
            // polygon 2
            new BrushMesh.HalfEdge{ twinIndex =  3, vertexIndex = 3 },	//  8 (3-0)
            new BrushMesh.HalfEdge{ twinIndex = 23, vertexIndex = 7 },	//  9 (7-3)
            new BrushMesh.HalfEdge{ twinIndex =  4, vertexIndex = 4 },	// 10 (4-7)
            new BrushMesh.HalfEdge{ twinIndex = 16, vertexIndex = 0 },	// 11 (0-4)
                    
            // polygon 3
            new BrushMesh.HalfEdge{ twinIndex = 18, vertexIndex = 5 },	// 12 (5-1)
            new BrushMesh.HalfEdge{ twinIndex =  6, vertexIndex = 6 },	// 13 (6-5)
            new BrushMesh.HalfEdge{ twinIndex = 21, vertexIndex = 2 },	// 14 (2-6)
            new BrushMesh.HalfEdge{ twinIndex =  1, vertexIndex = 1 },	// 15 (1-2)
                     
            // polygon 4
            new BrushMesh.HalfEdge{ twinIndex = 11, vertexIndex = 4 },	// 16 (4-0)
            new BrushMesh.HalfEdge{ twinIndex =  7, vertexIndex = 5 },	// 17 (5-4)
            new BrushMesh.HalfEdge{ twinIndex = 12, vertexIndex = 1 },	// 18 (1-5)
            new BrushMesh.HalfEdge{ twinIndex =  0, vertexIndex = 0 },	// 19 (0-1)
                    
            // polygon 5
            new BrushMesh.HalfEdge{ twinIndex =  2, vertexIndex = 2 },	// 20 (2-3)
            new BrushMesh.HalfEdge{ twinIndex = 14, vertexIndex = 6 },	// 21 (6-2)
            new BrushMesh.HalfEdge{ twinIndex =  5, vertexIndex = 7 },	// 22 (7-6)
            new BrushMesh.HalfEdge{ twinIndex =  9, vertexIndex = 3 }	// 23 (3-7)
        };

        static BrushMesh.Polygon[] CreateBoxPolygons(int descriptionIndex)
        {
            return new[]
            {
                // left/right
                new BrushMesh.Polygon{ firstEdge =  0, edgeCount = 4, descriptionIndex = descriptionIndex },
                new BrushMesh.Polygon{ firstEdge =  4, edgeCount = 4, descriptionIndex = descriptionIndex },
                
                // front/back
                new BrushMesh.Polygon{ firstEdge =  8, edgeCount = 4, descriptionIndex = descriptionIndex },
                new BrushMesh.Polygon{ firstEdge = 12, edgeCount = 4, descriptionIndex = descriptionIndex },
                
                // top/down
                new BrushMesh.Polygon{ firstEdge = 16, edgeCount = 4, descriptionIndex = descriptionIndex },
                new BrushMesh.Polygon{ firstEdge = 20, edgeCount = 4, descriptionIndex = descriptionIndex }
            };
        }
    }
}