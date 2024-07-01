using Unity.Mathematics;
using Unity.Collections;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static NativeArray<float3> GenerateTorusVertices(float   outerDiameter,
                                                                float   tubeWidth, float tubeHeight, 
                                                                float   tubeRotation, float startAngle, float totalAngle,
                                                                int     verticalSegments, int horizontalSegments,
                                                                bool    fitCircle,
                                                                Allocator allocator)
        {
            var tubeRadiusX		= (tubeWidth  * 0.5f);
            var tubeRadiusY		= (tubeHeight * 0.5f);
            var torusRadius		= (outerDiameter * 0.5f) - tubeRadiusX;
            
            var horzSegments	= horizontalSegments;
            var vertSegments	= verticalSegments;

            var horzRadiansPerSegment = math.radians(totalAngle / horzSegments);
            var vertRadiansPerSegment = math.radians(360.0f / vertSegments);

            var circleVertices = new NativeArray<float2>(vertSegments, Allocator.Temp);
            try
            { 
                var min = new float2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new float2(float.NegativeInfinity, float.NegativeInfinity);
                var tubeAngleOffset	= math.radians((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + tubeRotation);
                for (int v = 0; v < vertSegments; v++)
                {
                    var vRad = tubeAngleOffset + (v * vertRadiansPerSegment);
                    circleVertices[v] = new float2((math.sin(vRad) * tubeRadiusX) - torusRadius, 
                                                   (math.cos(vRad) * tubeRadiusY));
                    min.x = math.min(min.x, circleVertices[v].x);
                    min.y = math.min(min.y, circleVertices[v].y);
                    max.x = math.max(max.x, circleVertices[v].x);
                    max.y = math.max(max.y, circleVertices[v].y);
                }

                if (fitCircle)
                {
                    var center = (max + min) * 0.5f;
                    var size   = (max - min) * 0.5f;
                    size.x = tubeRadiusX / size.x;
                    size.y = tubeRadiusY / size.y;
                    for (int v = 0; v < vertSegments; v++)
                    {
                        var x = ((circleVertices[v].x - center.x) * size.x) - torusRadius;
                        var y = ((circleVertices[v].y - center.y) * size.y); 
                        circleVertices[v] = new float2(x, y);
                    }
                }

                horzSegments++;

                var horzOffset	= startAngle;
                var vertexCount = vertSegments * horzSegments;
                var vertices = new NativeArray<float3>(vertexCount, allocator);
                for (int h = 0, v = 0; h < horzSegments; h++)
                {
                    var hRadians = (h * horzRadiansPerSegment) + horzOffset;
                    var rotation = quaternion.AxisAngle(new float3(0, 1, 0), hRadians);
                    for (int i = 0; i < vertSegments; i++, v++)
                        vertices[v] = math.mul(rotation, new float3(circleVertices[i], 0));
                }
                return vertices;
            }
            finally { circleVertices.Dispose(); }
        }

              
        public static bool GenerateTorus(NativeList<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshes, 
                                                in NativeArray<float3> vertices, int verticalSegments, int horizontalSegments,
                                                in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                Allocator allocator)
        {
            var segmentIndices = new NativeArray<int>(2 + verticalSegments, Allocator.Temp);
            try
            {
                segmentIndices[0] = 0;
                segmentIndices[1] = 1;

                for (int v = 0; v < verticalSegments; v++)
                    segmentIndices[v + 2] = 2;

                for (int n1 = 1, n0 = 0; n1 < horizontalSegments + 1; n0 = n1, n1++)
                {

                    brushMeshes[n0] = ChiselBlobAssetReference<BrushMeshBlob>.Null;

                    using (var builder = new ChiselBlobBuilder(Allocator.Temp))
                    {
                        ref var root = ref builder.ConstructRoot<BrushMeshBlob>();

                        var localVertices = builder.Allocate(ref root.localVertices, verticalSegments * 2);
                        for (int v = 0; v < verticalSegments; v++)
                        {
                            localVertices[v                   ] = vertices[(n0 * verticalSegments) + v];
                            localVertices[v + verticalSegments] = vertices[(n1 * verticalSegments) + v];
                        }

                        // TODO: could probably just create one torus section and repeat that with different transformations
                        CreateExtrudedSubMesh(verticalSegments, segmentIndices, segmentIndices.Length, 0, 1, in localVertices, in surfaceDefinitionBlob, in builder, ref root, out var polygons, out var halfEdges);
                        
                        if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                            return false;

                        var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                        root.localPlaneCount = polygons.Length;
                        // TODO: calculate corner planes
                        var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                        CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                        UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                        root.localBounds = CalculateBounds(in localVertices);
                        brushMeshes[n0] = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                    }
                }
            }
            finally
            {
                segmentIndices.Dispose();
            }
            return true;
        }

        public static bool GenerateTorusVertices(ChiselTorusDefinition definition, ref float3[] vertices)
        {
            var tubeRadiusX		= (definition.settings.tubeWidth  * 0.5f);
            var tubeRadiusY		= (definition.settings.tubeHeight * 0.5f);
            var torusRadius		= (definition.settings.outerDiameter * 0.5f) - tubeRadiusX;
        
    
            var horzSegments	= definition.settings.horizontalSegments;
            var vertSegments	= definition.settings.verticalSegments;

            var horzRadiansPerSegment = math.radians(definition.settings.totalAngle / horzSegments);
            var vertRadiansPerSegment = math.radians(360.0f / vertSegments);
            
            var circleVertices	= new float3[vertSegments];

            var min = new float2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new float2(float.NegativeInfinity, float.NegativeInfinity);
            var tubeAngleOffset	= math.radians((((vertSegments & 1) == 1) ? 0.0f : ((360.0f / vertSegments) * 0.5f)) + definition.settings.tubeRotation);
            for (int v = 0; v < vertSegments; v++)
            {
                var vRad = tubeAngleOffset + (v * vertRadiansPerSegment);
                circleVertices[v] = new float3((math.sin(vRad) * tubeRadiusX) - torusRadius, 
                                               (math.cos(vRad) * tubeRadiusY), 0);
                min.x = math.min(min.x, circleVertices[v].x);
                min.y = math.min(min.y, circleVertices[v].y);
                max.x = math.max(max.x, circleVertices[v].x);
                max.y = math.max(max.y, circleVertices[v].y);
            }

            if (definition.settings.fitCircle)
            {
                var center = (max + min) * 0.5f;
                var size   = (max - min) * 0.5f;
                size.x = tubeRadiusX / size.x;
                size.y = tubeRadiusY / size.y;
                for (int v = 0; v < vertSegments; v++)
                {
                    circleVertices[v].x = (circleVertices[v].x - center.x) * size.x;
                    circleVertices[v].y = (circleVertices[v].y - center.y) * size.y;
                    circleVertices[v].x -= torusRadius;
                }
            }

            horzSegments++;
            
            var horzOffset	= definition.settings.startAngle;
            var vertexCount = vertSegments * horzSegments;
            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new float3[vertexCount];
            float3 up = new float3(0, 1, 0);
            for (int h = 0, v = 0; h < horzSegments; h++)
            {
                var hRadians1 = (h * horzRadiansPerSegment) + horzOffset;
                var rotation1 = quaternion.AxisAngle(up, hRadians1);
                for (int i = 0; i < vertSegments; i++, v++)
                {
                    vertices[v] = math.mul(rotation1, circleVertices[i]);
                }
            }
            return true;
        }
    }
}