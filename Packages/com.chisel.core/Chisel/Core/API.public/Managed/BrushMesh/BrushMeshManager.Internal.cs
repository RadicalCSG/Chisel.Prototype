using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public static partial class BrushMeshManager
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeChiselSurface Convert(in ChiselSurface surface)
        {
            return new NativeChiselSurface
            {
                layerDefinition     = surface?.brushMaterial?.LayerDefinition ?? SurfaceLayers.Empty,
                surfaceDescription  = surface?.surfaceDescription ?? SurfaceDescription.Default
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeChiselSurface Convert(int surfaceIndex, in ChiselSurfaceDefinition chiselSurfaceDefinition)
        {
            ref readonly var surface = ref chiselSurfaceDefinition.surfaces[surfaceIndex];
            return new NativeChiselSurface
            {
                layerDefinition     = surface?.brushMaterial?.LayerDefinition ?? SurfaceLayers.Empty,
                surfaceDescription  = surface?.surfaceDescription ?? SurfaceDescription.Default
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(in BrushMesh.Polygon srcPolygon, ref NativeChiselSurface surfaceDefinition, ref BrushMeshBlob.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.descriptionIndex = srcPolygon.descriptionIndex;
            dstPolygon.surface          = surfaceDefinition;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(in BrushMesh.Polygon srcPolygon, in ChiselSurfaceDefinition surfaceDefinition, ref BrushMeshBlob.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.descriptionIndex = srcPolygon.descriptionIndex;
            dstPolygon.surface          = Convert(srcPolygon.descriptionIndex, in surfaceDefinition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(in BrushMeshBlob.Polygon srcPolygon, ref BrushMesh.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.descriptionIndex = srcPolygon.descriptionIndex;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(NativeChiselSurface surface0,
                                                                                                         NativeChiselSurface surface1,
                                                                                                         NativeChiselSurface surface2,
                                                                                                         NativeChiselSurface surface3,
                                                                                                         NativeChiselSurface surface4,
                                                                                                         Allocator allocator)
        {
            var surfaceCount = 5;

            using (var builder = new ChiselBlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root    = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces    = builder.Allocate(ref root.surfaces, surfaceCount);
                surfaces[0] = surface0;
                surfaces[1] = surface1;
                surfaces[2] = surface2;
                surfaces[3] = surface3;
                surfaces[4] = surface4;
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(NativeChiselSurface surface0,
                                                                                                         NativeChiselSurface surface1,
                                                                                                         NativeChiselSurface surface2,
                                                                                                         NativeChiselSurface surface3,
                                                                                                         NativeChiselSurface surface4,
                                                                                                         NativeChiselSurface surface5,
                                                                                                         Allocator allocator)
        {
            var surfaceCount = 6;

            using (var builder = new ChiselBlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root    = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces    = builder.Allocate(ref root.surfaces, surfaceCount);
                surfaces[0] = surface0;
                surfaces[1] = surface1;
                surfaces[2] = surface2;
                surfaces[3] = surface3;
                surfaces[4] = surface4;
                surfaces[5] = surface5;
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(int surfaceCount, Allocator allocator)
        {
            using (var builder = new ChiselBlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces = builder.Allocate(ref root.surfaces, surfaceCount);
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(in ChiselSurfaceDefinition surfaceDefinition, Allocator allocator)
        {
            if (surfaceDefinition == null ||
                surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length < 1)
                return ChiselBlobAssetReference<NativeChiselSurfaceDefinition>.Null;

            var surfaceCount = surfaceDefinition.surfaces.Length;
            using (var builder = new ChiselBlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root    = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces    = builder.Allocate(ref root.surfaces, surfaceCount);
                for (int i = 0; i < surfaceCount; i++)
                    surfaces[i] = Convert(in surfaceDefinition.surfaces[i]);
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        public static ChiselBlobAssetReference<BrushMeshBlob> ConvertToBrushMeshBlob(BrushMesh brushMesh, in ChiselSurfaceDefinition surfaceDefinition, Allocator allocator = Allocator.Persistent)
        {
            if (brushMesh == null ||
                brushMesh.vertices == null ||
                brushMesh.polygons == null ||
                brushMesh.halfEdges == null ||
                brushMesh.vertices.Length < 4 ||
                brushMesh.polygons.Length < 4 ||
                brushMesh.halfEdges.Length < 12)
                return ChiselBlobAssetReference<BrushMeshBlob>.Null;

            brushMesh.CalculatePlanes();
            brushMesh.UpdateHalfEdgePolygonIndices();

            var srcVertices             = brushMesh.vertices;
            
            var totalPolygonIndicesSize = 16 + (brushMesh.halfEdgePolygonIndices.Length * UnsafeUtility.SizeOf<int>());
            var totalHalfEdgeSize       = 16 + (brushMesh.halfEdges.Length * UnsafeUtility.SizeOf<BrushMesh.HalfEdge>());
            var totalPolygonSize        = 16 + (brushMesh.polygons.Length  * UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>());
            var totalPlaneSize          = 16 + (brushMesh.planes.Length    * UnsafeUtility.SizeOf<float4>());
            var totalVertexSize         = 16 + (srcVertices.Length         * UnsafeUtility.SizeOf<float3>());
            var totalSize               = totalPlaneSize + totalPolygonSize + totalPolygonIndicesSize + totalHalfEdgeSize + totalVertexSize;

            var min = srcVertices[0];
            var max = srcVertices[0];
            for (int i = 1; i < srcVertices.Length; i++)
            {
                min = math.min(min, srcVertices[i]);
                max = math.max(max, srcVertices[i]);
            }
            var localBounds = new ChiselAABB { Min = min, Max = max };

            var builder = new ChiselBlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = localBounds;
            
            var dstHalfEdges = builder.Allocate(ref root.halfEdges, brushMesh.halfEdges.Length);
            var hashedVertices = new HashedVertices(//CSGConstants.kVertexEqualEpsilonDouble, 
                                                    srcVertices.Length, Allocator.Temp);
            for (int e = 0; e < brushMesh.halfEdges.Length; e++)
            {
                ref var srcHalfEdge = ref brushMesh.halfEdges[e];
                dstHalfEdges[e].twinIndex = srcHalfEdge.twinIndex;
                dstHalfEdges[e].vertexIndex = hashedVertices.AddNoResize(srcVertices[srcHalfEdge.vertexIndex]);
            }
            //builder.Construct(ref root.localVertices, srcVertices);
            var dstVertices = builder.Allocate(ref root.localVertices, hashedVertices.Length);
            for (int i = 0; i < dstVertices.Length; i++)
                dstVertices[i] = hashedVertices[i];
            hashedVertices.Dispose();
            hashedVertices = default;

            //builder.Construct(ref root.localPlanes, brushMesh.planes);
            root.localPlaneCount = brushMesh.planes.Length;
            var localPlanes = builder.Allocate(ref root.localPlanes, brushMesh.planes.Length + brushMesh.halfEdges.Length);
            for (int e = 0; e < brushMesh.planes.Length; e++)
            {
                localPlanes[e] = brushMesh.planes[e];
            }

            // Add additional planes for vertex "inside brush" testing, by adding average planes at edges. 
            // This prevents vertices from being accepted when two planes that intersect at edges having very sharp angles, 
            // which would otherwise accept vertices that are far away from the brush.
            for (int e = 0, o = brushMesh.planes.Length; e < brushMesh.halfEdges.Length; e++, o++)
            {
                var vertexIndex     = brushMesh.halfEdges[e].vertexIndex;
                var twinIndex       = brushMesh.halfEdges[e].twinIndex;
                var polygonIndex1   = brushMesh.halfEdgePolygonIndices[e];
                var polygonIndex2   = brushMesh.halfEdgePolygonIndices[twinIndex];
                var vertex          = brushMesh.vertices[vertexIndex];
                var plane1          = brushMesh.planes[polygonIndex1];
                var plane2          = brushMesh.planes[polygonIndex2];
                var averageNormal   = math.normalize(plane1.xyz + plane2.xyz);
                var distanceToPlane = -math.dot(averageNormal, vertex);
                var averagePlane    = new float4(averageNormal, distanceToPlane);
                localPlanes[o]  = averagePlane;
            }

            builder.Construct(ref root.halfEdgePolygonIndices, brushMesh.halfEdgePolygonIndices);
            var polygonArray = builder.Allocate(ref root.polygons, brushMesh.polygons.Length);
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                ref var srcPolygon = ref brushMesh.polygons[p];
                ref var dstPolygon = ref polygonArray[p];
                Convert(in srcPolygon, in surfaceDefinition, ref dstPolygon);
            }

            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }

        // TODO: store handles separately?
        unsafe struct BrushMeshPointers
        {
            public int                  index;
            
            public int                  surfacesOffset;
            public int                  surfacesLength;

            [NativeDisableUnsafePtrRestriction, NoAlias]
            public float3*              vertices;
            public int                  verticesLength;
            public ulong                verticesGCHandle;

            [NativeDisableUnsafePtrRestriction, NoAlias]
            public BrushMesh.Polygon*   polygons;
            public int                  polygonsLength;
            public ulong                polygonsGCHandle;

            [NativeDisableUnsafePtrRestriction, NoAlias]
            public BrushMesh.HalfEdge*  halfEdges;
            public int                  halfEdgesLength;
            public ulong                halfEdgesGCHandle;
        }

        static void RegisterBrushMeshes(in NativeList<BrushMeshPointers> brushMeshPointers, in NativeArray<CSGTreeBrush> nativeTreeBrushes, in NativeArray<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshBlobs)
        {
            for (int i = 0; i < brushMeshPointers.Length; i++)
            {
                var brush = nativeTreeBrushes[brushMeshPointers[i].index];
                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshBlobs[i]) };
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        public unsafe static void ConvertBrushMeshesToBrushMeshInstances(List<CSGTreeBrush> rebuildTreeBrushes, List<BrushMesh> rebuildTreeBrushOutlines, List<ChiselSurfaceDefinition> surfaceDefinitions)
        {
            Profiler.BeginSample("ConvertBrushMeshesToBrushMeshInstances");
            var brushMeshPointers   = new NativeList<BrushMeshPointers>(rebuildTreeBrushes.Count, defaultAllocator);
            var brushMeshBlobs      = new NativeArray<ChiselBlobAssetReference<BrushMeshBlob>>(rebuildTreeBrushes.Count, defaultAllocator);
            var nativeTreeBrushes   = rebuildTreeBrushes.ToNativeArray(defaultAllocator);
            try
            {
                const Allocator allocator = Allocator.Persistent;
                var surfacesOffset = 0;
                Profiler.BeginSample("brushMesh.Validate");
                try
                {
                    for (int i = 0; i < rebuildTreeBrushes.Count; i++)
                    {
                        var brush = rebuildTreeBrushes[i];
                        if (!brush.Valid)
                            return;

                        var brushMesh = rebuildTreeBrushOutlines[i];
                        var surfaceDefinition = surfaceDefinitions[i];
                        if (brushMesh == null ||
                            brushMesh.vertices == null ||
                            brushMesh.polygons == null ||
                            brushMesh.halfEdges == null ||
                            brushMesh.vertices.Length < 4 ||
                            brushMesh.polygons.Length < 4 ||
                            brushMesh.halfEdges.Length < 12 ||
                            surfaceDefinition == null ||
                            surfaceDefinition.surfaces == null ||
                            surfaceDefinition.surfaces.Length < 1)
                        {
                            brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                            continue;
                        }

                        // TODO: eventually remove when it's more battle tested
                        if (!brushMesh.Validate(logErrors: true))
                        {
                            brush.BrushMesh = BrushMeshInstance.InvalidInstance;
                            continue;
                        }

                        var surfacesLength = surfaceDefinition.surfaces.Length;
                        brushMeshPointers.AddNoResize(new BrushMeshPointers
                        {
                            index               = i,

                            surfacesOffset      = surfacesOffset,
                            surfacesLength      = surfacesLength
                        });
                        surfacesOffset += surfacesLength;
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("PinBrushMeshPointers");
                try
                {
                    for (int i = 0; i < brushMeshPointers.Length; i++)
                    {
                        var index               = brushMeshPointers[i].index;
                        var brushMesh           = rebuildTreeBrushOutlines[index];
                        var surfaceDefinition   = surfaceDefinitions[index];

                        var verticesPtr     = (float3*)UnsafeUtility.PinGCArrayAndGetDataAddress(brushMesh.vertices, out var verticesGCHandle);
                        var polygonsPtr     = (BrushMesh.Polygon*)UnsafeUtility.PinGCArrayAndGetDataAddress(brushMesh.polygons, out var polygonsGCHandle);
                        var halfEdgesPtr    = (BrushMesh.HalfEdge*)UnsafeUtility.PinGCArrayAndGetDataAddress(brushMesh.halfEdges, out var halfEdgesGCHandle);

                        var temp = brushMeshPointers[i];

                        temp.vertices            = verticesPtr;
                        temp.verticesLength      = brushMesh.vertices.Length;
                        temp.verticesGCHandle    = verticesGCHandle;
                
                        temp.polygons            = polygonsPtr;
                        temp.polygonsLength      = brushMesh.polygons.Length;
                        temp.polygonsGCHandle    = polygonsGCHandle;
                
                        temp.halfEdges           = halfEdgesPtr;
                        temp.halfEdgesLength     = brushMesh.halfEdges.Length;
                        temp.halfEdgesGCHandle   = halfEdgesGCHandle;

                        brushMeshPointers[i] = temp;
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("ConvertSurfaces");
                var surfaces = new NativeList<NativeChiselSurface>(surfacesOffset, defaultAllocator);
                surfaces.ResizeUninitialized(surfacesOffset);
                var materialManager = ChiselMaterialManager.Instance;
                for (int i = 0; i < brushMeshPointers.Length; i++)
                {
                    var index               = brushMeshPointers[i].index;
                    var inputSurfaces       = surfaceDefinitions[index].surfaces;
                    var surfacesLength      = brushMeshPointers[i].surfacesLength;
                    var offset              = brushMeshPointers[i].surfacesOffset;
                    for (int s = 0, o = offset; s < surfacesLength; s++, o++)
                    {
                        var surface = inputSurfaces[s];
                        if (surface == null)
                        {
                            surfaces[o] = NativeChiselSurface.Default;
                            continue;
                        }
                        
                        var brushMaterial   = surface.brushMaterial;
                        var layerDefinition = brushMaterial != null ? materialManager.GetLayerDefinition(brushMaterial) : SurfaceLayers.Empty;

                        surfaces[o] = new NativeChiselSurface
                        {
                            layerDefinition     = layerDefinition,
                            surfaceDescription  = surface.surfaceDescription
                        };
                    }
                }
                Profiler.EndSample();

                try
                {
                    Profiler.BeginSample("ConvertToBrushMeshBlob");
                    var brushMeshBlobCache = ChiselMeshLookup.Value.brushMeshBlobCache;
                    var convertToBrushMeshBlobJob = new ConvertToBrushMeshBlobJob
                    {
                        brushMeshPointers   = brushMeshPointers.AsArray(),
                        surfaces            = surfaces,
                        //brushMeshBlobCache  = brushMeshBlobCache,
                        brushMeshBlobs      = brushMeshBlobs,
                        allocator           = allocator
                    };
                    var jobHandle = convertToBrushMeshBlobJob.Schedule(brushMeshPointers.Length, 16);
                    jobHandle.Complete();
                    surfaces.Dispose();
                    surfaces = default;
                    Profiler.EndSample();

                    // TODO: use ScheduleBrushRegistration
                    Profiler.BeginSample("RegisterBrushMeshes");
                    try
                    {
                        RegisterBrushMeshes(in brushMeshPointers, in nativeTreeBrushes, in brushMeshBlobs);
                    } finally { Profiler.EndSample(); }
                }
                finally
                {
                    Profiler.BeginSample("ReleaseBrushMeshPointers");
                    try
                    {
                        for (int i = 0; i < brushMeshPointers.Length; i++)
                        {
                            UnsafeUtility.ReleaseGCObject(brushMeshPointers[i].verticesGCHandle);
                            UnsafeUtility.ReleaseGCObject(brushMeshPointers[i].polygonsGCHandle);
                            UnsafeUtility.ReleaseGCObject(brushMeshPointers[i].halfEdgesGCHandle);
                        }
                    } finally { Profiler.EndSample(); }
                }
            }
            finally
            {
                nativeTreeBrushes.Dispose(); nativeTreeBrushes = default;
                brushMeshPointers.Dispose(); brushMeshPointers = default;
                brushMeshBlobs.Dispose(); brushMeshBlobs = default;
                Profiler.EndSample(); 
            }
        }

        [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
        unsafe struct ConvertToBrushMeshBlobJob : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<BrushMeshPointers>                           brushMeshPointers;
            [NoAlias, ReadOnly] public NativeList<NativeChiselSurface>                          surfaces;

            [NoAlias, WriteOnly] public NativeArray<ChiselBlobAssetReference<BrushMeshBlob>>    brushMeshBlobs;

            public Allocator allocator;

            
            // Per thread scratch memory
            [NativeDisableContainerSafetyRestriction] ChiselBlobBuilder builder;


            static float4 CalculatePlane([ReadOnly] BrushMeshBlob.Polygon polygon, [ReadOnly] in ChiselBlobBuilderArray<float3> vertices, [ReadOnly] in ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges)
            {
                // Newell's algorithm to create a plane for concave polygons.
                // NOTE: doesn't work well for self-intersecting polygons
                var lastEdge = polygon.firstEdge + polygon.edgeCount;
                var normal = double3.zero;
                var prevVertex = (double3)vertices[halfEdges[lastEdge - 1].vertexIndex];
                for (int n = polygon.firstEdge; n < lastEdge; n++)
                {
                    var currVertex = (double3)vertices[halfEdges[n].vertexIndex];
                    normal.x += ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                    normal.y += ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                    normal.z += ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
                    prevVertex = currVertex;
                }
                normal = math.normalize(normal);

                var d = 0.0;
                for (int n = polygon.firstEdge; n < lastEdge; n++)
                    d -= math.dot(normal, vertices[halfEdges[n].vertexIndex]);
                d /= polygon.edgeCount;

                return new float4((float3)normal, (float)d);
            }
            
            public void Execute(int index)
            {
                if (!builder.IsCreated)
                    builder = new ChiselBlobBuilder(Allocator.Temp);
                else
                    builder.Reset();

                var brushMesh = brushMeshPointers[index];
                var surfaceOffset = brushMesh.surfacesOffset;

                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();

                var dstHalfEdges    = builder.Allocate(ref root.halfEdges, brushMesh.halfEdgesLength);
                var hashedVertices  = new HashedVertices(brushMesh.verticesLength, Allocator.Temp);
                for (int e = 0; e < brushMesh.halfEdgesLength; e++)
                {
                    ref var srcHalfEdge = ref brushMesh.halfEdges[e];
                    dstHalfEdges[e].twinIndex = srcHalfEdge.twinIndex;
                    dstHalfEdges[e].vertexIndex = hashedVertices.AddNoResize(brushMesh.vertices[srcHalfEdge.vertexIndex]);
                }

                var uniqueVertexLength = hashedVertices.Length;
                var dstVertices = builder.Allocate(ref root.localVertices, uniqueVertexLength);
                if (uniqueVertexLength > 0)
                {
                    var vertex = hashedVertices[0];
                    var min = vertex;
                    var max = vertex;
                    dstVertices[0] = vertex;
                    for (int i = 1; i < uniqueVertexLength; i++)
                    {
                        vertex = hashedVertices[i];
                        min = math.min(min, vertex);
                        max = math.max(max, vertex);
                        dstVertices[i] = vertex;
                    }
                    var localBounds = new ChiselAABB { Min = min, Max = max };
                    root.localBounds = localBounds;
                } else
                {
                    var localBounds = new ChiselAABB { Min = float3.zero, Max = float3.zero };
                    root.localBounds = localBounds;
                }
                hashedVertices.Dispose();
                hashedVertices = default;

                var dstPolygons = builder.Allocate(ref root.polygons, brushMesh.polygonsLength);
                for (int p = 0; p < brushMesh.polygonsLength; p++)
                {
                    ref var srcPolygon = ref brushMesh.polygons[p];
                    ref var dstPolygon = ref dstPolygons[p];
                    Debug.Assert(srcPolygon.descriptionIndex < brushMesh.surfacesLength);
                    var nativeChiselSurface = srcPolygon.descriptionIndex < brushMesh.surfacesLength ? surfaces[surfaceOffset + srcPolygon.descriptionIndex] : NativeChiselSurface.Default;
                    Convert(in srcPolygon, ref nativeChiselSurface, ref dstPolygon);
                }

                root.localPlaneCount = brushMesh.polygonsLength;
                var localPlanes = builder.Allocate(ref root.localPlanes, brushMesh.polygonsLength + brushMesh.halfEdgesLength);
                for (int p = 0; p < brushMesh.polygonsLength; p++)
                    localPlanes[p] = CalculatePlane(dstPolygons[p], in dstVertices, in dstHalfEdges);
                
                var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, brushMesh.halfEdgesLength);
                for (int p = 0; p < brushMesh.polygonsLength; p++)
                {
                    var firstEdge = dstPolygons[p].firstEdge;
                    var edgeCount = dstPolygons[p].edgeCount;
                    var lastEdge = firstEdge + edgeCount;
                    for (int e = firstEdge; e < lastEdge; e++)
                        halfEdgePolygonIndices[e] = p;
                }

                // Add additional planes for vertex "inside brush" testing, by adding average planes at edges. 
                // This prevents vertices from being accepted when two planes that intersect at edges having very sharp angles, 
                // which would otherwise accept vertices that are far away from the brush.
                for (int e = 0, o = brushMesh.polygonsLength; e < brushMesh.halfEdgesLength; e++, o++)
                {
                    var vertexIndex     = dstHalfEdges[e].vertexIndex;
                    var twinIndex       = dstHalfEdges[e].twinIndex;
                    var polygonIndex1   = halfEdgePolygonIndices[e];
                    var polygonIndex2   = halfEdgePolygonIndices[twinIndex];
                    var vertex          = dstVertices[vertexIndex];
                    var plane1          = localPlanes[polygonIndex1];
                    var plane2          = localPlanes[polygonIndex2];
                    var averageNormal   = math.normalize(plane1.xyz + plane2.xyz);
                    var distanceToPlane = -math.dot(averageNormal, vertex);
                    var averagePlane    = new float4(averageNormal, distanceToPlane);
                    localPlanes[o] = averagePlane;
                }

                var newBrushMeshBlob = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                // TODO: use ScheduleBrushRegistration
                //var instance = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshBlobCache, newBrushMeshBlob) };
                brushMeshBlobs[index] = newBrushMeshBlob;
            }
        }

        
        
        public static BrushMesh ConvertToBrushMesh(ChiselBlobAssetReference<BrushMeshBlob> brushMeshBlobRef)
        {
            if (!brushMeshBlobRef.IsCreated)
                return null;

            ref var brushMeshBlob = ref brushMeshBlobRef.Value;
            if (brushMeshBlob.localVertices.Length < 4 ||
                brushMeshBlob.polygons.Length < 4 ||
                brushMeshBlob.halfEdges.Length < 12)
                return null;

            var brushMesh = new BrushMesh
            {
                vertices                = brushMeshBlob.localVertices.ToArray(),
                halfEdges               = BlobArrayExtensions.ToArray<BrushMeshBlob.HalfEdge, BrushMesh.HalfEdge>(ref brushMeshBlob.halfEdges),
                halfEdgePolygonIndices  = brushMeshBlob.halfEdgePolygonIndices.ToArray(),
                planes                  = new float4[brushMeshBlob.polygons.Length],
                polygons                = new BrushMesh.Polygon[brushMeshBlob.polygons.Length]
            };

            for (int p = 0; p < brushMeshBlob.polygons.Length; p++)
            {
                brushMesh.planes[p] = brushMeshBlob.localPlanes[p];
                ref var dstPolygon = ref brushMesh.polygons[p];
                ref var srcPolygon = ref brushMeshBlob.polygons[p];
                Convert(in srcPolygon, ref dstPolygon);
            }
            return brushMesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<BrushMeshBlob> Copy(ChiselBlobAssetReference<BrushMeshBlob> srcBrushMeshBlob, Allocator allocator)
        {
            ref var brushMesh = ref srcBrushMeshBlob.Value;
            
            var totalPolygonIndicesSize = 16 + (brushMesh.halfEdgePolygonIndices.Length * UnsafeUtility.SizeOf<int>());
            var totalHalfEdgeSize       = 16 + (brushMesh.halfEdges.Length      * UnsafeUtility.SizeOf<BrushMesh.HalfEdge>());
            var totalPolygonSize        = 16 + (brushMesh.polygons.Length       * UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>());
            var totalPlaneSize          = 16 + (brushMesh.localPlanes.Length    * UnsafeUtility.SizeOf<float4>());
            var totalVertexSize         = 16 + (brushMesh.localVertices.Length  * UnsafeUtility.SizeOf<float3>());
            var totalSize               = totalPlaneSize + totalPolygonSize + totalPolygonIndicesSize + totalHalfEdgeSize + totalVertexSize;

            var builder = new ChiselBlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = brushMesh.localBounds;
            builder.Construct(ref root.localVertices,           ref brushMesh.localVertices);
            builder.Construct(ref root.halfEdges,               ref brushMesh.halfEdges);
            builder.Construct(ref root.halfEdgePolygonIndices,  ref brushMesh.halfEdgePolygonIndices);
            builder.Construct(ref root.polygons,                ref brushMesh.polygons);
            builder.Construct(ref root.localPlanes,             ref brushMesh.localPlanes);
            root.localPlaneCount = brushMesh.localPlaneCount;

            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool		IsBrushMeshIDValid		(Int32 brushMeshHash)
        {
            return IsBrushMeshIDValid(ChiselMeshLookup.Value.brushMeshBlobCache, brushMeshHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool		IsBrushMeshIDValid		(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, Int32 brushMeshHash)
        {
            if (!brushMeshBlobCache.IsCreated)
                return false;
            return brushMeshBlobCache.ContainsKey(brushMeshHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AssertBrushMeshIDValid(Int32 brushMeshHash)
        {
            return AssertBrushMeshIDValid(ChiselMeshLookup.Value.brushMeshBlobCache, brushMeshHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool			AssertBrushMeshIDValid	(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, Int32 brushMeshHash)
        {
            if (!IsBrushMeshIDValid(brushMeshBlobCache, brushMeshHash))
            {
                Debug.LogError($"Invalid ID {brushMeshHash}");
                return false;
            }
            return true;
        }

        public static ChiselAABB CalculateBounds(Int32 brushMeshHash, in float4x4 transformation)
        {
            if (!IsBrushMeshIDValid(brushMeshHash))
                return default;

            var brushMeshBlob = GetBrushMeshBlob(brushMeshHash);
            if (!brushMeshBlob.IsCreated)
                return default;

            return BoundsExtensions.Create(ref brushMeshBlob.Value.localVertices, transformation);
        }

        public static ChiselBlobAssetReference<BrushMeshBlob> GetBrushMeshBlob(Int32 brushMeshHash)
        {
            if (!AssertBrushMeshIDValid(brushMeshHash))
                return ChiselBlobAssetReference<BrushMeshBlob>.Null;
            var brushMeshBlobCache = ChiselMeshLookup.Value.brushMeshBlobCache;
            if (!brushMeshBlobCache.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                return ChiselBlobAssetReference<BrushMeshBlob>.Null;
            return refCountedBrushMeshBlob.brushMeshBlob;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChiselBlobAssetReference<BrushMeshBlob> GetBrushMeshBlob(BrushMeshInstance instance)
        {
            return GetBrushMeshBlob(instance.brushMeshHash);
        }

        public static Int32 RegisterBrushMesh(BrushMesh brushMesh, in ChiselSurfaceDefinition surfaceDefinition, Int32 oldBrushMeshHash = 0)
        {
            if (brushMesh			== null ||
                brushMesh.vertices	== null ||
                brushMesh.halfEdges	== null ||
                brushMesh.polygons	== null)
                return 0;

            var edgeCount       = brushMesh.halfEdges.Length;
            var polygonCount    = brushMesh.polygons.Length;
            var vertexCount     = brushMesh.vertices.Length;
            if (edgeCount < 12 || polygonCount < 4 || vertexCount < 4)
                return 0;

            var brushMeshBlobRef = ConvertToBrushMeshBlob(brushMesh, in surfaceDefinition, Allocator.Persistent);
            var brushMeshHash = brushMeshBlobRef.Value.GetHashCode();
            if (oldBrushMeshHash != 0)
            {
                if (oldBrushMeshHash == brushMeshHash) return oldBrushMeshHash;
                DecreaseRefCount(oldBrushMeshHash);
            }

            ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;
            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob { refCount = 1, brushMeshBlob = brushMeshBlobRef };
            else
                refCountedBrushMeshBlob.refCount++;
            brushMeshBlobs[brushMeshHash] = refCountedBrushMeshBlob;
            return brushMeshHash;
        }

        internal static void RegisterBrushMeshHash(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, Int32 newBrushMeshHash, Int32 oldBrushMeshHash = 0)
        {
            if (oldBrushMeshHash != 0)
            {
                if (oldBrushMeshHash == newBrushMeshHash)
                    return;
                DecreaseRefCount(brushMeshBlobCache, oldBrushMeshHash);
            }

            if (newBrushMeshHash == BrushMeshInstance.InvalidInstance.BrushMeshID)
                return;
            if (!brushMeshBlobCache.TryGetValue(newBrushMeshHash, out var refCountedBrushMeshBlob))
                throw new InvalidOperationException($"Unknown brushMeshHash used ({newBrushMeshHash})");
            
            refCountedBrushMeshBlob.refCount++;
            brushMeshBlobCache[newBrushMeshHash] = refCountedBrushMeshBlob;
        }

        internal static Int32 RegisterBrushMesh(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, ChiselBlobAssetReference<BrushMeshBlob> brushMeshBlobRef, Int32 oldBrushMeshHash = 0)
        {
            if (!brushMeshBlobRef.IsCreated)
                return 0;

            ref var brushMeshBlob = ref brushMeshBlobRef.Value;
            var edgeCount = brushMeshBlob.halfEdges.Length;
            var polygonCount = brushMeshBlob.polygons.Length;
            var vertexCount = brushMeshBlob.localVertices.Length;
            if (edgeCount < 12 || polygonCount < 4 || vertexCount < 4)
                return 0;

            int brushMeshHash = BrushMeshBlob.CalculateHashCode(ref brushMeshBlob);
            if (oldBrushMeshHash != 0)
            {
                if (oldBrushMeshHash == brushMeshHash) return oldBrushMeshHash;
                DecreaseRefCount(brushMeshBlobCache, oldBrushMeshHash);
            }

            if (!brushMeshBlobCache.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob { refCount = 1, brushMeshBlob = brushMeshBlobRef };
            else
                refCountedBrushMeshBlob.refCount++;
            brushMeshBlobCache[brushMeshHash] = refCountedBrushMeshBlob;
            return brushMeshHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int32 RegisterBrushMesh(ChiselBlobAssetReference<BrushMeshBlob> brushMeshBlobRef, Int32 oldBrushMeshHash = 0)
        {
            return RegisterBrushMesh(ChiselMeshLookup.Value.brushMeshBlobCache, brushMeshBlobRef, oldBrushMeshHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool DecreaseRefCount(Int32 brushMeshHash)
        {
            return DecreaseRefCount(ChiselMeshLookup.Value.brushMeshBlobCache, brushMeshHash);
        }

        internal static bool DecreaseRefCount(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, Int32 brushMeshHash)
        {
            if (brushMeshBlobCache.IsCreated && brushMeshBlobCache.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
            {
                refCountedBrushMeshBlob.refCount--;
                if (refCountedBrushMeshBlob.refCount <= 0)
                {
                    refCountedBrushMeshBlob.refCount = 0;
                    //Chisel.Core.CompactHierarchyManager.NotifyBrushMeshRemoved(brushMeshHash);
                    refCountedBrushMeshBlob.brushMeshBlob.Dispose();
                    refCountedBrushMeshBlob.brushMeshBlob = default;
                    brushMeshBlobCache.Remove(brushMeshHash);
                }
            } else
            {
                Debug.LogError($"Invalid ID {brushMeshHash}");
                return false;
            }
            return true;
        }

        [BurstCompile(CompileSynchronously = true)]
        struct RegisterBrushMeshesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>>    brushMeshBlobs;

            [NoAlias] public NativeList<GeneratedNodeDefinition>            generatedNodeDefinition;
            [NoAlias] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>    brushMeshBlobCache;

            // TODO: this is based on RegisterBrushMesh in BrushMeshManager, remove redundancy
            internal Int32 RegisterBrushMesh(NativeParallelHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache, ChiselBlobAssetReference<BrushMeshBlob> brushMeshBlobRef)
            {
                if (brushMeshBlobRef == ChiselBlobAssetReference<BrushMeshBlob>.Null || !brushMeshBlobRef.IsCreated)
                    return BrushMeshInstance.InvalidInstance.BrushMeshID;

                ref var brushMeshBlob = ref brushMeshBlobRef.Value;
                var edgeCount       = brushMeshBlob.halfEdges.Length;
                var polygonCount    = brushMeshBlob.polygons.Length;
                var vertexCount     = brushMeshBlob.localVertices.Length;
                if (edgeCount < 12 || polygonCount < 4 || vertexCount < 4)
                    return BrushMeshInstance.InvalidInstance.BrushMeshID;

                int brushMeshHash = brushMeshBlob.GetHashCode();
                if (!brushMeshBlobCache.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                    refCountedBrushMeshBlob = new RefCountedBrushMeshBlob { refCount = 1, brushMeshBlob = brushMeshBlobRef };
                else
                    refCountedBrushMeshBlob.refCount++;
                brushMeshBlobCache[brushMeshHash] = refCountedBrushMeshBlob;

                return brushMeshHash;
            }

            public void Execute()
            {
                for (int i = 0; i < brushMeshBlobs.Length; i++)
                {
                    var brushMeshBlobRef = brushMeshBlobs[i];
                    var brushMeshHash = RegisterBrushMesh(brushMeshBlobCache, brushMeshBlobRef);
                    var definition = generatedNodeDefinition[i];
                    definition.brushMeshHash = brushMeshHash;
                    generatedNodeDefinition[i] = definition;
                }
            }
        }

        internal static JobHandle ScheduleBrushRegistration(bool runInParallel,
                                                            [NoAlias, ReadOnly] NativeList<ChiselBlobAssetReference<BrushMeshBlob>>   brushMeshBlobs,
                                                            [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>            generatedNodeDefinitions, 
                                                            JobHandle dependsOn)
        {
            JobExtensions.CheckDependencies(runInParallel, dependsOn);
            if (!runInParallel)
            {
                Debug.Assert(generatedNodeDefinitions.IsCreated);
            }
            var brushMeshBlobCache = ChiselMeshLookup.Value.brushMeshBlobCache;
            var registerBrushesJob = new RegisterBrushMeshesJob
            {
                brushMeshBlobs          = brushMeshBlobs,
                generatedNodeDefinition = generatedNodeDefinitions,
                brushMeshBlobCache      = brushMeshBlobCache
            };
            return registerBrushesJob.Schedule(runInParallel, dependsOn);
        }   
    }
}
