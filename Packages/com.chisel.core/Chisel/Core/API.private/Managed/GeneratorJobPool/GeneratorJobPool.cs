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
        public CompactNodeID                        compactNodeID;
        public int                                  hierarchyIndex;
        public float4x4                             transformation;
        public BlobAssetReference<BrushMeshBlob>    brushMeshBlob;
    }

    public class GeneratorJobPoolManager : System.IDisposable
    {
        System.Collections.Generic.HashSet<GeneratorJobPool> generatorPools = new System.Collections.Generic.HashSet<GeneratorJobPool>();

        static GeneratorJobPoolManager s_Instance;        
        public static GeneratorJobPoolManager Instance => (s_Instance ??= new GeneratorJobPoolManager());

        public static bool Register(GeneratorJobPool pool) { return Instance.generatorPools.Add(pool); }
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
        unsafe struct GetHierarchyInformationJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<CompactHierarchy>         hierarchyList;
            [NoAlias, ReadOnly] public NativeArray<GeneratedNodeDefinition>  generatedNodes;
            
            [NoAlias, WriteOnly] public NativeArray<int>                hierarchyIndices;
            [NoAlias, WriteOnly] public NativeArray<CompactNodeID>      compactNodes;
            [NoAlias, WriteOnly] public NativeArray<float4x4>           transformations;

            public void Execute()
            {
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < generatedNodes.Length; i++)
                {
                    var compactNodeID   = generatedNodes[i].compactNodeID;
                    var hierarchyIndex  = generatedNodes[i].hierarchyIndex;
                    var transformation  = hierarchyListPtr[hierarchyIndex].GetTransformation(compactNodeID);
                    hierarchyIndices[i] = hierarchyIndex;
                    compactNodes    [i] = compactNodeID;
                    transformations [i] = generatedNodes[i].transformation;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct ResizeTempLists : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int>           totalCounts;
            
            [NoAlias] public NativeList<GeneratedNodeDefinition>  generatedNodes;
            [NoAlias] public NativeList<int>                      brushMeshHashes;
            [NoAlias] public NativeList<int>                      hierarchyIndices;
            [NoAlias] public NativeList<CompactNodeID>            compactNodes;
            [NoAlias] public NativeList<float4x4>                 transformations;

            public void Execute()
            {
                var totalCount = 0;
                for (int i = 0; i < totalCounts.Length; i++)
                    totalCount += totalCounts[i];
                generatedNodes.Capacity = totalCount;
                brushMeshHashes.Resize(totalCount, NativeArrayOptions.ClearMemory);
                hierarchyIndices.Resize(totalCount, NativeArrayOptions.ClearMemory);
                compactNodes.Resize(totalCount, NativeArrayOptions.ClearMemory);
                transformations.Resize(totalCount, NativeArrayOptions.ClearMemory);
            }
        }

        // TODO: Optimize this
        public static unsafe JobHandle ScheduleJobs(JobHandle dependsOn = default)
        {
            var hierarchyList = CompactHierarchyManager.HierarchyList;

            var combinedJobHandle = (JobHandle)default;
            var allGeneratorPools = Instance.generatorPools;
            Profiler.BeginSample("GenPool_Generate");
            foreach (var pool in allGeneratorPools)
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.ScheduleGenerateJob(dependsOn));
            Profiler.EndSample();

            Profiler.BeginSample("GenPool_UpdateHierarchy");
            var totalCounts = new NativeArray<int>(allGeneratorPools.Count, Allocator.TempJob);
            var lastJobHandle = combinedJobHandle;
            int index = 0;
            foreach (var pool in allGeneratorPools)
            {
                lastJobHandle = pool.ScheduleUpdateHierarchyJob(runInParallel: true, lastJobHandle, index, totalCounts);
                index++;
            }
            Profiler.EndSample();

            Profiler.BeginSample("GenPool_Allocator"); 
            var generatedNodes      = new NativeList<GeneratedNodeDefinition>(Allocator.TempJob);
            var brushMeshHashes     = new NativeList<int>(Allocator.TempJob);
            var hierarchyIndices    = new NativeList<int>(Allocator.TempJob);
            var compactNodes        = new NativeList<CompactNodeID>(Allocator.TempJob);
            var transformations     = new NativeList<float4x4>(Allocator.TempJob);

            var resizeTempLists = new ResizeTempLists
            {
                totalCounts         = totalCounts,
                generatedNodes      = generatedNodes,
                brushMeshHashes     = brushMeshHashes,
                hierarchyIndices    = hierarchyIndices,
                compactNodes        = compactNodes,
                transformations     = transformations
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
            foreach (var pool in allGeneratorPools)
            {
                var scheduleJobHandle = pool.ScheduleInitializeArraysJob(hierarchyList, generatedNodes, JobHandle.CombineDependencies(allocateJobHandle, lastJobHandle));
                lastJobHandle = scheduleJobHandle;
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, scheduleJobHandle);
            }

            var getHierarchyInformationJob = new GetHierarchyInformationJob
            {
                hierarchyList       = hierarchyList .AsDeferredJobArray(),
                generatedNodes      = generatedNodes.AsDeferredJobArray(),

                hierarchyIndices    = hierarchyIndices.AsDeferredJobArray(),
                compactNodes        = compactNodes    .AsDeferredJobArray(),
                transformations     = transformations .AsDeferredJobArray()
            };
            lastJobHandle = getHierarchyInformationJob.Schedule(true, JobHandle.CombineDependencies(allocateJobHandle, combinedJobHandle));
            lastJobHandle = BrushMeshManager.ScheduleBrushRegistration(true, generatedNodes, brushMeshHashes, lastJobHandle);
            lastJobHandle = ScheduleAssignMeshesJob(true, hierarchyList, hierarchyIndices, compactNodes, transformations, brushMeshHashes, lastJobHandle);
            previousJobHandle = lastJobHandle;

            var transformationsDisposeJobHandle  = transformations .Dispose(lastJobHandle);
            var hierarchyIndicesDisposeJobHandle = hierarchyIndices.Dispose(lastJobHandle);
            var compactNodesDisposeJobHandle     = compactNodes    .Dispose(lastJobHandle);
            var brushMeshHashesDisposeJobHandle  = brushMeshHashes .Dispose(lastJobHandle);
            var generatedNodesDisposeJobHandle   = generatedNodes  .Dispose(lastJobHandle);

            var allDisposes = JobHandleExtensions.CombineDependencies(
                                    transformationsDisposeJobHandle,
                                    hierarchyIndicesDisposeJobHandle,
                                    compactNodesDisposeJobHandle,
                                    brushMeshHashesDisposeJobHandle,
                                    generatedNodesDisposeJobHandle
                                );
            Profiler.EndSample();
            return lastJobHandle;
        }
        
        [BurstCompile(CompileSynchronously = true)]
        struct HierarchySortJob : IJob
        {
            [NoAlias] public NativeList<int>            brushMeshHashes;
            [NoAlias] public NativeList<CompactNodeID>  compactNodes;
            [NoAlias] public NativeList<int>            hierarchyIndices;
            [NoAlias] public NativeList<float4x4>       transformations;
            
            public void Execute()
            {
                for (int a = 0; a < compactNodes.Length - 1; a++)
                {
                    var a_hierarchyID = hierarchyIndices[a];
                    var a_compactNodeID = compactNodes[a].value;
                    for (int b = a + 1; b < compactNodes.Length; b++)
                    {
                        var b_hierarchyID = hierarchyIndices[b];
                        var b_compactNodeID = compactNodes[b].value;
                        if (a_hierarchyID < b_hierarchyID)
                            continue;

                        if (a_hierarchyID == b_hierarchyID &&
                            a_compactNodeID < b_compactNodeID)
                            continue;

                        {
                            { var t = compactNodes[a];     compactNodes[a]     = compactNodes[b];     compactNodes[b]     = t; }
                            { var t = brushMeshHashes[a];  brushMeshHashes[a]  = brushMeshHashes[b];  brushMeshHashes[b]  = t; }
                            { var t = hierarchyIndices[a]; hierarchyIndices[a] = hierarchyIndices[b]; hierarchyIndices[b] = t; }
                            { var t = transformations[a];  transformations[a]  = transformations[b];  transformations[b]  = t; }
                        }

                        a_hierarchyID = b_hierarchyID;
                        a_compactNodeID = b_compactNodeID;
                    }
                }
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        unsafe struct AssignMeshesJob : IJob
        {
            // ReadOnly
            [NoAlias, ReadOnly] public NativeList<int>              brushMeshHashes;
            [NoAlias, ReadOnly] public NativeList<float4x4>         transformations;
            [NoAlias, ReadOnly] public NativeList<CompactNodeID>    compactNodes;
            [NoAlias, ReadOnly] public NativeList<int>              hierarchyIndices;

            [NoAlias, ReadOnly] public NativeHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobCache;

            // Read/Write
            [NoAlias] public NativeList<CompactHierarchy>           hierarchyList;

            public void Execute()
            {
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafePtr();
                for (int index = 0; index < compactNodes.Length; index++)
                {
                    var compactNodeID = compactNodes[index];
                    var hierarchyIndex = hierarchyIndices[index];
                    ref var compactHierarchy = ref hierarchyListPtr[hierarchyIndex];

                    var transformation  = transformations[index];
                    var brushMeshHash   = brushMeshHashes[index];
                    if (brushMeshHash == 0)
                        compactHierarchy.SetTransformation(compactNodeID, brushMeshBlobCache, transformation);
                    else
                        compactHierarchy.SetBrushMeshIDTransformation(compactNodeID, brushMeshBlobCache, brushMeshHash, transformation);
                    compactHierarchy.SetTreeDirty();
                }
            }
        } 


        static JobHandle ScheduleAssignMeshesJob(bool runInParallel, NativeList<CompactHierarchy> hierarchyList, NativeList<int> hierarchyIndices, NativeList<CompactNodeID> compactNodes, NativeList<float4x4> transformations, NativeList<int> brushMeshHashes, JobHandle dependsOn)
        {
            var sortJob = new HierarchySortJob
            {
                brushMeshHashes     = brushMeshHashes,
                transformations     = transformations,
                compactNodes        = compactNodes,
                hierarchyIndices    = hierarchyIndices 
            };
            var sortJobHandle = sortJob.Schedule(runInParallel, dependsOn);
            
            var brushMeshBlobCache = ChiselMeshLookup.Value.brushMeshBlobCache;
            var assignJob = new AssignMeshesJob
            {
                brushMeshHashes     = brushMeshHashes,
                transformations     = transformations,
                compactNodes        = compactNodes,
                hierarchyIndices    = hierarchyIndices,
                hierarchyList       = hierarchyList,
                brushMeshBlobCache  = brushMeshBlobCache
            };
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

    public interface GeneratorJobPool : System.IDisposable
    {
        void AllocateOrClear();
        JobHandle ScheduleGenerateJob(JobHandle dependsOn);
        JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts);
        JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodes, JobHandle dependsOn);
    }

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
            [NoAlias, ReadOnly] public NativeArray<Generator> settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes;

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
                return default;

            brushMeshes.Clear();
            brushMeshes.Resize(generators.Length, NativeArrayOptions.ClearMemory);
            var job = new CreateBrushesJob
            {
                settings            = generators.AsArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray(),
                brushMeshes         = brushMeshes.AsDeferredJobArray()
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
                nodes = nodes,
                index = index,
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
            [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>    brushMeshes;

            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter  generatedNodes;


            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < nodes.Length; i++) 
                {
                    var nodeID = nodes[i];
                    var compactNodeID   = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, nodeID);
                    var hierarchyIndex  = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, compactNodeID);
                    var transformation  = hierarchyListPtr[hierarchyIndex].GetTransformation(compactNodeID);
                                        
                    generatedNodes.AddNoResize(new GeneratedNodeDefinition
                    {
                        compactNodeID   = compactNodeID,
                        hierarchyIndex  = hierarchyIndex,
                        transformation  = transformation,
                        brushMeshBlob   = brushMeshes[i]
                    });
                }
            }
        }

        public JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodes, JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                nodes           = nodes,
                brushMeshes     = brushMeshes,
                hierarchyList   = hierarchyList,

                // Write
                generatedNodes  = generatedNodes.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            //*
            initializeArraysJobHandle = initializeArraysJob.Schedule(true, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        nodes.Dispose(initializeArraysJobHandle),
                                        brushMeshes.Dispose(initializeArraysJobHandle));
            nodes = default;
            brushMeshes = default;
            return combinedJobHandle;
            /*/
            initializeArraysJob.Execute();
            nodes.Dispose();
            brushMeshes.Dispose();
            nodes = default;
            brushMeshes = default;
            return default;
            //*/
        }
    }

    public class GeneratorBranchJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        NativeList<Generator>                                         generators;
        NativeList<Range>                                             ranges;
        NativeList<BlobAssetReference<BrushMeshBlob>>                 brushMeshes;
        NativeList<NodeID>                                            nodes;
        
        JobHandle previousJobHandle = default;

        public GeneratorBranchJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Clear(); else surfaceDefinitions = new NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (brushMeshes       .IsCreated) brushMeshes       .Clear(); else brushMeshes        = new NativeList<BlobAssetReference<BrushMeshBlob>>(Allocator.Persistent);
            if (generators        .IsCreated) generators        .Clear(); else generators         = new NativeList<Generator>(Allocator.Persistent);
            if (ranges            .IsCreated) ranges            .Clear(); else ranges             = new NativeList<Range>(Allocator.Persistent);
            if (nodes             .IsCreated) nodes             .Clear(); else nodes              = new NativeList<NodeID>(Allocator.Persistent);
        }

        public void Dispose()
        {
            GeneratorJobPoolManager.Unregister(this);
            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Dispose();
            if (brushMeshes       .IsCreated) brushMeshes       .Dispose();
            if (generators        .IsCreated) generators        .Dispose();
            if (ranges            .IsCreated) ranges            .Dispose();
            if (nodes             .IsCreated) nodes             .Dispose();

            surfaceDefinitions = default; 
            brushMeshes = default;
            generators = default;
            ranges = default;
            nodes = default;
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
            [NoAlias, ReadOnly] public NativeArray<int>                     brushCounts;
            [NoAlias, WriteOnly] public NativeArray<Range>                  ranges;
            [NoAlias] public NativeList<BlobAssetReference<BrushMeshBlob>>  brushMeshes;

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
                brushMeshes.Clear();
                brushMeshes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range>                                         ranges;
            [NoAlias] public NativeArray<Generator>                                     settings;
            [NativeDisableParallelForRestriction]
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>  brushMeshes;

            public void Execute(int index)
            {
                try
                {
                    var range = ranges[index];
                    var requiredSubMeshCount = range.Length;
                    if (requiredSubMeshCount != 0)
                    {
                        using var generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp);

                        generatedBrushMeshes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory);

                        if (!surfaceDefinitions[index].IsCreated ||
                            !settings[index].GenerateMesh(surfaceDefinitions[index], generatedBrushMeshes, Allocator.Persistent))
                        {
                            ranges[index] = new Range { start = 0, end = 0 };
                            return;
                        }
                            
                        Debug.Assert(requiredSubMeshCount == generatedBrushMeshes.Length);
                        if (requiredSubMeshCount != generatedBrushMeshes.Length)
                            throw new InvalidOperationException();
                        for (int i = range.start, m = 0; i < range.end; i++, m++)
                        {
                            brushMeshes[i] = generatedBrushMeshes[m];
                        }
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

            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                var nodeID = nodes[i];
                if (surfaceDefinitions[i].IsCreated &&
                    CompactHierarchyManager.IsValidNodeID(nodeID) &&
                    CompactHierarchyManager.GetTypeOfNode(nodeID) == CSGNodeType.Branch)
                    continue;

                surfaceDefinitions.RemoveAt(i);
                generators.RemoveAt(i);
                nodes.RemoveAt(i);
            }

            if (nodes.Length == 0)
                return default;

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
                brushMeshes         = brushMeshes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(brushCountJobHandle);
            var createJob = new CreateBrushesJob
            {
                settings            = generators        .AsArray(),
                ranges              = ranges            .AsArray(),
                brushMeshes         = brushMeshes       .AsDeferredJobArray(),
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

                    generators[i].FixupOperations(branch);
                }

                totalCounts[index] = count;
            }
        }

        public JobHandle ScheduleUpdateHierarchyJob(bool runInParallel, JobHandle dependsOn, int index, NativeArray<int> totalCounts)
        {
            var updateHierarchyJob = new UpdateHierarchyJob
            {
                nodes               = nodes,
                ranges              = ranges,
                generators          = generators,
                index               = index,
                totalCounts         = totalCounts
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

            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    hierarchyIDLookupPtr;
            [NativeDisableUnsafePtrRestriction, NoAlias, ReadOnly] public IDManager*    nodeIDLookupPtr;
            [NoAlias, ReadOnly] public NativeList<CompactNodeID>                        nodesLookup;

            [NoAlias, ReadOnly] public NativeArray<NodeID>                              nodes;
            [NoAlias, ReadOnly] public NativeArray<Range>                               ranges;
            [NoAlias, ReadOnly] public NativeArray<CompactHierarchy>                    hierarchyList;
            [NoAlias, ReadOnly] public NativeList<BlobAssetReference<BrushMeshBlob>>    brushMeshes;

            [NoAlias, WriteOnly] public NativeList<GeneratedNodeDefinition>.ParallelWriter  generatedNodes;

            public void Execute()
            {
                ref var hierarchyIDLookup = ref UnsafeUtility.AsRef<IDManager>(hierarchyIDLookupPtr);
                ref var nodeIDLookup = ref UnsafeUtility.AsRef<IDManager>(nodeIDLookupPtr);
                var hierarchyListPtr = (CompactHierarchy*)hierarchyList.GetUnsafeReadOnlyPtr();
                for (int i = 0; i < nodes.Length; i++)
                {
                    var range = ranges[i];
                    if (range.Length == 0)
                        continue;
                    
                    var parentNodeID            = nodes[i];
                    var parentCompactNodeID     = CompactHierarchyManager.GetCompactNodeIDNoError(ref nodeIDLookup, nodesLookup, parentNodeID);
                    var hierarchyIndex          = CompactHierarchyManager.GetHierarchyIndexUnsafe(ref hierarchyIDLookup, parentCompactNodeID);
                    ref var compactHierarchy    = ref hierarchyListPtr[hierarchyIndex];
                    var transformation          = compactHierarchy.GetTransformation(parentCompactNodeID);

                    for (int b = 0, m = range.start; m < range.end; b++, m++) 
                    {
                        var compactNodeID = compactHierarchy.GetChildCompactNodeIDAtNoError(parentCompactNodeID, b);
                        generatedNodes.AddNoResize(new GeneratedNodeDefinition
                        {
                            compactNodeID   = compactNodeID,
                            hierarchyIndex  = hierarchyIndex,
                            transformation  = transformation,
                            brushMeshBlob   = brushMeshes[m]
                        });
                    }
                }
            }
        }

        public JobHandle ScheduleInitializeArraysJob([NoAlias, ReadOnly] NativeList<CompactHierarchy> hierarchyList, [NoAlias, WriteOnly] NativeList<GeneratedNodeDefinition> generatedNodes, JobHandle dependsOn)
        {
            var initializeArraysJob = new InitializeArraysJob
            {
                // Read
                nodes           = nodes,
                ranges          = ranges,
                brushMeshes     = brushMeshes,
                hierarchyList   = hierarchyList,

                // Write
                generatedNodes = generatedNodes.AsParallelWriter()
            };
            initializeArraysJob.InitializeLookups();
            //*
            var initializeArraysJobHandle = initializeArraysJob.Schedule(true, dependsOn);
            var combinedJobHandle = JobHandleExtensions.CombineDependencies(
                                        initializeArraysJobHandle,
                                        nodes.Dispose(initializeArraysJobHandle),
                                        ranges.Dispose(initializeArraysJobHandle),
                                        brushMeshes.Dispose(initializeArraysJobHandle));

            nodes = default;
            ranges = default;
            brushMeshes = default;
            return combinedJobHandle;
            /*/
            dependsOn.Complete();
            initializeArraysJob.Execute();
            nodes.Dispose();
            ranges.Dispose();
            brushMeshes.Dispose();
            nodes = default;
            ranges = default;
            brushMeshes = default;
            return default;
            //*/
        }
    }
}
