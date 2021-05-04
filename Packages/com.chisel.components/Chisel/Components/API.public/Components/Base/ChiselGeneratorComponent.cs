using System;
using System.Linq;
using AOT;
using Chisel.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Chisel.Components
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


        public static void Clear() 
        {
            var allGeneratorPools = Instance.generatorPools;
            foreach (var pool in allGeneratorPools)
                pool.AllocateOrClear();
        }

        public static JobHandle Schedule()
        {
            var combinedJobHandle = (JobHandle)default;
            var allGeneratorPools = Instance.generatorPools;
            foreach (var pool in allGeneratorPools)
                combinedJobHandle = JobHandle.CombineDependencies(combinedJobHandle, pool.Schedule());
            combinedJobHandle.Complete();
            foreach (var pool in allGeneratorPools)
                pool.Assign();
            return default;
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
        JobHandle Schedule();
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
            if (generators .IsCreated) generators.Dispose();
            if (brushMeshes       .IsCreated) brushMeshes.Dispose();
            if (nodes              .IsCreated) nodes.Dispose();

            surfaceDefinitions = default;
            generators = default;
            brushMeshes = default;
            nodes = default;
        }

        public void Add(CSGTreeNode node, Generator settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            surfaceDefinitions.Add(surfaceDefinition);
            generators .Add(settings);
            nodes             .Add(node);
        }
        
        [BurstCompile(CompileSynchronously = true)]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<Generator> settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>> brushMeshes;

            public void Execute(int index)
            {
                brushMeshes[index] = settings[index].GenerateMesh(surfaceDefinitions[index], Allocator.Persistent);
            }
        }


        public JobHandle Schedule()
        {
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
            for (int i = 0; i < surfaceDefinitions.Length; i++)
                surfaceDefinitions[i].Dispose();
            
            for (int i = 0; i < nodes.Length; i++)
            {
                var brushMesh = brushMeshes[i];
                var brush = (CSGTreeBrush)nodes[i];
                if (!brush.Valid)
                    continue;

                if (!brushMesh.IsCreated)
                    brush.BrushMesh = BrushMeshInstance.InvalidInstance;// TODO: deregister
                else
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMesh) };
            }
        }
    }

    public class GeneratorBranchJobPool<Generator> : GeneratorJobPool
        where Generator : unmanaged, IBranchGenerator
    {
        NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>>   surfaceDefinitions;
        NativeList<Generator>                                           generators;
        NativeList<Range>                                               ranges;
        NativeList<BlobAssetReference<BrushMeshBlob>>                   brushMeshes;
        NativeList<CSGTreeNode>                                         nodes;
        
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
            if (generators .IsCreated) generators .Dispose();
            if (brushMeshes       .IsCreated) brushMeshes       .Dispose();
            if (ranges            .IsCreated) ranges            .Dispose();
            if (nodes             .IsCreated) nodes             .Dispose();

            surfaceDefinitions = default; 
            generators = default;
            brushMeshes = default;
            ranges = default;
            nodes = default;
        }

        public void Add(CSGTreeNode node, Generator settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinition)
        {
            surfaceDefinitions.Add(surfaceDefinition);
            generators .Add(settings);
            nodes             .Add(node);
        }

        [BurstCompile]
        public unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        {
            [NoAlias] public NativeArray<Generator>          settings;
            [NoAlias, WriteOnly] public NativeArray<int>    brushCounts;

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
                brushMeshes.Resize(totalRequiredBrushCount, NativeArrayOptions.ClearMemory);
            }
        }

        [BurstCompile]
        public unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range>                                         ranges;
            [NoAlias] public NativeArray<Generator>                                      settings;
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

                        if (!settings[index].GenerateMesh(surfaceDefinitions[index], generatedBrushMeshes, Allocator.Persistent))
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
        
        public JobHandle Schedule()
        {
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
                var newBrushCount = desiredBrushCount - branch.Count;
                var newRange = new NativeArray<CSGTreeNode>(newBrushCount, Allocator.Temp);
                try
                {
                    var userID = branch.UserID;
                    for (int i = 0; i < newBrushCount; i++)
                        newRange[i] = CSGTreeBrush.Create(userID: userID, operation: CSGOperationType.Additive);
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
            for (int i = 0; i < surfaceDefinitions.Length; i++)
                surfaceDefinitions[i].Dispose();

            for (int i = 0; i < nodes.Length; i++)
            {
                var range = ranges[i];
                var branch = (CSGTreeBranch)nodes[i];
                if (range.Length == 0)
                {
                    ClearBrushes(branch);
                    continue;
                }

                if (branch.Count != range.Length)
                    BuildBrushes(branch, range.Length);

                for (int b = 0, m = range.start; m < range.end; b++, m++)
                {
                    var brush = (CSGTreeBrush)branch[b];
                    brush.LocalTransformation = float4x4.identity;
                    brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[m]) };
                }

                generators[i].FixupOperations(branch);
            }
        }
    }

    public abstract class ChiselBrushGeneratorComponent<DefinitionType, Generator> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBrushGenerator
        where DefinitionType : ISerializedBrushGenerator<Generator>, new()
    {
        CSGTreeBrush GenerateTopNode(CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var brush = (CSGTreeBrush)node;
            if (!brush.Valid)
            {
                if (node.Valid)
                    node.Destroy();
                return CSGTreeBrush.Create(userID: userID, operation: operation);
            }
            if (brush.Operation != operation)
                brush.Operation = operation;
            return brush;
        }

        static readonly GeneratorBrushJobPool<Generator> s_JobPool = new GeneratorBrushJobPool<Generator>();

        protected override JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            var brush = (CSGTreeBrush)node;
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return default;

            node = brush = GenerateTopNode(brush, userID, operation);
            var settings = definition.GetBrushGenerator();
            s_JobPool.Add(brush, settings, surfaceDefinitionBlob);
            return default;
        }
    }

    public abstract class ChiselBranchGeneratorComponent<Generator, DefinitionType> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBranchGenerator
        where DefinitionType : ISerializedBranchGenerator<Generator>, new()
    {
        CSGTreeBranch GenerateTopNode(CSGTreeBranch branch, int userID, CSGOperationType operation)
        {
            if (!branch.Valid)
            {
                if (branch.Valid)
                    branch.Destroy();
                return CSGTreeBranch.Create(userID: userID, operation: operation);
            }
            if (branch.Operation != operation)
                branch.Operation = operation;
            return branch;
        }

        static readonly GeneratorBranchJobPool<Generator> s_JobPool = new GeneratorBranchJobPool<Generator>();

        protected override JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID)
        {
            var branch = (CSGTreeBranch)node;
            OnValidateDefinition();
            var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in surfaceDefinition, Allocator.TempJob);
            if (!surfaceDefinitionBlob.IsCreated)
                return default;

            node = branch = GenerateTopNode(branch, userID, operation);
            var settings = definition.GetBranchGenerator();
            s_JobPool.Add(branch, settings, surfaceDefinitionBlob);
            return default;
        }
    }

    public abstract class ChiselNodeGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselNodeGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new DefinitionType();

        public ChiselSurfaceDefinition surfaceDefinition;
        public override ChiselSurfaceDefinition SurfaceDefinition { get { return surfaceDefinition; } }

        public override ChiselBrushMaterial GetBrushMaterial(int descriptionIndex) { return surfaceDefinition.GetBrushMaterial(descriptionIndex); }
        public override SurfaceDescription GetSurfaceDescription(int descriptionIndex) { return surfaceDefinition.GetSurfaceDescription(descriptionIndex); }
        public override void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description) { surfaceDefinition.SetSurfaceDescription(descriptionIndex, description); }
        public override UVMatrix GetSurfaceUV0(int descriptionIndex) { return surfaceDefinition.GetSurfaceUV0(descriptionIndex); }
        public override void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0) { surfaceDefinition.SetSurfaceUV0(descriptionIndex, uv0); }

        protected override void OnResetInternal()
        { 
            definition.Reset(); 
            surfaceDefinition?.Reset(); 
            base.OnResetInternal(); 
        }

        protected void OnValidateDefinition()
        {
            definition.Validate();
            if (surfaceDefinition == null)
            {
                surfaceDefinition = new ChiselSurfaceDefinition();
                surfaceDefinition.Reset();
            }
            surfaceDefinition.EnsureSize(definition.RequiredSurfaceCount);
            definition.UpdateSurfaces(ref surfaceDefinition);
        }

        protected override void OnValidateState()
        {
            OnValidateDefinition();
            base.OnValidateState(); 
        }

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            return base.HasValidState() && definition.HasValidState();
        }
    }

    public abstract class ChiselGeneratorComponent : ChiselNode
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName         = nameof(operation);

        [HideInInspector] CSGTreeNode Node = default;

        public abstract ChiselSurfaceDefinition SurfaceDefinition { get; }

        [SerializeField, HideInInspector] protected CSGOperationType operation;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField, HideInInspector] protected Matrix4x4 localTransformation = Matrix4x4.identity;
        [SerializeField, HideInInspector] protected Vector3 pivotOffset = Vector3.zero;

        public override CSGTreeNode TopTreeNode { get { if (!ValidNodes) return CSGTreeNode.InvalidNode; return Node; } protected set { Node = value; } }
        bool ValidNodes { get { return Node.Valid; } }
        

        public CSGOperationType Operation
        {
            get
            {
                return operation;
            }
            set
            {
                if (value == operation)
                    return;
                operation = value;

                if (ValidNodes)
                    Node.Operation = operation;

                // Let the hierarchy manager know that the contents of this node has been modified
                //	so we can rebuild/update sub-trees and regenerate meshes
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        public Vector3 PivotOffset
        {
            get
            {
                return pivotOffset;
            }
            set
            {
                if (value == pivotOffset)
                    return;
                pivotOffset = value;

                UpdateInternalTransformation();

                // Let the hierarchy manager know that this node has moved, so we can regenerate meshes
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(this);
            }
        }

        public Matrix4x4 LocalTransformation
        {
            get
            {
                return localTransformation;
            }
            set
            {
                if (value == localTransformation)
                    return;

                localTransformation = value;

                UpdateInternalTransformation();

                // Let the hierarchy manager know that this node has moved, so we can regenerate meshes
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(this);
            }
        }

        public Matrix4x4 PivotTransformation
        {
            get
            {
                // TODO: fix this mess

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 InversePivotTransformation
        {
            get
            {
                // TODO: fix this mess

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(-pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 LocalTransformationWithPivot
        {
            get
            {
                // TODO: fix this mess

                var localTransformationWithPivot = transform.localToWorldMatrix;
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    localTransformationWithPivot *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);

                var modelTransform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(transform);
                if (modelTransform)
                    localTransformationWithPivot = modelTransform.worldToLocalMatrix * localTransformationWithPivot;
                return localTransformationWithPivot;
            }
        }

        public abstract ChiselBrushMaterial GetBrushMaterial(int descriptionIndex);
        public abstract SurfaceDescription GetSurfaceDescription(int descriptionIndex);
        public abstract void SetSurfaceDescription(int descriptionIndex, SurfaceDescription description);
        public abstract UVMatrix GetSurfaceUV0(int descriptionIndex);
        public abstract void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0);

        protected override void OnDisable()
        {
            ResetTreeNodes();
            base.OnDisable();
        }

        protected override void OnResetInternal()
        {
            UpdateBrushMeshInstances();
            base.OnResetInternal();
        }

        // Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
        public override bool HasValidState()
        {
            if (!ValidNodes)
                return false;

            if (ChiselGeneratedComponentManager.IsDefaultModel(hierarchyItem.Model))
                return false;

            return true;
        }

        protected override void OnValidateState()
        {
            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            UpdateBrushMeshInstances();

            ChiselNodeHierarchyManager.NotifyContentsModified(this);
            base.OnValidateState();
        }

        public override void UpdateTransformation()
        {
            // TODO: recalculate transformation based on hierarchy up to (but not including) model
            var transform = hierarchyItem.Transform;
            if (!transform)
                return;

            // TODO: fix this mess
            var localToWorldMatrix = transform.localToWorldMatrix;
            var modelTransform = ChiselNodeHierarchyManager.FindModelTransformOfTransform(transform);
            if (modelTransform)
                localTransformation = modelTransform.worldToLocalMatrix * localToWorldMatrix;
            else
                localTransformation = localToWorldMatrix;

            if (!ValidNodes)
                return;

            UpdateInternalTransformation();
        }

        void UpdateInternalTransformation()
        {
            if (!ValidNodes)
                return;

            if (Node.Type == CSGNodeType.Brush)
            {
                Node.LocalTransformation = LocalTransformationWithPivot;
            } else
            {
                // TODO: Remove this once we have a proper transformation pipeline
                for (int i = 0; i < Node.Count; i++)
                {
                    var child = Node[i];
                    child.LocalTransformation = LocalTransformationWithPivot;
                }
            }
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            ResetTreeNodes();
        }

        internal override CSGTreeNode RebuildTreeNodes()
        {
            ResetTreeNodes();
            if (Node.Valid)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);

            Profiler.BeginSample("UpdateGenerator");
            try
            {
                var instanceID = GetInstanceID();
                var jobHandle = UpdateGeneratorInternal(ref Node, userID: instanceID);
                jobHandle.Complete();
            }
            finally { Profiler.EndSample(); }

            if (!ValidNodes)
                return default;

            Profiler.BeginSample("UpdateBrushMeshInstances");
            try { UpdateBrushMeshInstances(); }
            finally { Profiler.EndSample(); }
            
            if (Node.Operation != operation)
                Node.Operation = operation;
            return Node;
        }

        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            TopTreeNode.SetDirty();
        }


        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            PivotOffset += this.transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            base.AddPivotOffset(worldSpaceDelta);
        }

        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
        }

        protected abstract JobHandle UpdateGeneratorInternal(ref CSGTreeNode node, int userID);
    }
}