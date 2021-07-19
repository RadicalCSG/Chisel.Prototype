using System;
using Debug = UnityEngine.Debug;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnitySceneExtensions;
using Vector3 = UnityEngine.Vector3;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselRevolvedShape : IBranchGenerator
    {
        public readonly static ChiselRevolvedShape DefaultValues = new ChiselRevolvedShape
        {
            startAngle		= 0.0f,
            totalAngle		= 360.0f,
            curveSegments	= 8,
            revolveSegments	= 8
        };

        public int      curveSegments;
        public int      revolveSegments;
        public float    startAngle;
        public float    totalAngle;

        [UnityEngine.HideInInspector, NonSerialized] public ChiselBlobAssetReference<ChiselCurve2DBlob>   curveBlob;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex>             polygonVerticesList;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<int>                       polygonVerticesSegments;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<float4x4>                  pathMatrices;
                
        #region Generate
        [BurstCompile]
        public int PrepareAndCountRequiredBrushMeshes()
        {
            ref var curve = ref curveBlob.Value;
            if (!curve.ConvexPartition(curveSegments, out polygonVerticesList, out polygonVerticesSegments, Allocator.Persistent))
                return 0;

            BrushMeshFactory.GetCircleMatrices(out pathMatrices, revolveSegments, new float3(0, 1, 0), Allocator.Persistent);

            BrushMeshFactory.Split2DPolygonAlongOriginXAxis(ref polygonVerticesList, ref polygonVerticesSegments);

            return polygonVerticesSegments.Length * (pathMatrices.length - 1);
        }

        [BurstCompile()]
        public bool GenerateNodes(ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator)
        {
            using (var generatedBrushMeshes = new NativeList<ChiselBlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp))
            {
                generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
                if (!BrushMeshFactory.GenerateExtrudedShape(generatedBrushMeshes,
                                                            in polygonVerticesList,
                                                            in polygonVerticesSegments,
                                                            in pathMatrices,
                                                            in surfaceDefinitionBlob,
                                                            allocator))
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
            if (curveBlob.IsCreated) curveBlob.Dispose(); curveBlob = default;
            if (polygonVerticesList.IsCreated) polygonVerticesList.Dispose(); polygonVerticesList = default;
            if (polygonVerticesSegments.IsCreated) polygonVerticesSegments.Dispose(); polygonVerticesSegments = default;
            if (pathMatrices.IsCreated) pathMatrices.Dispose(); pathMatrices = default;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 6; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceDefinition surfaceDefinition) { }
        #endregion
        
        #region Validation
        public void Validate()
        {
            curveSegments	    = math.max(curveSegments, 2);
            revolveSegments	    = math.max(revolveSegments, 1);

            totalAngle		    = math.clamp(totalAngle, 1, 360); // TODO: constants
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultValues; }
        #endregion
    }

    [Serializable]
    public class ChiselRevolvedShapeDefinition : SerializedBranchGenerator<ChiselRevolvedShape>
    {
        public const string kNodeTypeName = "Revolved Shape";

        public static readonly Curve2D	kDefaultShape			= new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D  shape;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

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


        public override ChiselRevolvedShape GetBranchGenerator()
        {
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, Allocator.TempJob);
            return base.GetBranchGenerator();
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

        public override void OnEdit(IChiselHandles handles)
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
    }
}