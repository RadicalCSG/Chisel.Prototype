using System;
using System.Linq;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        [BurstCompile]
        public static unsafe bool GenerateStadium(float width, float height, float length,
                                                  float topLength,     int topSides,
                                                  float bottomLength,  int bottomSides, 
                                                  in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                                  out BlobAssetReference<BrushMeshBlob> brushMesh,
                                                  Allocator allocator)
        {
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                ref var surfaceDefinition = ref surfaceDefinitionBlob.Value;
                if (!GenerateStadiumVertices(width, height, length,
                                             topLength, topSides,
                                             bottomLength, bottomSides, in builder, ref root, out var localVertices))
                    return false;
                
                var haveRoundedTop      = (topLength    > 0) && (topSides    > 1);
                var haveRoundedBottom   = (bottomLength > 0) && (bottomSides > 1);
                var haveCenter			= (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadiumDefinition.kNoCenterEpsilon;
                var sides               = (haveCenter ? 2 : 0) + math.max(topSides, 1) + math.max(bottomSides, 1);

                CreateExtrudedSubMesh(sides, null, 0, 1, 
                                      in localVertices, in surfaceDefinitionBlob, in builder, ref root,
                                      out var polygons, out var halfEdges);

                // TODO: eventually remove when it's more battle tested
                if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                    return false;

                var localPlanes = builder.Allocate(ref root.localPlanes, polygons.Length);
                var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                root.localBounds = CalculateBounds(in localVertices);
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                return true;
            }
        }
        
        [BurstCompile]
        public static bool GenerateStadiumVertices(float diameter, float height, float length,
                                                   float topLength, int topSides,
                                                   float bottomLength, int bottomSides, 
                                                   in BlobBuilder               builder,
                                                   ref BrushMeshBlob            root,
                                                   out BlobBuilderArray<float3> vertices)
        {
            var haveRoundedTop      = (topLength    > 0) && (topSides    > 1);
            var haveRoundedBottom   = (bottomLength > 0) && (bottomSides > 1);
            var haveCenter			= (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadiumDefinition.kNoCenterEpsilon;
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


        public static bool GenerateStadium(ref ChiselBrushContainer brushContainer, ref ChiselSurfaceDefinition surfaceDefinition, ref ChiselStadiumDefinition definition)
        {
            definition.Validate(ref surfaceDefinition);
            Vector3[] vertices = null;
            if (!GenerateStadiumVertices(definition, ref surfaceDefinition, ref vertices))
                return false;

            brushContainer.EnsureSize(1);

            var surfaceIndices = new int[vertices.Length + 2];
            return BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[0], definition.sides, surfaceIndices, 0, 1, vertices, surfaceDefinition);
        }

        public static bool GenerateStadium(ref BrushMesh brushMesh, ref ChiselSurfaceDefinition surfaceDefinition, ref ChiselStadiumDefinition definition)
        {
            definition.Validate(ref surfaceDefinition);
            Vector3[] vertices = null;
            if (!GenerateStadiumVertices(definition, ref surfaceDefinition, ref vertices))
            {
                brushMesh.Clear();
                return false;
            }
            
            var surfaceIndices	= new int[vertices.Length + 2];
            if (!BrushMeshFactory.CreateExtrudedSubMesh(ref brushMesh, definition.sides, surfaceIndices, 0, 1, vertices, surfaceDefinition))
            {
                brushMesh.Clear();
                return false;
            }
            
            return true;
        }

        public static bool GenerateStadiumVertices(ChiselStadiumDefinition definition, ref ChiselSurfaceDefinition surfaceDefinition, ref Vector3[] vertices)
        {
            definition.Validate(ref surfaceDefinition);
            
            var topSides		= definition.topSides;
            var bottomSides		= definition.bottomSides;
            var sides			= definition.sides;

            var length			= definition.length;
            var topLength		= definition.topLength;
            var bottomLength	= definition.bottomLength;
            var diameter		= definition.width;
            var radius			= diameter * 0.5f;

            if (vertices == null ||
                vertices.Length != sides * 2)
                vertices		= new Vector3[sides * 2];
            
            var firstTopSide	= definition.firstTopSide;
            var lastTopSide		= definition.lastTopSide;
            var firstBottomSide = definition.firstBottomSide;
            var lastBottomSide  = definition.lastBottomSide;

            var haveCenter		= definition.haveCenter;
            
            int vertexIndex = 0;
            if (!definition.haveRoundedTop)
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
            if (!definition.haveRoundedBottom)
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

            var extrusion = new Vector3(0, 1, 0) * definition.height;
            for (int s = 0; s < sides; s++)
                vertices[s + sides] = vertices[s] + extrusion;
            return true;
        }

    }
}