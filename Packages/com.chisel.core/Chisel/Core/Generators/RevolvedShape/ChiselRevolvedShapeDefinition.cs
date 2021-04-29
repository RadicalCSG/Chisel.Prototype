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
    public struct RevolvedShapeSettings
    {
        public int      curveSegments;
        public int      revolveSegments;
        public float    startAngle;
        public float    totalAngle;
        public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
    }

    [Serializable]
    public struct ChiselRevolvedShapeDefinition : IChiselBranchGenerator
    {
        public const string kNodeTypeName = "Revolved Shape";

        public const int				kDefaultCurveSegments	= 8;
        public const int                kDefaultRevolveSegments = 8;
        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        [HideFoldout] public RevolvedShapeSettings settings;

        public Curve2D  shape;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public void Reset()
        {
            // TODO: create constants
            shape			            = kDefaultShape;
            settings.startAngle		    = 0.0f;
            settings.totalAngle		    = 360.0f;
            settings.curveSegments	    = kDefaultCurveSegments;
            settings.revolveSegments	= kDefaultRevolveSegments;
        }

        public int RequiredSurfaceCount { get { return 6; } }

        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }

        public void Validate()
        {
            settings.curveSegments	    = math.max(settings.curveSegments, 2);
            settings.revolveSegments	= math.max(settings.revolveSegments, 1);

            settings.totalAngle		    = math.clamp(settings.totalAngle, 1, 360); // TODO: constants
        }

        [BurstCompile(CompileSynchronously = true)]
        struct CreateRevolvedShapeJob : IJob
        {
            [NoAlias, ReadOnly] public RevolvedShapeSettings settings;
            
            [NoAlias, ReadOnly]
            public BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob;

            [NoAlias]
            public NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes;

            public void Execute()
            {
                ref var curve = ref settings.curveBlob.Value;
                if (!curve.ConvexPartition(settings.curveSegments, out var polygonVerticesList, out var polygonVerticesSegments, Allocator.Temp))
                {
                    brushMeshes.Clear();
                    return;
                }

                try
                {
                    BrushMeshFactory.Split2DPolygonAlongOriginXAxis(polygonVerticesList, polygonVerticesSegments);

                    int requiredSubMeshCount = polygonVerticesSegments.Length * settings.revolveSegments;
                    if (requiredSubMeshCount == 0)
                    {
                        brushMeshes.Clear();
                        return;
                    }

                    using (var pathMatrices = BrushMeshFactory.GetCircleMatrices(settings.revolveSegments, new float3(0, 1, 0), Allocator.Temp))
                    {
                        brushMeshes.Resize(requiredSubMeshCount, NativeArrayOptions.ClearMemory);
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
                            brushMeshes.Clear();
                        }
                    }
                }
                finally
                {
                    if (polygonVerticesList.IsCreated)
                        polygonVerticesList.Dispose();
                    if (polygonVerticesSegments.IsCreated)
                        polygonVerticesSegments.Dispose();
                }
            }
        }

        public void FixupOperations(CSGTreeBranch branch) { }

        public JobHandle Generate(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob)
        {
            using (var curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.TempJob))
            {
                settings.curveBlob = curveBlob;
                var createExtrudedShapeJob = new CreateRevolvedShapeJob
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

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        public void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            var controlPoints	= shape.controlPoints;
            
            var shapeVertices		= new System.Collections.Generic.List<SegmentVertex>();
            BrushMeshFactory.GetPathVertices(this.shape, settings.curveSegments, shapeVertices);

            
            var horzSegments			= settings.revolveSegments;
            var horzDegreePerSegment	= settings.totalAngle / horzSegments;
            var horzOffset				= settings.startAngle;
            
            var noZTestcolor = handles.GetStateColor(baseColor, false, true);
            var zTestcolor	 = handles.GetStateColor(baseColor, false, false);
            for (int h = 1, pr = 0; h < horzSegments + 1; pr = h, h++)
            {
                var hDegree0	= math.radians((pr * horzDegreePerSegment) + horzOffset);
                var hDegree1	= math.radians((h  * horzDegreePerSegment) + horzOffset);
                var rotation0	= quaternion.AxisAngle(normal, hDegree0);
                var rotation1	= quaternion.AxisAngle(normal, hDegree1);
                for (int p0 = controlPoints.Length - 1, p1 = 0; p1 < controlPoints.Length; p0 = p1, p1++)
                {
                    var point0	= controlPoints[p0].position;
                    //var point1	= controlPoints[p1].position;
                    var vertexA	= math.mul(rotation0, new float3(point0.x, point0.y, 0));
                    var vertexB	= math.mul(rotation1, new float3(point0.x, point0.y, 0));
                    //var vertexC	= rotation0 * new Vector3(point1.x, 0, point1.y);

                    handles.color = noZTestcolor;
                    handles.DrawLine(vertexA, vertexB, lineMode: LineMode.NoZTest, thickness: kHorzLineThickness);//, dashSize: kLineDash);

                    handles.color = zTestcolor;
                    handles.DrawLine(vertexA, vertexB, lineMode: LineMode.ZTest,   thickness: kHorzLineThickness);//, dashSize: kLineDash);
                }

                for (int v0 = shapeVertices.Count - 1, v1 = 0; v1 < shapeVertices.Count; v0=v1, v1++)
                {
                    var point0	= shapeVertices[v0].position;
                    var point1	= shapeVertices[v1].position;
                    var vertexA	= math.mul(rotation0, new float3(point0.x, point0.y, 0));
                    var vertexB	= math.mul(rotation0, new float3(point1.x, point1.y, 0));

                    handles.color = noZTestcolor;
                    handles.DrawLine(vertexA, vertexB, lineMode: LineMode.NoZTest, thickness: kHorzLineThickness, dashSize: kLineDash);

                    handles.color = zTestcolor;
                    handles.DrawLine(vertexA, vertexB, lineMode: LineMode.ZTest,   thickness: kHorzLineThickness, dashSize: kLineDash);
                }
            }
            handles.color = baseColor;

            {
                // TODO: make this work non grid aligned so we can place it upwards
                handles.DoShapeHandle(ref shape, float4x4.identity);
                handles.DrawLine(normal * 10, normal * -10, dashSize: 4.0f);
            }
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