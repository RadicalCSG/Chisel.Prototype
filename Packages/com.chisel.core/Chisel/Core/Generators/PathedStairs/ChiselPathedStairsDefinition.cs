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
        [NonSerialized]
        public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
        [NonSerialized] public bool closed;

        public int curveSegments;

        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        [HideFoldout] public ChiselLinearStairsDefinition stairs;
    }

    [Serializable]
    public struct ChiselPathedStairsDefinition : IChiselBranchGenerator
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

        [BurstCompile(CompileSynchronously = true)]
        struct CreatePathedStairsJob : IJob
        {
            public PathedStairsSettings settings;

            [NoAlias, ReadOnly]
            public BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob;

            [NoAlias]
            public NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes;

            public void Execute()
            {
                ref var curve = ref settings.curveBlob.Value;
                var closed = settings.closed;
                var bounds = settings.stairs.settings.bounds;
                using (var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp))
                {
                    curve.GetPathVertices(settings.curveSegments, shapeVertices);
                    if (shapeVertices.Length < 2)
                    {
                        brushMeshes.Clear();
                        return;
                    }

                    int requiredSubMeshCount = BrushMeshFactory.CountPathedStairBrushes(in shapeVertices, closed, bounds,
                                                                                    settings.stairs.settings.stepHeight, settings.stairs.settings.stepDepth, settings.stairs.settings.treadHeight, settings.stairs.settings.nosingDepth,
                                                                                    settings.stairs.settings.plateauHeight, settings.stairs.settings.riserType, settings.stairs.settings.riserDepth,
                                                                                    settings.stairs.settings.leftSide, settings.stairs.settings.rightSide,
                                                                                    settings.stairs.settings.sideWidth, settings.stairs.settings.sideHeight, settings.stairs.settings.sideDepth);
                    if (requiredSubMeshCount == 0)
                    {
                        brushMeshes.Clear();
                        return;
                    }

                    brushMeshes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory);
                    if (!BrushMeshFactory.GeneratePathedStairs(brushMeshes,
                                                               in shapeVertices, closed, bounds,
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
                        brushMeshes.Clear();
                    }
                }
            }
        }

        public void FixupOperations(CSGTreeBranch branch) { }

        public JobHandle Generate(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            using (var curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.TempJob))
            {
                settings.curveBlob = curveBlob;
                settings.closed = shape.closed;
                var createExtrudedShapeJob = new CreatePathedStairsJob
                {
                    settings                = settings,
                    surfaceDefinitionBlob   = surfaceDefinitionBlob,
                    brushMeshes             = brushMeshes
                };
                var handle = createExtrudedShapeJob.Schedule();
                handle.Complete();
                return default;
            }
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