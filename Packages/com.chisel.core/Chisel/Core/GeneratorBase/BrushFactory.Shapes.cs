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
                const int kTotalVertices    = 5;
                const int kTotalHalfEdges   = 16;
                const int kTotalPolygons    = 5;

                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,          kTotalVertices);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              kTotalHalfEdges);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, kTotalHalfEdges);
                var polygons                = builder.Allocate(ref root.polygons,               kTotalPolygons);
                var localPlanes             = builder.Allocate(ref root.localPlanes,            kTotalPolygons);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                const int vertIndex4 = 4;
                Debug.Assert(vertIndex4 == kTotalVertices - 1);

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
                Debug.Assert(polygon4 == kTotalPolygons - 1);
                
                const int polygon0_offset = 0;
                const int polygon0_count  = 4;
                const int edge0_0 = polygon0_offset + 0;
                const int edge0_1 = polygon0_offset + 1;
                const int edge0_2 = polygon0_offset + 2;
                const int edge0_3 = polygon0_offset + 3;

                const int polygon1_offset = polygon0_offset + polygon0_count;
                const int polygon1_count  = 3;
                const int edge1_0 = polygon1_offset + 0;
                const int edge1_1 = polygon1_offset + 1;
                const int edge1_2 = polygon1_offset + 2;

                const int polygon2_offset = polygon1_offset + polygon1_count;
                const int polygon2_count  = 3;
                const int edge2_0 = polygon2_offset + 0;
                const int edge2_1 = polygon2_offset + 1;
                const int edge2_2 = polygon2_offset + 2;

                const int polygon3_offset = polygon2_offset + polygon2_count;
                const int polygon3_count  = 3;
                const int edge3_0 = polygon3_offset + 0;
                const int edge3_1 = polygon3_offset + 1;
                const int edge3_2 = polygon3_offset + 2;

                const int polygon4_offset = polygon3_offset + polygon3_count;
                const int polygon4_count  = 3;
                const int edge4_0 = polygon4_offset + 0;
                const int edge4_1 = polygon4_offset + 1;
                const int edge4_2 = polygon4_offset + 2;
                Debug.Assert(edge4_2 == kTotalHalfEdges - 1);

                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_1, vertexIndex = vertIndex0 }; // 0  (0-3)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_1, vertexIndex = vertIndex1 }; // 1  (1-0)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_1, vertexIndex = vertIndex2 }; // 2  (2-1)
                halfEdges[edge0_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_1, vertexIndex = vertIndex3 }; // 3  (3-2)

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_2, vertexIndex = vertIndex3 }; // 4  (3-4)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_3, vertexIndex = vertIndex2 }; // 5  (2-3)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_0, vertexIndex = vertIndex4 }; // 6  (4-2)

                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_2, vertexIndex = vertIndex0 }; // 7  (0-4)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_0, vertexIndex = vertIndex3 }; // 8  (3-0)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_0, vertexIndex = vertIndex4 }; // 9  (4-3)

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_2, vertexIndex = vertIndex1 }; // 10 (1-4)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_1, vertexIndex = vertIndex0 }; // 11 (0-1)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_0, vertexIndex = vertIndex4 }; // 12 (4-0)

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_2, vertexIndex = vertIndex2 }; // 13 (2-4)
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_2, vertexIndex = vertIndex1 }; // 14 (1-2)
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_0, vertexIndex = vertIndex4 }; // 15 (4-1)

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

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge = polygon0_offset, edgeCount = polygon0_count, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge = polygon1_offset, edgeCount = polygon1_count, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge = polygon2_offset, edgeCount = polygon2_count, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = polygon3_offset, edgeCount = polygon3_count, descriptionIndex = 3, surface = surfaces[3] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = polygon4_offset, edgeCount = polygon4_count, descriptionIndex = 4, surface = surfaces[3] };// TODO: figure out if this should be [4]

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
                const int kTotalVertices    = 4;
                const int kTotalHalfEdges   = 12;
                const int kTotalPolygons    = 4;

                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,          kTotalVertices);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              kTotalHalfEdges);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, kTotalHalfEdges);
                var polygons                = builder.Allocate(ref root.polygons,               kTotalPolygons);
                var localPlanes             = builder.Allocate(ref root.localPlanes,            kTotalPolygons);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                Debug.Assert(vertIndex3 == kTotalVertices - 1);

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;
                Debug.Assert(polygon3 == kTotalPolygons - 1);

                const int polygon0_offset = 0;
                const int polygon0_count  = 3;
                const int edge0_0 = polygon0_offset + 0;
                const int edge0_1 = polygon0_offset + 1;
                const int edge0_2 = polygon0_offset + 2;

                const int polygon1_offset = polygon0_offset + polygon0_count;
                const int polygon1_count  = 3;
                const int edge1_0 = polygon1_offset + 0;
                const int edge1_1 = polygon1_offset + 1;
                const int edge1_2 = polygon1_offset + 2;

                const int polygon2_offset = polygon1_offset + polygon1_count;
                const int polygon2_count  = 3;
                const int edge2_0 = polygon2_offset + 0;
                const int edge2_1 = polygon2_offset + 1;
                const int edge2_2 = polygon2_offset + 2;

                const int polygon3_offset = polygon2_offset + polygon2_count;
                const int polygon3_count  = 3;
                const int edge3_0 = polygon3_offset + 0;
                const int edge3_1 = polygon3_offset + 1;
                const int edge3_2 = polygon3_offset + 2;
                Debug.Assert(edge3_2 == kTotalHalfEdges - 1);

                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_2, vertexIndex = vertIndex3 }; // 0  (3-1)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_2, vertexIndex = vertIndex2 }; // 1  (2-3)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_2, vertexIndex = vertIndex1 }; // 2  (1-2)
            
                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_1, vertexIndex = vertIndex0 }; // 3  (0-3)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_0, vertexIndex = vertIndex2 }; // 4  (2-0)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_1, vertexIndex = vertIndex3 }; // 5  (3-2)
            
                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_1, vertexIndex = vertIndex0 }; // 6  (0-2)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_0, vertexIndex = vertIndex1 }; // 7  (1-0)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_2, vertexIndex = vertIndex2 }; // 8  (2-1)
            
                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_1, vertexIndex = vertIndex0 }; // 9  (0-1)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_0, vertexIndex = vertIndex3 }; // 10 (3-0)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_0, vertexIndex = vertIndex1 }; // 11 (1-3)

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

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge = polygon0_offset, edgeCount = polygon0_count, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge = polygon1_offset, edgeCount = polygon1_count, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge = polygon2_offset, edgeCount = polygon2_count, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = polygon3_offset, edgeCount = polygon3_count, descriptionIndex = 3, surface = surfaces[3] };

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
                const int kTotalVertices    = 6;
                const int kTotalHalfEdges   = 18;
                const int kTotalPolygons    = 5;

                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,          kTotalVertices);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              kTotalHalfEdges);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, kTotalHalfEdges);
                var polygons                = builder.Allocate(ref root.polygons,               kTotalPolygons);
                var localPlanes             = builder.Allocate(ref root.localPlanes,            kTotalPolygons);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                const int vertIndex4 = 4;
                const int vertIndex5 = 5;
                Debug.Assert(vertIndex5 == kTotalVertices - 1);

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
                Debug.Assert(polygon4 == kTotalPolygons - 1);

                const int polygon0_offset = 0;
                const int polygon0_count = 3;
                const int edge0_0 = polygon0_offset + 0;
                const int edge0_1 = polygon0_offset + 1;
                const int edge0_2 = polygon0_offset + 2;

                const int polygon1_offset = polygon0_offset + polygon0_count;
                const int polygon1_count = 3;
                const int edge1_0 = polygon1_offset + 0;
                const int edge1_1 = polygon1_offset + 1;
                const int edge1_2 = polygon1_offset + 2;

                const int polygon2_offset = polygon1_offset + polygon1_count;
                const int polygon2_count = 4;
                const int edge2_0 = polygon2_offset + 0;
                const int edge2_1 = polygon2_offset + 1;
                const int edge2_2 = polygon2_offset + 2;
                const int edge2_3 = polygon2_offset + 3;

                const int polygon3_offset = polygon2_offset + polygon2_count;
                const int polygon3_count = 4;
                const int edge3_0 = polygon3_offset + 0;
                const int edge3_1 = polygon3_offset + 1;
                const int edge3_2 = polygon3_offset + 2;
                const int edge3_3 = polygon3_offset + 3;

                const int polygon4_offset = polygon3_offset + polygon3_count;
                const int polygon4_count = 4;
                const int edge4_0 = polygon4_offset + 0;
                const int edge4_1 = polygon4_offset + 1;
                const int edge4_2 = polygon4_offset + 2;
                const int edge4_3 = polygon4_offset + 3;
                Debug.Assert(edge4_3 == kTotalHalfEdges - 1);

                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_0, vertexIndex = vertIndex0 }; // 0  (0-2)
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_0, vertexIndex = vertIndex1 }; // 1  (1-0)
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_0, vertexIndex = vertIndex2 }; // 2  (2-1)

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_2, vertexIndex = vertIndex4 }; // 3  (4-5)
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_2, vertexIndex = vertIndex3 }; // 4  (3-4)
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_2, vertexIndex = vertIndex5 }; // 5  (5-3)
            
                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_0, vertexIndex = vertIndex2 }; // 6  (2-0)
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_3, vertexIndex = vertIndex5 }; // 7  (5-2)
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_2, vertexIndex = vertIndex3 }; // 8  (3-5)
                halfEdges[edge2_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_1, vertexIndex = vertIndex0 }; // 9  (0-3)

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_1, vertexIndex = vertIndex0 }; // 10 (0-1)
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_3, vertexIndex = vertIndex3 }; // 11 (3-0)
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_1, vertexIndex = vertIndex4 }; // 12 (4-3)
                halfEdges[edge3_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_1, vertexIndex = vertIndex1 }; // 13 (1-4)

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_2, vertexIndex = vertIndex1 }; // 14 (1-2)
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_3, vertexIndex = vertIndex4 }; // 15 (4-1)
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_0, vertexIndex = vertIndex5 }; // 16 (5-4)
                halfEdges[edge4_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_1, vertexIndex = vertIndex2 }; // 17 (2-5)

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

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge = polygon0_offset, edgeCount = polygon0_count, descriptionIndex = 0, surface = surfaces[0] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge = polygon1_offset, edgeCount = polygon1_count, descriptionIndex = 1, surface = surfaces[1] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge = polygon2_offset, edgeCount = polygon2_count, descriptionIndex = 2, surface = surfaces[2] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = polygon3_offset, edgeCount = polygon3_count, descriptionIndex = 3, surface = surfaces[3] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = polygon4_offset, edgeCount = polygon4_count, descriptionIndex = 4, surface = surfaces[4] };

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