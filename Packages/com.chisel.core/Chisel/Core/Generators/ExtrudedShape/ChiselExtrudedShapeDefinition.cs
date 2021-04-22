using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselExtrudedShapeDefinition : IChiselGenerator
    {
        public const string kNodeTypeName = "Extruded Shape";

        public const int                kDefaultCurveSegments   = 8;
        public static readonly Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public ChiselPath               path;
        public int                      curveSegments;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            curveSegments   = kDefaultCurveSegments;
            path			= new ChiselPath(ChiselPath.Default);
            shape			= new Curve2D(kDefaultShape);
        }

        public int RequiredSurfaceCount { get { return 2 + shape.controlPoints.Length; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            shape ??= new Curve2D(kDefaultShape);
        }

        public JobHandle Generate(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, ref CSGTreeNode node, int userID, CSGOperationType operation)
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

            using (var curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.Temp))
            {
                // TODO: brushes must be part of unique hierarchy, otherwise it'd be impossible to create brushes safely inside a job ....

                ref var curve = ref curveBlob.Value;
                if (!curve.ConvexPartition(curveSegments, out var polygonVerticesList, out var polygonVerticesSegments, Allocator.Temp))
                {
                    this.ClearBrushes(branch);
                    return default;
                }

                int requiredSubMeshCount = polygonVerticesSegments.Length;
                if (requiredSubMeshCount == 0)
                {
                    this.ClearBrushes(branch);
                    return default;
                }

                if (branch.Count != requiredSubMeshCount)
                    this.BuildBrushes(branch, requiredSubMeshCount);


                // TODO: maybe just not bother with pathblob and just convert to path-matrices directly?
                using (var pathBlob = ChiselPathBlob.Convert(path, Allocator.Temp))
                using (var pathMatrices = pathBlob.Value.GetMatrices(Allocator.Temp))
                {
                    using (var brushMeshes = new NativeArray<BlobAssetReference<BrushMeshBlob>>(requiredSubMeshCount, Allocator.Temp))
                    {
                        if (!BrushMeshFactory.GenerateExtrudedShape(brushMeshes, 
                                                                    in polygonVerticesList, 
                                                                    in polygonVerticesSegments, 
                                                                    in pathMatrices, 
                                                                    in surfaceDefinitionBlob, 
                                                                    Allocator.Persistent))
                        {
                            this.ClearBrushes(branch);
                            return default;
                        }

                        for (int i = 0; i < requiredSubMeshCount; i++)
                        {
                            var brush = (CSGTreeBrush)branch[i];
                            brush.LocalTransformation = float4x4.identity;
                            brush.BrushMesh = new BrushMeshInstance { brushMeshHash = BrushMeshManager.RegisterBrushMesh(brushMeshes[i]) };
                        }
                        return default;
                    }
                }
            }
        }


        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 1.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        public void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;            
            var noZTestcolor	= handles.GetStateColor(baseColor, false, true);
            var zTestcolor		= handles.GetStateColor(baseColor, false, false);

            path.UpgradeIfNecessary();

            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint	= path.segments[i];
                var currMatrix	= pathPoint.ToMatrix();

                handles.color = baseColor;
                handles.DoShapeHandle(ref shape, currMatrix);

                if (i == 0)
                {
                    if (handles.DoDirectionHandle(ref pathPoint.position, -(pathPoint.rotation * Vector3.forward)))
                    {
                        path.segments[i] = pathPoint;
                        path = new ChiselPath(path);
                    }
                } else
                if (i == path.segments.Length - 1)
                {
                    if (handles.DoDirectionHandle(ref pathPoint.position, (pathPoint.rotation * Vector3.forward)))
                    {
                        path.segments[i] = pathPoint;
                        path = new ChiselPath(path);
                    }
                }


                // Draw lines between different segments
                if (i + 1 < path.segments.Length)
                {
                    var nextPoint		= path.segments[i + 1];
                    var nextMatrix		= nextPoint.ToMatrix();
                    var controlPoints	= shape.controlPoints;

                    for (int c = 0; c < controlPoints.Length; c++)
                    {
                        var controlPoint = controlPoints[c].position;
                        var pointA		 = currMatrix.MultiplyPoint(controlPoint);
                        var pointB		 = nextMatrix.MultiplyPoint(controlPoint);
                        handles.color = noZTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    {
                        var pointA = currMatrix.MultiplyPoint(Vector3.zero);
                        var pointB = nextMatrix.MultiplyPoint(Vector3.zero);
                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    handles.color = baseColor;
                }

                // TODO: cannot rotate so far that one path plane intersects with shape on another plane
                //			... or just fail when it's wrong?
            }

            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint = path.segments[i];
                if (handles.DoPathPointHandle(ref pathPoint))
                {
                    var originalSegments = path.segments;
                    var newPath = new ChiselPath(new ChiselPathPoint[originalSegments.Length]);
                    System.Array.Copy(originalSegments, newPath.segments, originalSegments.Length);
                    newPath.segments[i] = pathPoint;
                    path = newPath;
                }
            }

            // TODO: draw curved path
        }
        #endregion

        public bool HasValidState()
        {
            return false;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}
