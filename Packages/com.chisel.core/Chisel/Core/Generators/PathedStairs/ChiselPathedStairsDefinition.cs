using System;
using Bounds = UnityEngine.Bounds;
using Vector3 = UnityEngine.Vector3;
using Debug = UnityEngine.Debug;
using Mathf = UnityEngine.Mathf;
using UnitySceneExtensions;
using UnityEngine.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselPathedStairsDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Pathed Stairs";

        public const int				kDefaultCurveSegments	= 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D					shape;
        public int                      curveSegments;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        public ChiselLinearStairsDefinition stairs;

        public ChiselSurfaceDefinition SurfaceDefinition { get { return stairs.surfaceDefinition; } }

        public void Reset()
        {
            shape           = kDefaultShape;
            curveSegments   = kDefaultCurveSegments;
            stairs.Reset();
        }

        public void Validate()
        {
            curveSegments = Mathf.Max(curveSegments, 2);
            stairs.Validate();
        }

        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            return BrushMeshFactory.GeneratePathedStairs(ref brushContainer, ref this);
        }

        public bool Generate(ref CSGTreeNode node, int userID, CSGOperationType operation)
        {
            var branch = (CSGTreeBranch)node;
            if (!branch.Valid)
            {
                node = branch = CSGTreeBranch.Create(userID: userID, operation: operation);
            } else
            {
                if (branch.Operation != operation)
                    branch.Operation = operation;
            }

            Validate();


            if (stairs.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
            {
                this.ClearBrushes(branch);
                return false;
            }

            using (var curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.Temp))
            {
                ref var curve = ref curveBlob.Value;
                using (var shapeVertices = new NativeList<SegmentVertex>(Allocator.Temp))
                {
                    curve.GetPathVertices(curveSegments, shapeVertices);
                    if (shapeVertices.Length < 2)
                    {
                        this.ClearBrushes(branch);
                        return false;
                    }

                    var minMaxAABB = new MinMaxAABB { Min = stairs.bounds.min, Max = stairs.bounds.max };
                    int requiredSubMeshCount = BrushMeshFactory.CountPathedStairBrushes(in shapeVertices, shape.closed,
                                                                                        minMaxAABB,
                                                                                        stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                                                        stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                                                        stairs.leftSide, stairs.rightSide,
                                                                                        stairs.sideWidth, stairs.sideHeight, stairs.sideDepth);
                    if (requiredSubMeshCount == 0)
                    {
                        this.ClearBrushes(branch);
                        return false;
                    }

                    if (branch.Count != requiredSubMeshCount)
                        this.BuildBrushes(branch, requiredSubMeshCount);

                    using (var surfaceDefinitionBlob = BrushMeshManager.BuildSurfaceDefinitionBlob(in stairs.surfaceDefinition, Allocator.Temp))
                    {
                        using (var brushMeshes = new NativeArray<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
                        {
                            if (!BrushMeshFactory.GeneratePathedStairs(brushMeshes, in shapeVertices,
                                                                       shape.closed, minMaxAABB,
                                                                       stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                                       stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                                       stairs.leftSide, stairs.rightSide,
                                                                       stairs.sideWidth, stairs.sideHeight, stairs.sideDepth,
                                                                       in surfaceDefinitionBlob, Allocator.Persistent))
                            {
                                this.ClearBrushes(branch);
                                return false;
                            }

                            for (int i = 0; i < requiredSubMeshCount; i++)
                            {
                                var brush = (CSGTreeBrush)branch[i];
                                brush.LocalTransformation = float4x4.identity;
                                brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[i]) };
                            }
                            return true;
                        }
                    }
                }
            }
        }

        public void OnEdit(IChiselHandles handles)
        {
            handles.DoShapeHandle(ref shape);
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}