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
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselExtrudedShape : IBranchGenerator
    {
        public readonly static ChiselExtrudedShape DefaultValues = new ChiselExtrudedShape
        {
            curveSegments = 8
        };

        public int curveSegments;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselPathBlob>     pathBlob;
        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob>  curveBlob;

        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex>            polygonVerticesList;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<int>                      polygonVerticesSegments;

        #region Generate
        [BurstCompile]
        public int PrepareAndCountRequiredBrushMeshes()
        {
            ref var curve = ref curveBlob.Value;
            if (!curve.ConvexPartition(curveSegments, out polygonVerticesList, out polygonVerticesSegments, Allocator.Persistent))
                return 0;

            return polygonVerticesSegments.Length;
        }

        [BurstCompile]
        public bool GenerateMesh(BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            // TODO: maybe just not bother with pathblob and just convert to path-matrices directly?
            using (var pathMatrices = pathBlob.Value.GetUnsafeMatrices(Allocator.Temp))
            {
                if (!BrushMeshFactory.GenerateExtrudedShape(brushMeshes,
                                                            in polygonVerticesList,
                                                            in polygonVerticesSegments,
                                                            in pathMatrices,
                                                            in surfaceDefinitionBlob,
                                                            Allocator.Persistent))
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
        }

        public void Dispose()
        {
            if (pathBlob.IsCreated) pathBlob.Dispose();
            if (curveBlob.IsCreated) curveBlob.Dispose();
            if (polygonVerticesList.IsCreated) polygonVerticesList.Dispose();
            if (polygonVerticesSegments.IsCreated) polygonVerticesSegments.Dispose();
            pathBlob = default;
            curveBlob = default;
            polygonVerticesList = default;
            polygonVerticesSegments = default;
        }

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch) { }
        #endregion
        
        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + (curveBlob.IsCreated ? curveBlob.Value.controlPoints.Length : 3); } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion
        
        #region Validation
        public void Validate() { }
        #endregion
    }

    [Serializable]
    public struct ChiselExtrudedShapeDefinition : ISerializedBranchGenerator<ChiselExtrudedShape>
    {
        public const string kNodeTypeName = "Extruded Shape";

        public static readonly Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public ChiselPath               path;

        [HideFoldout] public ChiselExtrudedShape settings;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            shape = new Curve2D(kDefaultShape);
            path  = new ChiselPath(ChiselPath.Default);
            settings = ChiselExtrudedShape.DefaultValues;
        }

        public int RequiredSurfaceCount { get { return 2 + shape.controlPoints.Length; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) 
        {
            settings.UpdateSurfaces(ref surfaceDefinition);
        }

        public void Validate()
        {
            shape ??= new Curve2D(kDefaultShape);
            path  ??= new ChiselPath(ChiselPath.Default);
            settings.Validate();
        }

        public ChiselExtrudedShape GetBranchGenerator()
        {
            settings.pathBlob = ChiselPathBlob.Convert(path, Allocator.TempJob);
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.TempJob);
            return settings;
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
            return true;
        }

        public void OnMessages(IChiselMessages messages)
        {
        }
    }
}
