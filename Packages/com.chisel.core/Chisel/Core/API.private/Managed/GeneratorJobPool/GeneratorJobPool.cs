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

namespace Chisel.Core
{
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

        public static JobHandle ScheduleJobs()
        {
            var combinedJobHandle = (JobHandle)default;
            var allGeneratorPools = Instance.generatorPools;
            foreach (var pool in allGeneratorPools)
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.ScheduleJob());
            combinedJobHandle.Complete();
            foreach (var pool in allGeneratorPools)
                pool.Assign();
            previousJobHandle = default;
            return previousJobHandle;
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
        JobHandle ScheduleJob();
        void Assign();
    }

    public class GeneratorBrushJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBrushGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>   surfaceDefinitions;
        NativeList<Generator>                                           generators;
        NativeList<BlobAssetReference<BrushMeshBlob>>                   brushMeshes;
        NativeList<CSGTreeNode>                                         nodes;
        
        JobHandle previousJobHandle = default;

        public GeneratorBrushJobPool() { GeneratorJobPoolManager.Register(this); }

        public void AllocateOrClear()
        {
            previousJobHandle.Complete(); // <- make sure we've completed the previous schedule
            previousJobHandle = default;

            if (surfaceDefinitions.IsCreated) surfaceDefinitions.Clear(); else surfaceDefinitions = new NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>(Allocator.Persistent);
            if (brushMeshes       .IsCreated) brushMeshes       .Clear(); else brushMeshes        = new NativeList<BlobAssetReference<BrushMeshBlob>>(Allocator.Persistent);
            if (generators        .IsCreated) generators        .Clear(); else generators         = new NativeList<Generator>(Allocator.Persistent);
            if (nodes             .IsCreated) nodes             .Clear(); else nodes              = new NativeList<CSGTreeNode>(Allocator.Persistent);
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

            var index = nodes.IndexOf(node);
            if (index != -1)
            {
                surfaceDefinitions[index] = surfaceDefinition;
                generators        [index] = settings;
                nodes             [index] = node;
            } else
            { 
                surfaceDefinitions.Add(surfaceDefinition);
                generators        .Add(settings);
                nodes             .Add(node);
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

        public JobHandle ScheduleJob()
        {
            if (!nodes.IsCreated ||
                !generators.IsCreated ||
                !brushMeshes.IsCreated ||
                !surfaceDefinitions.IsCreated)
                return default;

            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                if (nodes[i].Valid &&
                    surfaceDefinitions[i].IsCreated &&
                    nodes[i].Type == CSGNodeType.Brush)
                    continue;

                surfaceDefinitions.RemoveAt(i);
                generators.RemoveAt(i);
                nodes.RemoveAt(i);
            }

            brushMeshes.Clear();
            brushMeshes.Resize(generators.Length, NativeArrayOptions.ClearMemory);
            var job = new CreateBrushesJob
            {
                settings            = generators.AsArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray(),
                brushMeshes         = brushMeshes.AsArray()
            };
            return job.Schedule(generators, 8);
        }

        public void Assign()
        {
            if (surfaceDefinitions.IsCreated)
            {
                for (int i = 0; i < surfaceDefinitions.Length; i++)
                {
                    try
                    {
                        if (surfaceDefinitions[i].IsCreated)
                            surfaceDefinitions[i].Dispose();
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                surfaceDefinitions.Clear();
            }

            if (!nodes.IsCreated ||
                !brushMeshes.IsCreated)
                return;

            for (int i = 0; i < nodes.Length; i++)
            {
                var brush = (CSGTreeBrush)nodes[i];
                if (!brush.Valid)
                    continue;

                try
                {
                    var brushMesh   = brushMeshes[i];
                    if (!brushMesh.IsCreated)
                        brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                    else
                        brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
                }
                catch (Exception ex)
                {
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                    Debug.LogException(ex); 
                }
            }

            nodes.Clear();
            generators.Clear();
            brushMeshes.Clear();
        }
    }

    public class GeneratorBranchJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
        NativeList<Generator>                                         generators;
        NativeList<Range>                                             ranges;
        NativeList<BlobAssetReference<BrushMeshBlob>>                 brushMeshes;
        NativeList<CSGTreeNode>                                       nodes;
        
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
            if (nodes             .IsCreated) nodes             .Clear(); else nodes              = new NativeList<CSGTreeNode>(Allocator.Persistent);
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

            var index = nodes.IndexOf(node);
            if (index != -1)
            {
                surfaceDefinitions[index] = surfaceDefinition;
                generators        [index] = settings;
                nodes             [index] = node;
            } else
            {
                surfaceDefinitions.Add(surfaceDefinition);
                generators        .Add(settings);
                nodes             .Add(node);
            }
        }

        [BurstCompile]
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

        [BurstCompile]
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

        [BurstCompile]
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
        
        public JobHandle ScheduleJob()
        {
            if (!nodes.IsCreated ||
                !generators.IsCreated ||
                !brushMeshes.IsCreated ||
                !surfaceDefinitions.IsCreated)
                return default;

            for (int i = nodes.Length - 1; i >= 0; i--)
            {
                if (nodes[i].Valid &&
                    surfaceDefinitions[i].IsCreated &&
                    nodes[i].Type == CSGNodeType.Branch)
                    continue;

                surfaceDefinitions.RemoveAt(i);
                generators.RemoveAt(i);
                nodes.RemoveAt(i);
            }

            ranges.Clear();
            ranges.Resize(generators.Length, NativeArrayOptions.ClearMemory);

            var brushCounts = new NativeArray<int>(generators.Length, Allocator.TempJob);
            var countBrushesJob = new PrepareAndCountBrushesJob
            {
                settings            = generators.AsArray(),
                brushCounts         = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(generators, 8);
            var allocateBrushesJob = new AllocateBrushesJob
            {
                brushCounts         = brushCounts,
                ranges              = ranges.AsArray(),
                brushMeshes         = brushMeshes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(brushCountJobHandle);
            var createJob = new CreateBrushesJob
            {
                settings            = generators.AsArray(),
                ranges              = ranges.AsArray(),
                brushMeshes         = brushMeshes.AsDeferredJobArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray()
            };
            var createJobHandle = createJob.Schedule(generators, 8, allocateBrushesJobHandle);
            return brushCounts.Dispose(createJobHandle);
        }
        
        static void ClearBrushes(CSGTreeBranch branch)
        {
            for (int i = branch.Count - 1; i >= 0; i--)
                branch[i].Destroy();
            branch.Clear();
        }

        static unsafe void BuildBrushes(CSGTreeBranch branch, int desiredBrushCount)
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

        public void Assign()
        {
            if (surfaceDefinitions.IsCreated)
            {
                for (int i = 0; i < surfaceDefinitions.Length; i++)
                {
                    try
                    {
                        if (surfaceDefinitions[i].IsCreated)
                            surfaceDefinitions[i].Dispose();
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                surfaceDefinitions.Clear();
            }

            if (!nodes.IsCreated ||
                !brushMeshes.IsCreated)
                return;

            for (int i = 0; i < nodes.Length; i++)
            {
                var branch = (CSGTreeBranch)nodes[i];
                if (!branch.Valid)
                    continue;

                try
                {
                    var range = ranges[i];
                    if (range.Length == 0)
                    {
                        ClearBrushes(branch);
                        continue;
                    }

                    if (branch.Count != range.Length)
                        BuildBrushes(branch, range.Length);

                    var localTransformation = branch.LocalTransformation;

                    for (int b = 0, m = range.start; m < range.end; b++, m++)
                    {
                        var brush = (CSGTreeBrush)branch[b];
                        if (brushMeshes[m].IsCreated)
                        {
                            brush.LocalTransformation = localTransformation; // TODO: implement proper transformation pipeline
                            //brush.LocalTransformation = float4x4.identity;
                            brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[m]) };
                        } else
                        {
                            brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                        }
                    }

                    generators[i].FixupOperations(branch);
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            nodes.Clear();
            ranges.Clear();
            generators.Clear();
            brushMeshes.Clear();
        }
    }

}
