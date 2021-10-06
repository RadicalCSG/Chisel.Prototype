using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        [UnityEngine.HideInInspector, NonSerialized] public ChiselBlobAssetReference<ChiselCurve2DBlob> curveBlob;
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
        public bool GenerateNodes(ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator)
        {
            using (var generatedBrushMeshes = new NativeList<ChiselBlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp))
            {
                generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
                ref var curve = ref curveBlob.Value;
                var bounds = stairs.bounds;

                if (!BrushMeshFactory.GeneratePathedStairs(generatedBrushMeshes,
                                                            shapeVertices, closed, bounds,
                                                            stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                            stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                            stairs.leftSide, stairs.rightSide,
                                                            stairs.sideWidth, stairs.sideHeight, stairs.sideDepth,
                                                            in surfaceDefinitionBlob, allocator))
                {
                    for (int i = 0; i < generatedBrushMeshes.Length; i++)
                    {
                        if (generatedBrushMeshes[i].IsCreated)
                            generatedBrushMeshes[i].Dispose();
                    }
                    return false;
                }
                for (int i = 0; i < generatedBrushMeshes.Length; i++)
                    nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]);
                return true;
            }
        }

        public void Dispose()
        {
            if (curveBlob.IsCreated) curveBlob.Dispose();
        }
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

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    [Serializable]
    public class ChiselPathedStairsDefinition : SerializedBranchGenerator<ChiselPathedStairs>
    {
        public const string kNodeTypeName = "Pathed Stairs";

        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D					shape;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        //public ChiselLinearStairsDefinition stairs;

        public override void Reset()
        {
            shape = kDefaultShape;
            base.Reset();
        }

        public override void Validate() 
        {
            shape ??= kDefaultShape;
            base.Validate(); 
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        public override ChiselPathedStairs GetBranchGenerator()
        {
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, defaultAllocator);
            settings.closed = shape.closed;
            return base.GetBranchGenerator();
        }

        public override void OnEdit(IChiselHandles handles)
        {
            handles.DoShapeHandle(ref shape);
        }
    }
}