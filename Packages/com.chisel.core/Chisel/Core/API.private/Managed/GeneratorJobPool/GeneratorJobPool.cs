using System;
using System.Linq;
using Chisel.Core;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public struct GeneratedNodeDefinition
    {
        public int              hierarchyIndex;         // index to the hierarchy (model) our node lives on
        public CompactNodeID    parentCompactNodeID;    // node ID of the parent of this node
        public int              siblingIndex;           // the sibling index of this node, relative to other children of our parent
        public CompactNodeID    compactNodeID;          // the node ID of this node

        public CSGOperationType operation;              // the type of CSG operation of this node
        public float4x4         transformation;         // the transformation of this node
        public int              brushMeshHash;          // the hash of the brush-mesh (which is also the ID we lookup meshes with) this node uses (if any)
    }

    // TODO: move to core
    [BurstCompile(CompileSynchronously = true)]
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

            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>                  generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>>  brushMeshBlobs;

            public void Execute()
            {
                var totalCount = 0;
                for (int i = 0; i < totalCounts.Length; i++)
                    totalCount += totalCounts[i];
                generatedNodeDefinitions.Capacity = totalCount;
                brushMeshBlobs.Capacity = totalCount;
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        static readonly List<GeneratorJobPool> generatorJobs = new List<GeneratorJobPool>();

        // TODO: Optimize this
        public static JobHandle ScheduleJobs(bool runInParallel, JobHandle dependsOn = default)
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
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.ScheduleGenerateJob(runInParallel, dependsOn));
            Profiler.EndSample();

            NativeList<GeneratedNodeDefinition> generatedNodeDefinitions = default;
            NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshBlobs = default;
            NativeArray<int> totalCounts = default;
            var lastJobHandle = combinedJobHandle;
            var allocateJobHandle = default(JobHandle);
            try
            {
                Profiler.BeginSample("GenPool_UpdateHierarchy");
                totalCounts = new NativeArray<int>(generatorJobs.Count, defaultAllocator);
                int index = 0;
                foreach (var pool in generatorJobs)
                {
                    lastJobHandle = pool.ScheduleUpdateHierarchyJob(runInParallel, lastJobHandle, index, totalCounts);
                    index++;
                }
                Profiler.EndSample();

                Profiler.BeginSample("GenPool_Allocator"); 
                generatedNodeDefinitions    = new NativeList<GeneratedNodeDefinition>(defaultAllocator);
                brushMeshBlobs              = new NativeList<ChiselBlobAssetReference<BrushMeshBlob>>(defaultAllocator);
                var resizeTempLists = new ResizeTempLists
                {
                    // Read
                    totalCounts                 = totalCounts,

                    // Read / Write
                    generatedNodeDefinitions    = generatedNodeDefinitions,
                    brushMeshBlobs              = brushMeshBlobs
                };
                allocateJobHandle = resizeTempLists.Schedule(runInParallel, lastJobHandle);
                Profiler.EndSample();
            
                Profiler.BeginSample("GenPool_Complete");
                allocateJobHandle.Complete(); // TODO: get rid of this somehow
                Profiler.EndSample();

                Profiler.BeginSample("GenPool_Schedule");
                lastJobHandle = default;
                combinedJobHandle = allocateJobHandle;
                foreach (var pool in generatorJobs)
                {
                    var scheduleJobHandle = pool.ScheduleInitializeArraysJob(runInParallel, 
                                                                             // Read
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
                lastJobHandle = BrushMeshManager.ScheduleBrushRegistration(runInParallel, brushMeshBlobs, generatedNodeDefinitions, lastJobHandle);
                lastJobHandle = ScheduleAssignMeshesJob(runInParallel, hierarchyList, generatedNodeDefinitions, lastJobHandle);
                previousJobHandle = lastJobHandle;
                Profiler.EndSample();
            }
            finally
            {
                Profiler.BeginSample("GenPool_Dispose");
                var totalCountsDisposeJobHandle                 = totalCounts.Dispose(allocateJobHandle);
                totalCounts = default;
                var generatedNodeDefinitionsDisposeJobHandle    = generatedNodeDefinitions.Dispose(lastJobHandle);
                generatedNodeDefinitions = default;
                var brushMeshBlobsDisposeJobHandle              = brushMeshBlobs.Dispose(lastJobHandle);
                brushMeshBlobs = default;
                var allDisposes = JobHandle.CombineDependencies(totalCountsDisposeJobHandle,
                                                                generatedNodeDefinitionsDisposeJobHandle,
                                                                brushMeshBlobsDisposeJobHandle);
                Profiler.EndSample();
            }

            return lastJobHandle;
        }
        
        //[BurstCompile(CompileSynchronously = true)]
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


        //[BurstCompile(CompileSynchronously = true)]
        unsafe struct AssignMeshesJob : IJob
        {            
            public void InitializeLookups()
            {
                hierarchyIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.HierarchyIDLookup);
                nodeIDLookupPtr = (IDManager*)UnsafeUtility.AddressOf(ref CompactHierarchyManager.NodeIDLookup);
                nodesLookup = CompactHierarchyManager.Nodes;
            }

            // Read
            [NoAlias, ReadOnly] public NativeList<GeneratedNodeDefinition>      generatedNodeDefinitions;

            // Read/Write
            [NoAlias] public PointerReference<IDManager>                        hierarchyIDLookupRef;
            [NativeDisableUnsafePtrRestriction, NoAlias] public IDManager*      hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias] public IDManager*      nodeIDLookupPtr;
            [NoAlias] public NativeList<CompactNodeID>                          nodesLookup;
            [NoAlias] public NativeList<CompactHierarchy>                       hierarchyList;
            [NativeDisableContainerSafetyRestriction]
            [NoAlias] public NativeParallelHashMap<int, RefCountedBrushMeshBlob>        brushMeshBlobCache;
             
            public void Execute()
            {
                if (!generatedNodeDefinitions.IsCreated)
                    return;

                ref var hierarchyIDLookup   = ref hierarchyIDLookupRef.Value;// UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                //ref var hierarchyIDLookup   = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup        = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);

                // TODO: set all unique hierarchies dirty separately, somehow. Make this job parallel
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafePtr();
                for (int index = 0; index < generatedNodeDefinitions.Length; index++)
                {
                    if (index >= generatedNodeDefinitions.Length)
                        throw new Exception($"index {index} >= generatedNodeDefinitions.Length {generatedNodeDefinitions.Length}");

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
                    if (index >= generatedNodeDefinitions.Length)
                        throw new Exception($"index {index} >= generatedNodeDefinitions.Length {generatedNodeDefinitions.Length}");

                    var hierarchyIndex      = generatedNodeDefinitions[index].hierarchyIndex;
                    var parentCompactNodeID = generatedNodeDefinitions[index].parentCompactNodeID; 
                    var compactNodeID       = generatedNodeDefinitions[index].compactNodeID;
                    var siblingIndex        = generatedNodeDefinitions[index].siblingIndex;
                     
                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];
                    compactHierarchy.AttachToParentAt(ref hierarchyIDLookup, hierarchyList, ref nodeIDLookup, nodesLookup, parentCompactNodeID, siblingIndex, compactNodeID); // TODO: need to be able to do this
                }
            }
        }

        static JobHandle ScheduleAssignMeshesJob(bool                                   runInParallel, 
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
                hierarchyIDLookupRef     = new PointerReference<IDManager>(ref CompactHierarchyManager.HierarchyIDLookup),
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
                try 
                { 
                    allGeneratorPools[i].Dispose(); 
                    allGeneratorPools[i] = default; 
                }
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
        JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn);
        JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts);
        JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                              [NoAlias, ReadOnly] NativeList<CompactHierarchy>                          inHierarchyList, 
                                              [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>                  outGeneratedNodeDefinitions,
                                              [NoAlias, WriteOnly] NativeList<ChiselBlobAssetReference<BrushMeshBlob>>  outBrushMeshBlobs,
                                              JobHandle dependsOn);
    }

    [BurstCompile(CompileSynchronously = true)]
    struct CreateBrushesJob<Generator> : IJobParallelForDefer
        where Generator : unmanaged, IBrushGenerator
    {
        [NoAlias, ReadOnly] public NativeList<Generator> settings;
        [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshes;

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

    [BurstCompile(CompileSynchronously = true)]
    struct UpdateHierarchyJob : IJob
    {
        [NoAlias, ReadOnly] public NativeList<NodeID> generatorNodes;
        [NoAlias, ReadOnly] public int index;
        [NoAlias, WriteOnly] public NativeArray<int> totalCounts;

        public void Execute()
        {
            totalCounts[index] = generatorNodes.Length;
        }
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

        [NoAlias, ReadOnly] public NativeList<NodeID>                                   nodes;
        [NoAlias, ReadOnly] public NativeList<CompactHierarchy>                         hierarchyList;
        [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>>  brushMeshes;

        // Write
        [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter                  generatedNodeDefinitions;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>>.ParallelWriter  brushMeshBlobs;

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

    // TODO: move to core, call ScheduleUpdate when hash of definition changes (no more manual calls)
    [BurstCompile(CompileSynchronously = true)]
    public class GeneratorBrushJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBrushGenerator
    {
        NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        NativeList<Generator>                                               generators;
        NativeList<ChiselBlobAssetReference<BrushMeshBlob>>                 brushMeshes;
        NativeList<NodeID>                                                  generatorNodes;

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
            
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Clear(); else surfaceDefinitions = new NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (brushMeshes       .IsCreated) brushMeshes       .Clear(); else brushMeshes        = new NativeList<ChiselBlobAssetReference<BrushMeshBlob>>(Allocator.Persistent);
            if (generators        .IsCreated) generators        .Clear(); else generators         = new NativeList<Generator>(Allocator.Persistent);
            if (generatorNodes    .IsCreated) generatorNodes    .Clear(); else generatorNodes     = new NativeList<NodeID>(Allocator.Persistent);
        }

        public void Dispose()
        {
            GeneratorJobPoolManager.Unregister(this);
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.DisposeDeep();
            if (brushMeshes       .IsCreated) brushMeshes       .Dispose();
            if (generators        .IsCreated) generators        .Dispose();
            if (generatorNodes    .IsCreated) generatorNodes    .Dispose();

            surfaceDefinitions = default;
            brushMeshes = default;
            generators = default;
            generatorNodes = default;
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

        public void ScheduleUpdate(CSGTreeNode generatorNode, Generator settings, ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            if (!generatorNodes.IsCreated)
                AllocateOrClear();

            if (!surfaceDefinition.IsCreated)
                return;

            var nodeID = generatorNode.nodeID;
            var index = generatorNodes.IndexOf(nodeID);
            if (index != -1)
            {
                // TODO: get rid of this
                if (surfaceDefinitions[index].IsCreated)
                    surfaceDefinitions[index].Dispose();

                surfaceDefinitions[index] = surfaceDefinition;
                generators        [index] = settings;
                generatorNodes    [index] = nodeID;
            } else
            { 
                surfaceDefinitions.Add(surfaceDefinition);
                generators        .Add(settings);
                generatorNodes    .Add(nodeID); 
            }
        }

        public JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!generatorNodes.IsCreated)
                return dependsOn;

            for (int i = generatorNodes.Length - 1; i >= 0; i--)
            {
                var nodeID = generatorNodes[i];
                if (surfaceDefinitions[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Brush)
                    continue;

                if (surfaceDefinitions[i].IsCreated)
                    surfaceDefinitions[i].Dispose();
                surfaceDefinitions.RemoveAt(i);
                generators        .RemoveAt(i);
                generatorNodes    .RemoveAt(i);
            }

            if (generatorNodes.Length == 0)
                return dependsOn;

            brushMeshes.Clear();
            brushMeshes.Resize(generators.Length, NativeArrayOptions.ClearMemory);
            var job = new CreateBrushesJob<Generator>
            {
                settings            = generators,
                surfaceDefinitions  = surfaceDefinitions,
                brushMeshes         = brushMeshes
            };
            var createJobHandle = job.Schedule(runInParallel, generators, 8, dependsOn);
            var surfaceDeepDisposeJobHandle = surfaceDefinitions.DisposeDeep(createJobHandle);
            var generatorsDisposeJobHandle = generators.Dispose(createJobHandle);
            generators = default;
            surfaceDefinitions = default;
            return JobHandleExtensions.CombineDependencies(createJobHandle, surfaceDeepDisposeJobHandle, generatorsDisposeJobHandle);
        }
         
        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                generatorNodes  = generatorNodes,
                index           = index,
                totalCounts     = totalCounts
            };
            updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return updateHierarchyJobHandle;
        }

        public JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                                     [NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodeDefinitions,
                                                     [NoAlias, WriteOnly] NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshBlobs,
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                nodes                       = generatorNodes,
                brushMeshes                 = brushMeshes,
                hierarchyList               = hierarchyList,

                // Write
                generatedNodeDefinitions    = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs              = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            initializeArraysJobHandle = initializeArraysJob.Schedule(runInParallel, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        generatorNodes.Dispose(initializeArraysJobHandle),
                                        brushMeshes.Dispose(initializeArraysJobHandle));
            generatorNodes = default;
            brushMeshes = default;
            return combinedJobHandle;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public class GeneratorBranchJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        NativeList<Generator>       generators;
        NativeList<NodeID>          generatorRootNodeIDs;
        NativeList<Range>           generatorNodeRanges;
        NativeList<GeneratedNode>   generatedNodes;
        
        JobHandle previousJobHandle = default;

        public GeneratorBranchJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (generatorRootNodeIDs.IsCreated) generatorRootNodeIDs.Clear(); else generatorRootNodeIDs = new NativeList<NodeID>(Allocator.Persistent);
            if (generatorNodeRanges .IsCreated) generatorNodeRanges .Clear(); else generatorNodeRanges  = new NativeList<Range>(Allocator.Persistent);
            if (surfaceDefinitions  .IsCreated) surfaceDefinitions  .Clear(); else surfaceDefinitions   = new NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (generatedNodes      .IsCreated) generatedNodes      .Clear(); else generatedNodes       = new NativeList<GeneratedNode>(Allocator.Persistent);
            if (generators          .IsCreated) generators          .Clear(); else generators           = new NativeList<Generator>(Allocator.Persistent);
        }

        public void Dispose()
        {
            GeneratorJobPoolManager.Unregister(this);
            if (generatorRootNodeIDs.IsCreated) generatorRootNodeIDs.SafeDispose();
            if (generatorNodeRanges .IsCreated) generatorNodeRanges .SafeDispose();
            if (surfaceDefinitions  .IsCreated) surfaceDefinitions  .DisposeDeep();
            if (generatedNodes      .IsCreated) generatedNodes      .SafeDispose();
            if (generators          .IsCreated) generators          .SafeDispose();
            
            generatorRootNodeIDs = default;
            generatorNodeRanges = default;
            surfaceDefinitions = default;
            generatedNodes = default;
            generators = default;
        }

        public void ScheduleUpdate(CSGTreeNode node, Generator settings, ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            if (!generatorRootNodeIDs.IsCreated)
                AllocateOrClear();

            if (!surfaceDefinition.IsCreated)
                return;

            var nodeID = node.nodeID;
            var index = generatorRootNodeIDs.IndexOf(nodeID);
            if (index != -1)
            {
                // TODO: get rid of this
                if (surfaceDefinitions[index].IsCreated)
                    surfaceDefinitions[index].Dispose();

                generatorRootNodeIDs[index] = nodeID;
                surfaceDefinitions  [index] = surfaceDefinition;
                generators          [index] = settings;
            } else
            {
                generatorRootNodeIDs.Add(nodeID);
                surfaceDefinitions  .Add(surfaceDefinition);
                generators          .Add(settings);
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
            [NativeDisableUnsafePtrRestriction]
            [NoAlias] public UnsafeList<Generator>*     settings;
            [NoAlias, ReadOnly] public NativeList<Generator>      generators; // required because it's used for the count of IJobParallelForDefer
            [NativeDisableParallelForRestriction]
            [NoAlias] public NativeArray<int>           brushCounts;

            public void Execute(int index)
            {
                ref var setting = ref settings->ElementAt(index);
                brushCounts[index] = setting.PrepareAndCountRequiredBrushMeshes();
            }
        }
         
        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct AllocateBrushesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int>     brushCounts;
            [NativeDisableUnsafePtrRestriction]
            [NoAlias, WriteOnly] public UnsafeList<Range>*  ranges;
            [NoAlias] public NativeList<GeneratedNode>      generatedNodes;

            public void Execute()
            {
                var totalRequiredBrushCount = 0;
                for (int i = 0; i < brushCounts.Length; i++)
                {
                    var length = brushCounts[i];
                    var start = totalRequiredBrushCount;
                    var end = start + length;
                    (*ranges)[i] = new Range { start = start, end = end };
                    totalRequiredBrushCount += length;
                }
                generatedNodes.Clear();
                generatedNodes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<NativeChiselSurfaceDefinition>>  surfaceDefinitions;
            [NoAlias, ReadOnly] public NativeList<Generator> generators; // required because it's used for the count of IJobParallelForDefer

            [NativeDisableUnsafePtrRestriction]
            [NoAlias] public UnsafeList<Generator>* settings;
            
            [NativeDisableUnsafePtrRestriction]
            [NoAlias] public UnsafeList<Range>*     ranges;

            [NativeDisableParallelForRestriction]
            [NoAlias, WriteOnly] public NativeList<GeneratedNode>   generatedNodes;

            public void Execute(int index)
            {
                try
                {
                    ref var range = ref ranges->ElementAt(index);
                    var requiredSubMeshCount = range.Length;
                    if (requiredSubMeshCount != 0)
                    {
                        using var nodes = new NativeList<GeneratedNode>(requiredSubMeshCount, Allocator.Temp);
                        nodes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory); //<- get rid of resize, use add below

                        ref var setting = ref settings->ElementAt(index);
                        if (!surfaceDefinitions[index].IsCreated ||
                            !setting.GenerateNodes(surfaceDefinitions[index], nodes, Allocator.Persistent))
                        {
                            range = new Range { start = 0, end = 0 };
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
                    ref var setting = ref settings->ElementAt(index);
                    setting.Dispose();
                }
            }
        }

        const Allocator defaultAllocator = Allocator.TempJob;

        public unsafe JobHandle ScheduleGenerateJob(bool runInParallel, JobHandle dependsOn = default)
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (!generatorRootNodeIDs.IsCreated)
                return dependsOn;

            for (int i = generatorRootNodeIDs.Length - 1; i >= 0; i--)
            {
                var nodeID = generatorRootNodeIDs[i];
                if (surfaceDefinitions[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Branch)
                    continue;

                if (surfaceDefinitions[i].IsCreated)
                    surfaceDefinitions[i].Dispose();

                generatorRootNodeIDs.RemoveAt(i);
                surfaceDefinitions  .RemoveAt(i);
                generators          .RemoveAt(i);
            }

            if (generatorRootNodeIDs.Length == 0)
                return dependsOn;

            generatorNodeRanges.Clear();
            generatorNodeRanges.Resize(generators.Length, NativeArrayOptions.ClearMemory);

            var brushCounts = new NativeArray<int>(generators.Length, defaultAllocator);
            var countBrushesJob = new PrepareAndCountBrushesJob
            {
                settings            = generators.GetUnsafeList(),
                generators          = generators,// required because it's used for the count of IJobParallelForDefer
                brushCounts         = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(runInParallel, generators, 8, dependsOn);
            
            var allocateBrushesJob = new AllocateBrushesJob
            {
                brushCounts         = brushCounts,
                ranges              = generatorNodeRanges.GetUnsafeList(),
                generatedNodes      = generatedNodes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(runInParallel, brushCountJobHandle);

            var createJob = new CreateBrushesJob
            {
                settings            = generators.GetUnsafeList(),
                generators          = generators, // required because it's used for the count of IJobParallelForDefer
                ranges              = generatorNodeRanges.GetUnsafeList(),
                generatedNodes      = generatedNodes,
                surfaceDefinitions  = surfaceDefinitions
            };
            var createJobHandle = createJob.Schedule(runInParallel, generators, 8, allocateBrushesJobHandle);

            var surfaceDeepDisposeJobHandle = surfaceDefinitions.DisposeDeep(createJobHandle);
            surfaceDefinitions = default;
            return brushCounts.Dispose(surfaceDeepDisposeJobHandle);
        }
        

        // TODO: implement a way to setup a full hierarchy here, instead of a list of brushes
        // TODO: make this burstable
        [BurstCompile(CompileSynchronously = true)]
        struct UpdateHierarchyJob : IJob
        {
            [NoAlias, ReadOnly] public NativeList<NodeID>   generatorRootNodeIDs;
            [NoAlias, ReadOnly] public NativeList<Range>    generatorNodeRanges;
            
            [NoAlias] public NativeList<Generator>          generators;

            [NoAlias, ReadOnly] public int                  index;
            [NoAlias, WriteOnly] public NativeArray<int>    totalCounts;
            
            void ClearBrushes(CSGTreeBranch branch)
            {
                for (int i = branch.Count - 1; i >= 0; i--)
                    branch[i].Destroy();
                branch.Clear();
            }

            void BuildBrushes(CSGTreeBranch branch, int desiredBrushCount)
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
                        branch.AddRange(newRange, newBrushCount);
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
                for (int i = 0; i < generatorRootNodeIDs.Length; i++)
                {
                    var range = generatorNodeRanges[i];
                    var branch = CSGTreeBranch.Find(generatorRootNodeIDs[i]);
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
                generatorRootNodeIDs = generatorRootNodeIDs,
                generatorNodeRanges  = generatorNodeRanges,
                generators           = generators,
                index                = index,
                totalCounts          = totalCounts
            };
            // TODO: make this work in parallel (create new linear stairs => errors)
#if true
            dependsOn.Complete();
            updateHierarchyJob.Execute();
            return generators.Dispose((JobHandle)default);
#else
            var updateHierarchyJobHandle = updateHierarchyJob.Schedule(runInParallel, dependsOn);
            return generators.Dispose(updateHierarchyJobHandle);
#endif
        }
        
        //[BurstCompile(CompileSynchronously = true)]
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

            [NoAlias, ReadOnly] public NativeList<NodeID>               generatorRootNodeIDs;
            [NoAlias, ReadOnly] public NativeList<Range>                generatorNodeRanges;
            [NoAlias, ReadOnly] public NativeList<CompactHierarchy>     hierarchyList;
            [NoAlias, ReadOnly] public NativeList<GeneratedNode>        generatedNodes;

            // Write
            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter                  generatedNodeDefinitions;
            [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushMeshBlob>>.ParallelWriter  brushMeshBlobs;

            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < generatorRootNodeIDs.Length; i++)
                {
                    var range = generatorNodeRanges[i];
                    if (range.Length == 0)
                        continue;
                    
                    var rootNodeID              = generatorRootNodeIDs[i];
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

        public JobHandle ScheduleInitializeArraysJob(bool runInParallel, 
                                                     [NoAlias, ReadOnly] NativeList<CompactHierarchy>                   hierarchyList, 
                                                     [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition>           generatedNodeDefinitions, 
                                                     [NoAlias, WriteOnly] NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshBlobs, 
                                                     JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                generatorRootNodeIDs    = generatorRootNodeIDs,
                generatorNodeRanges     = generatorNodeRanges,
                generatedNodes          = generatedNodes,
                hierarchyList           = hierarchyList,

                // Write
                generatedNodeDefinitions = generatedNodeDefinitions.AsParallelWriter(),
                brushMeshBlobs           = brushMeshBlobs.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            var initializeArraysJobHandle = initializeArraysJob.Schedule(runInParallel, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        generatorRootNodeIDs.Dispose(initializeArraysJobHandle),
                                        generatorNodeRanges.Dispose(initializeArraysJobHandle),
                                        generatedNodes.Dispose(initializeArraysJobHandle));

            generatorRootNodeIDs = default;
            generatorNodeRanges = default;
            generatedNodes = default;
            return combinedJobHandle;
        }
    }
}
