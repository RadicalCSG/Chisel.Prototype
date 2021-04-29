using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Entities.UniversalDelegates;
using UnityEngine.Profiling;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public enum BoxSides
        {
            Top     = 0,
            Bottom  = 1,
            Right   = 2,
            Left    = 3,
            Back    = 4,
            Front   = 5,
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CreateBox(float3 min, float3 max, 
                                     in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition, 
                                     out BlobAssetReference<BrushMeshBlob> brushMesh,
                                     Allocator allocator)
        {
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
            if (surfaceDefinition == BlobAssetReference<NativeChiselSurfaceDefinition>.Null)
                return false;

            ref var surfaces = ref surfaceDefinition.Value.surfaces;
            if (surfaces.Length < 6)
                return false;

            if (!BoundsExtensions.IsValid(min, max))
                return false;

            if (min.x > max.x) { float x = min.x; min.x = max.x; max.x = x; }
            if (min.y > max.y) { float y = min.y; min.y = max.y; max.y = y; }
            if (min.z > max.z) { float z = min.z; min.z = max.z; max.z = z; }

            var vertex0 = new float3(min.x, min.y, max.z);
            var vertex1 = new float3(min.x, min.y, min.z);
            var vertex2 = new float3(max.x, min.y, min.z);
            var vertex3 = new float3(max.x, min.y, max.z);
            var vertex4 = new float3(min.x, max.y, max.z);
            var vertex5 = new float3(min.x, max.y, min.z);
            var vertex6 = new float3(max.x, max.y, min.z);
            var vertex7 = new float3(max.x, max.y, max.z);

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                const int kTotalVertices    = 8;
                const int kTotalHalfEdges   = 24;
                const int kTotalPolygons    = 6;

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
                const int vertIndex6 = 6;
                const int vertIndex7 = 7;
                Debug.Assert(vertIndex7 == kTotalVertices - 1);

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;
                localVertices[vertIndex4] = vertex4;
                localVertices[vertIndex5] = vertex5;
                localVertices[vertIndex6] = vertex6;
                localVertices[vertIndex7] = vertex7;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;
                const int polygon4 = 4;
                const int polygon5 = 5;
                Debug.Assert(polygon5 == kTotalPolygons - 1);

                const int polygon0_offset = 0;
                const int edge0_0 = polygon0_offset + 0;
                const int edge0_1 = polygon0_offset + 1;
                const int edge0_2 = polygon0_offset + 2;
                const int edge0_3 = polygon0_offset + 3;

                const int polygon1_offset = polygon0_offset + 4;
                const int edge1_0 = polygon1_offset + 0;
                const int edge1_1 = polygon1_offset + 1;
                const int edge1_2 = polygon1_offset + 2; 
                const int edge1_3 = polygon1_offset + 3;

                const int polygon2_offset = polygon1_offset + 4;
                const int edge2_0 = polygon2_offset + 0;
                const int edge2_1 = polygon2_offset + 1;
                const int edge2_2 = polygon2_offset + 2;
                const int edge2_3 = polygon2_offset + 3;

                const int polygon3_offset = polygon2_offset + 4;
                const int edge3_0 = polygon3_offset + 0;
                const int edge3_1 = polygon3_offset + 1;
                const int edge3_2 = polygon3_offset + 2;
                const int edge3_3 = polygon3_offset + 3;

                const int polygon4_offset = polygon3_offset + 4;
                const int edge4_0 = polygon4_offset + 0;
                const int edge4_1 = polygon4_offset + 1;
                const int edge4_2 = polygon4_offset + 2;
                const int edge4_3 = polygon4_offset + 3;

                const int polygon5_offset = polygon4_offset + 4;
                const int edge5_0 = polygon5_offset + 0;
                const int edge5_1 = polygon5_offset + 1;
                const int edge5_2 = polygon5_offset + 2;
                const int edge5_3 = polygon5_offset + 3;
                Debug.Assert(edge5_3 == kTotalHalfEdges - 1);



                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_3, vertexIndex = vertIndex1 };
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_2, vertexIndex = vertIndex0 };
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_1, vertexIndex = vertIndex4 };
                halfEdges[edge0_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_0, vertexIndex = vertIndex5 };

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_1, vertexIndex = vertIndex3 };
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_2, vertexIndex = vertIndex0 };
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_1, vertexIndex = vertIndex1 };
                halfEdges[edge1_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_2, vertexIndex = vertIndex2 };

                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_3, vertexIndex = vertIndex6 };
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_2, vertexIndex = vertIndex2 };
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_3, vertexIndex = vertIndex1 };
                halfEdges[edge2_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_0, vertexIndex = vertIndex5 };

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_3, vertexIndex = vertIndex3 };
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_0, vertexIndex = vertIndex2 };
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_1, vertexIndex = vertIndex6 };
                halfEdges[edge3_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_2, vertexIndex = vertIndex7 };

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_3, vertexIndex = vertIndex4 };
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_0, vertexIndex = vertIndex7 };
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_3, vertexIndex = vertIndex6 };
                halfEdges[edge4_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_0, vertexIndex = vertIndex5 };

                // polygon 5
                halfEdges[edge5_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_1, vertexIndex = vertIndex4 };
                halfEdges[edge5_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_2, vertexIndex = vertIndex0 };
                halfEdges[edge5_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_1, vertexIndex = vertIndex3 };
                halfEdges[edge5_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_0, vertexIndex = vertIndex7 };

                halfEdgePolygonIndices[edge0_0] = polygon0;
                halfEdgePolygonIndices[edge0_1] = polygon0;
                halfEdgePolygonIndices[edge0_2] = polygon0;
                halfEdgePolygonIndices[edge0_3] = polygon0;

                halfEdgePolygonIndices[edge1_0] = polygon1;
                halfEdgePolygonIndices[edge1_1] = polygon1;
                halfEdgePolygonIndices[edge1_2] = polygon1;
                halfEdgePolygonIndices[edge1_3] = polygon1;

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

                halfEdgePolygonIndices[edge5_0] = polygon5;
                halfEdgePolygonIndices[edge5_1] = polygon5;
                halfEdgePolygonIndices[edge5_2] = polygon5;
                halfEdgePolygonIndices[edge5_3] = polygon5;

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge = polygon0 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Left,    surface = surfaces[(int)BoxSides.Left  ] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge = polygon1 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Bottom,  surface = surfaces[(int)BoxSides.Bottom] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge = polygon2 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Back,    surface = surfaces[(int)BoxSides.Back  ] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = polygon3 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Right,   surface = surfaces[(int)BoxSides.Right ] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = polygon4 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Top,     surface = surfaces[(int)BoxSides.Top   ] };
                polygons[polygon5] = new BrushMeshBlob.Polygon { firstEdge = polygon5 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Front,   surface = surfaces[(int)BoxSides.Front ] };

                // xyz is the normal, w is the distance, in the inverse direction of the normal, to the origin
                localPlanes[polygon0] = new float4(-1f,  0f,  0f,  min.x);
                localPlanes[polygon1] = new float4( 0f, -1f,  0f,  min.y);
                localPlanes[polygon2] = new float4( 0f,  0f, -1f,  min.z); 
                localPlanes[polygon3] = new float4( 1f,  0f,  0f, -max.x);
                localPlanes[polygon4] = new float4( 0f,  1f,  0f, -max.y);
                localPlanes[polygon5] = new float4( 0f,  0f,  1f, -max.z);
                
                root.localBounds = new MinMaxAABB { Min = min, Max = max };
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                return true;
            }
        }

        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> CreateBox(float3 vertex0,
                                                                  float3 vertex1,
                                                                  float3 vertex2,
                                                                  float3 vertex3,
                                                                  float3 vertex4,
                                                                  float3 vertex5,
                                                                  float3 vertex6,
                                                                  float3 vertex7,
                                                                  in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition, 
                                                                  Allocator allocator)
        {
            if (surfaceDefinition == BlobAssetReference<NativeChiselSurfaceDefinition>.Null)
                return BlobAssetReference<BrushMeshBlob>.Null;

            ref var surfaces = ref surfaceDefinition.Value.surfaces;
            if (surfaces.Length < 6)
                return BlobAssetReference<BrushMeshBlob>.Null;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var localVertices           = builder.Allocate(ref root.localVertices,           8);
                var halfEdges               = builder.Allocate(ref root.halfEdges,              24);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, 24);
                var polygons                = builder.Allocate(ref root.polygons,                6);
                var localPlanes             = builder.Allocate(ref root.localPlanes,             6);
                
                const int vertIndex0 = 0;
                const int vertIndex1 = 1;
                const int vertIndex2 = 2;
                const int vertIndex3 = 3;
                const int vertIndex4 = 4;
                const int vertIndex5 = 5;
                const int vertIndex6 = 6;
                const int vertIndex7 = 7;

                localVertices[vertIndex0] = vertex0;
                localVertices[vertIndex1] = vertex1;
                localVertices[vertIndex2] = vertex2;
                localVertices[vertIndex3] = vertex3;
                localVertices[vertIndex4] = vertex4;
                localVertices[vertIndex5] = vertex5;
                localVertices[vertIndex6] = vertex6;
                localVertices[vertIndex7] = vertex7;

                const int polygon0 = 0;
                const int polygon1 = 1;
                const int polygon2 = 2;
                const int polygon3 = 3;
                const int polygon4 = 4;
                const int polygon5 = 5;

                const int edge0_0 = (polygon0 * 4) + 0;
                const int edge0_1 = (polygon0 * 4) + 1;
                const int edge0_2 = (polygon0 * 4) + 2;
                const int edge0_3 = (polygon0 * 4) + 3;

                const int edge1_0 = (polygon1 * 4) + 0;
                const int edge1_1 = (polygon1 * 4) + 1;
                const int edge1_2 = (polygon1 * 4) + 2; 
                const int edge1_3 = (polygon1 * 4) + 3;

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

                const int edge5_0 = (polygon5 * 4) + 0;
                const int edge5_1 = (polygon5 * 4) + 1;
                const int edge5_2 = (polygon5 * 4) + 2;
                const int edge5_3 = (polygon5 * 4) + 3;



                // polygon 0
                halfEdges[edge0_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_3, vertexIndex = vertIndex1 };
                halfEdges[edge0_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_2, vertexIndex = vertIndex0 };
                halfEdges[edge0_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_1, vertexIndex = vertIndex4 };
                halfEdges[edge0_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_0, vertexIndex = vertIndex5 };

                // polygon 1
                halfEdges[edge1_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_1, vertexIndex = vertIndex3 };
                halfEdges[edge1_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_2, vertexIndex = vertIndex0 };
                halfEdges[edge1_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_1, vertexIndex = vertIndex1 };
                halfEdges[edge1_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_2, vertexIndex = vertIndex2 };

                // polygon 2
                halfEdges[edge2_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_3, vertexIndex = vertIndex6 };
                halfEdges[edge2_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_2, vertexIndex = vertIndex2 };
                halfEdges[edge2_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_3, vertexIndex = vertIndex1 };
                halfEdges[edge2_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_0, vertexIndex = vertIndex5 };

                // polygon 3
                halfEdges[edge3_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_3, vertexIndex = vertIndex3 };
                halfEdges[edge3_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_0, vertexIndex = vertIndex2 };
                halfEdges[edge3_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_1, vertexIndex = vertIndex6 };
                halfEdges[edge3_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_2, vertexIndex = vertIndex7 };

                // polygon 4
                halfEdges[edge4_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_3, vertexIndex = vertIndex4 };
                halfEdges[edge4_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge5_0, vertexIndex = vertIndex7 };
                halfEdges[edge4_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_3, vertexIndex = vertIndex6 };
                halfEdges[edge4_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge2_0, vertexIndex = vertIndex5 };

                // polygon 5
                halfEdges[edge5_0] = new BrushMeshBlob.HalfEdge { twinIndex = edge4_1, vertexIndex = vertIndex4 };
                halfEdges[edge5_1] = new BrushMeshBlob.HalfEdge { twinIndex = edge0_2, vertexIndex = vertIndex0 };
                halfEdges[edge5_2] = new BrushMeshBlob.HalfEdge { twinIndex = edge1_1, vertexIndex = vertIndex3 };
                halfEdges[edge5_3] = new BrushMeshBlob.HalfEdge { twinIndex = edge3_0, vertexIndex = vertIndex7 };

                halfEdgePolygonIndices[edge0_0] = polygon0;
                halfEdgePolygonIndices[edge0_1] = polygon0;
                halfEdgePolygonIndices[edge0_2] = polygon0;
                halfEdgePolygonIndices[edge0_3] = polygon0;

                halfEdgePolygonIndices[edge1_0] = polygon1;
                halfEdgePolygonIndices[edge1_1] = polygon1;
                halfEdgePolygonIndices[edge1_2] = polygon1;
                halfEdgePolygonIndices[edge1_3] = polygon1;

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

                halfEdgePolygonIndices[edge5_0] = polygon5;
                halfEdgePolygonIndices[edge5_1] = polygon5;
                halfEdgePolygonIndices[edge5_2] = polygon5;
                halfEdgePolygonIndices[edge5_3] = polygon5;

                polygons[polygon0] = new BrushMeshBlob.Polygon { firstEdge = polygon0 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Left,    surface = surfaces[(int)BoxSides.Left  ] };
                polygons[polygon1] = new BrushMeshBlob.Polygon { firstEdge = polygon1 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Bottom,  surface = surfaces[(int)BoxSides.Bottom] };
                polygons[polygon2] = new BrushMeshBlob.Polygon { firstEdge = polygon2 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Back,    surface = surfaces[(int)BoxSides.Back  ] };
                polygons[polygon3] = new BrushMeshBlob.Polygon { firstEdge = polygon3 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Right,   surface = surfaces[(int)BoxSides.Right ] };
                polygons[polygon4] = new BrushMeshBlob.Polygon { firstEdge = polygon4 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Top,     surface = surfaces[(int)BoxSides.Top   ] };
                polygons[polygon5] = new BrushMeshBlob.Polygon { firstEdge = polygon5 * 4, edgeCount = 4, descriptionIndex = (int)BoxSides.Front,   surface = surfaces[(int)BoxSides.Front ] };

                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                root.localBounds = CalculateBounds(in localVertices);
                return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            }
        }

        public static void CreateBoxVertices(Vector3 min, Vector3 max, ref Vector3[] vertices)
        {
            if (vertices == null ||
                vertices.Length != 8)
                vertices = new Vector3[8];

            vertices[0] = new Vector3(min.x, max.y, min.z); // 0
            vertices[1] = new Vector3(max.x, max.y, min.z); // 1
            vertices[2] = new Vector3(max.x, max.y, max.z); // 2
            vertices[3] = new Vector3(min.x, max.y, max.z); // 3

            vertices[4] = new Vector3(min.x, min.y, min.z); // 4  
            vertices[5] = new Vector3(max.x, min.y, min.z); // 5
            vertices[6] = new Vector3(max.x, min.y, max.z); // 6
            vertices[7] = new Vector3(min.x, min.y, max.z); // 7
        }

        // TODO: do not use this version unless we have no choice ..
        public static Vector3[] CreateBoxVertices(Vector3 min, Vector3 max)
        {
            Vector3[] vertices = null;
            CreateBoxVertices(min, max, ref vertices);
            return vertices;
        }

        public static void CreateBox(Vector3 min, Vector3 max, int descriptionIndex, out BrushMesh box)
        {
            if (!BoundsExtensions.IsValid(min, max))
            {
                box = default;
                return;
            }

            if (min.x > max.x) { float x = min.x; min.x = max.x; max.x = x; }
            if (min.y > max.y) { float y = min.y; min.y = max.y; max.y = y; }
            if (min.z > max.z) { float z = min.z; min.z = max.z; max.z = z; }

            var vec_vertices = CreateBoxVertices(min, max);
            var vertices = new float3[vec_vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = vec_vertices[i];

            box = new BrushMesh
            {
                polygons	= CreateBoxPolygons(descriptionIndex),
                halfEdges	= boxHalfEdges.ToArray(),
                vertices	= vertices
            };
        }

        /// <summary>
        /// Creates a box <see cref="Chisel.Core.BrushMesh"/> with <paramref name="size"/> and optional <paramref name="material"/>
        /// </summary>
        /// <param name="size">The size of the box</param>
        /// <param name="material">The [UnityEngine.Material](https://docs.unity3d.com/ScriptReference/Material.html) that will be set to all surfaces of the box (optional)</param>
        /// <returns>A <see cref="Chisel.Core.BrushMesh"/> on success, null on failure</returns>
        public static void CreateBox(Vector3 size, int descriptionIndex, out BrushMesh box)
        {
            var halfSize = size * 0.5f;
            CreateBox(-halfSize, halfSize, descriptionIndex, out box);
        }
    }
}