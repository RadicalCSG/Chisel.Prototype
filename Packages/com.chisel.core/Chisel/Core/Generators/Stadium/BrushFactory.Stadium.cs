using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static unsafe bool GenerateStadium(float width, float height, float length,
                                                  float topLength,     int topSides,
                                                  float bottomLength,  int bottomSides, 
                                                  in ChiselBlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                  out ChiselBlobAssetReference<BrushMeshBlob> brushMesh,
                                                  Allocator allocator)
        {
            brushMesh = ChiselBlobAssetReference<BrushMeshBlob>.Null;
            using (var builder = new ChiselBlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
                if (!GenerateStadiumVertices(width, height, length,
                                             topLength, topSides,
                                             bottomLength, bottomSides, in builder, ref root, out var localVertices))
                    return false;
                
                var haveRoundedTop      = (topLength    > 0) && (topSides    > 1);
                var haveRoundedBottom   = (bottomLength > 0) && (bottomSides > 1);
                var haveCenter			= (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadium.kNoCenterEpsilon;
                var sides               = (haveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1);

                CreateExtrudedSubMesh(sides, null, 0, 0, 1, 
                                      in localVertices, in surfaceDefinitionBlob, in builder, ref root,
                                      out var polygons, out var halfEdges);

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

        public static bool GenerateStadiumVertices(float diameter, float height, float length,
                                                   float topLength, int topSides,
                                                   float bottomLength, int bottomSides, 
                                                   in ChiselBlobBuilder               builder,
                                                   ref BrushMeshBlob            root,
                                                   out ChiselBlobBuilderArray<float3> vertices)
        {
            var haveRoundedTop      = (topLength    > 0) && (topSides    > 1);
            var haveRoundedBottom   = (bottomLength > 0) && (bottomSides > 1);
            var haveCenter			= (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadium.kNoCenterEpsilon;
            var sides               = (haveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1);
            
            var radius			= diameter * 0.5f;

            vertices = builder.Allocate(ref root.localVertices, sides * 2);
            
            int vertexIndex = 0;
            if (!haveRoundedTop)
            {
                vertices[vertexIndex] = new float3(-radius, 0, length * -0.5f); vertexIndex++;
                vertices[vertexIndex] = new float3( radius, 0, length * -0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= math.radians(-180.0f);
                var degreePerSegment	= math.radians(180.0f / topSides);
                var center				= new float3(0, 0, (length * -0.5f) + topLength);
                for (int s = 0; s <= topSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (math.cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (math.sin(hRad) * topLength);

                    vertices[vertexIndex] = new float3(x, y, z);
                    vertexIndex++;
                }
            }

            if (!haveCenter)
                vertexIndex--;

            //vertexIndex = definition.firstBottomSide;
            if (!haveRoundedBottom)
            {
                vertices[vertexIndex] = new float3( radius, 0, length * 0.5f); vertexIndex++;
                vertices[vertexIndex] = new float3(-radius, 0, length * 0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= 0.0f;
                var degreePerSegment	= math.radians(180.0f / bottomSides);
                var center				= new float3(0, 0, (length * 0.5f) - bottomLength);
                for (int s = 0; s <= bottomSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (math.cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (math.sin(hRad) * bottomLength);

                    vertices[vertexIndex] = new float3(x, y, z);
                    vertexIndex++;
                }
            }

            var extrusion = new float3(0, 1, 0) * height;
            for (int s = 0; s < sides; s++)
                vertices[s + sides] = vertices[s] + extrusion;
            return true;
        }

        public static bool GenerateStadiumVertices(ChiselStadiumDefinition definition, ref Vector3[] vertices)
        {
            var topSides		= definition.settings.topSides;
            var bottomSides		= definition.settings.bottomSides;
            var sides			= definition.settings.Sides;

            var length			= definition.settings.length;
            var topLength		= definition.settings.topLength;
            var bottomLength	= definition.settings.bottomLength;
            var diameter		= definition.settings.width;
            var radius			= diameter * 0.5f;

            if (vertices == null ||
                vertices.Length != sides * 2)
                vertices		= new Vector3[sides * 2];
            
            var firstTopSide	= definition.settings.FirstTopSide;
            var lastTopSide		= definition.settings.LastTopSide;
            var firstBottomSide = definition.settings.FirstBottomSide;
            var lastBottomSide  = definition.settings.LastBottomSide;

            var haveCenter		= definition.settings.HaveCenter;
            
            int vertexIndex = 0;
            if (!definition.settings.HaveRoundedTop)
            {
                vertices[vertexIndex] = new Vector3(-radius, 0, length * -0.5f); vertexIndex++;
                vertices[vertexIndex] = new Vector3( radius, 0, length * -0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= -180.0f * Mathf.Deg2Rad;
                var degreePerSegment	= (180.0f / topSides) * Mathf.Deg2Rad;
                var center				= new Vector3(0, 0, (length * -0.5f) + topLength);
                for (int s = 0; s <= topSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (math.cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (math.sin(hRad) * topLength);

                    vertices[vertexIndex] = new Vector3(x, y, z);
                    vertexIndex++;
                }
            }

            if (!haveCenter)
                vertexIndex--;

            //vertexIndex = definition.firstBottomSide;
            if (!definition.settings.HaveRoundedBottom)
            {
                vertices[vertexIndex] = new Vector3( radius, 0, length * 0.5f); vertexIndex++;
                vertices[vertexIndex] = new Vector3(-radius, 0, length * 0.5f); vertexIndex++;
            } else
            {
                var degreeOffset		= 0.0f * Mathf.Deg2Rad;
                var degreePerSegment	= (180.0f / bottomSides) * Mathf.Deg2Rad;
                var center				= new Vector3(0, 0, (length * 0.5f) - bottomLength);
                for (int s = 0; s <= bottomSides; s++)
                {
                    var hRad = (s * degreePerSegment) + degreeOffset;

                    var x = center.x + (math.cos(hRad) * radius);
                    var y = center.y;
                    var z = center.z + (math.sin(hRad) * bottomLength);

                    vertices[vertexIndex] = new Vector3(x, y, z);
                    vertexIndex++;
                }
            }

            var extrusion = new Vector3(0, 1, 0) * definition.settings.height;
            for (int s = 0; s < sides; s++)
                vertices[s + sides] = vertices[s] + extrusion;
            return true;
        }

    }
}