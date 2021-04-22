using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public static partial class BrushMeshManager
    {
        internal static NativeHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobs; // same as ChiselMeshLookup.Value.brushMeshBlobs

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static NativeChiselSurface Convert(ChiselSurface surface)
        {
            return new NativeChiselSurface
            {
                layerDefinition     = surface?.brushMaterial?.LayerDefinition ?? SurfaceLayers.Empty,
                surfaceDescription  = surface?.surfaceDescription ?? SurfaceDescription.Default
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ChiselSurface Convert(NativeChiselSurface surface)
        {
            var materialManager = ChiselMaterialManager.Instance;
            return new ChiselSurface
            {
                brushMaterial       = new ChiselBrushMaterial() // TODO: try to find original BrushMaterial instead?
                {
                    LayerUsage      = surface.layerDefinition.layerUsage,
                    RenderMaterial  = materialManager.GetMaterial(surface.layerDefinition.layerParameter1),
                    PhysicsMaterial = materialManager.GetPhysicMaterial(surface.layerDefinition.layerParameter2)
                },
                surfaceDescription  = surface.surfaceDescription,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Convert(in BrushMesh.Polygon srcPolygon, ref BrushMeshBlob.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.descriptionIndex = srcPolygon.descriptionIndex;
            dstPolygon.surface          = Convert(srcPolygon.surface);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Convert(in BrushMeshBlob.Polygon srcPolygon, ref BrushMesh.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.descriptionIndex = srcPolygon.descriptionIndex;
            dstPolygon.surface          = Convert(srcPolygon.surface);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static BlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(NativeChiselSurface surface0,
                                                                                                          NativeChiselSurface surface1,
                                                                                                          NativeChiselSurface surface2,
                                                                                                          NativeChiselSurface surface3,
                                                                                                          NativeChiselSurface surface4,
                                                                                                          Allocator allocator)
        {
            var surfaceCount = 5;

            using (var builder = new BlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
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
        public unsafe static BlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(NativeChiselSurface surface0,
                                                                                                          NativeChiselSurface surface1,
                                                                                                          NativeChiselSurface surface2,
                                                                                                          NativeChiselSurface surface3,
                                                                                                          NativeChiselSurface surface4,
                                                                                                          NativeChiselSurface surface5,
                                                                                                          Allocator allocator)
        {
            var surfaceCount = 6;

            using (var builder = new BlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
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
        public unsafe static BlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(int surfaceCount, Allocator allocator)
        {
            using (var builder = new BlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces = builder.Allocate(ref root.surfaces, surfaceCount);
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static BlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(in ChiselSurfaceDefinition surfaceDefinition, Allocator allocator)
        {
            if (surfaceDefinition == null ||
                surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length < 1)
                return BlobAssetReference<NativeChiselSurfaceDefinition>.Null;

            var surfaceCount = surfaceDefinition.surfaces.Length;

            using (var builder = new BlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root    = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces    = builder.Allocate(ref root.surfaces, surfaceCount);
                for (int i = 0; i < surfaceCount; i++)
                    surfaces[i] = Convert(surfaceDefinition.surfaces[i]);
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static BlobAssetReference<NativeChiselSurfaceDefinition> BuildSurfaceDefinitionBlob(in ChiselSurface surface, int surfaceCount, Allocator allocator)
        {
            using (var builder = new BlobBuilder(Allocator.Temp, surfaceCount * UnsafeUtility.SizeOf<NativeChiselSurface>()))
            {
                ref var root    = ref builder.ConstructRoot<NativeChiselSurfaceDefinition>();
                var surfaces    = builder.Allocate(ref root.surfaces, surfaceCount);
                var nativeSurface = Convert(surface);
                for (int i = 0; i < surfaceCount; i++)
                    surfaces[i] = nativeSurface;
                return builder.CreateBlobAssetReference<NativeChiselSurfaceDefinition>(allocator);
            }
        }

        public unsafe static BlobAssetReference<BrushMeshBlob> ConvertToBrushMeshBlob(BrushMesh brushMesh, Allocator allocator = Allocator.Persistent)
        {
            if (brushMesh == null ||
                brushMesh.vertices == null ||
                brushMesh.polygons == null ||
                brushMesh.halfEdges == null ||
                brushMesh.halfEdgePolygonIndices == null ||
                brushMesh.vertices.Length < 4 ||
                brushMesh.polygons.Length < 4 ||
                brushMesh.halfEdges.Length < 12)
                return BlobAssetReference<BrushMeshBlob>.Null;

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
            var localBounds = new MinMaxAABB { Min = min, Max = max };

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = localBounds;
            builder.Construct(ref root.localVertices, srcVertices);
            var halfEdges = builder.Allocate(ref root.halfEdges, brushMesh.halfEdges.Length);
            for (int e = 0; e < brushMesh.halfEdges.Length; e++)
            {
                ref var srcHalfEdge = ref brushMesh.halfEdges[e];
                halfEdges[e].twinIndex   = srcHalfEdge.twinIndex;
                halfEdges[e].vertexIndex = srcHalfEdge.vertexIndex;
            }
            builder.Construct(ref root.halfEdgePolygonIndices, brushMesh.halfEdgePolygonIndices);
            var polygonArray = builder.Allocate(ref root.polygons, brushMesh.polygons.Length);
            for (int p = 0; p < brushMesh.polygons.Length; p++)
            {
                ref var srcPolygon = ref brushMesh.polygons[p];
                ref var dstPolygon = ref polygonArray[p];
                Convert(in srcPolygon, ref dstPolygon);
            }

            builder.Construct(ref root.localPlanes, brushMesh.planes);
            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }

        
        public unsafe static BrushMesh ConvertToBrushMesh(BlobAssetReference<BrushMeshBlob> brushMeshBlobRef, Allocator allocator = Allocator.Persistent)
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
                planes                  = brushMeshBlob.localPlanes.ToArray(),
                polygons                = new BrushMesh.Polygon[brushMeshBlob.polygons.Length]
            };

            for (int p = 0; p < brushMeshBlob.polygons.Length; p++)
            {
                ref var dstPolygon = ref brushMesh.polygons[p];
                ref var srcPolygon = ref brushMeshBlob.polygons[p];
                Convert(in srcPolygon, ref dstPolygon);
            }
            return brushMesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> Copy(BlobAssetReference<BrushMeshBlob> srcBrushMeshBlob, Allocator allocator)
        {
            ref var brushMesh = ref srcBrushMeshBlob.Value;
            
            var totalPolygonIndicesSize = 16 + (brushMesh.halfEdgePolygonIndices.Length * UnsafeUtility.SizeOf<int>());
            var totalHalfEdgeSize       = 16 + (brushMesh.halfEdges.Length      * UnsafeUtility.SizeOf<BrushMesh.HalfEdge>());
            var totalPolygonSize        = 16 + (brushMesh.polygons.Length       * UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>());
            var totalPlaneSize          = 16 + (brushMesh.localPlanes.Length    * UnsafeUtility.SizeOf<float4>());
            var totalVertexSize         = 16 + (brushMesh.localVertices.Length  * UnsafeUtility.SizeOf<float3>());
            var totalSize               = totalPlaneSize + totalPolygonSize + totalPolygonIndicesSize + totalHalfEdgeSize + totalVertexSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            root.localBounds = brushMesh.localBounds;
            builder.Construct(ref root.localVertices,           ref brushMesh.localVertices);
            builder.Construct(ref root.halfEdges,               ref brushMesh.halfEdges);
            builder.Construct(ref root.halfEdgePolygonIndices,  ref brushMesh.halfEdgePolygonIndices);
            builder.Construct(ref root.polygons,                ref brushMesh.polygons);
            builder.Construct(ref root.localPlanes,             ref brushMesh.localPlanes);

            var result = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
            builder.Dispose();
            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool		IsBrushMeshIDValid		(Int32 brushMeshHash)
        {
            if (!brushMeshBlobs.IsCreated)
                return false;
            return brushMeshBlobs.ContainsKey(brushMeshHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool			AssertBrushMeshIDValid	(Int32 brushMeshHash)
        {
            if (!IsBrushMeshIDValid(brushMeshHash))
            {
                Debug.LogError($"Invalid ID {brushMeshHash}");
                return false;
            }
            return true;
        }

        public static MinMaxAABB CalculateBounds(Int32 brushMeshHash, in float4x4 transformation)
        {
            if (!IsBrushMeshIDValid(brushMeshHash))
                return default;

            var brushMeshBlob = GetBrushMeshBlob(brushMeshHash);
            if (!brushMeshBlob.IsCreated)
                return default;

            return BoundsExtensions.Create(transformation, ref brushMeshBlob.Value.localVertices);
        }

        public static BlobAssetReference<BrushMeshBlob> GetBrushMeshBlob(Int32 brushMeshHash)
        {
            if (!AssertBrushMeshIDValid(brushMeshHash))
                return BlobAssetReference<BrushMeshBlob>.Null;
            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                return BlobAssetReference<BrushMeshBlob>.Null;
            return refCountedBrushMeshBlob.brushMeshBlob;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlobAssetReference<BrushMeshBlob> GetBrushMeshBlob(BrushMeshInstance instance)
        {
            return GetBrushMeshBlob(instance.brushMeshHash);
        }

        public static Int32 RegisterBrushMesh(BrushMesh brushMesh, Int32 oldBrushMeshHash = 0)
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

            int brushMeshHash = brushMesh.GetHashCode();
            if (oldBrushMeshHash != 0)
            {
                if (oldBrushMeshHash == brushMeshHash) return oldBrushMeshHash;
                DecreaseRefCount(oldBrushMeshHash);
            }

            var brushMeshBlobRef = ConvertToBrushMeshBlob(brushMesh, Allocator.Persistent);
            ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob { refCount = 1, brushMeshBlob = brushMeshBlobRef };
            else
                refCountedBrushMeshBlob.refCount++;
            brushMeshBlobs[brushMeshHash] = refCountedBrushMeshBlob;
            return brushMeshHash;
        }

        public static Int32 RegisterBrushMesh(BlobAssetReference<BrushMeshBlob> brushMeshBlobRef, Int32 oldBrushMeshHash = 0)
        {
            if (!brushMeshBlobRef.IsCreated)
                return 0;

            ref var brushMeshBlob   = ref brushMeshBlobRef.Value;
            var edgeCount           = brushMeshBlob.halfEdges.Length;
            var polygonCount        = brushMeshBlob.polygons.Length;
            var vertexCount         = brushMeshBlob.localVertices.Length;
            if (edgeCount < 12 || polygonCount < 4 || vertexCount < 4)
                return 0;

            int brushMeshHash = brushMeshBlob.GetHashCode();
            if (oldBrushMeshHash != 0)
            {
                if (oldBrushMeshHash == brushMeshHash) return oldBrushMeshHash;
                DecreaseRefCount(oldBrushMeshHash);
            }

            ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob { refCount = 1, brushMeshBlob = brushMeshBlobRef };
            else
                refCountedBrushMeshBlob.refCount++;
            brushMeshBlobs[brushMeshHash] = refCountedBrushMeshBlob;
            return brushMeshHash;
        }

        internal static bool DecreaseRefCount(Int32 brushMeshHash)
        {
            if (!AssertBrushMeshIDValid(brushMeshHash))
                return false;

            if (brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
            {
                refCountedBrushMeshBlob.refCount--;
                if (refCountedBrushMeshBlob.refCount <= 0)
                {
                    //Chisel.Core.CompactHierarchyManager.NotifyBrushMeshRemoved(brushMeshHash);
                    refCountedBrushMeshBlob.brushMeshBlob.Dispose();
                    brushMeshBlobs.Remove(brushMeshHash);
                }
            }
            return true;
        }
    }
}
