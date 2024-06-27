using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateSphere(float3 diameterXYZ, float offsetY, float rotation, bool generateFromCenter, int horzSegments, int vertSegments,
                                          in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                          out ChiselBlobAssetReference<BrushMeshBlob> brushMesh,
                                          Allocator allocator)
        {
            brushMesh = ChiselBlobAssetReference<BrushMeshBlob>.Null;
            using (var builder = new ChiselBlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;

                var transform = float4x4.TRS(Vector3.zero, quaternion.AxisAngle(new Vector3(0, 1, 0), rotation), Vector3.one);
                if (!CreateSphere(diameterXYZ, offsetY, generateFromCenter, horzSegments, vertSegments, 
                                  ref surfaceDefinition, in builder, ref root,
                                  out var localVertices, out var polygons, out var halfEdges))
                    return false;

                // TODO: do something more intelligent with surface assignment, and put it inside CreateSphere
                for (int i = 0; i < polygons.Length; i++)
                {
                    var surfaceID = i < surfaceDefinition.surfaces.Length ? i : 0;
                    polygons[i].descriptionIndex = surfaceID;
                    polygons[i].surface = surfaceDefinition.surfaces[surfaceID];
                }

                // TODO: eventually remove when it's more battle tested
                if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                    return false;

                var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                root.localPlaneCount = polygons.Length;
                // TODO: calculate corner planes
                var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                root.localBounds = CalculateBounds(in localVertices);
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                return true;
            }
        }
        
        public static bool CreateSphere(float3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments, 
                                        ref NativeChiselSurfaceDefinition surfaceDefinition,
                                        in ChiselBlobBuilder builder,
                                        ref BrushMeshBlob root,
                                        out ChiselBlobBuilderArray<float3>                 vertices,
                                        out ChiselBlobBuilderArray<BrushMeshBlob.Polygon>  polygons,
                                        out ChiselBlobBuilderArray<BrushMeshBlob.HalfEdge> halfEdges)
        {
            vertices = default;
            polygons = default;
            halfEdges = default;
            if (diameterXYZ.x == 0 ||
                diameterXYZ.y == 0 ||
                diameterXYZ.z == 0)
                return false;

            var lastVertSegment = vertSegments - 1;

            var triangleCount   = horzSegments + horzSegments;    // top & bottom
            var quadCount       = horzSegments * (vertSegments - 2);
            int polygonCount    = triangleCount + quadCount;
            int halfEdgeCount   = (triangleCount * 3) + (quadCount * 4);

            CreateSphereVertices(diameterXYZ, offsetY, generateFromCenter, horzSegments, vertSegments, in builder, ref root, out vertices);

            polygons    = builder.Allocate(ref root.polygons, polygonCount);
            halfEdges   = builder.Allocate(ref root.halfEdges, halfEdgeCount);

            var edgeIndex       = 0;
            var polygonIndex    = 0;
            var startVertex     = 2;
            for (int v = 0; v < vertSegments; v++)
            {
                var startEdge = edgeIndex;
                for (int h = 0, p = horzSegments - 1; h < horzSegments; p = h, h++)
                {
                    var n = (h + 1) % horzSegments;
                    int polygonEdgeCount;
                    if (v == 0) // top
                    {
                        //          0
                        //          *
                        //         ^ \
                        //     p1 /0 1\ n0
                        //       /  2  v
                        //		*<------*  
                        //     2    t    1
                        polygonEdgeCount = 3;
                        var p1 = (p * 3) + 1;
                        var n0 = (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge + (horzSegments * 3) + (h * 3) + 1) : (startEdge + (horzSegments * 3) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = 0 };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - p };
                    }
                    else
                    if (v == lastVertSegment)
                    {
                        //     0    t    1
                        //		*------>*
                        //       ^  1  /  
                        //     p1 \0 2/ n0
                        //         \ v
                        //          *
                        //          2
                        polygonEdgeCount = 3;
                        var p2 = startEdge + (p * 3) + 2;
                        var n0 = startEdge + (n * 3) + 0;
                        var t = ((vertSegments == 2) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p2, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = t, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = 1 };
                    }
                    else
                    {
                        //     0    t3   1
                        //		*------>*
                        //      ^   1   |  
                        //   p1 |0     2| n0
                        //      |   3   v
                        //		*<------*
                        //     3    t1   2
                        polygonEdgeCount = 4;
                        var p1 = startEdge + (p * 4) + 2;
                        var n0 = startEdge + (n * 4) + 0;
                        var t3 = ((v == 1) ? (startEdge - (horzSegments * 3) + (h * 3) + 2) : (startEdge - (horzSegments * 4) + (h * 4) + 3));
                        var t1 = ((v == lastVertSegment - 1) ? (startEdge + (horzSegments * 4) + (h * 3) + 1) : (startEdge + (horzSegments * 4) + (h * 4) + 1));
                        halfEdges[edgeIndex + 0] = new BrushMeshBlob.HalfEdge { twinIndex = p1, vertexIndex = startVertex + (horzSegments - 1) - p };
                        halfEdges[edgeIndex + 1] = new BrushMeshBlob.HalfEdge { twinIndex = t3, vertexIndex = startVertex + (horzSegments - 1) - h };
                        halfEdges[edgeIndex + 2] = new BrushMeshBlob.HalfEdge { twinIndex = n0, vertexIndex = startVertex + (horzSegments - 1) - h + horzSegments };
                        halfEdges[edgeIndex + 3] = new BrushMeshBlob.HalfEdge { twinIndex = t1, vertexIndex = startVertex + (horzSegments - 1) - p + horzSegments };
                    }

                    polygons[polygonIndex] = new BrushMeshBlob.Polygon
                    {
                        firstEdge   = edgeIndex,
                        edgeCount   = polygonEdgeCount,
                        // TODO: do something more intelligent with surface assignment
                        surface     = surfaceDefinition.surfaces[0]
                    };
                    
                    edgeIndex += polygonEdgeCount;
                    polygonIndex++;
                }
                if (v > 0)
                    startVertex += horzSegments;
            }
            return true;
        }

        public static void CreateSphereVertices(float3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments, 
                                                in ChiselBlobBuilder builder, ref BrushMeshBlob root, out ChiselBlobBuilderArray<float3> vertices)
        {
            var vertexCount = (horzSegments * (vertSegments - 1)) + 2;

            vertices = builder.Allocate(ref root.localVertices, vertexCount);

            var radius = 0.5f * diameterXYZ;

            var offset = generateFromCenter ? offsetY : radius.y + offsetY;
            vertices[0] = new float3(0, 1, 0) * -radius.y;
            vertices[1] = new float3(0, 1, 0) * radius.y;

            vertices[0].y += offset;
            vertices[1].y += offset;

            // TODO: optimize

            var doublePI            = math.PI * 2;
            var degreePerSegment    = doublePI / horzSegments;
            var angleOffset         = ((horzSegments & 1) == 1) ? 0.0f : degreePerSegment * 0.5f;
            for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
            {
                var segmentFactor   = ((v - (vertSegments / 2.0f)) / vertSegments); // [-0.5f ... 0.5f]
                var segmentDegree   = (segmentFactor * 180);                        // [-90 .. 90]
                var segmentHeight   = math.sin(math.radians(segmentDegree));
                var segmentRadius   = math.cos(math.radians(segmentDegree));     // [0 .. 0.707 .. 1 .. 0.707 .. 0]

                var yRingPos        = (segmentHeight * radius.y) + offset;
                var xRingRadius     = segmentRadius * radius.x;
                var zRingRadius     = segmentRadius * radius.z;

                if (radius.y < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new float3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new float3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                }
            }
        }

        public static bool GenerateSphereVertices(ChiselSphereDefinition definition, ref Vector3[] vertices)
        {
            //var transform = float4x4.TRS(Vector3.zero, quaternion.AxisAngle(new Vector3(0, 1, 0), definition.rotation), new Vector3(1));
            BrushMeshFactory.CreateSphereVertices(definition.settings.diameterXYZ, definition.settings.offsetY, definition.settings.generateFromCenter, definition.settings.horizontalSegments, definition.settings.verticalSegments, ref vertices);
            return true;
        }

        public static void CreateSphereVertices(Vector3 diameterXYZ, float offsetY, bool generateFromCenter, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            //var lastVertSegment	= vertSegments - 1;
            int vertexCount = (horzSegments * (vertSegments - 1)) + 2;

            if (vertices == null ||
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            var radius = 0.5f * diameterXYZ;

            var offset = generateFromCenter ? offsetY : radius.y + offsetY;
            vertices[0] = new Vector3(0, 1, 0) * -radius.y;
            vertices[1] = new Vector3(0, 1, 0) * radius.y;

            vertices[0].y += offset;
            vertices[1].y += offset;

            // TODO: optimize

            var degreePerSegment = (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset = ((horzSegments & 1) == 1) ? 0.0f : ((360.0f / horzSegments) * 0.5f) * Mathf.Deg2Rad;
            for (int v = 1, vertexIndex = 2; v < vertSegments; v++)
            {
                var segmentFactor   = ((v - (vertSegments / 2.0f)) / vertSegments); // [-0.5f ... 0.5f]
                var segmentDegree   = (segmentFactor * 180);                        // [-90 .. 90]
                var segmentHeight   = math.sin(segmentDegree * Mathf.Deg2Rad);
                var segmentRadius   = math.cos(segmentDegree * Mathf.Deg2Rad);     // [0 .. 0.707 .. 1 .. 0.707 .. 0]

                var yRingPos        = (segmentHeight * radius.y) + offset;
                var xRingRadius     = segmentRadius * radius.x;
                var zRingRadius     = segmentRadius * radius.z;

                if (radius.y < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        vertices[vertexIndex] = new Vector3(math.cos(hRad) * segmentRadius * radius.x,
                                                           yRingPos,
                                                           math.sin(hRad) * segmentRadius * radius.z);
                    }
                }
            }
        }
    }
}