using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;

namespace Chisel.Core
{
    [Serializable]
    public struct PathedStairsSettings
    {
        [NonSerialized] public bool closed;

        public int curveSegments;

        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        [HideFoldout] public ChiselLinearStairsDefinition stairs;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex> shapeVertices;
    }

    public struct ChiselPathedStairsGenerator : IChiselBranchTypeGenerator<PathedStairsSettings>
    {
        [BurstCompile()]
        unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        {
            [NoAlias] public NativeArray<PathedStairsSettings>  settings;
            [NoAlias, WriteOnly] public NativeArray<int>        brushCounts;

            public void Execute(int index)
            {
                var setting = settings[index];
                brushCounts[index] = PrepareAndCountRequiredBrushMeshes_(ref setting);
                settings[index] = setting;
            }
        }

        [BurstCompile()]
        unsafe struct AllocateBrushesJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<int> brushCounts;
            [NoAlias, WriteOnly] public NativeArray<Range> ranges;
            [NoAlias] public NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes;

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

        [BurstCompile()]
        unsafe struct CreateBrushesJob : IJobParallelForDefer
        {
            [NoAlias, ReadOnly] public NativeArray<PathedStairsSettings>                settings;
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range> ranges;
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
                        using (var generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
                        {
                            generatedBrushMeshes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory);
                            if (!GenerateMesh(settings[index], surfaceDefinitions[index], generatedBrushMeshes, Allocator.Persistent))
                            {
                                ranges[index] = new Range { start = 0, end = 0 };
                                return;
                            }
                            
                            Debug.Assert(requiredSubMeshCount == generatedBrushMeshes.Length);
                            if (requiredSubMeshCount != generatedBrushMeshes.Length)
                                throw new InvalidOperationException();

                            for (int i = range.start, m=0; i < range.end; i++,m++)
                            {
                                brushMeshes[i] = generatedBrushMeshes[m];
                            }
                        }
                    }
                }
                finally
                {
                    Dispose(settings[index]);
                }
            }
        }

        [BurstDiscard]
        public JobHandle Schedule(NativeList<PathedStairsSettings> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions, NativeList<Range> ranges, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
        {
            var brushCounts = new NativeArray<int>(settings.Length, Allocator.TempJob);
            var countBrushesJob = new PrepareAndCountBrushesJob
            {
                settings            = settings.AsArray(),
                brushCounts         = brushCounts
            };
            var brushCountJobHandle = countBrushesJob.Schedule(settings, 8);
            var allocateBrushesJob = new AllocateBrushesJob
            {
                brushCounts = brushCounts,
                ranges      = ranges.AsArray(),
                brushMeshes = brushMeshes
            };
            var allocateBrushesJobHandle = allocateBrushesJob.Schedule(brushCountJobHandle);
            var createJob = new CreateBrushesJob
            {
                settings            = settings.AsArray(),
                ranges              = ranges.AsArray(),
                brushMeshes         = brushMeshes.AsDeferredJobArray(),
                surfaceDefinitions  = surfaceDefinitions.AsArray()
            };
            var createJobHandle = createJob.Schedule(settings, 8, allocateBrushesJobHandle);
            return brushCounts.Dispose(createJobHandle);
        }

        public static void Dispose(PathedStairsSettings settings)
        {
            if (settings.curveBlob.IsCreated) settings.curveBlob.Dispose();
        }

        [BurstCompile()]
        public int PrepareAndCountRequiredBrushMeshes(ref PathedStairsSettings settings)
        {
            ref var curve = ref settings.curveBlob.Value;
            var closed = settings.closed;
            var bounds = settings.stairs.settings.bounds;
            curve.GetPathVertices(settings.curveSegments, out settings.shapeVertices, Allocator.Persistent);
            if (settings.shapeVertices.Length < 2)
                return 0;

            return BrushMeshFactory.CountPathedStairBrushes(settings.shapeVertices, closed, bounds,
                                                            settings.stairs.settings.stepHeight, settings.stairs.settings.stepDepth, settings.stairs.settings.treadHeight, settings.stairs.settings.nosingDepth,
                                                            settings.stairs.settings.plateauHeight, settings.stairs.settings.riserType, settings.stairs.settings.riserDepth,
                                                            settings.stairs.settings.leftSide, settings.stairs.settings.rightSide,
                                                            settings.stairs.settings.sideWidth, settings.stairs.settings.sideHeight, settings.stairs.settings.sideDepth);
        }

        [BurstCompile()]
        public static int PrepareAndCountRequiredBrushMeshes_(ref PathedStairsSettings settings)
        {
            ref var curve = ref settings.curveBlob.Value;
            var closed = settings.closed;
            var bounds = settings.stairs.settings.bounds;
            curve.GetPathVertices(settings.curveSegments, out settings.shapeVertices, Allocator.Persistent);
            if (settings.shapeVertices.Length < 2)
                return 0;

            return BrushMeshFactory.CountPathedStairBrushes(settings.shapeVertices, closed, bounds,
                                                            settings.stairs.settings.stepHeight, settings.stairs.settings.stepDepth, settings.stairs.settings.treadHeight, settings.stairs.settings.nosingDepth,
                                                            settings.stairs.settings.plateauHeight, settings.stairs.settings.riserType, settings.stairs.settings.riserDepth,
                                                            settings.stairs.settings.leftSide, settings.stairs.settings.rightSide,
                                                            settings.stairs.settings.sideWidth, settings.stairs.settings.sideHeight, settings.stairs.settings.sideDepth);
        }

        [BurstCompile()]
        public static bool GenerateMesh(PathedStairsSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            ref var curve = ref settings.curveBlob.Value;
            var closed = settings.closed;
            var bounds = settings.stairs.settings.bounds;

            if (!BrushMeshFactory.GeneratePathedStairs(brushMeshes,
                                                        settings.shapeVertices, closed, bounds,
                                                        settings.stairs.settings.stepHeight, settings.stairs.settings.stepDepth, settings.stairs.settings.treadHeight, settings.stairs.settings.nosingDepth,
                                                        settings.stairs.settings.plateauHeight, settings.stairs.settings.riserType, settings.stairs.settings.riserDepth,
                                                        settings.stairs.settings.leftSide, settings.stairs.settings.rightSide,
                                                        settings.stairs.settings.sideWidth, settings.stairs.settings.sideHeight, settings.stairs.settings.sideDepth,
                                                        in surfaceDefinitionBlob, Allocator.Persistent))
            {
                for (int i = 0; i < brushMeshes.Length; i++)
                {
                    if (brushMeshes[i].IsCreated)
                        brushMeshes[i].Dispose();
                }
                return false;
            }
            return true;
        }

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch, PathedStairsSettings settings) { }
    }

    [Serializable]
    public struct ChiselPathedStairsDefinition : IChiselBranchGenerator<ChiselPathedStairsGenerator, PathedStairsSettings>
    {
        public const string kNodeTypeName = "Pathed Stairs";

        public const int				kDefaultCurveSegments	= 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });


        [HideFoldout] public PathedStairsSettings settings;

        public Curve2D					shape;
        //public int                      curveSegments;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        //public ChiselLinearStairsDefinition stairs;

        public void Reset()
        {
            shape                   = kDefaultShape;
            settings.curveSegments  = kDefaultCurveSegments;
            settings.stairs.Reset();
        }

        public int RequiredSurfaceCount { get { return settings.stairs.RequiredSurfaceCount; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            settings.stairs.UpdateSurfaces(ref surfaceDefinition);
        }
            
        public void Validate()
        {
            settings.curveSegments = math.max(settings.curveSegments, 2);
            settings.stairs.Validate();
        }

        public PathedStairsSettings GenerateSettings()
        {
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.TempJob);
            settings.closed = shape.closed;
            return settings;
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoShapeHandle(ref shape);
        }

        public bool HasValidState()
        {
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}