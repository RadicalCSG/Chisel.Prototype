using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;

namespace Chisel.Core
{
    static partial class CSGManager
    {
        internal sealed class TreeInfo
        {
            public readonly List<int> treeBrushes = new List<int>();
            public readonly List<GeneratedMeshDescription> meshDescriptions = new List<GeneratedMeshDescription>();
            public readonly List<SubMeshCounts> subMeshCounts = new List<SubMeshCounts>();


            public void Reset()
            {
                subMeshCounts.Clear();
            }
        }

        internal struct TreeUpdate
        {
            public int                              treeNodeIndex;
            public NativeArray<int>                 allTreeBrushIndices;
            public NativeList<int>                  rebuildTreeBrushIndices;
            
            public BlobAssetReference<CompactTree>  compactTree;

            public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>            brushMeshLookup;
            public NativeHashMap<int, BlobAssetReference<NodeTransformations>>      transformations;
            public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>         basePolygons;
            public NativeHashMap<int, BlobAssetReference<BrushWorldPlanes>>         brushWorldPlanes;
            public NativeHashMap<int, BlobAssetReference<RoutingTable>>             routingTableLookup;
            public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushes;

            public NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBuffers;
            public NativeList<BlobAssetReference<BrushIntersectionLoops>>           intersectionLoopBlobs;
            public NativeMultiHashMap<int, BrushPair>                               brushBrushIntersections;
            public NativeList<BrushPair>                                            uniqueBrushPairs;
            public NativeList<BlobAssetReference<BrushPairIntersection>>            intersectingBrushes;
            
            public NativeStream   dataStream1;
            public NativeStream   dataStream2;

            public JobHandle generateBasePolygonLoopsJobHandle;

            public JobHandle findAllIntersectionsJobHandle;
            public JobHandle findIntersectingBrushesJobHandle;

            public JobHandle updateBrushWorldPlanesJobHandle;

            public JobHandle updateBrushCategorizationTablesJobHandle;

            public JobHandle findBrushPairsJobHandle;
            public JobHandle prepareBrushPairIntersectionsJobHandle;
            public JobHandle findAllIntersectionLoopsJobHandle;

            public JobHandle allFindLoopOverlapIntersectionsJobHandle;

            public JobHandle allPerformAllCSGJobHandle;
            public JobHandle allGenerateSurfaceTrianglesJobHandle;
        }

        struct TreeSorter : IComparer<TreeUpdate>
        {
            public int Compare(TreeUpdate x, TreeUpdate y)
            {
                if (!x.brushBrushIntersections.IsCreated)
                {
                    if (!y.brushBrushIntersections.IsCreated)
                        return 0;
                    return 1;
                }
                if (!y.brushBrushIntersections.IsCreated)
                    return -1;
                var xBrushBrushIntersectionsCount = x.brushBrushIntersections.Count();
                var yBrushBrushIntersectionsCount = y.brushBrushIntersections.Count();
                if (xBrushBrushIntersectionsCount < yBrushBrushIntersectionsCount)
                    return 1;
                if (xBrushBrushIntersectionsCount > yBrushBrushIntersectionsCount)
                    return -1;

                if (x.rebuildTreeBrushIndices.Length < y.rebuildTreeBrushIndices.Length)
                    return 1;
                if (x.rebuildTreeBrushIndices.Length > y.rebuildTreeBrushIndices.Length)
                    return -1;

                return x.treeNodeIndex - y.treeNodeIndex;
            }
        }


        internal static JobHandle UpdateTreeMeshes(int[] treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

#if UNITY_EDITOR
            //JobsUtility.JobWorkerCount = math.max(1, ((JobsUtility.JobWorkerMaximumCount + 1) / 2) - 1);
#endif

            var treeUpdates = new TreeUpdate[treeNodeIDs.Length];
            var treeUpdateLength = 0;
            Profiler.BeginSample("Tag_Setup");
            for (int t = 0; t < treeNodeIDs.Length; t++)
            {
                var treeNodeIndex       = treeNodeIDs[t] - 1;
                var treeInfo            = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;

                Profiler.BeginSample("Tag_Reset");
                treeInfo.Reset();
                Profiler.EndSample();

                var treeBrushes = treeInfo.treeBrushes;
                if (treeInfo.treeBrushes.Count == 0)
                    continue;
                
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeIndex];
                var chiselMeshValues    = ChiselMeshLookup.Value;
                
                ref var brushMeshBlobs          = ref chiselMeshValues.brushMeshBlobs;
                ref var transformations         = ref chiselLookupValues.transformations;
                ref var basePolygons            = ref chiselLookupValues.basePolygons;
                ref var brushesTouchedByBrushes = ref chiselLookupValues.brushesTouchedByBrushes;

                // Removes all brushes that have MeshID == 0 from treeBrushesArray
                var allBrushBrushIndicesList        = new List<int>();
                var rebuildTreeBrushIndicesList     = new List<int>();
                var transformTreeBrushIndicesList   = new List<int>();
                var rebuildBrushNodeIndices         = new List<int>();
                for (int b = 0; b < treeBrushes.Count; b++)
                {
                    var brushNodeID     = treeBrushes[b];
                    var brushNodeIndex  = brushNodeID - 1;
                    var brushMeshID     = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID;
                    if (brushMeshID == 0)
                        continue;

                    allBrushBrushIndicesList.Add(brushNodeIndex);
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (nodeFlags.status == NodeStatusFlags.None)
                        continue;

                    rebuildTreeBrushIndicesList.Add(brushNodeIndex);
                }

                if (rebuildTreeBrushIndicesList.Count == 0)
                    continue;

                var anyHierarchyModified = false;
                for (int b = 0; b < rebuildTreeBrushIndicesList.Count; b++)
                {
                    var brushNodeIndex = rebuildTreeBrushIndicesList[b];
                    
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (basePolygons.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        basePolygons.Remove(brushNodeIndex);
                        if (basePolygonsBlob.IsCreated)
                            basePolygonsBlob.Dispose();
                    }

                    if ((nodeFlags.status & NodeStatusFlags.ShapeModified) != NodeStatusFlags.None)
                    {
                        // Need to update the basePolygons for this node
                        nodeFlags.status &= ~NodeStatusFlags.ShapeModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    if ((nodeFlags.status & NodeStatusFlags.HierarchyModified) != NodeStatusFlags.None)
                    {
                        anyHierarchyModified = true;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    if ((nodeFlags.status & NodeStatusFlags.TransformationModified) != NodeStatusFlags.None)
                    {
                        transformTreeBrushIndicesList.Add(brushNodeIndex);
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                }
                
                if (rebuildTreeBrushIndicesList.Count != allBrushBrushIndicesList.Count)
                {
                    for (int b = 0; b < rebuildTreeBrushIndicesList.Count; b++)
                    {
                        var brushNodeIndex = rebuildTreeBrushIndicesList[b];
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        if ((nodeFlags.status & NodeStatusFlags.NeedAllTouchingUpdated) == NodeStatusFlags.None)
                            continue;

                        if (!chiselLookupValues.brushesTouchedByBrushes.TryGetValue(brushNodeIndex, out var brushTouchedByBrush))
                            continue;

                        ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                        for (int i = 0; i < brushIntersections.Length; i++)
                        {
                            var otherBrushIndex = brushIntersections[i].nodeIndex;
                            if (!rebuildTreeBrushIndicesList.Contains(otherBrushIndex))
                                rebuildTreeBrushIndicesList.Add(otherBrushIndex);
                        }
                    }
                }

                allBrushBrushIndicesList.Sort();

                // Clean up values we're rebuilding below, including the ones with brushMeshID == 0
                chiselLookupValues.RemoveSurfaceRenderBuffersByBrushID(rebuildTreeBrushIndicesList);
                chiselLookupValues.RemoveRoutingTablesByBrushID(rebuildTreeBrushIndicesList);
                chiselLookupValues.RemoveBrushTouchesByBrushID(allBrushBrushIndicesList);
                chiselLookupValues.RemoveBrushWorldPlanesByBrushID(rebuildTreeBrushIndicesList);

                chiselLookupValues.RemoveTransformationsByBrushID(transformTreeBrushIndicesList);


                Profiler.BeginSample("Tag_Allocations");//time=2.45ms
                var allTreeBrushIndices         = allBrushBrushIndicesList.ToNativeArray(Allocator.TempJob);
                var rebuildTreeBrushIndices     = rebuildTreeBrushIndicesList.ToNativeList(Allocator.TempJob);
                var brushMeshLookup             = new NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>(allTreeBrushIndices.Length, Allocator.TempJob);
                Profiler.EndSample();

                // NOTE: needs to contain ALL brushes in tree, EVEN IF THEY ARE NOT UPDATED!
                Profiler.BeginSample("Tag_BuildBrushMeshLookup");
                {
                    for (int i = 0; i < allTreeBrushIndices.Length; i++)
                    {
                        var brushNodeIndex = allTreeBrushIndices[i];
                        var brushMeshIndex = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID - 1;
                        brushMeshLookup[brushNodeIndex] = brushMeshBlobs[brushMeshIndex];
                    }
                }
                Profiler.EndSample();


                Profiler.BeginSample("Tag_DirtyAllOutlines");
                {
                    for (int b = 0; b < allTreeBrushIndices.Length; b++)
                    {
                        var brushNodeIndex = allTreeBrushIndices[b];
                        var brushInfo = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo;
                        brushInfo.brushOutlineGeneration++;
                        brushInfo.brushOutlineDirty = true;
                    }
                }
                Profiler.EndSample();

                // TODO: optimize, only do this when necessary
                Profiler.BeginSample("Tag_UpdateBrushTransformations");
                {
                    for (int b = 0; b < transformTreeBrushIndicesList.Count; b++)
                    {
                        var brushNodeIndex = transformTreeBrushIndicesList[b];
                        UpdateNodeTransformation(ref transformations, brushNodeIndex);
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        nodeFlags.status &= ~NodeStatusFlags.TransformationModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }
                }
                Profiler.EndSample();

                BlobAssetReference<CompactTree> compactTree = chiselLookupValues.compactTree;

                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    if (chiselLookupValues.compactTree.IsCreated)
                        chiselLookupValues.compactTree.Dispose();

                    // TODO: jobify?
                    Profiler.BeginSample("Tag_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex); 
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
                }


                Profiler.BeginSample("Tag_Allocations");
                Profiler.BeginSample("Tag_BrushOutputLoops");
                var brushLoopCount = rebuildTreeBrushIndices.Length;                
                for (int index = 0; index < brushLoopCount; index++)
                {
                    var brushNodeIndex  = rebuildTreeBrushIndices[index];

                    if (rebuildTreeBrushIndicesList.Contains(brushNodeIndex))
                    {
                        if (chiselLookupValues.brushRenderBuffers.TryGetValue(brushNodeIndex, out var oldBrushRenderBuffer) &&
                            oldBrushRenderBuffer.IsCreated)
                            oldBrushRenderBuffer.Dispose();
                        chiselLookupValues.brushRenderBuffers.Remove(brushNodeIndex);
                    }
                }
                Profiler.EndSample();
                
                var triangleArraySize       = GeometryMath.GetTriangleArraySize(allTreeBrushIndices.Length);
                var intersectionCount       = triangleArraySize;
                var intersectionLoopBlobs   = new NativeList<BlobAssetReference<BrushIntersectionLoops>>(intersectionCount, Allocator.TempJob);
                var brushBrushIntersections = new NativeMultiHashMap<int, BrushPair>(intersectionCount * 2, Allocator.TempJob);
                var uniqueBrushPairs        = new NativeList<BrushPair>(intersectionCount, Allocator.TempJob);
                var intersectingBrushes     = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.TempJob);
                var dataStream1             = new NativeStream(allTreeBrushIndices.Length, Allocator.TempJob);
                var dataStream2             = new NativeStream(allTreeBrushIndices.Length, Allocator.TempJob);
                Profiler.EndSample();


                treeUpdates[treeUpdateLength] = new TreeUpdate
                {
                    treeNodeIndex                   = treeNodeIndex,
                    allTreeBrushIndices             = allTreeBrushIndices,
                    rebuildTreeBrushIndices         = rebuildTreeBrushIndices,
                    brushMeshLookup                 = brushMeshLookup,
                    transformations                 = chiselLookupValues.transformations,
                    basePolygons                    = chiselLookupValues.basePolygons,
                    brushWorldPlanes                = chiselLookupValues.brushWorldPlanes,
                    routingTableLookup              = chiselLookupValues.routingTableLookup,
                    brushesTouchedByBrushes         = chiselLookupValues.brushesTouchedByBrushes,
                    brushRenderBuffers              = chiselLookupValues.brushRenderBuffers,
                    intersectionLoopBlobs           = intersectionLoopBlobs,
                    brushBrushIntersections         = brushBrushIntersections,
                    uniqueBrushPairs                = uniqueBrushPairs,
                    intersectingBrushes             = intersectingBrushes,
                    dataStream1                     = dataStream1,
                    dataStream2                     = dataStream2,
                    compactTree                     = compactTree
                };
                treeUpdateLength++;
            }
            Profiler.EndSample();


            // Sort trees from largest to smallest
            var treeSorter = new TreeSorter();
            Array.Sort(treeUpdates, treeSorter);

            Profiler.BeginSample("Tag_Jobified");


            // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
            Profiler.BeginSample("Tag_GenerateBasePolygonLoops");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate  = ref treeUpdates[t];
                    var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs 
                    {
                        // Read
                        treeBrushIndices    = treeUpdate.rebuildTreeBrushIndices,
                        brushMeshLookup     = treeUpdate.brushMeshLookup,
                        transformations     = treeUpdate.transformations,

                        // Write
                        basePolygons        = treeUpdate.basePolygons.AsParallelWriter()
                    };
                    treeUpdate.generateBasePolygonLoopsJobHandle = createBlobPolygonsBlobs.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 16);
                }
            }
            finally { Profiler.EndSample(); }

            // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
            Profiler.BeginSample("Tag_FindIntersectingBrushes");
            try
            {
                // TODO: optimize, use hashed grid
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var findAllIntersectionsJob = new FindAllBrushIntersectionsJob
                    {
                        // Read
                        updateBrushIndices      = treeUpdate.rebuildTreeBrushIndices,
                        allTreeBrushIndices     = treeUpdate.allTreeBrushIndices,
                        transformations         = treeUpdate.transformations,
                        brushMeshLookup         = treeUpdate.brushMeshLookup,
                        basePolygons            = treeUpdate.basePolygons,

                        // Write
                        brushBrushIntersections = treeUpdate.brushBrushIntersections.AsParallelWriter()
                    };
                    treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                        Schedule(treeUpdate.generateBasePolygonLoopsJobHandle);
                }
                
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findAllIntersectionsJobHandle;
                    var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                    {
                        // Read
                        treeNodeIndex           = treeUpdate.treeNodeIndex,
                        treeBrushIndices        = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        compactTree             = treeUpdate.compactTree,
                        brushBrushIntersections = treeUpdate.brushBrushIntersections,

                        // Write
                        brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes.AsParallelWriter()
                    };
                    treeUpdate.findIntersectingBrushesJobHandle = storeBrushIntersectionsJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 16, dependencies);
                }
            } finally { Profiler.EndSample(); }

            // TODO: should only do this at creation time + when moved / store with brush component itself
            Profiler.BeginSample("Tag_UpdateBrushWorldPlanes");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    var createBrushWorldPlanesJob = new CreateBrushWorldPlanesJob
                    {
                        // Read
                        treeBrushIndices        = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        brushMeshLookup         = treeUpdate.brushMeshLookup,
                        transformations         = treeUpdate.transformations,

                        // Write
                        brushWorldPlanes        = treeUpdate.brushWorldPlanes.AsParallelWriter()
                    };
                    treeUpdate.updateBrushWorldPlanesJobHandle = createBrushWorldPlanesJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 16, dependencies);
                }
            }
            finally { Profiler.EndSample(); }

            // TODO: only update when brush or any touching brush has been added/removed or changes operation/order
            Profiler.BeginSample("Tag_UpdateBrushCategorizationTables");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    // Build categorization trees for brushes
                    var createRoutingTableJob = new CreateRoutingTableJob
                    {
                        // Read
                        treeBrushIndices        = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes,
                        compactTree             = treeUpdate.compactTree,

                        // Write
                        routingTableLookup      = treeUpdate.routingTableLookup.AsParallelWriter()
                    };
                    treeUpdate.updateBrushCategorizationTablesJobHandle = createRoutingTableJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 16, dependencies);
                }
            } finally { Profiler.EndSample(); }
                                
            // Create unique loops between brush intersections
            Profiler.BeginSample("Tag_FindAllIntersectionLoops");
            try
            {
                // TODO: merge this with another job, there's not enough work 
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    var findBrushPairsJob = new FindBrushPairsJob
                    {
                        // Read
                        treeBrushIndices        = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes,
                                    
                        // Write
                        uniqueBrushPairs        = treeUpdate.uniqueBrushPairs,
                    };
                    treeUpdate.findBrushPairsJobHandle = findBrushPairsJob.
                        Schedule(dependencies);
                }

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findBrushPairsJobHandle;
                    var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                    {
                        // Read
                        uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
                        brushMeshBlobLookup     = treeUpdate.brushMeshLookup,
                        transformations         = treeUpdate.transformations,

                        // Write
                        intersectingBrushes     = treeUpdate.intersectingBrushes.AsParallelWriter()
                    };
                    treeUpdate.prepareBrushPairIntersectionsJobHandle = prepareBrushPairIntersectionsJob.
                        Schedule(treeUpdate.uniqueBrushPairs, 4, dependencies);
                }

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.updateBrushWorldPlanesJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle);
                    var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                    {
                        // Read
                        brushWorldPlanes        = treeUpdate.brushWorldPlanes,
                        intersectingBrushes     = treeUpdate.intersectingBrushes.AsDeferredJobArray(),

                        // Write
                        outputSurfaces          = treeUpdate.intersectionLoopBlobs.AsParallelWriter()
                    };
                    treeUpdate.findAllIntersectionLoopsJobHandle = findAllIntersectionLoopsJob.
                        Schedule(treeUpdate.intersectingBrushes, 4, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_FindLoopOverlapIntersections");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];                    
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle);
                    var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                    {
                        // Read
                        treeBrushIndices            = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        intersectionLoopBlobs       = treeUpdate.intersectionLoopBlobs.AsDeferredJobArray(),
                        brushWorldPlanes            = treeUpdate.brushWorldPlanes,
                        basePolygonBlobs            = treeUpdate.basePolygons,

                        // Write
                        output                      = treeUpdate.dataStream1.AsWriter()
                    };
                    treeUpdate.allFindLoopOverlapIntersectionsJobHandle = findLoopOverlapIntersectionsJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 64, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_PerformCSGJob");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.updateBrushCategorizationTablesJobHandle);

                    // Perform CSG
                    // TODO: determine when a brush is completely inside another brush
                    //		 (might not have any intersection loops)
                    var performCSGJob = new PerformCSGJob
                    {
                        // Read
                        treeBrushNodeIndices        = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        routingTableLookup          = treeUpdate.routingTableLookup,
                        brushWorldPlanes            = treeUpdate.brushWorldPlanes,
                        brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                        input                       = treeUpdate.dataStream1.AsReader(),

                        // Write
                        output                      = treeUpdate.dataStream2.AsWriter(),
                    };
                    treeUpdate.allPerformAllCSGJobHandle = performCSGJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 32, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_GenerateSurfaceTrianglesJob");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.allPerformAllCSGJobHandle;

                    // TODO: Make this work with burst so we can, potentially, merge it with PerformCSGJob?
                    var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                    {
                        // Read
                        treeBrushNodeIndices    = treeUpdate.rebuildTreeBrushIndices.AsDeferredJobArray(),
                        brushWorldPlanes        = treeUpdate.brushWorldPlanes,
                        basePolygons            = treeUpdate.basePolygons,
                        input                   = treeUpdate.dataStream2.AsReader(),

                        // Write
                        brushRenderBuffers      = treeUpdate.brushRenderBuffers.AsParallelWriter(),
                    };
                    treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                        Schedule(treeUpdate.rebuildTreeBrushIndices, 64, dependencies);
                }
            } finally { Profiler.EndSample(); }

            // Reset the flags before the dispose of these containers are scheduled
            for (int t = 0; t < treeUpdateLength; t++)
            {
                ref var treeUpdate = ref treeUpdates[t];
                for (int b = 0; b < treeUpdate.allTreeBrushIndices.Length; b++)
                { 
                    var brushNodeIndex = treeUpdate.allTreeBrushIndices[b];
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    nodeFlags.status = NodeStatusFlags.None;
                    CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                }
            }


            for (int t = 0; t < treeUpdateLength; t++)
            {
                ref var treeUpdate = ref treeUpdates[t];
                var treeNodeIndex = treeUpdate.treeNodeIndex;
                finalJobHandle = JobHandle.CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, finalJobHandle);

                {
                    var flags = nodeFlags[treeNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                    flags.SetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                    nodeFlags[treeNodeIndex] = flags;
                }
            }

            Profiler.EndSample();
            
            //JobHandle.ScheduleBatchedJobs();
            Profiler.BeginSample("Tag_Complete");
            finalJobHandle.Complete();
            Profiler.EndSample();


            // Note: Seems that scheduling a Dispose will cause previous jobs to be completed?
            //       Actually faster to just call them on main thread?
            Profiler.BeginSample("Tag_BrushOutputLoopsDispose");
            {
                var disposeJobHandle = finalJobHandle;
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];

                    treeUpdate.dataStream1              .Dispose();//disposeJobHandle);
                    treeUpdate.dataStream2              .Dispose();//disposeJobHandle);
                    treeUpdate.brushMeshLookup          .Dispose();//disposeJobHandle);                     
                    treeUpdate.allTreeBrushIndices      .Dispose();//disposeJobHandle);
                    treeUpdate.rebuildTreeBrushIndices  .Dispose();//disposeJobHandle);
                    treeUpdate.brushBrushIntersections  .Dispose();//disposeJobHandle);
                    treeUpdate.uniqueBrushPairs         .Dispose();//disposeJobHandle);
                    treeUpdate.intersectionLoopBlobs    .Dispose();//disposeJobHandle);
                    treeUpdate.intersectingBrushes      .Dispose();//disposeJobHandle);
                }
            }
            Profiler.EndSample();

            //JobsUtility.JobWorkerCount = JobsUtility.JobWorkerMaximumCount;

            return finalJobHandle;
        }



        #region Rebuild / Update
        static void UpdateNodeTransformation(ref NativeHashMap<int, BlobAssetReference<NodeTransformations>> transformations, int nodeIndex)
        {
            // TODO: clean this up and make this sensible

            // Note: Currently "localTransformation" is actually nodeToTree, but only for all the brushes. 
            //       Branches do not have a transformation set at the moment.
            
            // TODO: should be transformations the way up to the tree, not just tree vs brush
            var brushLocalTransformation     = CSGManager.nodeLocalTransforms[nodeIndex].localTransformation;
            var brushLocalInvTransformation  = CSGManager.nodeLocalTransforms[nodeIndex].invLocalTransformation;

            var nodeTransform                = CSGManager.nodeTransforms[nodeIndex];
            nodeTransform.nodeToTree = brushLocalTransformation;
            nodeTransform.treeToNode = brushLocalInvTransformation;
            CSGManager.nodeTransforms[nodeIndex] = nodeTransform;

            if (transformations.TryGetValue(nodeIndex, out BlobAssetReference<NodeTransformations> oldTransformations))
            {
                if (oldTransformations.IsCreated)
                    oldTransformations.Dispose();
            }
            transformations[nodeIndex] = NodeTransformations.Build(nodeTransform.nodeToTree, nodeTransform.treeToNode);
        }
        #endregion

        #region Reset/Rebuild
        static void Reset()
        {
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                if (!IsValidNodeID(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree))
                    continue;

                var treeNodeIndex   = treeNodeID - 1;
                var treeInfo        = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;
                treeInfo.Reset();
            }
        }

        internal static bool UpdateAllTreeMeshes(out JobHandle allTrees)
        {
            allTrees = default(JobHandle);
            bool needUpdate = false;
            // Check if we have a tree that needs updates
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                var treeNodeIndex = treeNodeID - 1;
                if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    needUpdate = true;
                    break;
                }
            }

            if (!needUpdate)
                return false;

            UpdateDelayedHierarchyModifications();

            allTrees = UpdateTreeMeshes(trees.ToArray());
            return true;
        }

        internal static bool RebuildAll()
        {
            Reset();
            if (!UpdateAllTreeMeshes(out JobHandle handle))
                return false;
            handle.Complete();
            return true;
        }
            #endregion
    }
}
