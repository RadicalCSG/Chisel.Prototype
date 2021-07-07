using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    internal sealed unsafe class ChiselTreeLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public NativeList<CompactNodeID>                brushIDValues;
            public NativeArray<ChiselLayerParameters>       parameters;
            public NativeHashSet<int>                       allKnownBrushMeshIndices;

            public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
            public NativeList<BlobAssetReference<RoutingTable>>                 routingTableCache;
            public NativeList<NodeTransformations>                              transformationCache;
            public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
            public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;
            public NativeList<MinMaxAABB>                                       brushTreeSpaceBoundCache;
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
            
            public NativeHashMap<CompactNodeID, MinMaxAABB>                                     brushTreeSpaceBoundLookup;
            public NativeHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferLookup;

            internal unsafe void Initialize()
            {
                brushIDValues               = new NativeList<CompactNodeID>(1000, Allocator.Persistent);
                allKnownBrushMeshIndices    = new NativeHashSet<int>(1000, Allocator.Persistent);

                // TODO: not used??
                brushTreeSpaceBoundLookup   = new NativeHashMap<CompactNodeID, MinMaxAABB>(1000, Allocator.Persistent);
                brushRenderBufferLookup     = new NativeHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                // brushIndex
                basePolygonCache            = new NativeList<BlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent);
                brushTreeSpaceBoundCache    = new NativeList<MinMaxAABB>(1000, Allocator.Persistent);
                treeSpaceVerticesCache      = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(1000, Allocator.Persistent);
                routingTableCache           = new NativeList<BlobAssetReference<RoutingTable>>(1000, Allocator.Persistent);
                brushTreeSpacePlaneCache    = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent);
                brushesTouchedByBrushCache  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent);
                transformationCache         = new NativeList<NodeTransformations>(1000, Allocator.Persistent);
                brushRenderBufferCache      = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                parameters                  = new NativeArray<ChiselLayerParameters>(SurfaceLayers.ParameterCount, Allocator.Persistent);
                // Regular index operator will return a copy instead of a reference *sigh*
                var parameterPtr = parameters.GetUnsafePtr();
                for (int i = 0; i < parameters.Length; i++)
                {
                    ref var parameter = ref UnsafeUtility.ArrayElementAsRef<ChiselLayerParameters>(parameterPtr, i);
                    parameter.Initialize(); 
                    Debug.Assert(parameter.IsCreated);
                }
            }

            internal void EnsureCapacity(int brushCount)
            {
                if (brushTreeSpaceBoundLookup.Capacity < brushCount)
                    brushTreeSpaceBoundLookup.Capacity = brushCount;

                if (brushRenderBufferLookup.Capacity < brushCount)
                    brushRenderBufferLookup.Capacity = brushCount;

                if (brushIDValues.Capacity < brushCount)
                    brushIDValues.Capacity = brushCount;

                if (allKnownBrushMeshIndices.Capacity < brushCount)
                    allKnownBrushMeshIndices.Capacity = brushCount;

                if (basePolygonCache.Capacity < brushCount)
                    basePolygonCache.Capacity = brushCount;

                if (brushTreeSpaceBoundCache.Capacity < brushCount)
                    brushTreeSpaceBoundCache.Capacity = brushCount;

                if (treeSpaceVerticesCache.Capacity < brushCount)
                    treeSpaceVerticesCache.Capacity = brushCount;

                if (routingTableCache.Capacity < brushCount)
                    routingTableCache.Capacity = brushCount;

                if (brushTreeSpacePlaneCache.Capacity < brushCount)
                    brushTreeSpacePlaneCache.Capacity = brushCount;

                if (brushesTouchedByBrushCache.Capacity < brushCount)
                    brushesTouchedByBrushCache.Capacity = brushCount;

                if (transformationCache.Capacity < brushCount)
                    transformationCache.Capacity = brushCount;

                if (brushRenderBufferCache.Capacity < brushCount)
                    brushRenderBufferCache.Capacity = brushCount;
            }

            internal void Dispose()
            {
                if (allKnownBrushMeshIndices.IsCreated)
                {
                    allKnownBrushMeshIndices.Clear();
                    allKnownBrushMeshIndices.Dispose();
                }
                allKnownBrushMeshIndices = default;
                if (brushIDValues.IsCreated)
                    brushIDValues.Dispose();
                brushIDValues = default;
                if (basePolygonCache.IsCreated)
                {
                    foreach (var item in basePolygonCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    basePolygonCache.Clear();
                    basePolygonCache.Dispose();
                }
                basePolygonCache = default;
                if (brushTreeSpaceBoundCache.IsCreated)
                {
                    brushTreeSpaceBoundCache.Clear();
                    brushTreeSpaceBoundCache.Dispose();
                }
                brushTreeSpaceBoundCache = default;
                if (treeSpaceVerticesCache.IsCreated)
                {
                    foreach (var item in treeSpaceVerticesCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    treeSpaceVerticesCache.Clear();
                    treeSpaceVerticesCache.Dispose();
                }
                treeSpaceVerticesCache = default;
                if (routingTableCache.IsCreated)
                {
                    foreach (var item in routingTableCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    routingTableCache.Clear();
                    routingTableCache.Dispose();
                }
                routingTableCache = default;
                if (brushTreeSpacePlaneCache.IsCreated)
                {
                    foreach (var item in brushTreeSpacePlaneCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushTreeSpacePlaneCache.Clear();
                    brushTreeSpacePlaneCache.Dispose();
                }
                brushTreeSpacePlaneCache = default;
                if (brushesTouchedByBrushCache.IsCreated)
                {
                    foreach (var item in brushesTouchedByBrushCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushesTouchedByBrushCache.Clear();
                    brushesTouchedByBrushCache.Dispose();
                }
                brushesTouchedByBrushCache = default;
                if (transformationCache.IsCreated)
                {
                    transformationCache.Clear();
                    transformationCache.Dispose();
                }
                transformationCache = default;
                if (brushRenderBufferCache.IsCreated)
                {
                    foreach (var item in brushRenderBufferCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushRenderBufferCache.Clear();
                    brushRenderBufferCache.Dispose();
                }
                brushRenderBufferCache = default;
                if (brushTreeSpaceBoundLookup.IsCreated)
                    brushTreeSpaceBoundLookup.Dispose();
                brushTreeSpaceBoundLookup = default;
                if (brushRenderBufferLookup.IsCreated)
                    brushRenderBufferLookup.Dispose();
                brushRenderBufferLookup = default;
                if (parameters.IsCreated)
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].IsCreated)
                            parameters[i].Dispose();
                    }
                    parameters.Dispose();
                }
                parameters = default;
            }
        }

        static ChiselTreeLookup _singleton;

        static void UpdateValue()
        {
            if (_singleton == null)
            {
                _singleton = ScriptableObject.CreateInstance<ChiselTreeLookup>();
                _singleton.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static ChiselTreeLookup Value
        {
            get
            {
                if (_singleton == null)
                    UpdateValue();
                return _singleton;
            }
        }

        public Data this[CSGTree treeNode]
        {
            get
            {
                if (!chiselTreeLookup.TryGetValue(treeNode, out int dataIndex))
                {
                    dataIndex = chiselTreeData.Count;
                    chiselTreeLookup[treeNode] = dataIndex;
                    chiselTreeData.Add(new Data());
                    chiselTreeData[dataIndex].Initialize();
                }
                return chiselTreeData[dataIndex];
            }
        }

        readonly Dictionary<CSGTree, int>   chiselTreeLookup    = new Dictionary<CSGTree, int>();
        readonly List<Data>                 chiselTreeData      = new List<Data>();

        public void Remove(CSGTree tree)
        {
            if (!chiselTreeLookup.TryGetValue(tree, out int dataIndex))
                return;

            var data = chiselTreeData[dataIndex];
            data.Dispose();
            // TODO: remove null entry and fix up indices
            chiselTreeData[dataIndex] = default;
            chiselTreeLookup.Remove(tree);
        }

        public void Clear()
        {
            if (_singleton == null)
                return;
            foreach (var data in chiselTreeData)
            {
                if (data != null)
                    data.Dispose();
            }
            chiselTreeData.Clear();
            chiselTreeLookup.Clear();
            DestroyImmediate(_singleton);
            _singleton = null;
        }

        internal void OnDisable()
        {
            foreach (var data in chiselTreeData)
            {
                if (data != null)
                    data.Dispose();
            }
            chiselTreeData.Clear();
            chiselTreeLookup.Clear();
            if (_singleton == this)
                _singleton = null;
        }
    }
    public struct RefCountedBrushMeshBlob
    {
        public int refCount;
        public BlobAssetReference<BrushMeshBlob> brushMeshBlob;
    }

    internal sealed unsafe class ChiselMeshLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public NativeHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache;

            internal void Initialize()
            {
                brushMeshBlobCache = new NativeHashMap<int, RefCountedBrushMeshBlob>(1000, Allocator.Persistent);
            }

            public void EnsureCapacity(int capacity)
            {
                if (brushMeshBlobCache.Capacity < capacity)
                    brushMeshBlobCache.Capacity = capacity;
            }

            internal void Dispose()
            {
                if (brushMeshBlobCache.IsCreated)
                {
                    try
                    {
                        using (var items = brushMeshBlobCache.GetValueArray(Allocator.Persistent))
                        {
                            foreach (var item in items)
                            {
                                if (item.brushMeshBlob.IsCreated)
                                    item.brushMeshBlob.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        brushMeshBlobCache.Dispose();
                        brushMeshBlobCache = default;
                    }
                }
                // temporary hack
                CompactHierarchyManager.ClearOutlines();
            }
        }

        static ChiselMeshLookup _singleton;

        static void UpdateValue()
        {
            if (_singleton == null)
            {
                _singleton = ScriptableObject.CreateInstance<ChiselMeshLookup>();
                _singleton.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static Data Value
        {
            get
            {
                if (_singleton == null)
                    UpdateValue();
                return _singleton.data;
            }
        }

        readonly Data data = new Data();

        internal void OnEnable()
        {
            data.Initialize();
        }

        internal void OnDisable()
        {
            data.Dispose();
            _singleton = null;
        }
    }
}
