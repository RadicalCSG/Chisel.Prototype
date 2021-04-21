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
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateHemisphere(float3 diameterXYZ, float rotation, int horzSegments, int vertSegments, 
                                              in BlobAssetReference<NativeChiselSurfaceDefinition> surfaceDefinitionBlob,
                                              out BlobAssetReference<BrushMeshBlob> brushMesh,
                                              Allocator allocator)
        {
            brushMesh = BlobAssetReference<BrushMeshBlob>.Null;
            if (math.any(diameterXYZ == float3.zero))
                return false;

            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
                var transform       = float4x4.TRS(float3.zero, quaternion.AxisAngle(new float3(0, 1, 0), math.radians(rotation)), new float3(1));

                var bottomCap		= true;
                var topCap			= false;
                var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

                var rings			= (vertSegments) + (topCap ? 1 : 0);
                var vertexCount		= (horzSegments * rings) + extraVertices;

                var topVertex		= 0;
                var bottomVertex	= (!topCap) ? 1 : 0;
                var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                                 diameterXYZ.y, 
                                                 diameterXYZ.z * 0.5f);

                var heightY = radius.y;
                var localVertices = builder.Allocate(ref root.localVertices, vertexCount);
                if (!topCap   ) localVertices[topVertex   ] = math.mul(transform, new float4(0, heightY, 0, 1)).xyz; // top
                if (!bottomCap) localVertices[bottomVertex] = float3.zero;         // bottom
                var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
                var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
                var vertexIndex			= extraVertices;
                if (heightY < 0)
                {
                    for (int h = horzSegments - 1; h >= 0; h--, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        localVertices[vertexIndex] = math.mul(transform, new float4(math.cos(hRad) * radius.x,
                                                                               0.0f,
                                                                               math.sin(hRad) * radius.z, 
                                                                               1.0f)).xyz;
                    }
                } else
                {
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        var hRad = (h * degreePerSegment) + angleOffset;
                        localVertices[vertexIndex] = math.mul(transform, new float4(math.cos(hRad) * radius.x,  
                                                                               0.0f, 
                                                                               math.sin(hRad) * radius.z,
                                                                               1.0f)).xyz;
                    }
                }
                for (int v = 1; v < rings; v++)
                {
                    var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f; // [0.0f ... 1.0f]
                    var segmentDegree	= math.radians(segmentFactor * 90);		 // [0 .. 90]
                    var segmentHeight	= math.sin(segmentDegree) * heightY;
                    var segmentRadius	= math.cos(segmentDegree);		         // [0 .. 0.707 .. 1 .. 0.707 .. 0]
                    for (int h = 0; h < horzSegments; h++, vertexIndex++)
                    {
                        localVertices[vertexIndex].x = localVertices[h + extraVertices].x * segmentRadius;
                        localVertices[vertexIndex].y = segmentHeight;
                        localVertices[vertexIndex].z = localVertices[h + extraVertices].z * segmentRadius; 
                    }
                }

                if (!GenerateSegmentedSubMesh(horzSegments, vertSegments, bottomCap, topCap, bottomVertex, topVertex,
                                              in localVertices,
                                              ref surfaceDefinitionBlob.Value,
                                              in builder, ref root,
                                              out var polygons,
                                              out var halfEdges))
                    return false;

                // TODO: eventually remove when it's more battle tested
                if (!Validate(in localVertices, in halfEdges, in polygons, logErrors: true))
                    return false;
                
                var localPlanes             = builder.Allocate(ref root.localPlanes, polygons.Length);
                var halfEdgePolygonIndices  = builder.Allocate(ref root.halfEdgePolygonIndices, halfEdges.Length);
                CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
                UpdateHalfEdgePolygonIndices(ref halfEdgePolygonIndices, in polygons);
                root.localBounds = CalculateBounds(in localVertices);
                brushMesh = builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
                return true;
            }
        }

        public static bool GenerateHemisphereVertices(ref ChiselHemisphereDefinition definition, ref Vector3[] vertices)
        {
            var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(definition.rotation, Vector3.up), Vector3.one);
            return GenerateHemisphereVertices(definition.diameterXYZ, transform, definition.horizontalSegments, definition.verticalSegments, ref vertices);
        }

        public static bool GenerateHemisphereVertices(Vector3 diameterXYZ, Matrix4x4 transform, int horzSegments, int vertSegments, ref Vector3[] vertices)
        {
            var bottomCap		= true;
            var topCap			= false;
            var extraVertices	= ((!bottomCap) ? 1 : 0) + ((!topCap) ? 1 : 0);

            var rings			= (vertSegments) + (topCap ? 1 : 0);
            var vertexCount		= (horzSegments * rings) + extraVertices;

            var topVertex		= 0;
            var bottomVertex	= (!topCap) ? 1 : 0;
            var radius			= new Vector3(diameterXYZ.x * 0.5f, 
                                             diameterXYZ.y, 
                                             diameterXYZ.z * 0.5f);
            
            if (vertices == null || 
                vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];
            if (!topCap   ) vertices[topVertex   ] = transform.MultiplyPoint(Vector3.up * radius.y); // top
            if (!bottomCap) vertices[bottomVertex] = transform.MultiplyPoint(Vector3.zero);          // bottom
            var degreePerSegment	= (360.0f / horzSegments) * Mathf.Deg2Rad;
            var angleOffset			= ((horzSegments & 1) == 1) ? 0.0f : 0.5f * degreePerSegment;
            var vertexIndex			= extraVertices;
            {
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    var hRad = (h * degreePerSegment) + angleOffset;
                    vertices[vertexIndex] = transform.MultiplyPoint(new Vector3(math.cos(hRad) * radius.x,  
                                                                               0.0f, 
                                                                               math.sin(hRad) * radius.z));
                }
            }
            for (int v = 1; v < rings; v++)
            {
                var segmentFactor	= ((v - (rings / 2.0f)) / rings) + 0.5f;			// [0.0f ... 1.0f]
                var segmentDegree	= (segmentFactor * 90);								// [0 .. 90]
                var segmentHeight	= Mathf.Sin(segmentDegree * Mathf.Deg2Rad) * radius.y;
                var segmentRadius	= Mathf.Cos(segmentDegree * Mathf.Deg2Rad);		    // [0 .. 0.707 .. 1 .. 0.707 .. 0]
                for (int h = 0; h < horzSegments; h++, vertexIndex++)
                {
                    vertices[vertexIndex].x = vertices[h + extraVertices].x * segmentRadius;
                    vertices[vertexIndex].y = segmentHeight;
                    vertices[vertexIndex].z = vertices[h + extraVertices].z * segmentRadius; 
                }
            }
            return true;
        }
    }
}