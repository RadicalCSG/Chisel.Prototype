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
    public struct ChiselPathedStairs : IBranchGenerator
    {
        public readonly static ChiselPathedStairs DefaultValues = new ChiselPathedStairs
        {
            curveSegments   = 8,
            closed          = true,
            stairs          = ChiselLinearStairs.DefaultValues
        };

        [NonSerialized] public bool closed;

        public int curveSegments;

        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        [HideFoldout] public ChiselLinearStairs stairs;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex> shapeVertices;

        #region Generate
        [BurstCompile]
        public int PrepareAndCountRequiredBrushMeshes()
        {
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;
            curve.GetPathVertices(curveSegments, out shapeVertices, Allocator.Persistent);
            if (shapeVertices.Length < 2)
                return 0;

            return BrushMeshFactory.CountPathedStairBrushes(shapeVertices, closed, bounds,
                                                            stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                            stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                            stairs.leftSide, stairs.rightSide,
                                                            stairs.sideWidth, stairs.sideHeight, stairs.sideDepth);
        }

        [BurstCompile()]
        public bool GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;

            if (!BrushMeshFactory.GeneratePathedStairs(brushMeshes,
                                                        shapeVertices, closed, bounds,
                                                        stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                        stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                        stairs.leftSide, stairs.rightSide,
                                                        stairs.sideWidth, stairs.sideHeight, stairs.sideDepth,
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

        public void Dispose()
        {
            if (curveBlob.IsCreated) curveBlob.Dispose();
        }

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch) { }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return stairs.RequiredSurfaceCount; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition)
        {
            stairs.UpdateSurfaces(ref surfaceDefinition);
        }
        #endregion

        #region Validation
        public const int kMinCurveSegments = 2;

        public void Validate()
        {
            curveSegments = math.max(curveSegments, kMinCurveSegments);
            stairs.Validate();
        }
        #endregion
    }

    [Serializable]
    public struct ChiselPathedStairsDefinition : ISerializedBranchGenerator<ChiselPathedStairs>
    {
        public const string kNodeTypeName = "Pathed Stairs";

        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        [HideFoldout] public ChiselPathedStairs settings;

        public Curve2D					shape;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        //public ChiselLinearStairsDefinition stairs;

        public void Reset()
        {
            shape    = kDefaultShape;
            settings = ChiselPathedStairs.DefaultValues;
        }

        public int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { settings.UpdateSurfaces(ref surfaceDefinition); }
            
        public void Validate() { settings.Validate(); }

        public ChiselPathedStairs GetBranchGenerator()
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