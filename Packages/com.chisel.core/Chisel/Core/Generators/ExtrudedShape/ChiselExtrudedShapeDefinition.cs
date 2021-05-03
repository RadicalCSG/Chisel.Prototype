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
    public struct ExtrudedShapeSettings
    {
        public int curveSegments;

        [UnityEngine.HideInInspector] public BlobAssetReference<ChiselPathBlob>     pathBlob;
        [UnityEngine.HideInInspector] public BlobAssetReference<ChiselCurve2DBlob>  curveBlob;
        [UnityEngine.HideInInspector] internal UnsafeList<SegmentVertex>            polygonVerticesList;
        [UnityEngine.HideInInspector] internal UnsafeList<int>                      polygonVerticesSegments;
    }

    public struct ChiselExtrudedShapeGenerator : IChiselBranchTypeGenerator<ExtrudedShapeSettings>
    {
        [BurstCompile()]
        unsafe struct PrepareAndCountBrushesJob : IJobParallelForDefer
        { 
            [NoAlias] public NativeArray<ExtrudedShapeSettings>     settings;
            [NoAlias, WriteOnly] public NativeArray<int>            brushCounts;

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
            [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions;
            [NoAlias] public NativeArray<Range>                                         ranges;
            [NoAlias] public NativeArray<ExtrudedShapeSettings>                         settings;
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

                            for (int i = range.start, m = 0; i < range.end; i++, m++)
                            {
                                brushMeshes[i] = generatedBrushMeshes[m];
                            }
                        }
                    }
                }
                finally
                {
                    var setting = settings[index];
                    Dispose(ref setting);
                    settings[index] = setting;
                }
            }
        }

        [BurstDiscard]
        public JobHandle Schedule(NativeList<ExtrudedShapeSettings> settings, NativeList<BlobAssetReference<NativeChiselSurfaceDefinition>> surfaceDefinitions, NativeList<Range> ranges, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes)
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

        public static void Dispose(ref ExtrudedShapeSettings settings)
        {
            if (settings.pathBlob.IsCreated) settings.pathBlob.Dispose();
            if (settings.curveBlob.IsCreated) settings.curveBlob.Dispose();
            if (settings.polygonVerticesList.IsCreated) settings.polygonVerticesList.Dispose();
            if (settings.polygonVerticesSegments.IsCreated) settings.polygonVerticesSegments.Dispose();
            settings.pathBlob = default;
            settings.curveBlob = default;
            settings.polygonVerticesList = default;
            settings.polygonVerticesSegments = default;
        }

        [BurstCompile()]
        public int PrepareAndCountRequiredBrushMeshes(ref ExtrudedShapeSettings settings)
        {
            ref var curve = ref settings.curveBlob.Value;
            if (!curve.ConvexPartition(settings.curveSegments, out UnsafeList<SegmentVertex> polygonVerticesList, out UnsafeList<int> polygonVerticesSegments, Allocator.Persistent))
                return 0;

            settings.polygonVerticesList = polygonVerticesList;
            settings.polygonVerticesSegments = polygonVerticesSegments;

            return polygonVerticesSegments.Length;
        }

        [BurstCompile()]
        public static int PrepareAndCountRequiredBrushMeshes_(ref ExtrudedShapeSettings settings)
        {
            ref var curve = ref settings.curveBlob.Value;
            if (!curve.ConvexPartition(settings.curveSegments, out UnsafeList<SegmentVertex> polygonVerticesList, out UnsafeList<int> polygonVerticesSegments, Allocator.Persistent))
                return 0;

            settings.polygonVerticesList = polygonVerticesList;
            settings.polygonVerticesSegments = polygonVerticesSegments;

            return polygonVerticesSegments.Length;
        }

        [BurstCompile()]
        public static bool GenerateMesh(ExtrudedShapeSettings settings, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, Allocator allocator)
        {
            // TODO: maybe just not bother with pathblob and just convert to path-matrices directly?
            using (var pathMatrices = settings.pathBlob.Value.GetUnsafeMatrices(Allocator.Temp))
            {
                if (!BrushMeshFactory.GenerateExtrudedShape(brushMeshes,
                                                            in settings.polygonVerticesList,
                                                            in settings.polygonVerticesSegments,
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

        [BurstDiscard]
        public void FixupOperations(CSGTreeBranch branch, ExtrudedShapeSettings settings) { }
    }

    [Serializable]
    public struct ChiselExtrudedShapeDefinition : IChiselBranchGenerator<ChiselExtrudedShapeGenerator, ExtrudedShapeSettings>
    {
        public const string kNodeTypeName = "Extruded Shape";

        public const int                kDefaultCurveSegments   = 8;
        public static readonly Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public ChiselPath               path;

        [HideFoldout] public ExtrudedShapeSettings settings;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            shape = new Curve2D(kDefaultShape);
            path  = new ChiselPath(ChiselPath.Default);
            settings.curveSegments = kDefaultCurveSegments;
        }

        public int RequiredSurfaceCount { get { return 2 + shape.controlPoints.Length; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            shape ??= new Curve2D(kDefaultShape);
        }

        public ExtrudedShapeSettings GenerateSettings()
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
