using System;
using System.Linq;
using Chisel.Core;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;

namespace Chisel.Core
{
    public struct GeneratedNodeDefinition
    {
        public int              hierarchyIndex;
        public CompactNodeID    parentCompactNodeID;
        public int              siblingIndex;
        public CompactNodeID    compactNodeID;

        public CSGOperationType operation;
        public float4x4         transformation;
        public int              brushMeshHash;
    }

    // TODO: move to core
    public class GeneratorJobPoolManager : System.IDisposable
    {
        System.Collections.Generic.HashSet<GeneratorJobPool> generatorPools = new System.Collections.Generic.HashSet<GeneratorJobPool>();

        static GeneratorJobPoolManager s_Instance;
        public static GeneratorJobPoolManager Instance => (s_Instance ??= new GeneratorJobPoolManager());

        public static bool Register  (GeneratorJobPool pool) { return Instance.generatorPools.Add(pool); }
        public static bool Unregister(GeneratorJobPool pool) { return Instance.generatorPools.Remove(pool); }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        public static void Init()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
        }

        private static void OnAssemblyReload()
        {
            if (s_Instance != null) 
                s_Instance.Dispose();
            s_Instance = null;
        }
#endif

        static JobHandle previousJobHandle = default;

        public static void Clear() 
        {
            previousJobHandle.Complete();
            var allGeneratorPools = Instance.generatorPools;
            foreach (var pool in allGeneratorPools)
                pool.AllocateOrClear();
        }

        [BurstCompile(CompileSynchronously = true)]
        struct ResizeTempLists : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int> totalCounts;

            [NoAlias] public NativeList<GeneratedNodeDefinition>            generatedNodeDefinitions;
            [NoAlias] public NativeList<BlobAssetReference<BrushMeshBlob>>  brushMeshBlobs;

            public void Execute()
            {
                var totalCount = 0;
                for (int i = 0; i < totalCounts.Length; i++)
                    totalCount += totalCounts[i];
                generatedNodeDefinitions.Capacity = totalCount;
                brushMeshBlobs.Capacity = totalCount;
            }
        }


        static readonly List<GeneratorJobPool> generatorJobs = new List<GeneratorJobPool>();

        // TODO: Optimize this
        public static unsafe JobHandle ScheduleJobs(JobHandle dependsOn = default)
        {
            var hierarchyList = CompactHierarchyManager.HierarchyList;

            var combinedJobHandle = (JobHandle)default;
            var allGeneratorPools = Instance.generatorPools;
            Profiler.BeginSample("GenPool_Generate");
            generatorJobs.Clear();
            foreach (var pool in allGeneratorPools)
            {
                if (pool.HasJobs)
                    generatorJobs.Add(pool);
            }
            foreach (var pool in generatorJobs)
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.ScheduleGenerateJob(dependsOn));
            Profiler.EndSample();

            Profiler.BeginSample("GenPool_UpdateHierarchy");
            var totalCounts = new NativeArray<int>(generatorJobs.Count, Allocator.TempJob);
            var lastJobHandle = combinedJobHandle;
            int index = 0;
            foreach (var pool in generatorJobs)
            {
                lastJobHandle = pool.ScheduleUpdateHierarchyJob(runInParallel: true, lastJobHandle, index, totalCounts);
                index++;
            }
            Profiler.EndSample();

            Profiler.BeginSample("GenPool_Allocator"); 
            var generatedNodeDefinitions    = new NativeList<GeneratedNodeDefinition>(Allocator.TempJob);
            var brushMeshBlobs              = new NativeList<BlobAssetReference<BrushMeshBlob>>(Allocator.TempJob);

            var resizeTempLists = new ResizeTempLists
            {
                // Read
                totalCounts                 = totalCounts,

                // Read / Write
                generatedNodeDefinitions    = generatedNodeDefinitions,
                brushMeshBlobs              = brushMeshBlobs
            };
            var allocateJobHandle = resizeTempLists.Schedule(runInParallel: true, lastJobHandle);
            totalCounts.Dispose(allocateJobHandle);
            Profiler.EndSample();
            
            Profiler.BeginSample("GenPool_Complete");
            allocateJobHandle.Complete(); // TODO: get rid of this somehow
            Profiler.EndSample();

            Profiler.BeginSample("GenPool_Schedule");
            lastJobHandle = default;
            combinedJobHandle = allocateJobHandle;
            foreach (var pool in generatorJobs)
            {
                var scheduleJobHandle = pool.ScheduleInitializeArraysJob(// Read
                                                                         hierarchyList, 
                                                                         // Write
                                                                         generatedNodeDefinitions,
                                                                         brushMeshBlobs,
                                                                         // Dependency
                                                                         JobHandle.CombineDependencies(allocateJobHandle, lastJobHandle));
                lastJobHandle = scheduleJobHandle;
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, scheduleJobHandle);
            }

            lastJobHandle = JobHandle.CombineDependencies(allocateJobHandle, combinedJobHandle);
            lastJobHandle = BrushMeshManager.ScheduleBrushRegistration(true, brushMeshBlobs, generatedNodeDefinitions, lastJobHandle);
            lastJobHandle = ScheduleAssignMeshesJob(true, hierarchyList, generatedNodeDefinitions, lastJobHandle);
            previousJobHandle = lastJobHandle;

            var generatedNodeDefinitionsDisposeJobHandle    = generatedNodeDefinitions.Dispose(lastJobHandle);
            var brushMeshBlobsDisposeJobHandle              = brushMeshBlobs.Dispose(lastJobHandle);
            var allDisposes = JobHandle.CombineDependencies(generatedNodeDefinitionsDisposeJobHandle,
                                                            brushMeshBlobsDisposeJobHandle);

            Profiler.EndSample();
            return lastJobHandle;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        struct HierarchySortJob : IJob
        {
            [NoAlias] public NativeList<GeneratedNodeDefinition>    generatedNodeDefinitions;

            struct GeneratedNodeDefinitionSorter : IComparer<GeneratedNodeDefinition>
            {
                public int Compare(GeneratedNodeDefinition x, GeneratedNodeDefinition y)
                {
                    var x_hierarchyID = x.hierarchyIndex;
                    var y_hierarchyID = y.hierarchyIndex;

                    if (x_hierarchyID != y_hierarchyID)
                        return x_hierarchyID - y_hierarchyID;
                    
                    var x_parentCompactNodeID = x.parentCompactNodeID.value;
                    var y_parentCompactNodeID = y.parentCompactNodeID.value;
                    if (x_parentCompactNodeID != y_parentCompactNodeID)
                        return x_parentCompactNodeID - y_parentCompactNodeID;

                    var x_siblingIndex = x.siblingIndex;
                    var y_siblingIndex = y.siblingIndex;
                    if (x_siblingIndex != y_siblingIndex)
                        return x_siblingIndex - y_siblingIndex;

                    var x_compactNodeID = x.compactNodeID.value;
                    var y_compactNodeID = y.compactNodeID.value;
                    if (x_compactNodeID != y_compactNodeID)
                        return x_compactNodeID - y_compactNodeID;
                    return 0;
                }
            }

            static readonly GeneratedNodeDefinitionSorter generatedNodeDefinitionSorter = new GeneratedNodeDefinitionSorter();

            public void Execute()
            {
                generatedNodeDefinitions.Sort(generatedNodeDefinitionSorter);
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        unsafe struct AssignMeshesJob : IJob
        {            
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            // ReadOnly
            [NoAlias, ReadOnly] public NativeList<GeneratedNodeDefinition>      generatedNodeDefinitions;

            // Read/Write
            [NativeDisableUnsafePtrRestriction, NoAlias] public IDManager*      hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias] public IDManager*      nodeIDLookupPtr;
            [NoAlias] public NativeList<CompactNodeID>                          nodesLookup;
            [NoAlias] public NativeList<CompactHierarchy>                       hierarchyList;
            [NativeDisableContainerSafetyRestriction]
            [NoAlias] public NativeHashMap<int, RefCountedBrushMeshBlob>        brushMeshBlobCache;
             
            public void Execute()
            {
                ref var hierarchyIDLookup   = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup        = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);

                // TODO: set all unique hierarchies dirty separately, somehow. Make this job parallel
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafePtr();
                for (int index = 0; index < generatedNodeDefinitions.Length; index++)
                {
                    var hierarchyIndex  = generatedNodeDefinitions[index].hierarchyIndex;
                    var compactNodeID   = generatedNodeDefinitions[index].compactNodeID;
                    var transformation  = generatedNodeDefinitions[index].transformation;
                    var operation       = generatedNodeDefinitions[index].operation;
                    var brushMeshHash   = generatedNodeDefinitions[index].brushMeshHash;

                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];
                    compactHierarchy.SetState(compactNodeID, brushMeshBlobCache, brushMeshHash, operation, transformation);
                    compactHierarchy.SetTreeDirty(); 
                } 

                // Reverse order so that we don't move nodes around when they're already in order (which is the fast path)
                for (int index = generatedNodeDefinitions.Length - 1; index >= 0; index--) 
                {
                    var hierarchyIndex      = generatedNodeDefinitions[index].hierarchyIndex;
                    var parentCompactNodeID = generatedNodeDefinitions[index].parentCompactNodeID; 
                    var compactNodeID       = generatedNodeDefinitions[index].compactNodeID;
                    var siblingIndex        = generatedNodeDefinitions[index].siblingIndex;
                     
                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];
                    compactHierarchy.AttachToParentAt(ref hierarchyIDLookup, hierarchyList, ref nodeIDLookup, nodesLookup, parentCompactNodeID, siblingIndex, compactNodeID); // TODO: need to be able to do this
                }
            }
        }

        static JobHandle ScheduleAssignMeshesJob(bool runInParallel, 
                                                 NativeList<CompactHierarchy>           hierarchyList,
                                                 NativeList<GeneratedNodeDefinition>    generatedNodeDefinitions,
                                                 JobHandle                              dependsOn)
        {
            var sortJob             = new HierarchySortJob { generatedNodeDefinitions = generatedNodeDefinitions };
            var sortJobHandle       = sortJob.Schedule(runInParallel, dependsOn);
            var brushMeshBlobCache  = ChiselMeshLookup.Value.brushMeshBlobCache;
            var assignJob = new AssignMeshesJob
            {
                // Read
                generatedNodeDefinitions = generatedNodeDefinitions,
                
                // Read/Write
                //hierarchyIDLookupPtr
                //nodeIDLookupPtr
                //nodesLookup
                hierarchyList            = hierarchyList,
                brushMeshBlobCache       = brushMeshBlobCache
            };
            assignJob.InitializeLookups(); 
            return assignJob.Schedule(runInParallel, sortJobHandle);
        }

        public void Dispose()
        {
            if (generatorPools == null)
                return;

            var allGeneratorPools = generatorPools.ToArray();
            for (int i = allGeneratorPools.Length - 1; i >= 0; i--)
            {
                try { allGeneratorPools[i].Dispose(); }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            generatorPools.Clear();
            generatorPools = null;
        }
    }

    // TODO: move to core
    public interface GeneratorJobPool : System.IDisposable
    {
        void AllocateOrClear();
        bool HasJobs { get; }
        JobHandle ScheduleGenerateJob(JobHandle dependsOn);
        JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts);
        JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy>                      inHierarchyList, 
                                              [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>              outGeneratedNodeDefinitions,
                                              [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>>    outBrushMeshBlobs,
                                              JobHandle dependsOn);
    }

    // TODO: move to core, call ScheduleUpdate when hash of definition changes (no more manual calls)
    public class GeneratorBrushJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBrushGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>   surfaceDefinitions;
        NativeList<Generator>                                           generators;
        NativeList<BlobAssetReference<BrushMeshBlob>>                   brushMeshes;
        NativeList<NodeID>                                              nodes;

        JobHandle updateHierarchyJobHandle = default;
        JobHandle initializeArraysJobHandle = default;
        JobHandle previousJobHandle = default;
        
        public GeneratorBrushJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            updateHierarchyJobHandle.Complete();
            updateHierarchyJobHandle = default;
            initializeArraysJobHandle.Complete();
            initializeArraysJobHandle = default;
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;
            
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Clear(); else surfaceDefinitions = new NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (brushMeshes       .IsCreated) brushMeshes       .Clear(); else brushMeshes        = new NativeList<BlobAssetReference<BrushMeshBlob>>(Allocator.Persistent);
            if (generators        .IsCreated) generators        .Clear(); else generators         = new NativeList<Generator>(Allocator.Persistent);
            if (nodes             .IsCreated) nodes             .Clear(); else nodes              = new NativeList<NodeID>(Allocator.Persistent);
        }

        public void Dispose()
        {
            GeneratorJobPoolManager.Unregister(this);
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Dispose();
            if (brushMeshes       .IsCreated) brushMeshes       .Dispose();
            if (generators        .IsCreated) generators        .Dispose();
            if (nodes             .IsCreated) nodes             .Dispose();

            surfaceDefinitions = default;
            brushMeshes = default;
            generators = default;
            nodes = default;
        }

        public bool HasJobs
        {
            get
            {
                previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
                previousJobHandle = default;

                return generators.IsCreated && generators.Length > 0;
            }
        }

        public void ScheduleUpdate(CSGTreeNode node, Generator settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            if (!nodes.IsCreated)
                AllocateOrClear();

            if (!surfaceDefinition.IsCreated)
                return;

            var nodeID = node.nodeID;
            var index = nodes.IndexOf(nodeID);
            if (index != -1)
            {
                surfaceDefinitions[index] = surfaceDefinition;
                generators        [index] = settings;
                nodes             [index] = nodeID;
            } else
            { 
                surfaceDefinitions.Add(surfaceDefinition);
                generators        .Add(settings);
                nodes             .Add(nodeID); 
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<Generator>                                           settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>>   surfaceDefinitions;
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>                  brushMeshes;

            public void Execute(int index)
            {
                if (!surfaceDefinitions.IsCreated ||
                    !surfaceDefinitions[index].IsCreated)
                {
                    brushMeshes[index] = default;
                    return;
                }

                brushMeshes[index] = settings[index].GenerateMesh(surfaceDefinitions[index], Allocator.Persistent);
            }
        }

        public JobHandle ScheduleGenerateJob(JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!nodes.IsCreated)
                return dependsOn;

            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                var nodeID = nodes[i];
                if (surfaceDefinitions[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Brush)
                    continue;

                surfaceDefinitions.RemoveAt(i);
                generators.RemoveAt(i);
                nodes.RemoveAt(i);
            }

            if (nodes.Length == 0)
                return dependsOn;

            brushMeshes.Clear();
            brushMeshes.Resize(generators.Length, NativeArrayOptions.ClearMemory);
            var job = new CreateBrushesJob
            {
                settings            = generators            .AsArray(),
                surfaceDefinitions  = surfaceDefinitions    .AsArray(),
                brushMeshes         = brushMeshes           .AsDeferredJobArray()
            };
            var createJobHandle = job.Schedule(generators, 8, dependsOn);
            var surfaceDeepDisposeJobHandle = surfaceDefinitions.DisposeDeep(createJobHandle);
            var generatorsDisposeJobHandle = generators.Dispose(createJobHandle);
            generators = default;
            surfaceDefinitions = default;
            return JobHandleExtensions.CombineDependencies(createJobHandle, surfaceDeepDisposeJobHandle, generatorsDisposeJobHandle);
        }
        
        [BurstCompile(CompileSynchronously = true)]
        unsafe struct UpdateHierarchyJob : IJob
        {
            [NoAlias, ReadOnly] public NativeList<NodeID>   nodes;
            [NoAlias, ReadOnly] public int                  index;
            [NoAlias, WriteOnly] public NativeArray<int>    totalCounts;

            public void Execute()
            {
                totalCounts[index] = nodes.Length;
            }
        }
         
        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                nodes       = nodes,
                index       = index,
                totalCounts = totalCounts
            };
            updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return updateHierarchyJobHandle;
        }

        [BurstCompile(CompileSynchronously = true)] 
        unsafe struct InitializeArraysJob : IJob
        {
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    nodeIDLookupPtr;
            [NoAlias, ReadOnly] public NativeList<CompactNodeID>                        nodesLookup;

            [NoAlias, ReadOnly] public NativeArray<NodeID>                              nodes;
            [NoAlias, ReadOnly] public NativeArray<CompactHierarchy>                    hierarchyList;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshes;

            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter              generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>.ParallelWriter    brushMeshBlobs;

            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < nodes.Length; i++) 
                {
                    var nodeID              = nodes[i];
                    var compactNodeID       = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, nodeID);
                    var hierarchyIndex      = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, compactNodeID);
                    var transformation      = hierarchyListPtr[hierarchyIndex].GetLocalTransformation(compactNodeID);
                    var operation           = hierarchyListPtr[hierarchyIndex].GetOperation(compactNodeID);
                    var parentCompactNodeID = hierarchyListPtr[hierarchyIndex].ParentOf(compactNodeID);
                    var siblingIndex        = hierarchyListPtr[hierarchyIndex].SiblingIndexOf(compactNodeID);
                    var brushMeshBlob       = brushMeshes[i];

                    generatedNodeDefinitions.AddNoResize(new GeneratedNodeDefinition
                    {
                        parentCompactNodeID = parentCompactNodeID,
                        compactNodeID       = compactNodeID,
                        hierarchyIndex      = hierarchyIndex,
                        siblingIndex        = siblingIndex,
                        operation           = operation,
                        transformation      = transformation
                    });
                    brushMeshBlobs.AddNoResize(brushMeshBlob);
                }
            }
        }

        public JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodeDefinitions,
                                                     [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshBlobs,
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                nodes                       = nodes,
                brushMeshes                 = brushMeshes.AsDeferredJobArray(),
                hierarchyList               = hierarchyList,

                // Write
                generatedNodeDefinitions    = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs              = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            initializeArraysJobHandle = initializeArraysJob.Schedule(true, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        nodes.Dispose(initializeArraysJobHandle),
                                        brushMeshes.Dispose(initializeArraysJobHandle));
            nodes = default;
            brushMeshes = default;
            return combinedJobHandle;
        }
    }

    public class GeneratorBranchJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        NativeList<Generator>                                         generators;
        NativeList<Range>                                             ranges;
        NativeList<GeneratedNode>                                     generatedNodes;
        NativeList<NodeID>                                            generatorNodeIDs;
        
        JobHandle previousJobHandle = default;

        public GeneratorBranchJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Clear(); else surfaceDefinitions = new NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (generatorNodeIDs  .IsCreated) generatorNodeIDs  .Clear(); else generatorNodeIDs   = new NativeList<NodeID>(Allocator.Persistent);
            if (generatedNodes    .IsCreated) generatedNodes    .Clear(); else generatedNodes     = new NativeList<GeneratedNode>(Allocator.Persistent);
            if (generators        .IsCreated) generators        .Clear(); else generators         = new NativeList<Generator>(Allocator.Persistent);
            if (ranges            .IsCreated) ranges            .Clear(); else ranges             = new NativeList<Range>(Allocator.Persistent);
        }

        public void Dispose()
        {
            GeneratorJobPoolManager.Unregister(this);
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Dispose();
            if (generatorNodeIDs  .IsCreated) generatorNodeIDs  .Dispose();
            if (generatedNodes    .IsCreated) generatedNodes    .Dispose();
            if (generators        .IsCreated) generators        .Dispose();
            if (ranges            .IsCreated) ranges            .Dispose();

            surfaceDefinitions = default;
            generatorNodeIDs = default;
            generatedNodes = default;
            generators = default;
            ranges = default;
        }

        public void ScheduleUpdate(CSGTreeNode node, Generator settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            if (!generatorNodeIDs.IsCreated)
                AllocateOrClear();

            if (!surfaceDefinition.IsCreated)
                return;

            var nodeID = node.nodeID;
            var index = generatorNodeIDs.IndexOf(nodeID);
            if (index != -1)
            {
                surfaceDefinitions[index] = surfaceDefinition;
                generators        [index] = settings;
                generatorNodeIDs  [index] = nodeID;
            } else
            {
                surfaceDefinitions.Add(surfaceDefinition);
                generators        .Add(settings);
                generatorNodeIDs  .Add(nodeID);
            }
        }

        public bool HasJobs
        {
            get
            {
                previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
                previousJobHandle = default;

                return generators.IsCreated && generators.Length > 0;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        {
            [NoAlias] public NativeArray<Generator>      settings;
            [NoAlias, WriteOnly] public NativeArray<int> brushCounts;

            public unsafe void Execute(int index)
            {
                var setting = settings[index];
                brushCounts[index] = setting.PrepareAndCountRequiredBrushMeshes();
                settings[index] = setting;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct AllocateBrushesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int>     brushCounts;
            [NoAlias, WriteOnly] public NativeArray<Range>  ranges;
            [NoAlias] public NativeList<GeneratedNode>      generatedNodes;

            public void Execute()
            {
                var totalRequiredBrushCount = 0;
                for (int i = 0; i < brushCounts.Length; i++)
                {
                    var length = brushCounts[i];
                    var start = totalRequiredBrushCount;
                    var end = start + length;
                    ranges[i] = new Range { start = start, end = end };
                    totalRequiredBrushCount += length;
                }
                generatedNodes.Clear();
                generatedNodes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range>                     ranges;
            [NoAlias] public NativeArray<Generator>                 settings;
            [NativeDisableParallelForRestriction]
            [NoAlias, WriteOnly] public NativeArray<GeneratedNode>  generatedNodes;

            public void Execute(int index)
            {
                try
                {
                    var range = ranges[index];
                    var requiredSubMeshCount = range.Length;
                    if (requiredSubMeshCount != 0)
                    {
                        using var nodes = new NativeList<GeneratedNode>(requiredSubMeshCount, Allocator.Temp);
                        nodes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory); //<- get rid of resize, use add below

                        if (!surfaceDefinitions[index].IsCreated ||
                            !settings[index].GenerateNodes(surfaceDefinitions[index], nodes, Allocator.Persistent))
                        {
                            ranges[index] = new Range { start = 0, end = 0 };
                            return;
                        }

                        Debug.Assert(requiredSubMeshCount == nodes.Length);
                        if (requiredSubMeshCount != nodes.Length)
                            throw new InvalidOperationException();
                        for (int i = range.start, m = 0; i < range.end; i++, m++)
                            generatedNodes[i] = nodes[m];
                    }
                }
                finally
                {
                    settings[index].Dispose();
                }
            }
        }
        
        public JobHandle ScheduleGenerateJob(JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!generatorNodeIDs.IsCreated)
                return dependsOn;

            for (int i = generatorNodeIDs.Length - 1; i >= 0; i--)
            {
                var nodeID = generatorNodeIDs[i];
                if (surfaceDefinitions[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Branch)
                    continue;

                surfaceDefinitions.RemoveAt(i);
                generators.RemoveAt(i);
                generatorNodeIDs.RemoveAt(i);
            }

            if (generatorNodeIDs.Length == 0)
                return dependsOn;

            ranges.Clear();
            ranges.Resize(generators.Length, NativeArrayOptions.ClearMemory);

            var brushCounts = new NativeArray<int>(generators.Length, Allocator.TempJob);
            var countBrushesJob = new PrepareAndCountBrushesJob
            {
                settings            = generators.AsArray(),
                brushCounts         = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(generators, 8, dependsOn);
            var allocateBrushesJob = new AllocateBrushesJob
            {
                brushCounts         = brushCounts,
                ranges              = ranges.AsArray(),
                generatedNodes      = generatedNodes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(brushCountJobHandle);
            var createJob = new CreateBrushesJob
            {
                settings            = generators        .AsArray(),
                ranges              = ranges            .AsArray(),
                generatedNodes      = generatedNodes    .AsDeferredJobArray(),
                surfaceDefinitions  = surfaceDefinitions.AsDeferredJobArray()
            };
            var createJobHandle = createJob.Schedule(generators, 8, allocateBrushesJobHandle);
            var surfaceDeepDisposeJobHandle = surfaceDefinitions.DisposeDeep(createJobHandle);
            surfaceDefinitions = default;
            return brushCounts.Dispose(surfaceDeepDisposeJobHandle);
        }
        

        // TODO: implement a way to setup a full hierarchy here, instead of a list of brushes
        // TODO: make this burstable
        //[BurstCompile(CompileSynchronously = true)]
        unsafe struct UpdateHierarchyJob : IJob
        {
            [NoAlias, ReadOnly] public NativeList<Range>    ranges;
            [NoAlias, ReadOnly] public NativeList<NodeID>   nodes;
            
            [NoAlias] public NativeList<Generator>          generators;

            [NoAlias, ReadOnly] public int                  index;
            [NoAlias, WriteOnly] public NativeArray<int>    totalCounts;
            
            void ClearBrushes(CSGTreeBranch branch)
            {
                for (int i = branch.Count - 1; i >= 0; i--)
                    branch[i].Destroy();
                branch.Clear();
            }

            unsafe void BuildBrushes(CSGTreeBranch branch, int desiredBrushCount)
            {
                if (branch.Count < desiredBrushCount)
                {
                    var tree = branch.Tree;
                    var newBrushCount = desiredBrushCount - branch.Count;
                    var newRange = new NativeArray<CSGTreeNode>(newBrushCount, Allocator.Temp);
                    try
                    {
                        var userID = branch.UserID;
                        for (int i = 0; i < newBrushCount; i++)
                            newRange[i] = tree.CreateBrush(userID: userID, operation: CSGOperationType.Additive);
                        branch.AddRange((CSGTreeNode*)newRange.GetUnsafePtr(), newBrushCount);
                    }
                    finally { newRange.Dispose(); }
                } else
                {
                    for (int i = branch.Count - 1; i >= desiredBrushCount; i--)
                    {
                        var oldBrush = branch[i];
                        branch.RemoveAt(i);
                        oldBrush.Destroy();
                    }
                }
            }
        
            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < nodes.Length; i++)
                {
                    var range = ranges[i];
                    var branch = CSGTreeBranch.Find(nodes[i]);
                    if (!branch.Valid)
                        continue;

                    if (range.Length == 0)
                    {
                        ClearBrushes(branch);
                        continue;
                    }

                    count += range.Length;

                    if (branch.Count != range.Length)
                        BuildBrushes(branch, range.Length);
                }

                totalCounts[index] = count;
            }
        }

        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                nodes       = generatorNodeIDs,
                ranges      = ranges,
                generators  = generators,
                index       = index,
                totalCounts = totalCounts
            };
            var updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return generators.Dispose(updateHierarchyJobHandle);
        }

        
        [BurstCompile(CompileSynchronously = true)]
        unsafe struct InitializeArraysJob : IJob
        {
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            // Read
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    nodeIDLookupPtr;
            [NoAlias, ReadOnly] public NativeList<CompactNodeID>                        nodesLookup;

            [NoAlias, ReadOnly] public NativeArray<NodeID>              parentNodeIDs;
            [NoAlias, ReadOnly] public NativeArray<Range>               ranges;
            [NoAlias, ReadOnly] public NativeArray<CompactHierarchy>    hierarchyList;
            [NoAlias, ReadOnly] public NativeList<GeneratedNode>        generatedNodes;

            // Write
            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter              generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>.ParallelWriter    brushMeshBlobs;

            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < parentNodeIDs.Length; i++)
                {
                    var range = ranges[i];
                    if (range.Length == 0)
                        continue;
                    
                    var rootNodeID              = parentNodeIDs[i];
                    var rootCompactNodeID       = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, rootNodeID);
                    var hierarchyIndex          = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, rootCompactNodeID);
                    ref var compactHierarchy    = ref hierarchyListPtr[hierarchyIndex];
                    
                    // TODO: just pass an array of compactNodeIDs along from the place were we create these nodes (no lookup needed)
                    // TODO: how can we re-use existing compactNodeIDs instead re-creating them when possible?
                    var childCompactNodeIDs     = new NativeArray<CompactNodeID>(range.Length, Allocator.Temp);
                    for (int b = 0; b < range.Length; b++)
                        childCompactNodeIDs[b] = compactHierarchy.GetChildCompactNodeIDAtNoError(rootCompactNodeID, b);

                    // TODO: sort generatedNodes span, by parentIndex + original index so that all parentIndices are sequential, and in order from small to large

                    int siblingIndex = 0;
                    int prevParentIndex = -1;
                    for (int b = 0, m = range.start; m < range.end; b++, m++)
                    {
                        var compactNodeID       = childCompactNodeIDs[b];
                        var operation           = generatedNodes[m].operation;
                        var brushMeshBlob       = generatedNodes[m].brushMesh;
                        var parentIndex         = generatedNodes[m].parentIndex;
                        if (parentIndex < prevParentIndex || parentIndex >= b)
                            parentIndex = prevParentIndex;
                        if (prevParentIndex != parentIndex)
                            siblingIndex = 0;
                        var parentCompactNodeID = (parentIndex == -1) ? rootCompactNodeID : childCompactNodeIDs[parentIndex];
                        var transformation      = generatedNodes[m].transformation;
                        generatedNodeDefinitions.AddNoResize(new GeneratedNodeDefinition
                        {
                            parentCompactNodeID = parentCompactNodeID,
                            compactNodeID       = compactNodeID,
                            hierarchyIndex      = hierarchyIndex,
                            siblingIndex        = siblingIndex,
                            operation           = operation,
                            transformation      = transformation
                        });
                        brushMeshBlobs.AddNoResize(brushMeshBlob);
                        siblingIndex++;
                    }

                    childCompactNodeIDs.Dispose();
                }
            }
        }

        public JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy>                   hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>           generatedNodeDefinitions, 
                                                     [NoAlias, WriteOnly] NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshBlobs, 
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                parentNodeIDs   = generatorNodeIDs,
                ranges          = ranges,
                generatedNodes  = generatedNodes,
                hierarchyList   = hierarchyList,

                // Write
                generatedNodeDefinitions = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs           = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            var initializeArraysJobHandle = initializeArraysJob.Schedule(true, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        generatorNodeIDs.Dispose(initializeArraysJobHandle),
                                        ranges.Dispose(initializeArraysJobHandle),
                                        generatedNodes.Dispose(initializeArraysJobHandle));

            ranges = default;
            generatedNodes = default;
            generatorNodeIDs = default;
            return combinedJobHandle;
        }
    }
}
