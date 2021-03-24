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
        public unsafe static void Convert(in BrushMesh.Polygon srcPolygon, ref BrushMeshBlob.Polygon dstPolygon)
        {
            dstPolygon.firstEdge        = srcPolygon.firstEdge;
            dstPolygon.edgeCount        = srcPolygon.edgeCount;
            dstPolygon.layerDefinition  = srcPolygon.surface?.brushMaterial?.LayerDefinition ?? SurfaceLayers.Empty;
            dstPolygon.UV0              = srcPolygon.surface?.surfaceDescription.UV0 ?? UVMatrix.identity;
        }

        public unsafe static BlobAssetReference<BrushMeshBlob> BuildBrushMeshBlob(BrushMesh brushMesh, Allocator allocator = Allocator.Persistent)
        {
            if (brushMesh == null ||
                brushMesh.vertices == null ||
                brushMesh.polygons == null ||
                brushMesh.halfEdges == null ||
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
            builder.Construct(ref root.halfEdges, brushMesh.halfEdges);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool		IsBrushMeshIDValid		(Int32 brushMeshHash)
        {
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

        public static Int32 CreateBrushMesh(BrushMesh brushMesh, Int32 oldBrushMeshHash = 0)
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

            ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
            {
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob
                {
                    refCount        = 1,
                    brushMeshBlob   = BuildBrushMeshBlob(brushMesh, Allocator.Persistent),
                };
            } else
                refCountedBrushMeshBlob.refCount++;

            brushMeshBlobs[brushMeshHash] = refCountedBrushMeshBlob;
            return brushMeshHash;
        }

        public static Int32 CreateBrushMesh(BlobAssetReference<BrushMeshBlob> brushMeshBlobRef, Int32 oldBrushMeshHash = 0)
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

            if (!brushMeshBlobs.TryGetValue(brushMeshHash, out var refCountedBrushMeshBlob))
            {
                refCountedBrushMeshBlob = new RefCountedBrushMeshBlob
                {
                    refCount = 1,
                    brushMeshBlob = brushMeshBlobRef,
                };
            } else
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
                    Chisel.Core.CompactHierarchyManager.NotifyBrushMeshRemoved(brushMeshHash);
                    refCountedBrushMeshBlob.brushMeshBlob.Dispose();
                    brushMeshBlobs.Remove(brushMeshHash);
                }
            }

            return true;
        }
    }
}
