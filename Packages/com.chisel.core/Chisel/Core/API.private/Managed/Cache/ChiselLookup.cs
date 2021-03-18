using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    internal sealed unsafe class ChiselTreeLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public NativeList<CompactNodeID>    brushIDValues;
            public ChiselLayerParameters        parameters1;
            public ChiselLayerParameters        parameters2;
            public HashSet<int>                 allKnownBrushMeshIndices    = new HashSet<int>();
            public Dictionary<int, int>         previousMeshIDGeneration    = new Dictionary<int, int>();

            public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
            public NativeList<MinMaxAABB>                                       brushTreeSpaceBoundCache;
            public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
            public NativeList<BlobAssetReference<RoutingTable>>                 routingTableCache;
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
            public NativeList<NodeTransformations>                              transformationCache;
            public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;
            
            public NativeHashMap<CompactNodeID, MinMaxAABB>                                     brushTreeSpaceBoundLookup;
            public NativeHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>>    brushRenderBufferLookup;

            public BlobAssetReference<CompactTree>                              compactTree;

            internal void Initialize()
            {
                brushIDValues               = new NativeList<CompactNodeID>(1000, Allocator.Persistent);
                
                brushTreeSpaceBoundLookup    = new NativeHashMap<CompactNodeID, MinMaxAABB>(1000, Allocator.Persistent);
                brushRenderBufferLookup      = new NativeHashMap<CompactNodeID, BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                // brushIndex
                basePolygonCache            = new NativeList<BlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent);
                brushTreeSpaceBoundCache    = new NativeList<MinMaxAABB>(1000, Allocator.Persistent);
                treeSpaceVerticesCache      = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(1000, Allocator.Persistent);
                routingTableCache           = new NativeList<BlobAssetReference<RoutingTable>>(1000, Allocator.Persistent);
                brushTreeSpacePlaneCache    = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent);
                brushesTouchedByBrushCache  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent);
                transformationCache         = new NativeList<NodeTransformations>(1000, Allocator.Persistent);
                brushRenderBufferCache      = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                parameters1.Initialize();
                parameters2.Initialize();
            }

            internal void EnsureCapacity(int brushCount)
            {
                if (brushTreeSpaceBoundLookup.Capacity < brushCount)
                    brushTreeSpaceBoundLookup.Capacity = brushCount;

                if (brushRenderBufferLookup.Capacity < brushCount)
                    brushRenderBufferLookup.Capacity = brushCount;

                if (brushIDValues.Capacity < brushCount)
                    brushIDValues.Capacity = brushCount;

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
                if (compactTree.IsCreated)
                    compactTree.Dispose();
                compactTree = default;

                parameters1.Dispose();
                parameters1 = default;
                parameters2.Dispose();
                parameters2 = default;
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

        public Data this[NodeID nodeID]
        {
            get
            {
                if (!chiselTreeLookup.TryGetValue(nodeID, out int dataIndex))
                {
                    dataIndex = chiselTreeData.Count;
                    chiselTreeLookup[nodeID] = dataIndex;
                    chiselTreeData.Add(new Data());
                    chiselTreeData[dataIndex].Initialize();
                }
                return chiselTreeData[dataIndex];
            }
        }

        readonly Dictionary<NodeID, int>    chiselTreeLookup    = new Dictionary<NodeID, int>();
        readonly List<Data>                 chiselTreeData      = new List<Data>();

        public void Remove(NodeID nodeID)
        {
            if (!chiselTreeLookup.TryGetValue(nodeID, out int dataIndex))
                return;

            var data = chiselTreeData[dataIndex];
            data.Dispose();
            // TODO: remove null entry and fix up indices
            chiselTreeData[dataIndex] = default;
            chiselTreeLookup.Remove(nodeID);
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

    internal sealed unsafe class ChiselMeshLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public readonly HashSet<int> brushMeshUpdateList = new HashSet<int>();
            public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>> brushMeshBlobs;
            public NativeHashMap<int, int> brushMeshBlobGeneration;

            internal void Initialize()
            {
                brushMeshBlobs          = new NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>(1000, Allocator.Persistent);
                brushMeshBlobGeneration = new NativeHashMap<int, int>(1000, Allocator.Persistent);
            }

            public void EnsureCapacity(int capacity)
            {
                if (brushMeshBlobs.Capacity < capacity)
                {
                    brushMeshBlobs.Capacity = capacity;
                    brushMeshBlobGeneration.Capacity = capacity;
                }
            }

            internal void Dispose()
            {
                if (brushMeshBlobs.IsCreated)
                {
                    using (var items = brushMeshBlobs.GetValueArray(Allocator.Temp))
                    {
                        brushMeshBlobs.Clear();
                        brushMeshBlobs.Dispose();
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                    }
                    brushMeshBlobGeneration.Dispose();
                    brushMeshBlobs = default;
                    brushMeshBlobGeneration = default;
                }
                // temporary hack
                CompactHierarchyManager.ClearOutlines();
            }
        }

        public static void Update() 
        {
            var instance                = ChiselMeshLookup.Value;
            var brushMeshBlobGeneration = instance.brushMeshBlobGeneration;
            var brushMeshBlobs          = instance.brushMeshBlobs;
            foreach (var brushMeshIndex in Value.brushMeshUpdateList)
            {
                var brushMeshID = brushMeshIndex + 1;
                var brushMesh   = BrushMeshManager.GetBrushMesh(brushMeshID); //<-- should already be blobs
                if (brushMesh == null)
                {
                    brushMeshBlobs[brushMeshIndex] = BlobAssetReference<BrushMeshBlob>.Null;
                } else
                {
                    var newBrushMesh = BrushMeshGenerator.Build(brushMesh);
                    brushMeshBlobs[brushMeshIndex] = newBrushMesh;
                }
                if (!brushMeshBlobGeneration.TryGetValue(brushMeshIndex, out var currentGeneration))
                    brushMeshBlobGeneration[brushMeshIndex] = 1;
                else
                {
                    var newGeneration = currentGeneration + 1;
                    if (newGeneration == 0)
                        newGeneration++;
                    brushMeshBlobGeneration[brushMeshIndex] = newGeneration;
                }
            }
            instance.brushMeshUpdateList.Clear();
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
