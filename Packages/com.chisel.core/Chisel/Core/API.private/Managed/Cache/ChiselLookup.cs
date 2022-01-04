using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    internal sealed class ChiselTreeLookup : ScriptableObject
    {
        public class Data
        {
            public JobHandle                                lastJobHandle;

            public NativeList<CompactNodeID>                brushIDValues;
            public NativeArray<ChiselLayerParameters>       parameters;
            public NativeHashSet<int>                       allKnownBrushMeshIndices;

            public NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
            public NativeList<ChiselBlobAssetReference<RoutingTable>>                 routingTableCache;
            public NativeList<NodeTransformations>                              transformationCache;
            public NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
            public NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;
            public NativeList<ChiselAABB>                                       brushTreeSpaceBoundCache;
            public NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
            public NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
            
            public NativeHashMap<CompactNodeID, ChiselAABB>                                     brushTreeSpaceBoundLookup;
            public NativeHashMap<CompactNodeID, ChiselBlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferLookup;

            internal void Initialize()
            {
                brushIDValues               = new NativeList<CompactNodeID>(1000, Allocator.Persistent);
                allKnownBrushMeshIndices    = new NativeHashSet<int>(1000, Allocator.Persistent);

                // TODO: not used??
                brushTreeSpaceBoundLookup   = new NativeHashMap<CompactNodeID, ChiselAABB>(1000, Allocator.Persistent);
                brushRenderBufferLookup     = new NativeHashMap<CompactNodeID, ChiselBlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                // brushIndex
                basePolygonCache            = new NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent);
                brushTreeSpaceBoundCache    = new NativeList<ChiselAABB>(1000, Allocator.Persistent);
                treeSpaceVerticesCache      = new NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>(1000, Allocator.Persistent);
                routingTableCache           = new NativeList<ChiselBlobAssetReference<RoutingTable>>(1000, Allocator.Persistent);
                brushTreeSpacePlaneCache    = new NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent);
                brushesTouchedByBrushCache  = new NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent);
                transformationCache         = new NativeList<NodeTransformations>(1000, Allocator.Persistent);
                brushRenderBufferCache      = new NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                parameters                  = new NativeArray<ChiselLayerParameters>(SurfaceLayers.ParameterCount, Allocator.Persistent);
                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    parameter.Initialize(); 
                    Debug.Assert(parameter.IsCreated);
                    parameters[i] = parameter;
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
                    for (int i = 0; i < basePolygonCache.Length; i++)
                    {
                        if (basePolygonCache[i].IsCreated)
                            basePolygonCache[i].Dispose();
                        basePolygonCache[i] = default;
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
                    for (int i = 0; i < treeSpaceVerticesCache.Length; i++)
                    {
                        if (treeSpaceVerticesCache[i].IsCreated)
                            treeSpaceVerticesCache[i].Dispose();
                        treeSpaceVerticesCache[i] = default;
                    }
                    treeSpaceVerticesCache.Clear();
                    treeSpaceVerticesCache.Dispose();
                }
                treeSpaceVerticesCache = default;
                if (routingTableCache.IsCreated)
                {
                    for (int i = 0; i < routingTableCache.Length; i++)
                    {
                        if (routingTableCache[i].IsCreated)
                            routingTableCache[i].Dispose();
                        routingTableCache[i] = default;
                    }
                    routingTableCache.Clear();
                    routingTableCache.Dispose();
                }
                routingTableCache = default;
                if (brushTreeSpacePlaneCache.IsCreated)
                {
                    for (int i = 0; i < brushTreeSpacePlaneCache.Length; i++)
                    {
                        if (brushTreeSpacePlaneCache[i].IsCreated)
                            brushTreeSpacePlaneCache[i].Dispose();
                        brushTreeSpacePlaneCache[i] = default;
                    }
                    brushTreeSpacePlaneCache.Clear();
                    brushTreeSpacePlaneCache.Dispose();
                }
                brushTreeSpacePlaneCache = default;
                if (brushesTouchedByBrushCache.IsCreated)
                {
                    for (int i = 0; i < brushesTouchedByBrushCache.Length; i++)
                    {
                        if (brushesTouchedByBrushCache[i].IsCreated)
                            brushesTouchedByBrushCache[i].Dispose();
                        brushesTouchedByBrushCache[i] = default;
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
                    for (int i = 0; i < brushRenderBufferCache.Length; i++)
                    {
                        if (brushRenderBufferCache[i].IsCreated)
                            brushRenderBufferCache[i].Dispose();
                        brushRenderBufferCache[i] = default;
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
                        parameters[i] = default;
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
        public ChiselBlobAssetReference<BrushMeshBlob> brushMeshBlob;
    }

    internal sealed class ChiselMeshLookup : ScriptableObject
    {
        public class Data
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
