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
        [BurstCompile]
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
        public bool GenerateMesh(ref PathedStairsSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
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

        public void Dispose(ref PathedStairsSettings settings)
        {
            if (settings.curveBlob.IsCreated) settings.curveBlob.Dispose();
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